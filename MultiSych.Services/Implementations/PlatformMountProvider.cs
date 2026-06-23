using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using DokanNet;
using Microsoft.EntityFrameworkCore;
using MultiSych.Services.Data;
using MultiSych.Services.Interfaces;
using MultiSych.Services.Configuration;
using MultiSych.Services.Models;
using Serilog;

namespace MultiSych.Services.Implementations
{
    public class PlatformMountProvider : IPlatformMountProvider
    {
        private readonly ILogger _logger = Log.ForContext<PlatformMountProvider>();
        private readonly IStorageService _storageService;
        private readonly IDbContextFactory<LocalCacheDbContext> _dbContextFactory;
        private readonly RuntimeSyncSettings _runtimeSyncSettings;

        public PlatformMountProvider(IStorageService storageService, IDbContextFactory<LocalCacheDbContext> dbContextFactory, RuntimeSyncSettings runtimeSyncSettings)
        {
            _storageService = storageService;
            _dbContextFactory = dbContextFactory;
            _runtimeSyncSettings = runtimeSyncSettings;
        }

        public string GetAvailableDriveLetter()
        {
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                // Linux veya macOS için sürücü harfi mantığı yoktur, klasör yolu döndürürüz.
                var linuxPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "MultiSych_Drives", Guid.NewGuid().ToString("N").Substring(0, 8));
                Directory.CreateDirectory(linuxPath);
                return linuxPath;
            }

            // Windows için Z'den başlayarak C'ye kadar boşta olan ilk sürücü harfini bul.
            var usedDrives = DriveInfo.GetDrives().Select(d => d.Name.Substring(0, 1).ToUpper()).ToList();
            for (char c = 'Z'; c >= 'D'; c--)
            {
                if (!usedDrives.Contains(c.ToString()))
                {
                    return $"{c}:";
                }
            }
            
