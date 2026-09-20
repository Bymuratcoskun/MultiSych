#if WINDOWS
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.AccessControl;
using DokanNet;
using Microsoft.EntityFrameworkCore;
using MultiSych.Services.Data;
using MultiSych.Services.Interfaces;
using MultiSych.Services.Models;
using MultiSych.Services.Configuration;
using Serilog;

namespace MultiSych.Services.Implementations;

/// <summary>
/// DokanNet (Windows) entegrasyonu için sanal dosya sistemi.
/// </summary>
public class CloudVirtualFileSystem : IDokanOperations
{
    private readonly string _accountId;
    private readonly IStorageService _storageService;
    private readonly IDbContextFactory<LocalCacheDbContext> _dbContextFactory;
    private readonly RuntimeSyncSettings? _runtimeSyncSettings;
    private readonly ILogger _logger = Log.ForContext<CloudVirtualFileSystem>();

    // Anlık okumaları ram üzerinde tutacak geçici nesnemiz
    public class FileContext
    {
        public string FileId { get; set; } = string.Empty;
        public string? LocalTempPath { get; set; }
        public bool IsModified { get; set; }
    }

    public CloudVirtualFileSystem(
        string accountId, 
        IStorageService storageService, 
        IDbContextFactory<LocalCacheDbContext> dbContextFactory,
        RuntimeSyncSettings? runtimeSyncSettings = null)
    {
        _accountId = accountId;
        _storageService = storageService;
        _dbContextFactory = dbContextFactory;
        _runtimeSyncSettings = runtimeSyncSettings;
    }

    public void Mount(string mountPoint, DokanOptions dokanOptions)
    {
        try
        {
            using var dokan = new Dokan(null);
            var builder = new DokanInstanceBuilder(dokan)
                .ConfigureOptions(options =>
                {
                    options.Options = dokanOptions;
                    options.MountPoint = mountPoint;
                });
            using var dokanInstance = builder.Build(this);
            dokanInstance.WaitForFileSystemClosed(uint.MaxValue);
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Dokan mount failed for {MountPoint}", mountPoint);
            throw;
        }
    }

    public NtStatus CreateFile(string fileName, DokanNet.FileAccess access, FileShare share, FileMode mode, FileOptions options, FileAttributes attributes, IDokanFileInfo info)
    {
        if (fileName == "\\")
        {
            info.IsDirectory = true;
            return DokanResult.Success;
        }

        var path = GetCleanPath(fileName);
        using var dbContext = _dbContextFactory.CreateDbContext();
        var file = dbContext.CloudFiles.FirstOrDefault(f => f.AccountId == _accountId && f.Path == path);

        if (file == null)
        {
            if (mode == FileMode.CreateNew || mode == FileMode.Create || mode == FileMode.OpenOrCreate)
            {
                var newFile = new CloudFileEntity
                {
                    AccountId = _accountId,
                    FileId = "temp_" + Guid.NewGuid().ToString("N"),
                    FileName = Path.GetFileName(path),
                    Path = path,
                    ParentId = GetParentIdFromPath(path),
                    IsDirectory = false,
                    FileSize = 0,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };
                dbContext.CloudFiles.Add(newFile);
                dbContext.SaveChanges();
                
                info.Context = new FileContext { FileId = newFile.FileId, IsModified = true };
                return DokanResult.Success;
            }
            return DokanResult.FileNotFound;
        }

        if (file.IsDirectory)
            info.IsDirectory = true;
        else
            info.Context = new FileContext { FileId = file.FileId }; // Dosya açıldığında id'sini context'e atıyoruz

        return DokanResult.Success;
    }

    public NtStatus GetFileInformation(string fileName, out FileInformation fileInfo, IDokanFileInfo info)
    {
        if (fileName == "\\")
        {
            fileInfo = new FileInformation
            {
                FileName = "\\",
                Attributes = FileAttributes.Directory,
                CreationTime = DateTime.UtcNow, LastAccessTime = DateTime.UtcNow, LastWriteTime = DateTime.UtcNow, Length = 0
            };
            return DokanResult.Success;
        }

        var path = GetCleanPath(fileName);

        // Windows Explorer'ın arka planda sürekli sorguladığı gizli/sistem dosyalarını yoksay (Performans optimizasyonu)
        var justFileName = Path.GetFileName(path);
        if (justFileName.Equals("desktop.ini", StringComparison.OrdinalIgnoreCase) || 
            justFileName.Equals("autorun.inf", StringComparison.OrdinalIgnoreCase) || 
            justFileName.Equals("Thumbs.db", StringComparison.OrdinalIgnoreCase))
        {
            fileInfo = default;
            return DokanResult.FileNotFound;
        }

        using var dbContext = _dbContextFactory.CreateDbContext();
        var file = dbContext.CloudFiles.FirstOrDefault(f => f.AccountId == _accountId && f.Path == path);

        if (file == null)
        {
            fileInfo = default;
            return DokanResult.FileNotFound;
        }

        fileInfo = new FileInformation
        {
            FileName = file.FileName,
            Attributes = file.IsDirectory ? FileAttributes.Directory : (FileAttributes.Normal | FileAttributes.Offline),
            CreationTime = file.CreatedAt,
            LastAccessTime = file.UpdatedAt,
            LastWriteTime = file.UpdatedAt,
            Length = file.FileSize
        };
        return DokanResult.Success;
    }

