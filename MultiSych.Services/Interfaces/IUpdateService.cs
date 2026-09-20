using System.Threading.Tasks;

namespace MultiSych.Services.Interfaces;

public record UpdateInfo(string Version, string ReleaseNotes, string DownloadUrl);

public interface IUpdateService
{
    Task<UpdateInfo?> CheckForUpdateAsync();
    Task<bool> DownloadAndInstallAsync(UpdateInfo update);
    string CurrentVersion { get; }
}
