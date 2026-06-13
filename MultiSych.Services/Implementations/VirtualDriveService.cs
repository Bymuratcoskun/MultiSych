using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using MultiSych.Services.Interfaces;
using Serilog;

namespace MultiSych.Services.Implementations;

public class VirtualDriveService : IVirtualDriveService, IDisposable
{
    private readonly ILogger _logger = Log.ForContext<VirtualDriveService>();
    private readonly IPlatformMountProvider _mountProvider;
    private readonly IAccountStore _accountStore;
    
    // Oturum süresince hangi hesabın hangi sürücü harfine bağlandığını tutan takip sözlüğü (AccountId -> DrivePath)
    private readonly Dictionary<string, string> _mountedDrives = new();

    public VirtualDriveService(IPlatformMountProvider mountProvider, IAccountStore accountStore)
    {
        _mountProvider = mountProvider;
        _accountStore = accountStore;
    }

    public async Task<bool> MountDriveAsync(string accountId)
    {
        _logger.Information("VirtualDrive Simulation: Requesting mount for account {AccountId}", accountId);
        
        var account = await _accountStore.GetAccountByIdAsync(accountId);
        if (account == null)
        {
            _logger.Warning("Account not found. Cannot mount virtual drive.");
            return false;
        }

        if (_mountedDrives.ContainsKey(accountId))
        {
            _logger.Information("Virtual drive for {Email} is already mounted.", account.Email);
            return true;
        }

        try
        {
            var drivePath = _mountProvider.GetAvailableDriveLetter();
            var targetFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "MultiSych_Drives", accountId);
            
            var success = await _mountProvider.MountAsync(drivePath, targetFolder, $"{account.Provider} Drive");
            
            if (success)
            {
                _mountedDrives[accountId] = drivePath;
                _logger.Information("Successfully mounted {Provider} drive for {Email} at {MountPoint}", account.Provider, account.Email, drivePath);
                
                // OS Dosya Yöneticisinde (File Explorer) otomatik açma
                try
                {
                    if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                        Process.Start(new ProcessStartInfo { FileName = drivePath, UseShellExecute = true });
                    else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                        Process.Start(new ProcessStartInfo { FileName = "xdg-open", ArgumentList = { targetFolder }, UseShellExecute = false });
                    else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                        Process.Start(new ProcessStartInfo { FileName = "open", ArgumentList = { targetFolder }, UseShellExecute = false });
                }
                catch (Exception ex)
                {
                    _logger.Warning(ex, "Failed to automatically open the mounted drive in file explorer.");
                }
            }
            
            return success;
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Failed to mount virtual drive for account {AccountId}", accountId);
            return false;
        }
    }

    public async Task<bool> UnmountDriveAsync(string accountId)
    {
        if (_mountedDrives.TryGetValue(accountId, out var drivePath))
        {
            var success = await _mountProvider.UnmountAsync(drivePath);
            if (success)
            {
                _mountedDrives.Remove(accountId);
                _logger.Information("Successfully unmounted drive for account {AccountId} from {MountPoint}", accountId, drivePath);
                return true;
            }
        }
        return false;
    }

    public Task<bool> IsMountedAsync(string accountId) => Task.FromResult(_mountedDrives.ContainsKey(accountId));

    public void Dispose()
    {
        _logger.Information("Disposing VirtualDriveService. Unmounting all active drives...");
        var activeMounts = _mountedDrives.Keys.ToList();
        foreach (var accountId in activeMounts)
        {
            try
            {
                UnmountDriveAsync(accountId).GetAwaiter().GetResult();
            }
            catch (Exception ex) { _logger.Error(ex, "Failed to unmount drive for account {AccountId} during shutdown.", accountId); }
        }
    }
}
