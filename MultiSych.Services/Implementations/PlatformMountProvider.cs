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
using Serilog;

namespace MultiSych.Services.Implementations
{
    public class PlatformMountProvider : IPlatformMountProvider
    {
        private readonly ILogger _logger = Log.ForContext<PlatformMountProvider>();
        private readonly IStorageService _storageService;
        private readonly IDbContextFactory<LocalCacheDbContext> _dbContextFactory;

        public PlatformMountProvider(IStorageService storageService, IDbContextFactory<LocalCacheDbContext> dbContextFactory)
        {
            _storageService = storageService;
            _dbContextFactory = dbContextFactory;
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
                var cvfs = new CloudVirtualFileSystem(accountId, _storageService, _dbContextFactory);

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
                _logger.Information("Starting FUSE mount on {MountPoint} (Linux/macOS)", mountPoint);
                
                // Linux FUSE dosya sistemini bağlayabilmek için bağlama noktasının klasör olarak var olması gerekir.
                if (!Directory.Exists(mountPoint))
                    Directory.CreateDirectory(mountPoint);

                _logger.Warning("Linux/macOS FUSE mount is disabled in this build due to .NET 8 compatibility. MountPoint={MountPoint}", mountPoint);
                await Task.Delay(100);
                return false;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Exception during Linux FUSE mount");
            }
            return false;
        }

        private async Task<bool> UnmountLinuxAsync(string mountPoint)
        {
            try
            {
                _logger.Information("Unmounting FUSE volume from {MountPoint}", mountPoint);
                
                // Linux'ta FUSE sürücülerini güvenle ayırmak için fusermount, macOS'ta ise umount kullanılır.
                var processInfo = new ProcessStartInfo
                {
                    FileName = OperatingSystem.IsMacOS() ? "umount" : "fusermount",
                    Arguments = OperatingSystem.IsMacOS() ? $"\"{mountPoint}\"" : $"-u \"{mountPoint}\"",
                    RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false, CreateNoWindow = true
                };

                using var process = Process.Start(processInfo);
                if (process != null) { await process.WaitForExitAsync(); return process.ExitCode == 0; }
            }
            catch (Exception ex) { _logger.Error(ex, "Exception during Linux unmount"); }
            return false;
        }
    }
}
