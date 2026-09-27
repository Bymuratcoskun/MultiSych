using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Threading.Tasks;
using System.Xml.Linq;
using FuseDotNet;
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

        // fuse3-devel paketi kurulu olmayan uçlarda sürümsüz "libfuse3.so" sembolik
        // bağlantısı bulunmuyor (yalnız "libfuse3.so.4" var) — .NET'in P/Invoke çözücüsü
        // sürüm son ekini otomatik denemiyor, bu yüzden LTRData.FuseDotNet'in aradığı
        // "fuse3" adını burada elle sürümlü dosyaya yönlendiriyoruz. Bu makinede ölçüldü
        // (docs/KARARLAR.md K22): çözücü olmadan DllNotFoundException, olunca çalışıyor.
        static PlatformMountProvider()
        {
            NativeLibrary.SetDllImportResolver(typeof(IFuseOperations).Assembly, (libraryName, assembly, searchPath) =>
            {
                if (libraryName == "fuse3" && NativeLibrary.TryLoad("libfuse3.so.4", out var handle))
                {
                    return handle;
                }
                return IntPtr.Zero;
            });
        }

        // Aktif FUSE mount'ları (mountPoint -> operasyon nesnesi) — Dispose ve
        // UnmountAsync'in temizleyebilmesi için tutuluyor. CloudMirrorFsOperations
        // yalnız linux/freebsd işaretli (K18'de Windows tamamen kaldırıldığı için bu
        // sınıf da yalnız o platformlarda kullanılıyor) — CA1416 burada tek satırda
        // bastırılıyor, SecureStorageService.cs'teki aynı desenle tutarlı.
#pragma warning disable CA1416
        private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, CloudMirrorFsOperations> _activeFuseMounts = new();
