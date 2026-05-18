using System.Threading.Tasks;

namespace MultiSych.Services.Interfaces
{
    public interface IPlatformMountProvider
    {
        string GetAvailableDriveLetter();
        Task<bool> MountAsync(string mountPoint, string targetPath, string volumeLabel);
        Task<bool> UnmountAsync(string mountPoint);
    }
}