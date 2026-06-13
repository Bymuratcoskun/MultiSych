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
    private readonly ILogger _logger = Log.ForContext<CloudVirtualFileSystem>();

    // Anlık okumaları ram üzerinde tutacak geçici nesnemiz
    private class FileContext
    {
        public string FileId { get; set; } = string.Empty;
        public Stream? DataStream { get; set; }
        public string? LocalTempPath { get; set; }
        public bool IsModified { get; set; }
    }

    public CloudVirtualFileSystem(string accountId, IStorageService storageService, IDbContextFactory<LocalCacheDbContext> dbContextFactory)
    {
        _accountId = accountId;
        _storageService = storageService;
        _dbContextFactory = dbContextFactory;
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
                var newFile = new Data.Entities.CloudFileEntity
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
            if (parentDir == null) return DokanResult.DirectoryNotFound;
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

    public NtStatus ReadFile(string fileName, byte[] buffer, out int bytesRead, long offset, IDokanFileInfo info)
    {
        bytesRead = 0;
        if (info.Context is not FileContext ctx) return DokanResult.InvalidHandle;

        try
        {
            // Eğer stream (akış) henüz çekilmediyse On-Demand olarak (anlık) indiriyoruz.
            if (ctx.DataStream == null)
            {
                using var dbContext = _dbContextFactory.CreateDbContext();
                var accountEntity = dbContext.Accounts.FirstOrDefault(a => a.AccountId == _accountId);
                if (accountEntity == null) return DokanResult.AccessDenied;
                
                var credentials = new AccountCredentials 
                {
                    AccountId = accountEntity.AccountId, Email = accountEntity.Email, Provider = accountEntity.Provider,
                    AccessToken = accountEntity.AccessToken, RefreshToken = accountEntity.RefreshToken, ExpiresAt = accountEntity.ExpiresAt
                };

                _logger.Information("On-Demand Download triggered for file {FileName}", fileName);
                
                // Senkron bir yapı (Dokan) içerisinde asenkron stream çağırma
                ctx.DataStream = _storageService.DownloadFileAsync(credentials, ctx.FileId).GetAwaiter().GetResult();
            }

            if (ctx.DataStream.CanSeek)
                ctx.DataStream.Position = offset;
            
            bytesRead = ctx.DataStream.Read(buffer, 0, buffer.Length);
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
            ctx.DataStream?.Dispose(); // İşletim sistemi dosyayı okumayı bitirince RAM'i boşalt
            ctx.DataStream = null;
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
                    _logger.Information("Successfully deleted file {FileName} from cloud.", fileName);
                }
                catch (Exception ex)
                {
                    _logger.Error(ex, "Failed to delete file {FileName} from cloud.", fileName);
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

        if (dirEntity == null) return DokanResult.DirectoryNotFound;
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
                    _logger.Information("Successfully deleted directory {FileName} from cloud.", fileName);
                }
                catch (Exception ex)
                {
                    _logger.Error(ex, "Failed to delete directory {FileName} from cloud.", fileName);
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
            if (newParentPath != "/" && newParentEntity == null) return DokanResult.DirectoryNotFound;
            
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

            // Arka planda bulut taşıma/yeniden adlandırma işlemini tetikle
            Task.Run(async () =>
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

                    // newParentEntity.FileId, root için null olabilir. Servis "root" anahtar kelimesini yönetmelidir.
                    var newParentCloudId = newParentEntity?.FileId ?? "root";
                    await _storageService.MoveFileAsync(credentials, sourceEntity.FileId, newParentCloudId, newFileName);
                    _logger.Information("Successfully moved file {OldName} to {NewName} in the cloud.", oldName, newName);
                }
                catch (Exception ex)
                {
                    _logger.Error(ex, "Failed to move file {OldName} to {NewName} in the cloud.", oldName, newName);
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
            // Düzenleme işlemi için diske geçici bir stream oluşturuyoruz.
            if (ctx.DataStream == null || !ctx.DataStream.CanWrite)
            {
                ctx.LocalTempPath = Path.GetTempFileName();
                ctx.DataStream = new FileStream(ctx.LocalTempPath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.ReadWrite);
                ctx.IsModified = true;
            }

            ctx.DataStream.Position = offset;
            ctx.DataStream.Write(buffer, 0, buffer.Length);
            bytesWritten = buffer.Length;
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
        security = null!;
        return DokanResult.NotImplemented;
    }

    public NtStatus SetFileSecurity(string fileName, FileSystemSecurity security, AccessControlSections sections, IDokanFileInfo info)
    {
        return DokanResult.NotImplemented;
    }

    public NtStatus Mounted(IDokanFileInfo info)
    {
        _logger.Information("Dokan volume mounted");
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
}
