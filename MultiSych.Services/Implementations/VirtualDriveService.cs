using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using MultiSych.Services.Interfaces;
using MultiSych.Services.Models;
using Serilog;

namespace MultiSych.Services.Implementations
{
    public class VirtualDriveService : IVirtualDriveService
    {
        private readonly Dictionary<string, VirtualDriveInfo> _mountedDrives = new(StringComparer.OrdinalIgnoreCase);
        private readonly ILogger _logger = Log.ForContext<VirtualDriveService>();
        private readonly IPlatformMountProvider _platformMountProvider;

        public VirtualDriveService(IPlatformMountProvider platformMountProvider)
        {
            _platformMountProvider = platformMountProvider ?? throw new ArgumentNullException(nameof(platformMountProvider));
        }

        public Task<List<VirtualDriveInfo>> GetMountedDrivesAsync()
        {
            return Task.FromResult(_mountedDrives.Values.ToList());
        }

        public async Task<VirtualDriveInfo> MountDriveAsync(AccountCredentials credentials)
        {
            if (credentials is null)
                throw new ArgumentNullException(nameof(credentials));

            var accountId = credentials.AccountId ?? string.Empty;
            if (_mountedDrives.TryGetValue(accountId, out var existingDrive))
                return existingDrive;

            var mountPoint = _platformMountProvider.GetAvailableDriveLetter();
            var targetPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "MultiSych", "Drives", credentials.AccountId ?? "Unknown");
            
            var driveInfo = new VirtualDriveInfo
            {
                AccountId = accountId,
                Provider = credentials.Provider ?? "Unknown",
                DriveLabel = $"{credentials.Provider} Drive ({credentials.Email})",
                MountPoint = mountPoint,
                IsMounted = true,
                StatusMessage = "Successfully mounted virtual drive."
            };

            var registered = await _platformMountProvider.MountAsync(mountPoint, targetPath, driveInfo.DriveLabel);
            if (!registered)
            {
                driveInfo.IsMounted = false;
                driveInfo.StatusMessage = "Failed to mount drive on OS.";
            }
            else
            {
                driveInfo.StatusMessage = "Drive is active.";
            }

            _mountedDrives[accountId] = driveInfo;
            _logger.Information("Registered virtual drive for account {AccountId} at {MountPoint}, registered={Registered}", credentials.AccountId, mountPoint, registered);
            return driveInfo;
        }

        public async Task<bool> UnmountDriveAsync(string accountId)
        {
            if (string.IsNullOrWhiteSpace(accountId))
                return false;

            if (!_mountedDrives.TryGetValue(accountId, out var driveInfo))
                return false;

            var unregistered = await _platformMountProvider.UnmountAsync(driveInfo.MountPoint);
            var removed = _mountedDrives.Remove(accountId);
            _logger.Information("Unmounted virtual drive for account {AccountId}, removed={Removed}, unregistered={Unregistered}", accountId, removed, unregistered);
            return removed;
        }


    }
}
