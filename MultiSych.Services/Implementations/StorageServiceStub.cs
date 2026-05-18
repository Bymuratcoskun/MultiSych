using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using MultiSych.Services.Interfaces;
using MultiSych.Services.Models;
using Serilog;

namespace MultiSych.Services.Implementations
{
    public class StorageServiceStub : IStorageService
    {
        private readonly ILogger _logger;

        public StorageServiceStub()
        {
            _logger = Log.ForContext<StorageServiceStub>();
        }

        public Task<List<CloudFile>> ListFilesAsync(AccountCredentials credentials, string folderId = "root")
        {
            _logger.Information("[StorageServiceStub] ListFilesAsync called for provider {Provider}, folderId={FolderId}.", credentials.Provider, folderId);
            return Task.FromResult(new List<CloudFile>());
        }

        public Task<CloudFile> GetFileAsync(AccountCredentials credentials, string fileId)
        {
            _logger.Information("[StorageServiceStub] GetFileAsync called for provider {Provider}, fileId={FileId}.", credentials.Provider, fileId);
            return Task.FromResult(new CloudFile
            {
                FileId = fileId,
                FileName = "placeholder.txt",
                FilePath = $"/placeholder/{fileId}",
                MimeType = "text/plain",
                FileSize = 0,
                CreatedDate = DateTime.UtcNow,
                ModifiedDate = DateTime.UtcNow,
                Owner = credentials.Email,
                IsDirectory = false,
                Provider = credentials.Provider,
                AccountId = credentials.AccountId,
                Metadata = new Dictionary<string, object> { ["Placeholder"] = true }
            });
        }

        public Task<string> UploadFileAsync(AccountCredentials credentials, string filePath, string destinationFolderId = "root")
        {
            _logger.Information("[StorageServiceStub] UploadFileAsync called for provider {Provider}, filePath={FilePath}, destinationFolderId={FolderId}.", credentials.Provider, filePath, destinationFolderId);
            return Task.FromResult(Guid.NewGuid().ToString());
        }

        public Task<bool> DeleteFileAsync(AccountCredentials credentials, string fileId)
        {
            _logger.Information("[StorageServiceStub] DeleteFileAsync called for provider {Provider}, fileId={FileId}.", credentials.Provider, fileId);
            return Task.FromResult(true);
        }

        public Task<Stream> DownloadFileAsync(AccountCredentials credentials, string fileId)
        {
            _logger.Information("[StorageServiceStub] DownloadFileAsync called for provider {Provider}, fileId={FileId}.", credentials.Provider, fileId);
            return Task.FromResult<Stream>(new MemoryStream(Array.Empty<byte>()));
        }

        public Task SyncStorageAsync(AccountCredentials credentials)
        {
            _logger.Information("[StorageServiceStub] SyncStorageAsync called for provider {Provider}.", credentials.Provider);
            return Task.CompletedTask;
        }

        public Task<List<CloudFile>> SearchFilesAsync(AccountCredentials credentials, string query)
        {
            _logger.Information("[StorageServiceStub] SearchFilesAsync called for provider {Provider}, query={Query}.", credentials.Provider, query);
            return Task.FromResult(new List<CloudFile>());
        }
    }
}
