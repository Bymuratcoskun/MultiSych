using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Versioning;
using System.Threading.Tasks;
using FuseDotNet;
using FuseDotNet.Extensions;
using LTRData.Extensions.Native.Memory;
using Microsoft.EntityFrameworkCore;
using MultiSych.Services.Data;
using MultiSych.Services.Interfaces;
using Serilog;

namespace MultiSych.Services.Implementations;

/// <summary>
/// LTRData.FuseDotNet'in resmi "MirrorFs" örneğinden (MIT lisans) uyarlandı
/// (docs/KARARLAR.md K22). Gerçek bir yerel klasörü (basePath — hesabın
/// zaten senkronize edilmiş yer tutucu/gerçek dosyaları içeren gölge
/// klasörü) çekirdek seviyesinde gerçek bir FUSE mount noktasına
/// yansıtır. Yazma geri senkronu burada YOK — o hâlâ PlatformMountProvider'ın
/// FileSystemWatcher'ında (basePath'i izliyor). OKUMA tarafı (K24/FAZ 8):
/// Open() çağrıldığında, yerel yer tutucu dosya gerçek bulut içeriğinden
/// küçükse (henüz indirilmemiş demektir), dosya açılmadan ÖNCE gerçek
/// içerik indirilip yerel dosyaya yazılır — kullanıcı artık boş dosya
/// görmüyor.
/// </summary>
[SupportedOSPlatform("linux")]
[SupportedOSPlatform("freebsd")]
internal sealed class CloudMirrorFsOperations(
    string basePath,
    string accountId,
    IStorageService storageService,
    IAccountStore accountStore,
    IDbContextFactory<LocalCacheDbContext> dbContextFactory) : IFuseOperations
{
    private readonly ILogger _logger = Log.ForContext<CloudMirrorFsOperations>();

    public string BasePath { get; } = basePath;

    private string GetPath(ReadOnlyNativeMemory<byte> fileNamePtr)
        => Path.Join(BasePath, FuseHelper.GetString(fileNamePtr));

    /// <summary>
    /// Yerel yer tutucu dosya, veritabanındaki gerçek bulut dosya boyutundan
    /// küçükse (henüz indirilmemiş) gerçek içeriği indirip yerel dosyanın
    /// üzerine yazar. Dosya izlenmiyorsa (DB'de yoksa) ya da zaten tam
    /// boyuttaysa hiçbir şey yapmaz — gereksiz tekrar indirme olmaz.
    /// </summary>
    private async Task EnsureRealContentDownloadedAsync(string localPath)
    {
        if (!File.Exists(localPath)) return;

        var relativePath = "/" + Path.GetRelativePath(BasePath, localPath).Replace('\\', '/');

        await using var dbContext = await dbContextFactory.CreateDbContextAsync();
        var fileEntity = await dbContext.CloudFiles.FirstOrDefaultAsync(
            f => f.AccountId == accountId && f.Path == relativePath && !f.IsDirectory);

        if (fileEntity == null) return;

        var localSize = new System.IO.FileInfo(localPath).Length;
        if (localSize >= fileEntity.FileSize) return;

        var credentials = await accountStore.GetAccountAsync(accountId);
        if (credentials == null)
        {
            _logger.Warning("FUSE lazy-download: account {AccountId} not found, cannot download {Path}", accountId, relativePath);
            return;
        }

        // Atomik indirme (K27/FAZ 11 G02, dış inceleme): doğrudan hedef dosyaya
        // yazmak yerine önce AYNI dizinde geçici bir dosyaya yazılır, sonra tek
        // bir rename ile hedefin üzerine taşınır. Bağlantı kesilirse / süreç
        // çökerse hedef dosya ya ESKİ hâliyle (tam) ya hiç değişmemiş kalır —
        // "yarım indirme tamamlanmış gibi görünür" riski ortadan kalkar. Geçici
        // dosya aynı dizinde olduğu için rename aynı dosya sisteminde ve atomik.
        var tempPath = localPath + ".multisych-indiriliyor-" + Guid.NewGuid().ToString("N");
        try
        {
            _logger.Information("FUSE lazy-download: fetching real content for {Path} ({Size} bytes)", relativePath, fileEntity.FileSize);
            using var remoteStream = await storageService.DownloadFileAsync(credentials, fileEntity.FileId);
            long downloadedBytes;
            await using (var tempStream = File.Open(tempPath, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                await remoteStream.CopyToAsync(tempStream);
                downloadedBytes = tempStream.Length;
            }

            if (downloadedBytes != fileEntity.FileSize)
            {
                // DB'deki boyut bazı durumlarda (ör. native Google Docs export)
                // yalnız bir tahmin olabilir (bkz. docs/KARARLAR.md K27) — tutmazsa
                // indirmeyi reddetmiyoruz, yalnız logluyoruz. 0 bayt ise (gerçek
                // bir boş yanıt) yine de kabul ediyoruz, ama bariz görünsün diye ayrı not.
                _logger.Information("FUSE lazy-download: {Path} için indirilen boyut ({Downloaded}) DB'deki beklenen boyuttan ({Expected}) farklı — kabul edildi, DB tahmini olabilir.", relativePath, downloadedBytes, fileEntity.FileSize);
            }

            File.Move(tempPath, localPath, overwrite: true);
        }
        catch (Exception ex)
        {
            _logger.Warning(ex, "FUSE lazy-download failed for {Path}; serving placeholder content.", relativePath);
            try { File.Delete(tempPath); } catch { /* geçici dosya zaten yoksa ya da silinemiyorsa önemli değil */ }
        }
    }

    public PosixResult Access(ReadOnlyNativeMemory<byte> fileNamePtr, PosixAccessMode mask)
    {
        var path = GetPath(fileNamePtr);
        return File.Exists(path) || Directory.Exists(path) ? PosixResult.Success : PosixResult.ENOENT;
    }

    public PosixResult Create(ReadOnlyNativeMemory<byte> fileNamePtr, int mode, ref FuseFileInfo fileInfo) => Open(fileNamePtr, ref fileInfo);

    public void Dispose() { }

    public PosixResult Flush(ReadOnlyNativeMemory<byte> readOnlySpan, ref FuseFileInfo fileInfo) => PosixResult.ENOSYS;

    public PosixResult FSync(ReadOnlyNativeMemory<byte> fileNamePtr, bool datasync, ref FuseFileInfo fileInfo) => PosixResult.ENOSYS;

    public PosixResult FSyncDir(ReadOnlyNativeMemory<byte> fileNamePtr, bool datasync, ref FuseFileInfo fileInfo) => PosixResult.ENOSYS;

    /// <summary>
    /// DB'de izlenen bir dosyaysa gerçek bulut boyutunu, değilse yerel boyutu
    /// döndürür. GetAttr çok sık çağrıldığı için senkron ve ucuz tutuluyor
    /// (EF Core DbContextFactory senkron çağrıyı destekliyor, ekstra iş
    /// parçacığı atlaması gerekmiyor).
    /// </summary>
    private long GetReportedSize(string localPath, long localSize)
    {
        try
        {
            var relativePath = "/" + Path.GetRelativePath(BasePath, localPath).Replace('\\', '/');
            using var dbContext = dbContextFactory.CreateDbContext();
            var fileEntity = dbContext.CloudFiles
                .FirstOrDefault(f => f.AccountId == accountId && f.Path == relativePath && !f.IsDirectory);
            return fileEntity != null && fileEntity.FileSize > localSize ? fileEntity.FileSize : localSize;
        }
        catch (Exception ex)
        {
            _logger.Warning(ex, "GetReportedSize sorgusu başarısız, yerel boyuta geri dönülüyor: {Path}", localPath);
            return localSize;
        }
    }

    public PosixResult GetAttr(ReadOnlyNativeMemory<byte> fileNamePtr, out FuseFileStat stat, ref FuseFileInfo fileInfo)
    {
        var path = GetPath(fileNamePtr);

        if (File.Exists(path))
        {
            var info = new System.IO.FileInfo(path);
            stat = new()
            {
                // Henüz indirilmemiş (yer tutucu, 0 bayt) dosyalar için bile
                // GERÇEK bulut boyutunu bildiriyoruz — aksi halde Dolphin/Nautilus
                // gibi dosya yöneticileri indirmeden önce her şeyi "0 B" gösterir,
                // bu da kullanıcıya "bozuk sürücü" izlenimi verir (K24/FAZ 8).
                st_size = GetReportedSize(path, info.Length),
                st_birthtim = info.CreationTimeUtc,
                st_mtim = info.LastWriteTimeUtc,
                st_ctim = info.LastWriteTimeUtc,
                st_atim = info.LastAccessTimeUtc,
                st_mode = info.Attributes.ToPosixFileMode()
            };
            return PosixResult.Success;
        }
        else if (Directory.Exists(path))
        {
            var info = new System.IO.DirectoryInfo(path);
            stat = new()
            {
                st_birthtim = info.CreationTimeUtc,
                st_mtim = info.LastWriteTimeUtc,
                st_ctim = info.LastWriteTimeUtc,
                st_atim = info.LastAccessTimeUtc,
                st_mode = info.Attributes.ToPosixFileMode()
            };
            return PosixResult.Success;
        }

        stat = default;
        return PosixResult.ENOENT;
    }

    public void Init(ref FuseConnInfo fuse_conn_info) { }

    public PosixResult IoCtl(ReadOnlyNativeMemory<byte> readOnlySpan, int cmd, nint arg, ref FuseFileInfo fileInfo, FuseIoctlFlags flags, nint data) => PosixResult.ENOSYS;

    public PosixResult Link(ReadOnlyNativeMemory<byte> from, ReadOnlyNativeMemory<byte> to) => PosixResult.ENOSYS;

    public PosixResult MkDir(ReadOnlyNativeMemory<byte> fileNamePtr, PosixFileMode mode)
    {
        Directory.CreateDirectory(GetPath(fileNamePtr));
        return PosixResult.Success;
    }

    public PosixResult Open(ReadOnlyNativeMemory<byte> fileNamePtr, ref FuseFileInfo fileInfo)
    {
        var path = GetPath(fileNamePtr);

        // FUSE callback'leri senkron — libfuse zaten her isteği kendi iş
        // parçacığında çalıştırıyor, burada bloklamak (indirme bitene kadar)
        // güvenli ve beklenen davranış (gerçek ağ dosya sistemlerinin de
        // yaptığı şey). .GetAwaiter().GetResult() deadlock riski taşımıyor
        // çünkü SynchronizationContext yok (konsol/arka plan iş parçacığı).
        EnsureRealContentDownloadedAsync(path).GetAwaiter().GetResult();

        fileInfo.Context = File.Open(path, fileInfo.flags.ToFileMode(), fileInfo.flags.ToFileAccess(), fileInfo.flags.ToFileShare());
        return PosixResult.Success;
    }

    public PosixResult OpenDir(ReadOnlyNativeMemory<byte> fileNamePtr, ref FuseFileInfo fileInfo)
        => Directory.Exists(GetPath(fileNamePtr)) ? PosixResult.Success : PosixResult.ENOENT;

    public PosixResult Read(ReadOnlyNativeMemory<byte> fileNamePtr, NativeMemory<byte> buffer, long position, out int readLength, ref FuseFileInfo fileInfo)
    {
        if (fileInfo.Context is not Stream stream)
        {
            readLength = 0;
            return PosixResult.EBADF;
        }

        stream.Position = position;
        readLength = stream.Read(buffer.Span);
        return PosixResult.Success;
    }

    public PosixResult ReadDir(ReadOnlyNativeMemory<byte> fileNamePtr, out IEnumerable<FuseDirEntry> entries, ref FuseFileInfo fileInfo, long offset, FuseReadDirFlags flags)
    {
        var path = GetPath(fileNamePtr);

        var files = Directory.EnumerateFiles(path)
            .Select(entry => new FuseDirEntry(Path.GetFileName(entry), 0, 0, new() { st_mode = PosixFileMode.Regular }));

        var dirs = Directory.EnumerateDirectories(path)
            .Select(entry => new FuseDirEntry(Path.GetFileName(entry), 0, 0, new() { st_mode = PosixFileMode.Directory }));

        entries = FuseHelper.DotEntries.Concat(files).Concat(dirs);
        return PosixResult.Success;
    }

    public PosixResult ReadLink(ReadOnlyNativeMemory<byte> fileNamePtr, NativeMemory<byte> target) => PosixResult.ENOSYS;

    public PosixResult Release(ReadOnlyNativeMemory<byte> fileNamePtr, ref FuseFileInfo fileInfo)
    {
        if (fileInfo.Context is Stream stream)
        {
            stream.Close();
        }
        return PosixResult.Success;
    }

    public PosixResult ReleaseDir(ReadOnlyNativeMemory<byte> fileNamePtr, ref FuseFileInfo fileInfo) => PosixResult.Success;

    public PosixResult Rename(ReadOnlyNativeMemory<byte> from, ReadOnlyNativeMemory<byte> to)
    {
        File.Move(GetPath(from), GetPath(to));
        return PosixResult.Success;
    }

    public PosixResult RmDir(ReadOnlyNativeMemory<byte> fileNamePtr)
    {
        Directory.Delete(GetPath(fileNamePtr));
        return PosixResult.Success;
    }

    public PosixResult StatFs(ReadOnlyNativeMemory<byte> fileNamePtr, out FuseVfsStat statvfs)
    {
        statvfs = new() { f_bsize = 512, f_bfree = 1, f_bavail = 1 };
        return PosixResult.Success;
    }

    public PosixResult SymLink(ReadOnlyNativeMemory<byte> from, ReadOnlyNativeMemory<byte> to) => PosixResult.ENOSYS;

    public PosixResult Truncate(ReadOnlyNativeMemory<byte> fileNamePtr, long size)
    {
        using var file = File.Open(GetPath(fileNamePtr), FileMode.Open, FileAccess.ReadWrite, FileShare.ReadWrite | FileShare.Delete);
        file.SetLength(size);
        return PosixResult.Success;
    }

    public PosixResult Unlink(ReadOnlyNativeMemory<byte> fileNamePtr)
    {
        File.Delete(GetPath(fileNamePtr));
        return PosixResult.Success;
    }

    public PosixResult UTime(ReadOnlyNativeMemory<byte> fileNamePtr, TimeSpec atime, TimeSpec mtime, ref FuseFileInfo fileInfo)
    {
        var path = GetPath(fileNamePtr);
        File.SetLastAccessTimeUtc(path, atime.ToDateTime().UtcDateTime);
        File.SetLastWriteTimeUtc(path, mtime.ToDateTime().UtcDateTime);
        return PosixResult.Success;
    }

    public PosixResult Write(ReadOnlyNativeMemory<byte> fileNamePtr, ReadOnlyNativeMemory<byte> buffer, long position, out int writtenLength, ref FuseFileInfo fileInfo)
    {
        if (fileInfo.Context is not Stream stream)
        {
            writtenLength = 0;
            return PosixResult.EBADF;
        }

        stream.Position = position;
        stream.Write(buffer.Span);
        writtenLength = buffer.Length;
        return PosixResult.Success;
    }

    public PosixResult ChMod(NativeMemory<byte> fileNamePtr, PosixFileMode mode)
    {
        if (!OperatingSystem.IsWindows())
        {
            File.SetUnixFileMode(GetPath(fileNamePtr), (UnixFileMode)mode);
        }
        return PosixResult.Success;
    }

    public PosixResult ChOwn(NativeMemory<byte> fileNamePtr, int uid, int gid) => PosixResult.Success;

    public PosixResult FAllocate(NativeMemory<byte> fileNamePtr, FuseAllocateMode mode, long offset, long length, ref FuseFileInfo fileInfo)
    {
        if (mode != 0) return PosixResult.ENOSYS;
        if (fileInfo.Context is not Stream stream) return PosixResult.EBADF;

        var newLength = checked(offset + length);
        if (newLength < stream.Length)
        {
            stream.SetLength(newLength);
        }
        return PosixResult.Success;
    }
}
