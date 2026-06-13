using System.Threading.Tasks;

namespace MultiSych.Services.Interfaces;

public interface IVirtualDriveService
{
    Task<bool> MountDriveAsync(string accountId);
    Task<bool> UnmountDriveAsync(string accountId);
    Task<bool> IsMountedAsync(string accountId);
}