            throw new Exception("No available drive letters found.");
        }

        public async Task<bool> MountAsync(string mountPoint, string targetPath, string volumeLabel)
        {
            _logger.Information("Mounting {TargetPath} to {MountPoint} (Label: {Label})", targetPath, mountPoint, volumeLabel);

            if (!Directory.Exists(targetPath))
            {
                Directory.CreateDirectory(targetPath);
            }

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                return await MountWindowsAsync(mountPoint, targetPath);
            }
            else
            {
                return await MountLinuxAsync(mountPoint, targetPath);
            }
        }

        public async Task<bool> UnmountAsync(string mountPoint)
        {
            _logger.Information("Unmounting {MountPoint}", mountPoint);

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                return await UnmountWindowsAsync(mountPoint);
            }
            else
            {
                return await UnmountLinuxAsync(mountPoint);
            }
        }

        private async Task<bool> MountWindowsAsync(string driveLetter, string targetPath)
        {
            try
            {
                var accountId = Path.GetFileName(targetPath);
                var cvfs = new CloudVirtualFileSystem(accountId, _storageService, _dbContextFactory, _runtimeSyncSettings);

                var drive = driveLetter.Replace("\\", "").Replace("/", "");
                if (!drive.EndsWith("\\")) drive += "\\";

                _logger.Information("Starting Dokan mount on {Drive}", drive);

                // Dokan.Mount işlemi bloklayıcıdır (blocking), bu yüzden arka plan görevine alıyoruz
                _ = Task.Run(() =>
                {
                    try
                    {
                        cvfs.Mount(drive, DokanOptions.DebugMode | DokanOptions.RemovableDrive);
                    }
                    catch (Exception ex)
                    {
                        _logger.Error(ex, "Dokan mount failed for {Drive}", drive);
                    }
                });

                // Sürücünün işletim sistemine yansıması için kısa bir bekleme süresi
                await Task.Delay(1000);
                return true;
            }
            catch (Exception ex) { _logger.Error(ex, "Exception during Windows mount"); }
            return false;
        }

        private Task<bool> UnmountWindowsAsync(string driveLetter)
        {
            try
            {
                var drive = driveLetter.Replace("\\", "").Replace("/", "");
                char letter = drive[0];

                _logger.Information("Unmounting Dokan volume from {Drive}", letter);
                var dokan = new Dokan(null);
                dokan.Unmount(letter);
                
                return Task.FromResult(true);
            }
            catch (Exception ex) { _logger.Error(ex, "Exception during Windows unmount"); }
            return Task.FromResult(false);
        }

        private async Task<bool> MountLinuxAsync(string mountPoint, string targetPath)
        {
            try
            {
                _logger.Information("Starting simulated Unix mount on {MountPoint} linking to {TargetPath}", mountPoint, targetPath);
                
                if (!Directory.Exists(targetPath))
                {
                    Directory.CreateDirectory(targetPath);
                }

                // If mountPoint already exists as a folder or a symlink, delete it
                if (Directory.Exists(mountPoint) || File.Exists(mountPoint))
                {
                    try { Directory.Delete(mountPoint, true); } catch { }
                    try { File.Delete(mountPoint); } catch { }
                }

                // Create symbolic link from mountPoint to targetPath
                Directory.CreateSymbolicLink(mountPoint, targetPath);

                // Populate directory with metadata files from db
                var accountId = Path.GetFileName(targetPath);
                using (var dbContext = _dbContextFactory.CreateDbContext())
                {
                    var cachedFiles = await dbContext.CloudFiles.Where(f => f.AccountId == accountId).ToListAsync();
                    foreach (var f in cachedFiles)
                    {
                        var localPath = Path.Combine(targetPath, f.Path.TrimStart('/'));
                        var localDir = Path.GetDirectoryName(localPath);
                        if (!string.IsNullOrEmpty(localDir) && !Directory.Exists(localDir))
                        {
                            Directory.CreateDirectory(localDir);
                        }

                        if (f.IsDirectory)
                        {
                            if (!Directory.Exists(localPath)) Directory.CreateDirectory(localPath);
                        }
                        else
                        {
                            // Create empty placeholder file if it doesn't exist
                            if (!File.Exists(localPath))
                            {
                                try { await File.WriteAllBytesAsync(localPath, Array.Empty<byte>()); } catch { }
                            }
                        }
                    }
                }

                // Set up FileSystemWatcher to sync changes back to cloud
                var watcher = new FileSystemWatcher(targetPath)
                {
                    NotifyFilter = NotifyFilters.FileName | NotifyFilters.DirectoryName | NotifyFilters.LastWrite | NotifyFilters.Size,
                    IncludeSubdirectories = true,
                    EnableRaisingEvents = true
                };

                watcher.Created += (s, e) => _ = HandleUnixFileCreatedAsync(targetPath, e.FullPath);
                watcher.Changed += (s, e) => _ = HandleUnixFileChangedAsync(targetPath, e.FullPath);
                watcher.Deleted += (s, e) => _ = HandleUnixFileDeletedAsync(targetPath, e.FullPath);
                watcher.Renamed += (s, e) => _ = HandleUnixFileRenamedAsync(targetPath, e.OldFullPath, e.FullPath);

                _activeWatchers[mountPoint] = watcher;

                return true;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Exception during Linux symlink mount setup");
            }
            return false;
        }

        private Task<bool> UnmountLinuxAsync(string mountPoint)
        {
            try
            {
                _logger.Information("Unmounting Unix mount point: {MountPoint}", mountPoint);
                
                if (_activeWatchers.TryRemove(mountPoint, out var watcher))
                {
                    watcher.EnableRaisingEvents = false;
                    watcher.Dispose();
                }

                if (Directory.Exists(mountPoint) || File.Exists(mountPoint))
                {
                    // Symbolic link behaves as a file or empty directory in Directory.Delete
                    try { Directory.Delete(mountPoint, false); }
                    catch
                    {
                        try { File.Delete(mountPoint); } catch { }
                    }
                }
                
                return Task.FromResult(true);
            }
            catch (Exception ex) 
            { 
                _logger.Error(ex, "Exception during Linux unmount"); 
            }
            return Task.FromResult(false);
        }

        private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, FileSystemWatcher> _activeWatchers = new();

        private async Task HandleUnixFileCreatedAsync(string targetPath, string fullPath)
        {
            if (Directory.Exists(fullPath)) return;
            await Task.Delay(500); // Wait for file locks to clear

            try
            {
                var accountId = Path.GetFileName(targetPath);
                using var dbContext = _dbContextFactory.CreateDbContext();
                var account = await dbContext.Accounts.FirstOrDefaultAsync(a => a.AccountId == accountId);
                if (account == null) return;

                var credentials = new AccountCredentials
                {
                    AccountId = account.AccountId, Email = account.Email, Provider = account.Provider,
                    AccessToken = account.AccessToken, RefreshToken = account.RefreshToken, ExpiresAt = account.ExpiresAt
                };

                var relativePath = "/" + Path.GetRelativePath(targetPath, fullPath).Replace('\\', '/');
                var parentId = GetParentIdFromPath(dbContext, accountId, relativePath);

                _logger.Information("Unix watcher: Local file created: {Path}. Uploading to cloud.", relativePath);
                var cloudId = await _storageService.UploadFileAsync(credentials, fullPath, parentId ?? "root");

                var existing = await dbContext.CloudFiles.FirstOrDefaultAsync(f => f.AccountId == accountId && f.Path == relativePath);
                var fileInfo = new System.IO.FileInfo(fullPath);
                if (existing != null)
                {
                    existing.FileId = cloudId;
                    existing.FileSize = fileInfo.Length;
                    existing.UpdatedAt = DateTime.UtcNow;
                }
                else
                {
                    dbContext.CloudFiles.Add(new CloudFileEntity
                    {
                        AccountId = accountId,
                        FileId = cloudId,
                        FileName = Path.GetFileName(fullPath),
                        Path = relativePath,
                        ParentId = parentId,
                        MimeType = "application/octet-stream",
                        FileSize = fileInfo.Length,
                        IsDirectory = false,
                        Provider = credentials.Provider ?? string.Empty,
                        CreatedAt = DateTime.UtcNow,
                        UpdatedAt = DateTime.UtcNow
                    });
                }
                await dbContext.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                _logger.Warning(ex, "Failed to upload created file on Unix. Queueing for offline sync: {Path}", fullPath);
                await EnqueueOfflineSyncAsync(targetPath, "Upload", fullPath, string.Empty);
            }
        }

        private async Task HandleUnixFileChangedAsync(string targetPath, string fullPath)
        {
            if (Directory.Exists(fullPath)) return;
            await Task.Delay(500); // Wait for file locks to clear

            try
            {
                var accountId = Path.GetFileName(targetPath);
                using var dbContext = _dbContextFactory.CreateDbContext();
                var account = await dbContext.Accounts.FirstOrDefaultAsync(a => a.AccountId == accountId);
                if (account == null) return;

                var credentials = new AccountCredentials
                {
                    AccountId = account.AccountId, Email = account.Email, Provider = account.Provider,
                    AccessToken = account.AccessToken, RefreshToken = account.RefreshToken, ExpiresAt = account.ExpiresAt
                };

                var relativePath = "/" + Path.GetRelativePath(targetPath, fullPath).Replace('\\', '/');
                var existing = await dbContext.CloudFiles.FirstOrDefaultAsync(f => f.AccountId == accountId && f.Path == relativePath);
                if (existing == null) return;

                _logger.Information("Unix watcher: Local file modified: {Path}. Uploading change.", relativePath);
                var cloudId = await _storageService.UploadFileAsync(credentials, fullPath, existing.ParentId ?? "root");

                var fileInfo = new System.IO.FileInfo(fullPath);
                existing.FileSize = fileInfo.Length;
                existing.UpdatedAt = DateTime.UtcNow;
                if (!string.IsNullOrEmpty(cloudId))
                {
                    existing.FileId = cloudId;
                }
                await dbContext.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                _logger.Warning(ex, "Failed to upload modified file on Unix. Queueing for offline sync: {Path}", fullPath);
                await EnqueueOfflineSyncAsync(targetPath, "Upload", fullPath, string.Empty);
            }
        }

        private async Task HandleUnixFileDeletedAsync(string targetPath, string fullPath)
        {
            try
            {
                var accountId = Path.GetFileName(targetPath);
                using var dbContext = _dbContextFactory.CreateDbContext();
                var account = await dbContext.Accounts.FirstOrDefaultAsync(a => a.AccountId == accountId);
                if (account == null) return;

                var credentials = new AccountCredentials
                {
                    AccountId = account.AccountId, Email = account.Email, Provider = account.Provider,
                    AccessToken = account.AccessToken, RefreshToken = account.RefreshToken, ExpiresAt = account.ExpiresAt
                };

                var relativePath = "/" + Path.GetRelativePath(targetPath, fullPath).Replace('\\', '/');
                var existing = await dbContext.CloudFiles.FirstOrDefaultAsync(f => f.AccountId == accountId && f.Path == relativePath);
                if (existing == null) return;

                _logger.Information("Unix watcher: Local file deleted: {Path}. Deleting from cloud.", relativePath);
                var fileId = existing.FileId;

                dbContext.CloudFiles.Remove(existing);
                await dbContext.SaveChangesAsync();

                try
                {
                    await _storageService.DeleteFileAsync(credentials, fileId);
                }
                catch
                {
                    await EnqueueOfflineSyncAsync(targetPath, "Delete", fullPath, fileId);
                }
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error handling Unix file delete for: {Path}", fullPath);
            }
        }

        private async Task HandleUnixFileRenamedAsync(string targetPath, string oldFullPath, string fullPath)
        {
            try
            {
                var accountId = Path.GetFileName(targetPath);
                using var dbContext = _dbContextFactory.CreateDbContext();
                var account = await dbContext.Accounts.FirstOrDefaultAsync(a => a.AccountId == accountId);
                if (account == null) return;

                var credentials = new AccountCredentials
                {
                    AccountId = account.AccountId, Email = account.Email, Provider = account.Provider,
                    AccessToken = account.AccessToken, RefreshToken = account.RefreshToken, ExpiresAt = account.ExpiresAt
                };

                var oldRelativePath = "/" + Path.GetRelativePath(targetPath, oldFullPath).Replace('\\', '/');
                var relativePath = "/" + Path.GetRelativePath(targetPath, fullPath).Replace('\\', '/');
                var existing = await dbContext.CloudFiles.FirstOrDefaultAsync(f => f.AccountId == accountId && f.Path == oldRelativePath);
                if (existing == null) return;

                var newFileName = Path.GetFileName(fullPath);
                var parentId = GetParentIdFromPath(dbContext, accountId, relativePath);

                existing.Path = relativePath;
                existing.FileName = newFileName;
                existing.ParentId = parentId;
                existing.UpdatedAt = DateTime.UtcNow;

                await dbContext.SaveChangesAsync();

                _logger.Information("Unix watcher: Local file renamed from {Old} to {New}. Syncing rename to cloud.", oldRelativePath, relativePath);
                try
                {
                    await _storageService.MoveFileAsync(credentials, existing.FileId, parentId ?? "root", newFileName);
                }
                catch
                {
                    await EnqueueOfflineSyncAsync(targetPath, "Move", fullPath, existing.FileId, parentId ?? "root", newFileName);
                }
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error handling Unix file rename from {Old} to {New}", oldFullPath, fullPath);
            }
        }

        private static string? GetParentIdFromPath(LocalCacheDbContext dbContext, string accountId, string path)
        {
            var cleanPath = path.Replace('\\', '/');
            var parentDir = Path.GetDirectoryName(cleanPath)?.Replace('\\', '/');
            if (string.IsNullOrEmpty(parentDir) || parentDir == "/")
            {
                return "root";
            }
            var parent = dbContext.CloudFiles.FirstOrDefault(f => f.AccountId == accountId && f.Path == parentDir);
            return parent?.FileId ?? "root";
        }

        private async Task EnqueueOfflineSyncAsync(string targetPath, string action, string fullPath, string fileId, string targetFolderId = "root", string newFileName = "")
        {
            try
            {
                var accountId = Path.GetFileName(targetPath);
                using var dbContext = _dbContextFactory.CreateDbContext();
                
                var queueItem = new SyncQueueItemEntity
                {
                    AccountId = accountId,
                    Action = action,
                    FileId = fileId,
                    LocalFilePath = fullPath,
                    TargetFolderId = targetFolderId,
                    NewFileName = newFileName,
                    IsProcessed = false,
                    RetryCount = 0
                };
                
                dbContext.SyncQueueItems.Add(queueItem);
                await dbContext.SaveChangesAsync();
                _logger.Information("Enqueued offline task: {Action} for {Path}", action, fullPath);
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to enqueue offline task: {Action} for {Path}", action, fullPath);
            }
        }
    }
}