#pragma warning restore CA1416

        public PlatformMountProvider(IStorageService storageService, IDbContextFactory<LocalCacheDbContext> dbContextFactory, RuntimeSyncSettings runtimeSyncSettings)
        {
            _storageService = storageService;
            _dbContextFactory = dbContextFactory;
            _runtimeSyncSettings = runtimeSyncSettings;
        }

        public string GetAvailableDriveLetter()
        {
            // Linux veya macOS için sürücü harfi mantığı yoktur, klasör yolu döndürürüz.
            var linuxPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "MultiSych_Drives", Guid.NewGuid().ToString("N").Substring(0, 8));
            Directory.CreateDirectory(linuxPath);
            return linuxPath;
        }

        public async Task<bool> MountAsync(string mountPoint, string targetPath, string volumeLabel)
        {
            _logger.Information("Mounting {TargetPath} to {MountPoint} (Label: {Label})", targetPath, mountPoint, volumeLabel);

            if (!Directory.Exists(targetPath))
            {
                Directory.CreateDirectory(targetPath);
            }

            return await MountLinuxAsync(mountPoint, targetPath, volumeLabel);
        }

        public async Task<bool> UnmountAsync(string mountPoint)
        {
            _logger.Information("Unmounting {MountPoint}", mountPoint);

            return await UnmountLinuxAsync(mountPoint);
        }

        private async Task<bool> MountLinuxAsync(string mountPoint, string targetPath, string volumeLabel)
        {
            try
            {
                _logger.Information("Starting real FUSE mount on {MountPoint} mirroring {TargetPath}", mountPoint, targetPath);

                if (!Directory.Exists(targetPath))
                {
                    Directory.CreateDirectory(targetPath);
                }

                // Mount noktası boş bir gerçek klasör olmalı (FUSE bunu üstüne biner,
                // sembolik bağlantı değil artık — bkz. docs/KARARLAR.md K22).
                try { Directory.Delete(mountPoint, true); } catch { }
                try { File.Delete(mountPoint); } catch { }
                Directory.CreateDirectory(mountPoint);

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
                                try { await File.WriteAllBytesAsync(localPath, Array.Empty<byte>()); }
                                catch (Exception ex) { _logger.Warning(ex, "Linux mount yer tutucu dosyası oluşturulamadı: {Path}", localPath); }
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

                // Gerçek FUSE mount'u ayrı bir arka plan iş parçacığında başlat — Mount()
                // çağrısı unmount edilene kadar bloklar, bu yüzden burada await edilemez.
#pragma warning disable CA1416
                var operations = new CloudMirrorFsOperations(targetPath);
                _activeFuseMounts[mountPoint] = operations;

                // "-f" ŞART: fuse_main varsayılan olarak süreci native fork() ile ikiye
                // ayırıyor. Çok iş parçacıklı bir .NET/GTK uygulamasında (GC, thread pool,
                // GTK ana döngüsü) fork() güvenli değil — bu makinede ölçüldü, "-f" olmadan
                // uygulama Mount Drive'a basınca donuyordu (K22 düzeltmesi). "-f" ile
                // fuse_main mevcut süreçte (bizim Task.Run arka plan iş parçacığımızda)
                // kalıp unmount edilene kadar bloklanıyor — fork hiç olmuyor.
                var mountArgs = new[] { "MultiSych", mountPoint, "-f" };
                _ = Task.Run(() =>
                {
                    try
                    {
                        operations.Mount(mountArgs, new FuseDotNet.Logging.ConsoleLogger());
                    }
                    catch (Exception ex)
                    {
                        _logger.Error(ex, "FUSE mount thread'i beklenmedik şekilde sonlandı: {MountPoint}", mountPoint);
                        _activeFuseMounts.TryRemove(mountPoint, out _);
                    }
                });
#pragma warning restore CA1416

                // fuse_main gerçekten mount'u kurana kadar kısa bir süre bekleyip
                // /proc/mounts'tan doğruluyoruz — "başladı" ile "gerçekten bağlandı"
                // farklı şeyler, iddia değil kanıt istiyoruz.
                var mounted = false;
                for (var i = 0; i < 30; i++)
                {
                    await Task.Delay(100);
                    if (IsRealMountActive(mountPoint)) { mounted = true; break; }
                }

                if (!mounted)
                {
                    _logger.Error("FUSE mount {MountPoint} için /proc/mounts'ta zamanında doğrulanamadı.", mountPoint);
#pragma warning disable CA1416
                    _activeFuseMounts.TryRemove(mountPoint, out _);
#pragma warning restore CA1416
                    return false;
                }

                AddDolphinBookmark(mountPoint, volumeLabel);

                return true;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Exception during Linux FUSE mount setup");
            }
            return false;
        }

        private static bool IsRealMountActive(string mountPoint)
        {
            try
            {
                return File.ReadAllLines("/proc/mounts").Any(line => line.Contains(" " + mountPoint + " "));
            }
            catch
            {
                return false;
            }
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

                RemoveDolphinBookmark(mountPoint);

#pragma warning disable CA1416
                if (_activeFuseMounts.TryRemove(mountPoint, out _))
                {
                    // Gerçek FUSE mount'ları "fusermount3 -u" ile kaldırılır (FuseDotNet
                    // kod içinden unmount API'si sunmuyor — README bunu da söylüyor).
                    // Bu makinede doğrulandı (K22 spike): temiz şekilde unmount ediyor.
                    using var process = Process.Start(new ProcessStartInfo
                    {
                        FileName = "fusermount3",
                        ArgumentList = { "-u", mountPoint },
                        UseShellExecute = false,
                        RedirectStandardError = true
                    });
                    process?.WaitForExit(5000);
                    if (process != null && process.ExitCode != 0)
                    {
                        var err = process.StandardError.ReadToEnd();
                        _logger.Warning("fusermount3 -u {MountPoint} sıfırdan farklı çıkış kodu döndü: {ExitCode} {Error}", mountPoint, process.ExitCode, err);
                    }
                }
#pragma warning restore CA1416

                try { Directory.Delete(mountPoint, false); } catch { }

                return Task.FromResult(true);
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Exception during Linux unmount");
            }
            return Task.FromResult(false);
        }

        // KDE Dolphin (ve diğer XBEL uyumlu dosya yöneticileri), bağlanan sanal sürücüleri
        // kendiliğinden "Yerler" (Places) kenar çubuğunda göstermez — bu yalnızca gerçek
        // GVfs/udisks aygıtları için otomatik olur. Bizim mount bir sembolik bağlantı
        // olduğu için burayı elle ~/.local/share/user-places.xbel'e yazarak sağlıyoruz.
        // Dolphin bu dosyayı canlı izliyor, yeniden başlatma gerekmiyor.
        private static string GetDolphinPlacesFilePath() =>
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "user-places.xbel");

        private void AddDolphinBookmark(string mountPoint, string volumeLabel)
        {
            try
            {
                var path = GetDolphinPlacesFilePath();
                XDocument doc;
                if (File.Exists(path))
                {
                    doc = XDocument.Load(path);
                }
                else
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                    doc = new XDocument(new XElement("xbel"));
                }

                var root = doc.Root!;
                var href = new Uri(mountPoint).AbsoluteUri;

                // Aynı mount noktası için eski bir kayıt varsa önce temizle (tekrar mount durumu)
                root.Elements("bookmark").Where(b => (string?)b.Attribute("href") == href).Remove();

                var bookmark = new XElement("bookmark",
                    new XAttribute("href", href),
                    new XElement("title", volumeLabel));
                root.Add(bookmark);

                doc.Save(path);
                _logger.Information("Dolphin 'Yerler' kısayolu eklendi: {Label} -> {MountPoint}", volumeLabel, mountPoint);
            }
            catch (Exception ex)
            {
                _logger.Warning(ex, "Dolphin 'Yerler' kısayolu eklenemedi (kritik değil, mount yine de çalışıyor).");
            }
        }

        private void RemoveDolphinBookmark(string mountPoint)
        {
            try
            {
                var path = GetDolphinPlacesFilePath();
                if (!File.Exists(path)) return;

                var doc = XDocument.Load(path);
                var href = new Uri(mountPoint).AbsoluteUri;
                var removed = doc.Root!.Elements("bookmark").Where(b => (string?)b.Attribute("href") == href).ToList();
                if (removed.Count == 0) return;

                foreach (var b in removed) b.Remove();
                doc.Save(path);
                _logger.Information("Dolphin 'Yerler' kısayolu kaldırıldı: {MountPoint}", mountPoint);
            }
            catch (Exception ex)
            {
                _logger.Warning(ex, "Dolphin 'Yerler' kısayolu kaldırılamadı (kritik değil).");
            }
        }

        public void RevealInFileManager(string path)
        {
            try
            {
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                    Process.Start(new ProcessStartInfo { FileName = "xdg-open", ArgumentList = { path }, UseShellExecute = false });
                else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                    Process.Start(new ProcessStartInfo { FileName = "open", ArgumentList = { path }, UseShellExecute = false });
            }
            catch (Exception ex)
            {
                _logger.Warning(ex, "Failed to reveal {Path} in file manager", path);
            }
        }

        public async Task UpdateLocalMountFolderAsync(string accountId, string targetPath)
        {
            try
            {
                _logger.Information("Updating local mount folder {TargetPath} for account {AccountId} from DB.", targetPath, accountId);

                // Mount klasörü kullanıcı tarafından silinmiş olabilir. Bu durumda hem aşağıdaki
                // dosya işlemleri hem de FileSystemWatcher'ın yeniden etkinleştirilmesi
                // "No such file or directory" (dosyası veya klasörü yok) hatası fırlatır.
                // Klasörü her döngüde garanti altına alarak bu tekrarlayan hatayı önlüyoruz.
                if (!Directory.Exists(targetPath))
                {
                    Directory.CreateDirectory(targetPath);
                }

                var watcher = _activeWatchers.Values.FirstOrDefault(w => string.Equals(w.Path, targetPath, StringComparison.OrdinalIgnoreCase));
                if (watcher != null) watcher.EnableRaisingEvents = false;

                try
                {
                    using var dbContext = _dbContextFactory.CreateDbContext();
                    var cachedFiles = await dbContext.CloudFiles.Where(f => f.AccountId == accountId).ToListAsync();
                    var dbPaths = cachedFiles.ToDictionary(f => Path.Combine(targetPath, f.Path.TrimStart('/')).Replace('\\', '/'), f => f, StringComparer.OrdinalIgnoreCase);

                    if (Directory.Exists(targetPath))
                    {
                        var localFiles = Directory.GetFiles(targetPath, "*", SearchOption.AllDirectories);
                        var localDirs = Directory.GetDirectories(targetPath, "*", SearchOption.AllDirectories);

                        var pendingQueuePaths = await dbContext.SyncQueueItems
                            .Where(q => q.AccountId == accountId && !q.IsProcessed)
                            .Select(q => q.LocalFilePath)
                            .ToListAsync();

                        var pendingQueueSet = new HashSet<string>(pendingQueuePaths, StringComparer.OrdinalIgnoreCase);

                        foreach (var lf in localFiles)
                        {
                            var cleanLf = lf.Replace('\\', '/');
                            if (!dbPaths.ContainsKey(cleanLf) && !pendingQueueSet.Contains(cleanLf))
                            {
                                try { File.Delete(lf); } catch { }
                            }
                        }

                        foreach (var ld in localDirs.OrderByDescending(d => d.Length))
                        {
                            var cleanLd = ld.Replace('\\', '/');
                            if (!dbPaths.ContainsKey(cleanLd))
                            {
                                try { Directory.Delete(ld, true); } catch { }
                            }
                        }
                    }

                    foreach (var file in cachedFiles.OrderBy(f => f.Path.Length))
                    {
                        var localPath = Path.Combine(targetPath, file.Path.TrimStart('/')).Replace('\\', '/');
                        var localDir = Path.GetDirectoryName(localPath);
                        if (!string.IsNullOrEmpty(localDir) && !Directory.Exists(localDir))
                        {
                            Directory.CreateDirectory(localDir);
                        }

                        if (file.IsDirectory)
                        {
                            if (!Directory.Exists(localPath)) Directory.CreateDirectory(localPath);
                        }
                        else
                        {
                            if (!File.Exists(localPath))
                            {
                                try { await File.WriteAllBytesAsync(localPath, Array.Empty<byte>()); }
                                catch (Exception ex) { _logger.Warning(ex, "Linux senkronizasyon yer tutucu dosyası oluşturulamadı: {Path}", localPath); }
                            }
                        }
                    }
                }
                finally
                {
                    // Watcher yalnızca izlediği dizin hâlâ mevcutsa yeniden etkinleştirilebilir.
                    if (watcher != null && Directory.Exists(targetPath))
                    {
                        try { watcher.EnableRaisingEvents = true; }
                        catch (Exception ex) { _logger.Warning(ex, "Failed to re-enable file watcher for {TargetPath}", targetPath); }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error updating local mount folder for account {AccountId}", accountId);
            }
        }

        internal static readonly System.Collections.Concurrent.ConcurrentDictionary<string, FileSystemWatcher> _activeWatchers = new();

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

                bool hasConflict = false;
                string conflictStrategy = _runtimeSyncSettings?.ConflictResolutionStrategy ?? "KeepBoth";

                if (!existing.FileId.StartsWith("temp_"))
                {
                    try
                    {
                        var cloudFile = await _storageService.GetFileAsync(credentials, existing.FileId);
                        if (cloudFile != null && (cloudFile.ModifiedDate - existing.UpdatedAt).TotalSeconds > 2.0)
                        {
                            _logger.Warning("Unix watcher: Conflict detected for {FileName}. Cloud version modified at {CloudTime}, Local version opened with last known update time {LocalTime}.", existing.FileName, cloudFile.ModifiedDate, existing.UpdatedAt);
                            hasConflict = true;
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.Warning(ex, "Unix watcher: Failed to fetch cloud file metadata for conflict checking of {FileName}. Assuming no conflict.", existing.FileName);
                    }
                }

                if (hasConflict)
                {
                    if (conflictStrategy == "ServerWins")
                    {
                        _logger.Information("Unix watcher: Conflict resolution strategy is ServerWins. Discarding local changes for {FileName}.", existing.FileName);
                        try { File.Delete(fullPath); } catch { }
                        return;
                    }
                    else if (conflictStrategy == "KeepBoth")
                    {
                        _logger.Information("Unix watcher: Conflict resolution strategy is KeepBoth. Renaming local version of {FileName}.", existing.FileName);
                        
                        var directoryPath = Path.GetDirectoryName(relativePath)?.Replace("\\", "/");
                        if (string.IsNullOrEmpty(directoryPath)) directoryPath = "/";
                        if (!directoryPath.EndsWith("/")) directoryPath += "/";

                        var origNameWithoutExt = Path.GetFileNameWithoutExtension(relativePath);
                        var ext = Path.GetExtension(relativePath);
                        var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                        var newFileName = $"{origNameWithoutExt} (Local Conflict {timestamp}){ext}";
                        var newRelativePath = directoryPath + newFileName;
                        var newFullPath = Path.Combine(targetPath, newRelativePath.TrimStart('/')).Replace('\\', '/');

                        try
                        {
                            var watcher = _activeWatchers.Values.FirstOrDefault(w => string.Equals(w.Path, targetPath, StringComparison.OrdinalIgnoreCase));
                            if (watcher != null) watcher.EnableRaisingEvents = false;
                            
                            File.Move(fullPath, newFullPath, true);
                            
                            if (watcher != null) watcher.EnableRaisingEvents = true;

                            fullPath = newFullPath;
                            relativePath = newRelativePath;

                            var fileInfo = new System.IO.FileInfo(fullPath);
                            var parentId = existing.ParentId;
                            var newFileEntity = new CloudFileEntity
                            {
                                AccountId = accountId,
                                FileId = "temp_" + Guid.NewGuid().ToString("N"),
                                FileName = newFileName,
                                Path = relativePath,
                                ParentId = parentId,
                                MimeType = "application/octet-stream",
                                FileSize = fileInfo.Length,
                                IsDirectory = false,
                                Provider = credentials.Provider ?? string.Empty,
                                CreatedAt = DateTime.UtcNow,
                                UpdatedAt = DateTime.UtcNow
                            };
                            dbContext.CloudFiles.Add(newFileEntity);
                            await dbContext.SaveChangesAsync();
                            
                            existing = newFileEntity;
                        }
                        catch (Exception moveEx)
                        {
                            _logger.Error(moveEx, "Unix watcher: Failed to rename local file during KeepBoth conflict resolution for {FileName}", existing.FileName);
                            return;
                        }
                    }
                }

                _logger.Information("Unix watcher: Local file modified: {Path}. Uploading change.", relativePath);
                var cloudId = await _storageService.UploadFileAsync(credentials, fullPath, existing.ParentId ?? "root");

                var fileInfoFinal = new System.IO.FileInfo(fullPath);
                existing.FileSize = fileInfoFinal.Length;
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
