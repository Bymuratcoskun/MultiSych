using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using MultiSych.Services.Data;
using MultiSych.Services.Interfaces;
using Serilog;

namespace MultiSych.Services.Implementations;

/// <summary>
/// SQLite WAL checkpoint + ZIP snapshot tabanlı çoklu cihaz senkronizasyonu.
/// Strateji: her cihaz periyodik olarak DB snapshot'ı ortak bir klasöre (USB, NAS, OneDrive/GDrive sync folder)
/// yazar. Diğer cihazlar bu snapshot'ı alıp kendi yerel DB'leriyle merge eder.
/// </summary>
public sealed class DeviceSyncService : IDeviceSyncService
{
    private readonly ILogger _logger = Log.ForContext<DeviceSyncService>();
    private readonly IDbContextFactory<LocalCacheDbContext> _dbContextFactory;
    private readonly string _deviceRegistryPath;

    public string LocalDeviceId { get; } = GetOrCreateDeviceId();

    public DeviceSyncService(IDbContextFactory<LocalCacheDbContext> dbContextFactory)
    {
        _dbContextFactory = dbContextFactory;
        _deviceRegistryPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "MultiSych", "devices.json");
    }

    public async Task RegisterDeviceAsync(CancellationToken ct = default)
    {
        var devices = await LoadDeviceRegistryAsync(ct);
        var me = new DeviceInfo(
            LocalDeviceId,
            Environment.MachineName,
            RuntimeInformation.OSDescription,
            DateTime.UtcNow);

        var updated = devices
            .Where(d => d.DeviceId != LocalDeviceId)
            .Append(me)
            .ToArray();

        await SaveDeviceRegistryAsync(updated, ct);
        _logger.Information("Device registered: {DeviceId} ({Name})", LocalDeviceId, Environment.MachineName);
    }

    public async Task<DeviceInfo[]> GetPairedDevicesAsync(CancellationToken ct = default)
    {
        return await LoadDeviceRegistryAsync(ct);
    }

    /// <summary>
    /// Yerel SQLite DB'sini WAL checkpoint sonrası ZIP olarak dışa aktarır.
    /// destinationPath: ortak senkronizasyon klasörü (USB, NAS vb.)
    /// </summary>
    public async Task ExportSnapshotAsync(string destinationPath, CancellationToken ct = default)
    {
        await using var db = await _dbContextFactory.CreateDbContextAsync(ct);
        var dbPath = db.Database.GetConnectionString()
            ?.Split(';')
            .FirstOrDefault(p => p.Trim().StartsWith("Data Source=", StringComparison.OrdinalIgnoreCase))
            ?.Substring("Data Source=".Length).Trim();

        if (string.IsNullOrEmpty(dbPath) || !File.Exists(dbPath))
        {
            _logger.Warning("Could not determine database path for export.");
            return;
        }

        // WAL kontrol noktası: bekleyen yazmaları ana DB'ye al
        await db.Database.ExecuteSqlRawAsync("PRAGMA wal_checkpoint(TRUNCATE);", ct);

        Directory.CreateDirectory(destinationPath);
        var snapshotFile = Path.Combine(destinationPath, $"multisych-snapshot-{LocalDeviceId}-{DateTime.UtcNow:yyyyMMddHHmmss}.zip");

        await Task.Run(() =>
        {
            using var zip = ZipFile.Open(snapshotFile, ZipArchiveMode.Create);
            zip.CreateEntryFromFile(dbPath, "localcache.db", CompressionLevel.Fastest);

            var manifest = JsonSerializer.Serialize(new
            {
                DeviceId = LocalDeviceId,
                DeviceName = Environment.MachineName,
                ExportedAt = DateTime.UtcNow.ToString("o")
            });
            var entry = zip.CreateEntry("manifest.json");
            using var writer = new StreamWriter(entry.Open());
            writer.Write(manifest);
        }, ct);

        _logger.Information("Snapshot exported to {File}", snapshotFile);
    }

    /// <summary>
    /// Başka bir cihazın ZIP snapshot'ını alıp eksik kayıtları yerel DB'ye ekler.
    /// Çakışmalarda "en son güncelleme kazanır" (last-write-wins) stratejisi uygulanır.
    /// </summary>
    public async Task ImportSnapshotAsync(string sourcePath, CancellationToken ct = default)
    {
        if (!File.Exists(sourcePath))
        {
            _logger.Warning("Snapshot file not found: {Path}", sourcePath);
            return;
        }

        var tempDir = Path.Combine(Path.GetTempPath(), $"multisych-import-{Guid.NewGuid():N}");
        try
        {
            Directory.CreateDirectory(tempDir);
            await Task.Run(() => ZipFile.ExtractToDirectory(sourcePath, tempDir, overwriteFiles: true), ct);

            var importDbPath = Path.Combine(tempDir, "localcache.db");
            if (!File.Exists(importDbPath))
            {
                _logger.Warning("No localcache.db found in snapshot {Source}", sourcePath);
                return;
            }

            await MergeEmailsAsync(importDbPath, ct);
            await MergeCalendarEventsAsync(importDbPath, ct);
            await MergeCloudFilesAsync(importDbPath, ct);

            _logger.Information("Snapshot imported from {Source}", sourcePath);
        }
        finally
        {
            try { Directory.Delete(tempDir, recursive: true); } catch { }
        }
    }

    public async Task<string> GetSyncStatusAsync()
    {
        var devices = await LoadDeviceRegistryAsync();
        return $"Bu cihaz: {Environment.MachineName} ({LocalDeviceId[..8]}…)\n"
             + $"Kayıtlı cihazlar: {devices.Length}\n"
             + string.Join("\n", devices.Select(d => $"  • {d.DeviceName} — son görülme: {d.LastSeen:g}"));
    }

    // --- Merge helpers (last-write-wins per entity) ---

    private async Task MergeEmailsAsync(string importDbPath, CancellationToken ct)
    {
        var importOptions = BuildImportOptions(importDbPath);
        await using var importDb = new LocalCacheDbContext(importOptions);
        await using var localDb = await _dbContextFactory.CreateDbContextAsync(ct);

        var importedEmails = await importDb.CachedEmails.AsNoTracking().ToListAsync(ct);
        foreach (var email in importedEmails)
        {
            var existing = await localDb.CachedEmails.FindAsync([email.AccountId, email.MessageId], ct);
            if (existing == null)
                localDb.CachedEmails.Add(email);
            else if (email.ReceivedDate > existing.ReceivedDate)
            {
                existing.Subject = email.Subject;
                existing.Body = email.Body;
                existing.IsRead = email.IsRead;
            }
        }
        await localDb.SaveChangesAsync(ct);
    }

    private async Task MergeCalendarEventsAsync(string importDbPath, CancellationToken ct)
    {
        var importOptions = BuildImportOptions(importDbPath);
        await using var importDb = new LocalCacheDbContext(importOptions);
        await using var localDb = await _dbContextFactory.CreateDbContextAsync(ct);

        var importedEvents = await importDb.CachedEvents.AsNoTracking().ToListAsync(ct);
        foreach (var ev in importedEvents)
        {
            var existing = await localDb.CachedEvents.FindAsync([ev.AccountId, ev.EventId], ct);
            if (existing == null)
                localDb.CachedEvents.Add(ev);
            else if (ev.UpdatedAt > existing.UpdatedAt)
            {
                existing.Title = ev.Title;
                existing.Description = ev.Description;
                existing.StartTime = ev.StartTime;
                existing.EndTime = ev.EndTime;
                existing.UpdatedAt = ev.UpdatedAt;
            }
        }
        await localDb.SaveChangesAsync(ct);
    }

    private async Task MergeCloudFilesAsync(string importDbPath, CancellationToken ct)
    {
        var importOptions = BuildImportOptions(importDbPath);
        await using var importDb = new LocalCacheDbContext(importOptions);
        await using var localDb = await _dbContextFactory.CreateDbContextAsync(ct);

        var importedFiles = await importDb.CloudFiles.AsNoTracking().ToListAsync(ct);
        foreach (var file in importedFiles)
        {
            var existing = await localDb.CloudFiles
                .FirstOrDefaultAsync(f => f.AccountId == file.AccountId && f.FileId == file.FileId, ct);
            if (existing == null)
                localDb.CloudFiles.Add(file);
            else if (file.UpdatedAt > existing.UpdatedAt)
            {
                existing.FileName = file.FileName;
                existing.FileSize = file.FileSize;
                existing.MimeType = file.MimeType;
                existing.UpdatedAt = file.UpdatedAt;
            }
        }
        await localDb.SaveChangesAsync(ct);
    }

    private static DbContextOptions<LocalCacheDbContext> BuildImportOptions(string dbPath)
    {
        return new DbContextOptionsBuilder<LocalCacheDbContext>()
            .UseSqlite($"Data Source={dbPath};")
            .Options;
    }

    // --- Device registry (local JSON file) ---

    private async Task<DeviceInfo[]> LoadDeviceRegistryAsync(CancellationToken ct = default)
    {
        if (!File.Exists(_deviceRegistryPath)) return Array.Empty<DeviceInfo>();
        try
        {
            var json = await File.ReadAllTextAsync(_deviceRegistryPath, ct);
            return JsonSerializer.Deserialize<DeviceInfo[]>(json) ?? Array.Empty<DeviceInfo>();
        }
        catch (Exception ex)
        {
            _logger.Warning(ex, "Failed to load device registry.");
            return Array.Empty<DeviceInfo>();
        }
    }

    private async Task SaveDeviceRegistryAsync(DeviceInfo[] devices, CancellationToken ct = default)
    {
        var dir = Path.GetDirectoryName(_deviceRegistryPath)!;
        Directory.CreateDirectory(dir);
        var json = JsonSerializer.Serialize(devices, new JsonSerializerOptions { WriteIndented = true });
        await File.WriteAllTextAsync(_deviceRegistryPath, json, ct);
    }

    private static string GetOrCreateDeviceId()
    {
        var idPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "MultiSych", "device.id");

        if (File.Exists(idPath))
            return File.ReadAllText(idPath).Trim();

        var id = Guid.NewGuid().ToString("N");
        Directory.CreateDirectory(Path.GetDirectoryName(idPath)!);
        File.WriteAllText(idPath, id);
        return id;
    }
}