    public NtStatus FindFiles(string fileName, out IList<FileInformation> files, IDokanFileInfo info)
    {
        files = new List<FileInformation>();
        var path = GetCleanPath(fileName);

        using var dbContext = _dbContextFactory.CreateDbContext();

        string? parentId;
        if (path == "/")
        {
            // Root directory's children have a null ParentId
            parentId = null;
        }
        else
        {
            // Find the directory we are listing to get its FileId
            var parentDir = dbContext.CloudFiles.FirstOrDefault(f => f.AccountId == _accountId && f.Path == path && f.IsDirectory);
            if (parentDir == null) return DokanResult.FileNotFound;
            parentId = parentDir.FileId;
        }

        var cloudFiles = dbContext.CloudFiles.Where(f => f.AccountId == _accountId && f.ParentId == parentId).ToList();

        // Add standard virtual directory entries
        files.Add(new FileInformation { FileName = ".", Attributes = FileAttributes.Directory, CreationTime = DateTime.UtcNow, LastAccessTime = DateTime.UtcNow, LastWriteTime = DateTime.UtcNow });
        files.Add(new FileInformation { FileName = "..", Attributes = FileAttributes.Directory, CreationTime = DateTime.UtcNow, LastAccessTime = DateTime.UtcNow, LastWriteTime = DateTime.UtcNow });

        foreach (var file in cloudFiles)
        {
            files.Add(new FileInformation
            {
                FileName = file.FileName,
                Attributes = file.IsDirectory ? FileAttributes.Directory : (FileAttributes.Normal | FileAttributes.Offline),
                CreationTime = file.CreatedAt,
                LastAccessTime = file.UpdatedAt,
                LastWriteTime = file.UpdatedAt,
                Length = file.FileSize
            });
        }
        return DokanResult.Success;
    }

    private void EnsureLocalTempPath(FileContext ctx, string fileName)
    {
        if (!string.IsNullOrEmpty(ctx.LocalTempPath) && File.Exists(ctx.LocalTempPath))
            return;

        var cacheFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "MultiSych", "Cache", _accountId);
        if (!Directory.Exists(cacheFolder))
        {
            Directory.CreateDirectory(cacheFolder);
        }
        var localCachePath = Path.Combine(cacheFolder, ctx.FileId);

        using var dbContext = _dbContextFactory.CreateDbContext();
        var fileEntity = dbContext.CloudFiles.FirstOrDefault(f => f.AccountId == _accountId && f.FileId == ctx.FileId);
        
        var useEncryption = string.Equals(Environment.GetEnvironmentVariable("MULTISYCH_ENCRYPT_STORAGE"), "true", StringComparison.OrdinalIgnoreCase);
        var storagePassword = Environment.GetEnvironmentVariable("MULTISYCH_STORAGE_PASSWORD");

        if (!ctx.FileId.StartsWith("temp_"))
        {
            var cacheFileExists = File.Exists(localCachePath);
            if (!cacheFileExists)
            {
                var accountEntity = dbContext.Accounts.FirstOrDefault(a => a.AccountId == _accountId);
                if (accountEntity == null) throw new InvalidOperationException("Account not found.");

                var credentials = new AccountCredentials 
                {
                    AccountId = accountEntity.AccountId, Email = accountEntity.Email, Provider = accountEntity.Provider,
                    AccessToken = accountEntity.AccessToken, RefreshToken = accountEntity.RefreshToken, ExpiresAt = accountEntity.ExpiresAt
                };

                _logger.Information("On-Demand Download triggered (Cache Miss) for file {FileName}", fileName);
                
                var tempFilePath = Path.GetTempFileName();
                try
                {
                    var cloudStream = _storageService.DownloadFileAsync(credentials, ctx.FileId).GetAwaiter().GetResult();
                    using (var tempWriter = new FileStream(tempFilePath, FileMode.Create, System.IO.FileAccess.Write, System.IO.FileShare.None))
                    {
                        cloudStream.CopyTo(tempWriter);
                    }

                    if (File.Exists(localCachePath))
                    {
                        File.Delete(localCachePath);
                    }

                    if (useEncryption && !string.IsNullOrEmpty(storagePassword))
                    {
                        var plaintextBytes = File.ReadAllBytes(tempFilePath);
                        var encryptedBytes = MultiSych.Services.Security.SecurityHelper.EncryptBytes(plaintextBytes, storagePassword);
                        File.WriteAllBytes(localCachePath, encryptedBytes);
                        try { File.Delete(tempFilePath); } catch { }
                    }
                    else
                    {
                        File.Move(tempFilePath, localCachePath);
                    }
                }
                catch (Exception ex)
                {
                    _logger.Warning(ex, "Failed to download from cloud. Checking if stale cache is available.");
                    if (File.Exists(tempFilePath))
                    {
                        try { File.Delete(tempFilePath); } catch { }
                    }
                    if (!File.Exists(localCachePath))
                    {
                        throw;
                    }
                }
            }
        }

