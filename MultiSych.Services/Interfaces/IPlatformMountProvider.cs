using System.Threading.Tasks;

namespace MultiSych.Services.Interfaces
{
    public interface IPlatformMountProvider
    {
        string GetAvailableDriveLetter();
        Task<bool> MountAsync(string mountPoint, string targetPath, string volumeLabel);
        Task<bool> UnmountAsync(string mountPoint);
        Task UpdateLocalMountFolderAsync(string accountId, string targetPath);

        /// <summary>
        /// Bağlanan sürücüyü işletim sisteminin dosya yöneticisinde açar (best-effort,
        /// gerçek Process.Start çağrısı burada — bkz. docs/KARARLAR.md K11).
        /// </summary>
        void RevealInFileManager(string path);
    }
}
