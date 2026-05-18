using MultiSych.Services.Models;

namespace MultiSych.Services.Interfaces
{
    public interface IVirtualDriveService
    {
        Task<List<VirtualDriveInfo>> GetMountedDrivesAsync();
        Task<VirtualDriveInfo> MountDriveAsync(AccountCredentials credentials);
        Task<bool> UnmountDriveAsync(string accountId);
    }
}
