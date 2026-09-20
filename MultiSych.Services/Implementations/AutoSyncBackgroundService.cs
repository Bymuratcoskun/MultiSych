using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;
using MultiSych.Services.Interfaces;
using MultiSych.Services.Configuration;
using Microsoft.EntityFrameworkCore;
using MultiSych.Services.Data;
using MultiSych.Services.Models;

namespace MultiSych.Services.Implementations;

public class AutoSyncBackgroundService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger _logger;
    private readonly ISyncSignalService _syncSignalService;
    private readonly RuntimeSyncSettings _runtimeSyncSettings;
    private readonly HashSet<string> _notifiedEventIds = new();
    private readonly HashSet<string> _notifiedEmailIds = new();
    private readonly INotificationService? _notificationService;
    private readonly IEventBus? _eventBus;
    // Kullanıcı tarafından çözülen çakışmalar: "accountId:fileId" → strateji
    private readonly System.Collections.Concurrent.ConcurrentDictionary<string, string> _resolvedConflicts = new();

    public AutoSyncBackgroundService(
        IServiceScopeFactory scopeFactory,
        ISyncSignalService syncSignalService,
        RuntimeSyncSettings runtimeSyncSettings,
        INotificationService? notificationService = null,
        IEventBus? eventBus = null)
    {
        _scopeFactory = scopeFactory;
        _logger = Log.ForContext<AutoSyncBackgroundService>();
        _syncSignalService = syncSignalService;
        _runtimeSyncSettings = runtimeSyncSettings;
        _notificationService = notificationService;
        _eventBus = eventBus;

        // Kullanıcı bir çakışmayı çözdüğünde dictionary'e al; bir sonraki sync döngüsünde uygulanır
        _eventBus?.Subscribe<ConflictResolvedEvent>(ev =>
        {
            var key = $"{ev.AccountId}:{ev.FileId}";
            _resolvedConflicts[key] = ev.Strategy;
            _syncSignalService.TriggerSync();
        });
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.Information("AutoSync Background Service is starting.");

        // Etkinlik hatırlatıcılarını kontrol eden bağımsız paralel döngü
        _ = Task.Run(async () => await CheckRemindersAsync(stoppingToken), stoppingToken);

        // Uygulama kapanmadığı sürece döngü devam eder
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                CancellationToken waitToken = stoppingToken;
                IDisposable? linkedCts = null;

                if (_runtimeSyncSettings.AutoSyncEnabled)
                {
                    var currentInterval = TimeSpan.FromMinutes(_runtimeSyncSettings.SyncIntervalMinutes);
                    
                    if (PowerStatusHelper.IsOnBattery())
                    {
                        var batteryPercent = PowerStatusHelper.GetBatteryPercent();
                        if (batteryPercent < 20)
                        {
                            _logger.Information("Battery is low ({Percent}%). Extending background sync interval to 60 minutes to save power.", batteryPercent);
                            currentInterval = TimeSpan.FromMinutes(60);
                        }
                        else
                        {
                            var doubled = _runtimeSyncSettings.SyncIntervalMinutes * 2;
                            _logger.Information("Device is running on battery ({Percent}%). Doubling sync interval to {Minutes} minutes.", batteryPercent, doubled);
                            currentInterval = TimeSpan.FromMinutes(doubled);
                        }
                    }

                    var cts = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
                    cts.CancelAfter(currentInterval);
                    waitToken = cts.Token;
                    linkedCts = cts; // Dispose etmek için referansı sakla
                }

                try
                {
                    // Manuel bir sinyal (Channel üzerinden) gelmesini bekliyoruz.
                    await _syncSignalService.WaitAsync(waitToken);
                    if (!waitToken.IsCancellationRequested)
                        _logger.Information("Manual sync signal received.");
                }
                catch (OperationCanceledException)
                {
                    // Sinyal gelmedi, zaman aşımı doldu veya uygulama kapatılıyor. Bu beklenen bir durum.
                }
                finally { linkedCts?.Dispose(); }
                
                // Eğer uygulama tamamen kapatılmıyorsa (zaman dolduğu için veya manuel tıklandığı için buradaysak)
                if (!stoppingToken.IsCancellationRequested)
                {
                    await PerformSyncAsync(stoppingToken);
                }
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Unexpected error in background sync loop.");
            }
        }

        _logger.Information("AutoSync Background Service is stopping.");
    }

    private async Task PerformSyncAsync(CancellationToken cancellationToken)
    {
        _logger.Information("Starting automated sync cycle...");

        try
        {
            if (PowerStatusHelper.IsOnBattery() && PowerStatusHelper.GetBatteryPercent() < 20)
            {
                _logger.Information("Battery is critical (< 20%). Aborting sync execution to conserve power.");
                return;
            }
            // Arka plan servisleri Singleton olduğu için Scoped servisleri yeni bir Scope içinde çağırıyoruz
            using var scope = _scopeFactory.CreateScope();
            
            var accountStore = scope.ServiceProvider.GetRequiredService<IAccountStore>();
            var emailService = scope.ServiceProvider.GetRequiredService<IEmailService>();
            var calendarService = scope.ServiceProvider.GetRequiredService<ICalendarService>();
            var storageService = scope.ServiceProvider.GetRequiredService<IStorageService>();
            var hybridAiService = scope.ServiceProvider.GetRequiredService<IHybridAIService>();

            var accounts = await accountStore.GetAccountsAsync();
            
            if (accounts.Count == 0)
            {
                _logger.Information("No connected accounts found for auto-sync.");
                return;
            }

            foreach (var account in accounts)
            {
                if (cancellationToken.IsCancellationRequested)
                    break;
                
                _logger.Information("Auto-syncing account: {Provider} - {Email}", account.Provider, account.Email);
                
                // Process any pending offline actions before syncing from cloud
                await ProcessOfflineSyncQueueAsync(account, storageService, cancellationToken);
                
                await emailService.SyncEmailsAsync(account);
                
                // E-posta senkronizasyonu sonrası AI analizi
                await hybridAiService.AnalyzeUnprocessedEmailsAsync(account.AccountId);

                if (cancellationToken.IsCancellationRequested) break;
                await calendarService.SyncEventsAsync(account);
                
                if (cancellationToken.IsCancellationRequested) break;
                await storageService.SyncStorageAsync(account);

                if (!System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(System.Runtime.InteropServices.OSPlatform.Windows))
                {
                    try
                    {
                        var mountProvider = scope.ServiceProvider.GetRequiredService<IPlatformMountProvider>();
                        var targetFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "MultiSych_Drives", account.AccountId ?? string.Empty);
                        await mountProvider.UpdateLocalMountFolderAsync(account.AccountId ?? string.Empty, targetFolder);
                    }
                    catch (Exception ex)
                    {
                        _logger.Error(ex, "Failed to update local mount folder for account: {Email}", account.Email);
                    }
                }
            }

            _logger.Information("Automated sync cycle completed successfully.");

        }
        catch (Exception ex)
        {
            _logger.Error(ex, "An error occurred during the automated sync cycle.");
        }
    }

    private async Task CheckRemindersAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var dbContext = scope.ServiceProvider.GetRequiredService<LocalCacheDbContext>();

                var now = DateTime.UtcNow;
                var upcomingLimit = now.AddMinutes(15);
                
                var upcomingEvents = await dbContext.CachedEvents
                    .Where(e => e.StartTime > now && e.StartTime <= upcomingLimit)
                    .ToListAsync(stoppingToken);

                foreach (var ev in upcomingEvents)
                {
                    if (_notifiedEventIds.Add(ev.EventId)) // Add metodu, koleksiyonda yoksa ekler ve true döner
                    {
                        var localTime = ev.StartTime.ToLocalTime();
                        _logger.Information("🔔 HATIRLATMA: '{Title}' etkinliğine az kaldı! Zaman: {Time}", ev.Title, localTime);
                        _notificationService?.ShowNotification("Yaklaşan Etkinlik", $"'{ev.Title}' etkinliğine az kaldı! ({localTime:HH:mm})", "Event");
                    }
                }
                
                // Bellek sızıntısını önlemek için, süresi geçmiş etkinlik ID'lerini HashSet'ten temizliyoruz
                _notifiedEventIds.RemoveWhere(id => !upcomingEvents.Any(e => e.EventId == id));

                // --- Yeni E-Posta Bildirim Kontrolü ---
                var recentEmailLimit = now.AddDays(-1); // Yalnızca son 24 saat içindeki okunmamışları dikkate al
                var unreadEmails = await dbContext.CachedEmails
                    .Where(e => !e.IsRead && e.ReceivedAt > recentEmailLimit)
                    .ToListAsync(stoppingToken);

                foreach (var email in unreadEmails)
                {
                    // Eşsiz bir ID oluşturarak aynı mailin tekrar bildirilmesini engelliyoruz
                    var uniqueId = $"{email.AccountId}_{email.ReceivedAt.Ticks}";
                    if (_notifiedEmailIds.Add(uniqueId))
                    {
                        _logger.Information("📧 YENİ E-POSTA: {Subject} (Hesap: {AccountId})", email.Subject, email.AccountId);
                        _notificationService?.ShowNotification("Yeni E-Posta", $"{email.Subject}", "Email");
                    }
                }
                
                _notifiedEmailIds.RemoveWhere(id => !unreadEmails.Any(e => $"{e.AccountId}_{e.ReceivedAt.Ticks}" == id));
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                // Uygulama kapanırken iptal edilmesi beklenen durumdur.
                break;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error checking event reminders.");
            }

            try
            {
                // Hatırlatıcıları her 1 dakikada bir kontrol et
                await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
        }
    }

    private async Task ProcessOfflineSyncQueueAsync(
        AccountCredentials account,
        IStorageService storageService,
        CancellationToken cancellationToken)
    {
        _logger.Information("Checking offline sync queue for account {Email}...", account.Email);

        try
        {
            using var scope = _scopeFactory.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<LocalCacheDbContext>();

            var queueItems = await dbContext.SyncQueueItems
                .Where(q => q.AccountId == account.AccountId && !q.IsProcessed && q.RetryCount < 3)
                .OrderBy(q => q.CreatedAt)
                .ToListAsync(cancellationToken);

            if (queueItems.Count == 0)
            {
                return;
            }

            _logger.Information("Found {Count} pending offline sync queue items for account {Email}.", queueItems.Count, account.Email);

            int processed = 0;

            foreach (var item in queueItems)
            {
                if (cancellationToken.IsCancellationRequested)
                    break;

                var fileName = Path.GetFileName(item.LocalFilePath);
                var progressPercent = queueItems.Count > 0
                    ? (double)processed / queueItems.Count * 100.0
                    : 0;
                _eventBus?.Publish(new SyncProgressEvent(fileName, progressPercent, queueItems.Count, processed));

                _logger.Information("Processing offline queue item: {Action} for {Path} (Id: {Id})", item.Action, item.LocalFilePath, item.Id);
                
                try
                {
                    if (item.Action == "Upload")
                    {
                        if (File.Exists(item.LocalFilePath))
                        {
                            var relativePath = "/" + Path.GetRelativePath(
                                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "MultiSych_Drives", account.AccountId ?? string.Empty),
                                item.LocalFilePath).Replace('\\', '/');
                                
                            var fileEntity = await dbContext.CloudFiles.FirstOrDefaultAsync(f => f.AccountId == account.AccountId && f.Path == relativePath, cancellationToken);

                            bool hasConflict = false;
                            string conflictStrategy = _runtimeSyncSettings?.ConflictResolutionStrategy ?? "KeepBoth";

                            if (fileEntity != null && !fileEntity.FileId.StartsWith("temp_"))
                            {
                                try
                                {
                                    var cloudFile = await storageService.GetFileAsync(account, fileEntity.FileId);
                                    if (cloudFile != null && (cloudFile.ModifiedDate - fileEntity.UpdatedAt).TotalSeconds > 2.0)
                                    {
                                        _logger.Warning("Offline Queue: Conflict detected for {FileName}. Cloud version modified at {CloudTime}, Local version opened with last known update time {LocalTime}.", fileEntity.FileName, cloudFile.ModifiedDate, fileEntity.UpdatedAt);
                                        hasConflict = true;
                                    }
                                }
                                catch (Exception ex)
                                {
                                    _logger.Warning(ex, "Offline Queue: Failed to check conflicts for {FileName}. Assuming no conflict.", fileEntity.FileName);
                                }
                            }

                            if (hasConflict)
                            {
                                if (conflictStrategy == "AskUser")
                                {
                                    var conflictKey = $"{account.AccountId}:{fileEntity?.FileId}";
                                    if (_resolvedConflicts.TryRemove(conflictKey, out var userChoice))
                                    {
                                        // Kullanıcı çözümü uygulandı — stratejiyi geçici olarak değiştir
                                        conflictStrategy = userChoice;
                                        _logger.Information("Offline Queue: Applying user-resolved strategy '{Strategy}' for {FileName}.", userChoice, fileEntity?.FileName ?? item.LocalFilePath);
                                    }
                                    else
                                    {
                                        // Henüz kullanıcı karar vermedi — bildir ve ertele
                                        _eventBus?.Publish(new FileConflictDetectedEvent(
                                            AccountId: account.AccountId ?? string.Empty,
                                            FileId: fileEntity?.FileId ?? string.Empty,
                                            FileName: fileEntity?.FileName ?? Path.GetFileName(item.LocalFilePath),
                                            LocalPath: item.LocalFilePath,
                                            LocalModifiedAt: File.Exists(item.LocalFilePath) ? File.GetLastWriteTimeUtc(item.LocalFilePath) : DateTime.UtcNow,
                                            CloudModifiedAt: fileEntity?.UpdatedAt ?? DateTime.UtcNow));
                                        _logger.Information("Offline Queue: Conflict for {FileName} queued for user resolution.", fileEntity?.FileName ?? item.LocalFilePath);
                                        continue;
                                    }
                                }

                                if (conflictStrategy == "ServerWins")
                                {
                                    _logger.Information("Offline Queue: Conflict resolution strategy is ServerWins. Discarding local changes for {FileName}.", fileEntity?.FileName ?? item.LocalFilePath);
                                    try { File.Delete(item.LocalFilePath); } catch { }
                                    item.IsProcessed = true;
                                    item.UpdatedAt = DateTime.UtcNow;
                                    await dbContext.SaveChangesAsync(cancellationToken);
                                    continue;
                                }
                                else if (conflictStrategy == "KeepBoth")
                                {
                                    _logger.Information("Offline Queue: Conflict resolution strategy is KeepBoth. Renaming local version of {FileName}.", fileEntity?.FileName ?? item.LocalFilePath);

                                    var directoryPath = Path.GetDirectoryName(relativePath)?.Replace("\\", "/");
                                    if (string.IsNullOrEmpty(directoryPath)) directoryPath = "/";
                                    if (!directoryPath.EndsWith("/")) directoryPath += "/";

                                    var origNameWithoutExt = Path.GetFileNameWithoutExtension(relativePath);
                                    var ext = Path.GetExtension(relativePath);
                                    var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                                    var newFileName = $"{origNameWithoutExt} (Local Conflict {timestamp}){ext}";
                                    var newRelativePath = directoryPath + newFileName;

                                    var targetPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "MultiSych_Drives", account.AccountId ?? string.Empty);
                                    var newFullPath = Path.Combine(targetPath, newRelativePath.TrimStart('/')).Replace('\\', '/');

                                    try
                                    {
                                        var watcher = PlatformMountProvider._activeWatchers.Values.FirstOrDefault(w => string.Equals(w.Path, targetPath, StringComparison.OrdinalIgnoreCase));
                                        if (watcher != null) watcher.EnableRaisingEvents = false;

                                        File.Move(item.LocalFilePath, newFullPath, true);

                                        if (watcher != null) watcher.EnableRaisingEvents = true;

                                        item.LocalFilePath = newFullPath;
                                        relativePath = newRelativePath;

                                        var fileInfo = new System.IO.FileInfo(newFullPath);
                                        var parentId = fileEntity?.ParentId;
                                        var newFileEntity = new CloudFileEntity
                                        {
                                            AccountId = account.AccountId!,
                                            FileId = "temp_" + Guid.NewGuid().ToString("N"),
                                            FileName = newFileName,
                                            Path = relativePath,
                                            ParentId = parentId,
                                            MimeType = "application/octet-stream",
                                            FileSize = fileInfo.Length,
                                            IsDirectory = false,
                                            Provider = account.Provider ?? string.Empty,
                                            CreatedAt = DateTime.UtcNow,
                                            UpdatedAt = DateTime.UtcNow
                                        };
                                        dbContext.CloudFiles.Add(newFileEntity);
                                        await dbContext.SaveChangesAsync(cancellationToken);

                                        fileEntity = newFileEntity;
                                    }
                                    catch (Exception moveEx)
                                    {
                                        _logger.Error(moveEx, "Offline Queue: Failed to rename local file during KeepBoth resolution for {FileName}", fileEntity?.FileName ?? item.LocalFilePath);
                                        item.RetryCount++;
                                        item.ErrorMessage = moveEx.Message;
                                        item.UpdatedAt = DateTime.UtcNow;
                                        await dbContext.SaveChangesAsync(cancellationToken);
                                        continue;
                                    }
                                }
                            }

                            var cloudId = await storageService.UploadFileAsync(account, item.LocalFilePath, item.TargetFolderId);

                            if (fileEntity != null && !string.IsNullOrEmpty(cloudId))
                            {
                                fileEntity.FileId = cloudId;
                                fileEntity.UpdatedAt = DateTime.UtcNow;
                            }
                        }
                    }
                    else if (item.Action == "Delete")
                    {
                        if (!string.IsNullOrEmpty(item.FileId))
                        {
                            await storageService.DeleteFileAsync(account, item.FileId);
                        }
                    }
                    else if (item.Action == "Move")
                    {
                        if (!string.IsNullOrEmpty(item.FileId))
                        {
                            await storageService.MoveFileAsync(account, item.FileId, item.TargetFolderId, item.NewFileName);
                        }
                    }

                    item.IsProcessed = true;
                    item.UpdatedAt = DateTime.UtcNow;
                    processed++;
                }
                catch (Exception ex)
                {
                    item.RetryCount++;
                    item.ErrorMessage = ex.Message;
                    item.UpdatedAt = DateTime.UtcNow;
                    _logger.Warning(ex, "Failed to process offline queue item (Retry: {RetryCount})", item.RetryCount);
                }
            }

            _eventBus?.Publish(new SyncProgressEvent(string.Empty, 100, queueItems.Count, processed));
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error processing offline sync queue for account {Email}", account.Email);
        }
    }
}