        if (ctx.FileId.StartsWith("temp_"))
        {
            ctx.LocalTempPath = Path.GetTempFileName();
        }
        else if (useEncryption && !string.IsNullOrEmpty(storagePassword) && File.Exists(localCachePath))
        {
            try
            {
                var encryptedBytes = File.ReadAllBytes(localCachePath);
                var decryptedBytes = MultiSych.Services.Security.SecurityHelper.DecryptBytes(encryptedBytes, storagePassword);
                var tempPlaintextPath = Path.GetTempFileName();
                File.WriteAllBytes(tempPlaintextPath, decryptedBytes);
                ctx.LocalTempPath = tempPlaintextPath;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to decrypt local cache file {FileId}", ctx.FileId);
                throw;
            }
        }
        else
        {
            ctx.LocalTempPath = localCachePath;
        }
    }

    public NtStatus ReadFile(string fileName, byte[] buffer, out int bytesRead, long offset, IDokanFileInfo info)
    {
        bytesRead = 0;
        if (info.Context is not FileContext ctx) return DokanResult.InvalidHandle;

        try
        {
            EnsureLocalTempPath(ctx, fileName);

            using (var fs = new FileStream(ctx.LocalTempPath, FileMode.Open, System.IO.FileAccess.Read, System.IO.FileShare.ReadWrite))
            {
                if (fs.CanSeek) fs.Position = offset;
                bytesRead = fs.Read(buffer, 0, buffer.Length);
            }
            return DokanResult.Success;
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Failed to read file {FileName}", fileName);
            return DokanResult.Error;
        }
    }

    // --- Diğer Dokan metotlarının temel (stub) uygulamaları ---

    public NtStatus FindFilesWithPattern(string fileName, string searchPattern, out IList<FileInformation> files, IDokanFileInfo info)
    {
        files = new List<FileInformation>();
        return DokanResult.NotImplemented;
    }

    private string GetCleanPath(string dokanPath)
    {
        if (string.IsNullOrEmpty(dokanPath) || dokanPath == "\\") return "/";
        // Normalize to forward slashes and ensure it starts with a slash
        return (dokanPath.Replace('\\', '/'));
    }

    public void Cleanup(string fileName, IDokanFileInfo info)
    {
        if (info.Context is FileContext ctx)
        {
            if (ctx.IsModified && !string.IsNullOrEmpty(ctx.LocalTempPath) && File.Exists(ctx.LocalTempPath))
            {
                try
                {
                    _logger.Information("Uploading modified file {FileName} to cloud...", fileName);

                    using var dbContext = _dbContextFactory.CreateDbContext();
                    var accountEntity = dbContext.Accounts.FirstOrDefault(a => a.AccountId == _accountId);
                    if (accountEntity != null)
                    {
                        var credentials = new AccountCredentials 
                        {
                            AccountId = accountEntity.AccountId, Email = accountEntity.Email, Provider = accountEntity.Provider,
                            AccessToken = accountEntity.AccessToken, RefreshToken = accountEntity.RefreshToken, ExpiresAt = accountEntity.ExpiresAt
                        };

                        var parentId = GetParentIdFromPath(GetCleanPath(fileName)) ?? "root";
                        var path = GetCleanPath(fileName);
                        var fileEntity = dbContext.CloudFiles.FirstOrDefault(f => f.AccountId == _accountId && f.Path == path);

                        var cacheFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "MultiSych", "Cache", _accountId);
                        var persistentCachePath = Path.Combine(cacheFolder, fileEntity?.FileId ?? ctx.FileId);
                        
                        var useEncryption = string.Equals(Environment.GetEnvironmentVariable("MULTISYCH_ENCRYPT_STORAGE"), "true", StringComparison.OrdinalIgnoreCase);
                        var storagePassword = Environment.GetEnvironmentVariable("MULTISYCH_STORAGE_PASSWORD");
                        
                        var persistentDir = Path.GetDirectoryName(persistentCachePath);
                        if (!string.IsNullOrEmpty(persistentDir)) Directory.CreateDirectory(persistentDir);
                        
                        if (useEncryption && !string.IsNullOrEmpty(storagePassword))
                        {
                            var plaintextBytes = File.ReadAllBytes(ctx.LocalTempPath);
                            var encryptedBytes = MultiSych.Services.Security.SecurityHelper.EncryptBytes(plaintextBytes, storagePassword);
                            File.WriteAllBytes(persistentCachePath, encryptedBytes);
                        }
                        else
                        {
                            File.Copy(ctx.LocalTempPath, persistentCachePath, true);
                        }

                        bool hasConflict = false;
                        string conflictStrategy = _runtimeSyncSettings?.ConflictResolutionStrategy ?? "KeepBoth";

                        if (fileEntity != null && !fileEntity.FileId.StartsWith("temp_"))
                        {
                            try
                            {
                                var cloudFile = _storageService.GetFileAsync(credentials, fileEntity.FileId).GetAwaiter().GetResult();
                                if (cloudFile != null && (cloudFile.ModifiedDate - fileEntity.UpdatedAt).TotalSeconds > 2.0)
                                {
                                    _logger.Warning("Conflict detected for {FileName}. Cloud version modified at {CloudTime}, Local version opened with last known update time {LocalTime}.", fileName, cloudFile.ModifiedDate, fileEntity.UpdatedAt);
                                    hasConflict = true;
                                }
                            }
                            catch (Exception ex)
                            {
                                _logger.Warning(ex, "Failed to fetch cloud file metadata for conflict checking of {FileName}. Assuming no conflict.", fileName);
                            }
                        }

                        if (hasConflict)
                        {
                            if (conflictStrategy == "ServerWins")
                            {
                                _logger.Information("Conflict resolution strategy is ServerWins. Discarding local changes for {FileName}.", fileName);
                                if (fileEntity != null)
                                {
                                    EvictLocalCache(fileEntity.FileId);
                                }
                                return;
                            }
                            else if (conflictStrategy == "KeepBoth")
                            {
                                _logger.Information("Conflict resolution strategy is KeepBoth. Renaming local version of {FileName}.", fileName);

                                var directoryPath = Path.GetDirectoryName(path)?.Replace("\\", "/");
                                if (string.IsNullOrEmpty(directoryPath)) directoryPath = "/";
                                if (!directoryPath.EndsWith("/")) directoryPath += "/";

                                var origNameWithoutExt = Path.GetFileNameWithoutExtension(path);
                                var ext = Path.GetExtension(path);
                                var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                                var newFileName = $"{origNameWithoutExt} (Local Conflict {timestamp}){ext}";
                                var newPath = directoryPath + newFileName;

                                var tempDir = Path.GetDirectoryName(ctx.LocalTempPath);
                                var newTempPath = Path.Combine(tempDir ?? "", Guid.NewGuid().ToString("N"));
                                File.Copy(ctx.LocalTempPath, newTempPath);

                                var newFileId = _storageService.UploadFileAsync(credentials, newTempPath, parentId).GetAwaiter().GetResult();

                                try { File.Delete(newTempPath); } catch { }

                                var fileInfo = new System.IO.FileInfo(ctx.LocalTempPath);
                                dbContext.CloudFiles.Add(new CloudFileEntity
                                {
                                    AccountId = _accountId,
                                    FileId = newFileId,
                                    FileName = newFileName,
                                    Path = newPath,
                                    ParentId = parentId,
                                    IsDirectory = false,
                                    FileSize = fileInfo.Length,
                                    MimeType = fileEntity?.MimeType ?? "application/octet-stream",
                                    Provider = accountEntity.Provider ?? string.Empty,
                                    CreatedAt = DateTime.UtcNow,
                                    UpdatedAt = DateTime.UtcNow
                                });
                                dbContext.SaveChanges();

                                if (fileEntity != null)
                                {
                                    EvictLocalCache(fileEntity.FileId);
                                }
                                return;
                            }
                        }

                        var uploadedFileId = _storageService.UploadFileAsync(credentials, ctx.LocalTempPath, parentId).GetAwaiter().GetResult();

                        var fileInfoNormal = new System.IO.FileInfo(ctx.LocalTempPath);

                        if (fileEntity != null)
                        {
                            if (fileEntity.FileId.StartsWith("temp_"))
                            {
                                dbContext.CloudFiles.Remove(fileEntity);
                                dbContext.SaveChanges();

                                var finalFileEntity = new CloudFileEntity
                                {
                                    AccountId = _accountId,
                                    FileId = uploadedFileId,
                                    FileName = fileEntity.FileName,
                                    Path = fileEntity.Path,
                                    ParentId = fileEntity.ParentId,
                                    IsDirectory = false,
                                    FileSize = fileInfoNormal.Length,
                                    MimeType = fileEntity.MimeType,
                                    Provider = accountEntity.Provider ?? string.Empty,
                                    CreatedAt = DateTime.UtcNow,
                                    UpdatedAt = DateTime.UtcNow
                                };
                                dbContext.CloudFiles.Add(finalFileEntity);
                                dbContext.SaveChanges();

                                var oldEncryptedCachePath = Path.Combine(cacheFolder, fileEntity.FileId);
                                var newEncryptedCachePath = Path.Combine(cacheFolder, uploadedFileId);
                                if (File.Exists(oldEncryptedCachePath))
                                {
                                    try { File.Move(oldEncryptedCachePath, newEncryptedCachePath, true); } catch { }
                                }
                            }
                            else
                            {
                                fileEntity.FileSize = fileInfoNormal.Length;
                                fileEntity.UpdatedAt = DateTime.UtcNow;
                                if (!string.IsNullOrEmpty(uploadedFileId))
                                {
                                    fileEntity.FileId = uploadedFileId;
                                }
                                dbContext.SaveChanges();
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.Error(ex, "Failed to upload modified file {FileName} to cloud during Cleanup. Queueing for offline sync.", fileName);
                    try
                    {
                        using var queueDb = _dbContextFactory.CreateDbContext();
                        var relativePath = GetCleanPath(fileName);
                        var fileEntity = queueDb.CloudFiles.FirstOrDefault(f => f.AccountId == _accountId && f.Path == relativePath);
                        var parentId = GetParentIdFromPath(relativePath) ?? "root";

                        var cacheFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "MultiSych", "Cache", _accountId);
                        var persistentCachePath = Path.Combine(cacheFolder, fileEntity?.FileId ?? ctx.FileId);

                        queueDb.SyncQueueItems.Add(new SyncQueueItemEntity
                        {
                            AccountId = _accountId,
                            Action = "Upload",
                            FileId = fileEntity?.FileId ?? string.Empty,
                            LocalFilePath = persistentCachePath,
                            TargetFolderId = parentId,
                            NewFileName = string.Empty,
                            IsProcessed = false,
                            RetryCount = 0
                        });
                        queueDb.SaveChanges();
                    }
                    catch (Exception dbEx)
                    {
                        _logger.Error(dbEx, "Failed to queue offline upload for {FileName}", fileName);
                    }
                }
            }

            if (!string.IsNullOrEmpty(ctx.LocalTempPath) && File.Exists(ctx.LocalTempPath))
            {
                var cacheRoot = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "MultiSych", "Cache");
                if (!ctx.LocalTempPath.StartsWith(cacheRoot, StringComparison.OrdinalIgnoreCase))
                {
                    try { File.Delete(ctx.LocalTempPath); } catch { }
                }
            }
        }
    }

    private void EvictLocalCache(string fileId)
    {
        var cacheFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "MultiSych", "Cache", _accountId);
        var localCachePath = Path.Combine(cacheFolder, fileId);
        if (File.Exists(localCachePath))
        {
            try { File.Delete(localCachePath); } catch { }
        }
    }

    public void CloseFile(string fileName, IDokanFileInfo info) { }

    public NtStatus FlushFileBuffers(string fileName, IDokanFileInfo info) => DokanResult.Success;

    public NtStatus DeleteFile(string fileName, IDokanFileInfo info)
    {
        var path = GetCleanPath(fileName);
        using var dbContext = _dbContextFactory.CreateDbContext();
        var fileEntity = dbContext.CloudFiles.FirstOrDefault(f => f.AccountId == _accountId && f.Path == path);

        if (fileEntity == null) return DokanResult.FileNotFound;
        if (fileEntity.IsDirectory) return DokanResult.AccessDenied; // Klasörler için DeleteDirectory metodu çalışır

        try
        {
            var fileId = fileEntity.FileId;
            
            // Dosyayı yerel veritabanından (Sanal Sürücü önbelleğinden) derhal kaldırıyoruz
            dbContext.CloudFiles.Remove(fileEntity);
            dbContext.SaveChanges();

            // İşletim sistemini bekletmemek için buluttan silme (veya çöpe taşıma) işlemini arka plana atıyoruz
            Task.Run(async () => 
            {
                int maxRetries = 3;
                int delayMs = 2000;

                for (int attempt = 1; attempt <= maxRetries; attempt++)
                {
                    try
                    {
                        using var bgDbContext = _dbContextFactory.CreateDbContext();
                        var accountEntity = bgDbContext.Accounts.FirstOrDefault(a => a.AccountId == _accountId);
                        if (accountEntity == null) return;
                        
                        var credentials = new AccountCredentials 
                        {
                            AccountId = accountEntity.AccountId, Email = accountEntity.Email, Provider = accountEntity.Provider,
                            AccessToken = accountEntity.AccessToken, RefreshToken = accountEntity.RefreshToken, ExpiresAt = accountEntity.ExpiresAt
                        };

                        await _storageService.DeleteFileAsync(credentials, fileId);
                        _logger.Information("Successfully deleted file {FileName} from cloud on attempt {Attempt}.", fileName, attempt);
                        break;
                    }
                    catch (Exception ex)
                    {
                        if (attempt == maxRetries)
                        {
                            _logger.Error(ex, "Failed to delete file {FileName} from cloud after {MaxRetries} attempts. Queueing for offline sync.", fileName, maxRetries);
                            try
                            {
                                using var queueDb = _dbContextFactory.CreateDbContext();
                                queueDb.SyncQueueItems.Add(new SyncQueueItemEntity
                                {
                                    AccountId = _accountId,
                                    Action = "Delete",
                                    FileId = fileId,
                                    LocalFilePath = string.Empty,
                                    TargetFolderId = string.Empty,
                                    NewFileName = string.Empty,
                                    IsProcessed = false,
                                    RetryCount = 0
                                });
                                queueDb.SaveChanges();
                            }
                            catch (Exception dbEx)
                            {
                                _logger.Error(dbEx, "Failed to queue offline delete for {FileName}", fileName);
                            }
                        }
                        else
                        {
                            _logger.Warning(ex, "Attempt {Attempt} failed to delete file {FileName} from cloud. Retrying in {Delay}ms...", attempt, fileName, delayMs);
                            await Task.Delay(delayMs);
                            delayMs *= 2; // Exponential Backoff
                        }
                    }
                }
            });

            return DokanResult.Success;
        }
        catch (Exception ex) { _logger.Error(ex, "Failed to process local DeleteFile for {FileName}", fileName); }
        return DokanResult.Error;
    }

    public NtStatus DeleteDirectory(string fileName, IDokanFileInfo info)
    {
        var path = GetCleanPath(fileName);
        using var dbContext = _dbContextFactory.CreateDbContext();
        var dirEntity = dbContext.CloudFiles.FirstOrDefault(f => f.AccountId == _accountId && f.Path == path);

        if (dirEntity == null) return DokanResult.FileNotFound;
        if (!dirEntity.IsDirectory) return DokanResult.NotADirectory;

        // Check if directory is empty
        var hasChildren = dbContext.CloudFiles.Any(f => f.AccountId == _accountId && f.ParentId == dirEntity.FileId);
        if (hasChildren) return DokanResult.DirectoryNotEmpty;

        try
        {
            var fileId = dirEntity.FileId;
            dbContext.CloudFiles.Remove(dirEntity);
            dbContext.SaveChanges();

            // Background task to delete from cloud
            Task.Run(async () =>
            {
                int maxRetries = 3;
                int delayMs = 2000;

                for (int attempt = 1; attempt <= maxRetries; attempt++)
                {
                    try
                    {
                        using var bgDbContext = _dbContextFactory.CreateDbContext();
                        var accountEntity = bgDbContext.Accounts.FirstOrDefault(a => a.AccountId == _accountId);
                        if (accountEntity == null) return;

                        var credentials = new AccountCredentials
                        {
                            AccountId = accountEntity.AccountId, Email = accountEntity.Email, Provider = accountEntity.Provider,
                            AccessToken = accountEntity.AccessToken, RefreshToken = accountEntity.RefreshToken, ExpiresAt = accountEntity.ExpiresAt
                        };

                        await _storageService.DeleteFileAsync(credentials, fileId);
                        _logger.Information("Successfully deleted directory {FileName} from cloud on attempt {Attempt}.", fileName, attempt);
                        break;
                    }
                    catch (Exception ex)
                    {
                        if (attempt == maxRetries)
                        {
                            _logger.Error(ex, "Failed to delete directory {FileName} from cloud after {MaxRetries} attempts. Queueing for offline sync.", fileName, maxRetries);
                            try
                            {
                                using var queueDb = _dbContextFactory.CreateDbContext();
                                queueDb.SyncQueueItems.Add(new SyncQueueItemEntity
                                {
                                    AccountId = _accountId,
                                    Action = "Delete",
                                    FileId = fileId,
                                    LocalFilePath = string.Empty,
                                    TargetFolderId = string.Empty,
                                    NewFileName = string.Empty,
                                    IsProcessed = false,
                                    RetryCount = 0
                                });
                                queueDb.SaveChanges();
                            }
                            catch (Exception dbEx)
                            {
                                _logger.Error(dbEx, "Failed to queue offline delete for directory {FileName}", fileName);
                            }
                        }
                        else
                        {
                            _logger.Warning(ex, "Attempt {Attempt} failed to delete directory {FileName} from cloud. Retrying in {Delay}ms...", attempt, fileName, delayMs);
                            await Task.Delay(delayMs);
                            delayMs *= 2; // Exponential Backoff
                        }
                    }
                }
            });

            return DokanResult.Success;
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Failed to process local DeleteDirectory for {FileName}", fileName);
            return DokanResult.Error;
        }
    }

    public NtStatus MoveFile(string oldName, string newName, bool replace, IDokanFileInfo info)
    {
        var oldPath = GetCleanPath(oldName);
        var newPath = GetCleanPath(newName);

        using var dbContext = _dbContextFactory.CreateDbContext();
        var sourceEntity = dbContext.CloudFiles.FirstOrDefault(f => f.AccountId == _accountId && f.Path == oldPath);
        if (sourceEntity == null) return DokanResult.FileNotFound;

        var destinationEntity = dbContext.CloudFiles.FirstOrDefault(f => f.AccountId == _accountId && f.Path == newPath);
        if (destinationEntity != null)
        {
            // Replacing is complex, let's deny for now.
            return DokanResult.FileExists;
        }

        try
        {
            var oldSourcePath = sourceEntity.Path;
            var newFileName = Path.GetFileName(newPath);
            var newParentPath = Path.GetDirectoryName(newPath)?.Replace('\\', '/') ?? "/";
            if (string.IsNullOrEmpty(newParentPath) || newParentPath == "\\") newParentPath = "/";

            var newParentEntity = dbContext.CloudFiles.FirstOrDefault(f => f.AccountId == _accountId && f.Path == newParentPath);
            if (newParentPath != "/" && newParentEntity == null) return DokanResult.FileNotFound;
            
            sourceEntity.Path = newPath;
            sourceEntity.FileName = newFileName;
            sourceEntity.ParentId = newParentEntity?.FileId;
            sourceEntity.UpdatedAt = DateTime.UtcNow;

            if (sourceEntity.IsDirectory)
            {
                var children = dbContext.CloudFiles.Where(f => f.AccountId == _accountId && f.Path.StartsWith(oldSourcePath + "/")).ToList();
                foreach (var child in children)
                {
                    child.Path = newPath + child.Path.Substring(oldSourcePath.Length);
                    child.UpdatedAt = DateTime.UtcNow;
                }
            }
            
            dbContext.SaveChanges();
            _logger.Information("File {OldName} moved to {NewName} locally. Triggering cloud move operation.", oldName, newName);

            var newParentCloudId = newParentEntity?.FileId ?? "root";

            // Arka planda bulut taşıma/yeniden adlandırma işlemini tetikle (Retry Mekanizmalı)
            Task.Run(async () =>
            {
                int maxRetries = 3;
                int delayMs = 2000;

                for (int attempt = 1; attempt <= maxRetries; attempt++)
                {
                    try
                    {
                        using var bgDbContext = _dbContextFactory.CreateDbContext();
                        var accountEntity = bgDbContext.Accounts.FirstOrDefault(a => a.AccountId == _accountId);
                        if (accountEntity == null) return;

                        var credentials = new AccountCredentials 
                        {
                            AccountId = accountEntity.AccountId, Email = accountEntity.Email, Provider = accountEntity.Provider,
                            AccessToken = accountEntity.AccessToken, RefreshToken = accountEntity.RefreshToken, ExpiresAt = accountEntity.ExpiresAt
                        };

                        await _storageService.MoveFileAsync(credentials, sourceEntity.FileId, newParentCloudId, newFileName);
                        
                        _logger.Information("Successfully moved file {OldName} to {NewName} in the cloud on attempt {Attempt}.", oldName, newName, attempt);
                        break; // Başarılı olursa döngüden çık
                    }
                    catch (Exception ex)
                    {
                        if (attempt == maxRetries)
                        {
                            _logger.Error(ex, "Failed to move file {OldName} to {NewName} in the cloud after {MaxRetries} attempts. Queueing for offline sync.", oldName, newName, maxRetries);
                            try
                            {
                                using var queueDb = _dbContextFactory.CreateDbContext();
                                queueDb.SyncQueueItems.Add(new SyncQueueItemEntity
                                {
                                    AccountId = _accountId,
                                    Action = "Move",
                                    FileId = sourceEntity.FileId,
                                    LocalFilePath = string.Empty,
                                    TargetFolderId = newParentCloudId,
                                    NewFileName = newFileName ?? string.Empty,
                                    IsProcessed = false,
                                    RetryCount = 0
                                });
                                queueDb.SaveChanges();
                            }
                            catch (Exception dbEx)
                            {
                                _logger.Error(dbEx, "Failed to queue offline move for {OldName}", oldName);
                            }
                        }
                        else
                        {
                            _logger.Warning(ex, "Attempt {Attempt} failed to move file {OldName} in the cloud. Retrying in {Delay}ms...", attempt, oldName, delayMs);
                            await Task.Delay(delayMs);
                            delayMs *= 2; // Exponential Backoff (Kademeli Gecikme: 2sn, 4sn, 8sn)
                        }
                    }
                }
            });

            return DokanResult.Success;
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Failed to process MoveFile from {OldName} to {NewName}", oldName, newName);
            return DokanResult.Error;
        }
    }

    public NtStatus SetEndOfFile(string fileName, long length, IDokanFileInfo info) => DokanResult.Error;

    public NtStatus SetAllocationSize(string fileName, long length, IDokanFileInfo info) => DokanResult.Error;

    public NtStatus SetFileAttributes(string fileName, FileAttributes attributes, IDokanFileInfo info) => DokanResult.Error;

    public NtStatus SetFileTime(string fileName, DateTime? creationTime, DateTime? lastAccessTime, DateTime? lastWriteTime, IDokanFileInfo info) => DokanResult.Error;

    public NtStatus UnlockFile(string fileName, long offset, long length, IDokanFileInfo info) => DokanResult.Success;

    public NtStatus WriteFile(string fileName, byte[] buffer, out int bytesWritten, long offset, IDokanFileInfo info)
    {
        bytesWritten = 0;
        if (info.Context is not FileContext ctx) return DokanResult.InvalidHandle;

        try
        {
            EnsureLocalTempPath(ctx, fileName);

            using (var fs = new FileStream(ctx.LocalTempPath, FileMode.OpenOrCreate, System.IO.FileAccess.Write, System.IO.FileShare.ReadWrite))
            {
                fs.Position = offset;
                fs.Write(buffer, 0, buffer.Length);
                bytesWritten = buffer.Length;
            }
            ctx.IsModified = true;
            return DokanResult.Success;
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Failed to write virtual file {FileName}", fileName);
            return DokanResult.Error;
        }
    }

    public NtStatus GetDiskFreeSpace(out long freeBytesAvailable, out long totalNumberOfBytes, out long totalNumberOfFreeBytes, IDokanFileInfo info)
    {
        freeBytesAvailable = 512 * 1024 * 1024;
        totalNumberOfBytes = 1024 * 1024 * 1024;
        totalNumberOfFreeBytes = 512 * 1024 * 1024;
        return DokanResult.Success;
    }

    public NtStatus GetVolumeInformation(out string volumeLabel, out FileSystemFeatures features, out string fileSystemName, out uint maximumComponentLength, IDokanFileInfo info)
    {
        volumeLabel = "MultiSych Drive";
        fileSystemName = "NTFS";
        maximumComponentLength = 256;
        features = FileSystemFeatures.CasePreservedNames | FileSystemFeatures.CaseSensitiveSearch | FileSystemFeatures.PersistentAcls | FileSystemFeatures.SupportsRemoteStorage | FileSystemFeatures.UnicodeOnDisk;
        return DokanResult.Success;
    }

    public NtStatus GetFileSecurity(string fileName, out FileSystemSecurity security, AccessControlSections sections, IDokanFileInfo info)
    {
        if (!OperatingSystem.IsWindows())
        {
            security = null!;
            return DokanResult.NotImplemented;
        }

        try
        {
            var path = GetCleanPath(fileName);
            using var dbContext = _dbContextFactory.CreateDbContext();
            var file = dbContext.CloudFiles.FirstOrDefault(f => f.AccountId == _accountId && f.Path == path);

            if (file == null && fileName != "\\")
            {
                security = null!;
                return DokanResult.FileNotFound;
            }

            if (info.IsDirectory)
            {
                var dirSecurity = new DirectorySecurity();
                dirSecurity.AddAccessRule(new FileSystemAccessRule(
                    new System.Security.Principal.SecurityIdentifier(System.Security.Principal.WellKnownSidType.BuiltinUsersSid, null),
                    FileSystemRights.FullControl,
                    InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit,
                    PropagationFlags.None,
                    AccessControlType.Allow));
                security = dirSecurity;
            }
            else
            {
                var fileSecurity = new FileSecurity();
                fileSecurity.AddAccessRule(new FileSystemAccessRule(
                    new System.Security.Principal.SecurityIdentifier(System.Security.Principal.WellKnownSidType.BuiltinUsersSid, null),
                    FileSystemRights.FullControl,
                    AccessControlType.Allow));
                security = fileSecurity;
            }

            return DokanResult.Success;
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "GetFileSecurity failed for {FileName}", fileName);
            security = null!;
            return DokanResult.Error;
        }
    }

    public NtStatus SetFileSecurity(string fileName, FileSystemSecurity security, AccessControlSections sections, IDokanFileInfo info)
    {
        return DokanResult.Success;
    }

    public NtStatus Mounted(string mountPoint, IDokanFileInfo info)
    {
        _logger.Information("Dokan volume mounted at {mountPoint}", mountPoint);
        return DokanResult.Success;
    }

    public NtStatus Unmounted(IDokanFileInfo info)
    {
        _logger.Information("Dokan volume unmounted");
        return DokanResult.Success;
    }

    public NtStatus FindStreams(string fileName, out IList<FileInformation> streams, IDokanFileInfo info)
    {
        streams = new List<FileInformation>();
        return DokanResult.NotImplemented;
    }

    public NtStatus LockFile(string fileName, long offset, long length, IDokanFileInfo info)
    {
        return DokanResult.Success;
    }

    private string? GetParentIdFromPath(string path)
    {
        var directoryPath = Path.GetDirectoryName(path)?.Replace('\\', '/');
        if (string.IsNullOrEmpty(directoryPath) || directoryPath == "/")
        {
            return null;
        }

        using var dbContext = _dbContextFactory.CreateDbContext();
        var parent = dbContext.CloudFiles.FirstOrDefault(f => f.AccountId == _accountId && f.Path == directoryPath && f.IsDirectory);
        return parent?.FileId;
    }
}
#endif
