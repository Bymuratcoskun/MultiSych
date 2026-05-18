using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using MultiSych.Services.Interfaces;
using Serilog;

namespace MultiSych.Services.Implementations
{
    public class PlatformMountProvider : IPlatformMountProvider
    {
        private readonly ILogger _logger = Log.ForContext<PlatformMountProvider>();

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
                _logger.Information("Simulating Linux mount at {MountPoint}", mountPoint);
                return true; // Linux/Avalonia için şimdilik simüle edildi
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
                _logger.Information("Simulating Linux unmount at {MountPoint}", mountPoint);
                return true;
            }
        }

        private async Task<bool> MountWindowsAsync(string driveLetter, string targetPath)
        {
            try
            {
                var drive = driveLetter.Replace("\\", "").Replace("/", "");
                var processInfo = new ProcessStartInfo
                {
                    FileName = "subst",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                // Güvenlik: Command Injection'ı önlemek için argümanları ArgumentList ile veriyoruz
                processInfo.ArgumentList.Add(drive);
                processInfo.ArgumentList.Add(targetPath);

                using var process = Process.Start(processInfo);
                if (process != null)
                {
                    await process.WaitForExitAsync();
                    if (process.ExitCode == 0) return true;
                    
                    var error = await process.StandardError.ReadToEndAsync();
                    _logger.Error("Windows subst mount failed: {Error}", error);
                }
            }
            catch (Exception ex) { _logger.Error(ex, "Exception during Windows mount"); }
            return false;
        }

        private async Task<bool> UnmountWindowsAsync(string driveLetter)
        {
            try
            {
                var drive = driveLetter.Replace("\\", "").Replace("/", "");
                var processInfo = new ProcessStartInfo
                {
                    FileName = "subst",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                // Güvenlik: Command Injection'ı önlemek için argümanları ArgumentList ile veriyoruz
                processInfo.ArgumentList.Add(drive);
                processInfo.ArgumentList.Add("/D");

                using var process = Process.Start(processInfo);
                if (process != null)
                {
                    await process.WaitForExitAsync();
                    return process.ExitCode == 0;
                }
            }
            catch (Exception ex) { _logger.Error(ex, "Exception during Windows unmount"); }
            return false;
        }
    }
}