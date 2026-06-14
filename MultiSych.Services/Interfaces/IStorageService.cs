using MultiSych.Services.Models;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace MultiSych.Services.Interfaces
{
    public interface IStorageService
    {
        Task<List<CloudFile>> ListFilesAsync(AccountCredentials credentials, string folderId = "root");
        Task<CloudFile> GetFileAsync(AccountCredentials credentials, string fileId);
        Task<string> UploadFileAsync(AccountCredentials credentials, string filePath, string destinationFolderId = "root");
        Task<bool> DeleteFileAsync(AccountCredentials credentials, string fileId);
        Task<Stream> DownloadFileAsync(AccountCredentials credentials, string fileId);
        Task SyncStorageAsync(AccountCredentials credentials);
        Task<List<CloudFile>> SearchFilesAsync(AccountCredentials credentials, string query);
        Task<bool> MoveFileAsync(AccountCredentials credentials, string fileId, string newParentId, string? newName = null);
    }
}
