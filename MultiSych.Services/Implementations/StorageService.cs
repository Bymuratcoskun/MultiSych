using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Threading.Tasks;
using Google.Apis.Auth.OAuth2;
using Google.Apis.Drive.v3;
using Google.Apis.Services;
using Microsoft.Extensions.Logging;
using MultiSych.Services.Data;
using MultiSych.Services.Interfaces;
using MultiSych.Services.Models;

namespace MultiSych.Services.Implementations;

public class StorageService : IStorageService
{
    private readonly ILogger<StorageService> _logger;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly LocalCacheDbContext _dbContext;

    public StorageService(ILogger<StorageService> logger, IHttpClientFactory httpClientFactory, LocalCacheDbContext dbContext)
    {
        _logger = logger;
        _httpClientFactory = httpClientFactory;
        _dbContext = dbContext;
    }

    public async Task SyncStorageAsync(AccountCredentials account)
    {
        _logger.LogInformation("Starting storage sync for provider: {Provider}", account.Provider);

        try
        {
            if (account.Provider == "Google")
            {
                await SyncGoogleDriveAsync(account);
            }
            else if (account.Provider == "Microsoft")
            {
                await SyncOneDriveAsync(account);
            }
            else
            {
                _logger.LogWarning("Storage sync is not yet implemented for {Provider}", account.Provider);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to sync storage for {Email}", account.Email);
        }
    }

    private async Task SyncGoogleDriveAsync(AccountCredentials account)
    {
        _logger.LogInformation("Fetching files from Google Drive for {Email}...", account.Email);

        var credential = GoogleCredential.FromAccessToken(account.AccessToken);
        var service = new DriveService(new BaseClientService.Initializer
        {
            HttpClientInitializer = credential,
            ApplicationName = "MultiSych"
        });

        var request = service.Files.List();
        request.Q = "'root' in parents and trashed = false"; // Ana dizindeki silinmemiş dosyaları getirir
        request.Fields = "files(id, name, mimeType, size)";

        var response = await request.ExecuteAsync();

        if (response.Files != null && response.Files.Count > 0)
        {
            foreach (var file in response.Files)
            {
                _logger.LogInformation("Found Google Drive file: {Name} (ID: {Id}, Size: {Size} bytes)", file.Name, file.Id, file.Size);
                
                var cloudFile = new CloudFile
                {
                    AccountId = account.AccountId ?? string.Empty,
                    FileId = file.Id ?? string.Empty,
                    FileName = file.Name ?? string.Empty,
                    MimeType = file.MimeType ?? "application/octet-stream",
                    FileSize = file.Size ?? 0,
                    Provider = "Google",
                    IsDirectory = file.MimeType == "application/vnd.google-apps.folder",
                    CreatedDate = DateTime.UtcNow,
                    ModifiedDate = DateTime.UtcNow
                };

                var existing = await _dbContext.CloudFiles.FindAsync(cloudFile.AccountId, cloudFile.FileId);
                if (existing != null)
                {
                    existing.FileName = cloudFile.FileName;
                    existing.MimeType = cloudFile.MimeType;
                    existing.FileSize = cloudFile.FileSize;
                    existing.IsDirectory = cloudFile.IsDirectory;
                    existing.UpdatedAt = DateTime.UtcNow;
                }
                else await _dbContext.CloudFiles.AddAsync(new CloudFileEntity
                {
                    AccountId = cloudFile.AccountId,
                    FileId = cloudFile.FileId,
                    FileName = cloudFile.FileName,
                    MimeType = cloudFile.MimeType,
                    FileSize = cloudFile.FileSize,
                    IsDirectory = cloudFile.IsDirectory,
                    Provider = cloudFile.Provider,
                    CreatedAt = cloudFile.CreatedDate,
                    UpdatedAt = cloudFile.ModifiedDate
                });
            }
            await _dbContext.SaveChangesAsync();
            _logger.LogInformation("Successfully listed {Count} files from Google Drive.", response.Files.Count);
        }
        else
        {
            _logger.LogInformation("No files found in the root of Google Drive.");
        }
    }

    private async Task SyncOneDriveAsync(AccountCredentials account)
    {
        _logger.LogInformation("Fetching files from OneDrive for {Email}...", account.Email);

        var endpoint = "https://graph.microsoft.com/v1.0/me/drive/root/children";
        using var httpClient = _httpClientFactory.CreateClient();
        httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", account.AccessToken);

        var response = await httpClient.GetAsync(endpoint);
        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync();
            _logger.LogError("Microsoft Graph API returned an error: {Error}", error);
            throw new Exception($"Microsoft Graph API error: {response.StatusCode}");
        }

        var content = await response.Content.ReadAsStringAsync();
        using var document = JsonDocument.Parse(content);

        if (document.RootElement.TryGetProperty("value", out var items))
        {
            var count = items.GetArrayLength();
            foreach (var item in items.EnumerateArray())
            {
                var name = item.GetProperty("name").GetString();
                var id = item.GetProperty("id").GetString();
                var size = item.TryGetProperty("size", out var sizeProp) ? sizeProp.GetInt64() : 0;
                
                _logger.LogInformation("Found OneDrive file: {Name} (ID: {Id}, Size: {Size} bytes)", name, id, size);
                
                var cloudFile = new CloudFile
                {
                    AccountId = account.AccountId ?? string.Empty,
                    FileId = id ?? string.Empty,
                    FileName = name ?? string.Empty,
                    MimeType = "application/octet-stream",
                    FileSize = size,
                    Provider = "Microsoft",
                    IsDirectory = item.TryGetProperty("folder", out _),
                    CreatedDate = DateTime.UtcNow,
                    ModifiedDate = DateTime.UtcNow
                };

                var existing = await _dbContext.CloudFiles.FindAsync(cloudFile.AccountId, cloudFile.FileId);
                if (existing != null)
                {
                    existing.FileName = cloudFile.FileName;
                    existing.MimeType = cloudFile.MimeType;
                    existing.FileSize = cloudFile.FileSize;
                    existing.IsDirectory = cloudFile.IsDirectory;
                    existing.UpdatedAt = DateTime.UtcNow;
                }
                else await _dbContext.CloudFiles.AddAsync(new CloudFileEntity
                {
                    AccountId = cloudFile.AccountId,
                    FileId = cloudFile.FileId,
                    FileName = cloudFile.FileName,
                    MimeType = cloudFile.MimeType,
                    FileSize = cloudFile.FileSize,
                    IsDirectory = cloudFile.IsDirectory,
                    Provider = cloudFile.Provider,
                    CreatedAt = cloudFile.CreatedDate,
                    UpdatedAt = cloudFile.ModifiedDate
                });
            }
            await _dbContext.SaveChangesAsync();
            _logger.LogInformation("Successfully listed {Count} files from OneDrive.", count);
        }
        else
        {
            _logger.LogInformation("No files found in the root of OneDrive.");
        }
    }

    // IStorageService arabirimindeki diğer metotların taslakları (Arayüz sözleşmesini yerine getirmek için)
    public Task<List<CloudFile>> ListFilesAsync(AccountCredentials credentials, string folderId = "root") => Task.FromResult(new List<CloudFile>());
    public Task<CloudFile> GetFileAsync(AccountCredentials credentials, string fileId) => throw new NotImplementedException();
    public Task<string> UploadFileAsync(AccountCredentials credentials, string filePath, string destinationFolderId = "root") => throw new NotImplementedException();
    public Task<bool> DeleteFileAsync(AccountCredentials credentials, string fileId) => throw new NotImplementedException();
    public Task<Stream> DownloadFileAsync(AccountCredentials credentials, string fileId) => throw new NotImplementedException();
    public Task<List<CloudFile>> SearchFilesAsync(AccountCredentials credentials, string query) => throw new NotImplementedException();
}
