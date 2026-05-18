using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Net.Http;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Google.Apis.Auth.OAuth2;
using Google.Apis.Drive.v3;
using Google.Apis.Services;
using MultiSych.Services.Data;
using MultiSych.Services.Interfaces;
using MultiSych.Services.Models;
using Serilog;

namespace MultiSych.Services.Implementations
{
    public class CloudStorageService : IStorageService
    {
        private readonly ILogger _logger = Log.ForContext<CloudStorageService>();
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IServiceScopeFactory _scopeFactory;

        public CloudStorageService(IHttpClientFactory httpClientFactory, IServiceScopeFactory scopeFactory)
        {
            _httpClientFactory = httpClientFactory;
            _scopeFactory = scopeFactory;
        }

        public async Task<List<CloudFile>> ListFilesAsync(AccountCredentials credentials, string folderId = "root")
        {
            _logger.Information("Fetching files from {Provider} for account {Email}", credentials.Provider, credentials.Email);

            List<CloudFile> files = new();
            bool isOnline = true;

            try
            {
                if (credentials.Provider == "Google")
                    files = await GetGoogleDriveFilesAsync(credentials, folderId);
                else if (credentials.Provider == "Microsoft")
                    files = await GetMicrosoftOneDriveFilesAsync(credentials, folderId);
                else if (credentials.Provider == "Yandex")
                    files = await GetYandexDiskFilesAsync(credentials, folderId);

                // İnternet varsa dosyaları yerel veritabanına kaydet (Önbellek - Cache Update)
                using var scope = _scopeFactory.CreateScope();
                var dbContext = scope.ServiceProvider.GetRequiredService<LocalCacheDbContext>();
                
                foreach (var file in files)
                {
                    var existing = await dbContext.CachedFiles.FindAsync(file.AccountId, file.FileId);
                    if (existing != null)
                        dbContext.Entry(existing).CurrentValues.SetValues(file);
                    else
                        dbContext.CachedFiles.Add(file);
                }
                await dbContext.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                _logger.Warning(ex, "API fetch failed. Falling back to local cache for {Email}", credentials.Email);
                isOnline = false;
            }

            if (!isOnline)
            {
                // Çevrimdışıysak dosyaları yerel veritabanından getir
                using var scope = _scopeFactory.CreateScope();
                var dbContext = scope.ServiceProvider.GetRequiredService<LocalCacheDbContext>();
                
                // O hesaba ait tüm önbelleklenmiş dosyaları listele
                files = await dbContext.CachedFiles
                    .Where(f => f.AccountId == credentials.AccountId)
                    .ToListAsync();
            }

            return files;
        }

        private async Task<List<CloudFile>> GetGoogleDriveFilesAsync(AccountCredentials credentials, string folderId)
        {
            var credential = GoogleCredential.FromAccessToken(credentials.AccessToken);
            var service = new DriveService(new BaseClientService.Initializer
            {
                HttpClientInitializer = credential,
                ApplicationName = "MultiSych"
            });

            var request = service.Files.List();
            // Belirtilen klasördeki silinmemiş dosyaları getirir
            request.Q = $"'{folderId}' in parents and trashed = false";
            request.Fields = "files(id, name, mimeType, size, createdTime, modifiedTime, owners)";

            var response = await request.ExecuteAsync();

            return response.Files?.Select(f => new CloudFile
            {
                FileId = f.Id ?? string.Empty,
                FileName = f.Name ?? string.Empty,
                MimeType = f.MimeType ?? "application/octet-stream",
                FileSize = f.Size ?? 0,
                CreatedDate = f.CreatedTimeDateTimeOffset?.UtcDateTime ?? DateTime.UtcNow,
                ModifiedDate = f.ModifiedTimeDateTimeOffset?.UtcDateTime ?? DateTime.UtcNow,
                IsDirectory = f.MimeType == "application/vnd.google-apps.folder",
                Provider = "Google",
                AccountId = credentials.AccountId ?? string.Empty
            }).ToList() ?? new List<CloudFile>();
        }

        private async Task<List<CloudFile>> GetMicrosoftOneDriveFilesAsync(AccountCredentials credentials, string folderId)
        {
            var endpoint = folderId == "root"
                ? "https://graph.microsoft.com/v1.0/me/drive/root/children"
                : $"https://graph.microsoft.com/v1.0/me/drive/items/{folderId}/children";

            using var httpClient = _httpClientFactory.CreateClient();
            httpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", credentials.AccessToken);

            var response = await httpClient.GetAsync(endpoint);
            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync();
                _logger.Error("Microsoft Graph API returned an error: {Error}", error);
                throw new Exception($"Microsoft Graph API error: {response.StatusCode}");
            }

            var content = await response.Content.ReadAsStringAsync();
            using var document = JsonDocument.Parse(content);
            var files = new List<CloudFile>();

            if (document.RootElement.TryGetProperty("value", out var items))
            {
                foreach (var item in items.EnumerateArray())
                {
                    var isFolder = item.TryGetProperty("folder", out _);
                    
                    files.Add(new CloudFile
                    {
                        FileId = item.GetProperty("id").GetString() ?? string.Empty,
                        FileName = item.GetProperty("name").GetString() ?? string.Empty,
                        MimeType = isFolder ? "folder" : (item.TryGetProperty("file", out var f) && f.TryGetProperty("mimeType", out var m) ? m.GetString() ?? "application/octet-stream" : "application/octet-stream"),
                        FileSize = item.TryGetProperty("size", out var size) ? size.GetInt64() : 0,
                        CreatedDate = item.TryGetProperty("createdDateTime", out var cDate) ? cDate.GetDateTime() : DateTime.UtcNow,
                        ModifiedDate = item.TryGetProperty("lastModifiedDateTime", out var mDate) ? mDate.GetDateTime() : DateTime.UtcNow,
                        IsDirectory = isFolder,
                        Provider = "Microsoft",
                        AccountId = credentials.AccountId
                    });
                }
            }

            return files;
        }

        private async Task<List<CloudFile>> GetYandexDiskFilesAsync(AccountCredentials credentials, string folderId)
        {
            var path = folderId == "root" ? "disk:/" : folderId;
            var endpoint = $"https://cloud-api.yandex.net/v1/disk/resources?path={Uri.EscapeDataString(path)}&limit=1000";

            using var httpClient = _httpClientFactory.CreateClient();
            httpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("OAuth", credentials.AccessToken);

            var response = await httpClient.GetAsync(endpoint);
            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync();
                _logger.Error("Yandex Disk API returned an error: {Error}", error);
                throw new Exception($"Yandex Disk API error: {response.StatusCode}");
            }

            var content = await response.Content.ReadAsStringAsync();
            using var document = JsonDocument.Parse(content);
            var files = new List<CloudFile>();

            if (document.RootElement.TryGetProperty("_embedded", out var embedded) && embedded.TryGetProperty("items", out var items))
            {
                foreach (var item in items.EnumerateArray())
                {
                    var isFolder = item.TryGetProperty("type", out var type) && type.GetString() == "dir";
                    files.Add(new CloudFile
                    {
                        FileId = item.GetProperty("path").GetString() ?? string.Empty,
                        FileName = item.GetProperty("name").GetString() ?? string.Empty,
                        MimeType = item.TryGetProperty("mime_type", out var mime) ? (mime.GetString() ?? "application/octet-stream") : (isFolder ? "folder" : "application/octet-stream"),
                        FileSize = item.TryGetProperty("size", out var size) ? size.GetInt64() : 0,
                        CreatedDate = item.TryGetProperty("created", out var cDate) ? cDate.GetDateTime() : DateTime.UtcNow,
                        ModifiedDate = item.TryGetProperty("modified", out var mDate) ? mDate.GetDateTime() : DateTime.UtcNow,
                        IsDirectory = isFolder,
                        Provider = "Yandex",
                        AccountId = credentials.AccountId
                    });
                }
            }

            return files;
        }

        public async Task<string> UploadFileAsync(AccountCredentials credentials, string filePath, string destinationFolderId = "root")
        {
            _logger.Information("Uploading file {FilePath} to {Provider} for account {Email}", filePath, credentials.Provider, credentials.Email);

            if (!File.Exists(filePath))
                throw new FileNotFoundException($"The file to upload was not found: {filePath}");

            if (credentials.Provider == "Google")
            {
                return await UploadToGoogleDriveAsync(credentials, filePath, destinationFolderId);
            }
            else if (credentials.Provider == "Microsoft")
            {
                return await UploadToMicrosoftOneDriveAsync(credentials, filePath, destinationFolderId);
            }
            else if (credentials.Provider == "Yandex")
            {
                return await UploadToYandexDiskAsync(credentials, filePath, destinationFolderId);
            }
            else
            {
                throw new NotSupportedException($"Provider {credentials.Provider} is not supported for file uploads.");
            }
        }

        private async Task<string> UploadToGoogleDriveAsync(AccountCredentials credentials, string filePath, string destinationFolderId)
        {
            var credential = GoogleCredential.FromAccessToken(credentials.AccessToken);
            var service = new DriveService(new BaseClientService.Initializer
            {
                HttpClientInitializer = credential,
                ApplicationName = "MultiSych"
            });

            var fileName = Path.GetFileName(filePath);
            var fileMetadata = new Google.Apis.Drive.v3.Data.File
            {
                Name = fileName,
                Parents = new List<string> { destinationFolderId }
            };

            using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read);
            var request = service.Files.Create(fileMetadata, stream, "application/octet-stream");
            request.Fields = "id";

            var response = await request.UploadAsync();
            if (response.Status != Google.Apis.Upload.UploadStatus.Completed)
            {
                throw new Exception($"Google Drive upload failed: {response.Exception?.Message}");
            }

            return request.ResponseBody?.Id ?? string.Empty;
        }

        private async Task<string> UploadToMicrosoftOneDriveAsync(AccountCredentials credentials, string filePath, string destinationFolderId)
        {
            var fileName = Path.GetFileName(filePath);
            var endpoint = destinationFolderId == "root"
                ? $"https://graph.microsoft.com/v1.0/me/drive/root:/{Uri.EscapeDataString(fileName)}:/content"
                : $"https://graph.microsoft.com/v1.0/me/drive/items/{destinationFolderId}:/{Uri.EscapeDataString(fileName)}:/content";

            using var httpClient = _httpClientFactory.CreateClient();
            httpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", credentials.AccessToken);

            using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read);
            using var content = new StreamContent(stream);
            content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/octet-stream");

            var response = await httpClient.PutAsync(endpoint, content);
            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync();
                _logger.Error("Microsoft Graph API returned an error during upload: {Error}", error);
                throw new Exception($"Microsoft Graph API upload error: {response.StatusCode}");
            }

            var responseBody = await response.Content.ReadAsStringAsync();
            using var document = JsonDocument.Parse(responseBody);
            return document.RootElement.TryGetProperty("id", out var idElement) ? idElement.GetString() ?? string.Empty : string.Empty;
        }

        private async Task<string> UploadToYandexDiskAsync(AccountCredentials credentials, string filePath, string destinationFolderId)
        {
            var fileName = Path.GetFileName(filePath);
            var basePath = destinationFolderId == "root" ? "disk:/" : destinationFolderId;
            var targetPath = basePath.EndsWith("/") ? $"{basePath}{fileName}" : $"{basePath}/{fileName}";
            var linkEndpoint = $"https://cloud-api.yandex.net/v1/disk/resources/upload?path={Uri.EscapeDataString(targetPath)}&overwrite=true";

            using var httpClient = _httpClientFactory.CreateClient();
            httpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("OAuth", credentials.AccessToken);

            var linkResponse = await httpClient.GetAsync(linkEndpoint);
            if (!linkResponse.IsSuccessStatusCode)
                throw new Exception($"Yandex Disk link generation error: {linkResponse.StatusCode}");

            var linkContent = await linkResponse.Content.ReadAsStringAsync();
            using var linkDoc = JsonDocument.Parse(linkContent);
            var uploadUrl = linkDoc.RootElement.GetProperty("href").GetString() ?? throw new Exception("Upload href is null");

            using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read);
            using var content = new StreamContent(stream);
            
            var uploadResponse = await httpClient.PutAsync(uploadUrl, content);
            if (!uploadResponse.IsSuccessStatusCode)
                throw new Exception($"Yandex Disk upload error: {uploadResponse.StatusCode}");

            // Yandex uses the path itself as the FileId
            return targetPath;
        }

        public async Task<Stream> DownloadFileAsync(AccountCredentials credentials, string fileId)
        {
            _logger.Information("Downloading file {FileId} from {Provider} for account {Email}", fileId, credentials.Provider, credentials.Email);

            if (credentials.Provider == "Google")
            {
                return await DownloadFromGoogleDriveAsync(credentials, fileId);
            }
            else if (credentials.Provider == "Microsoft")
            {
                return await DownloadFromMicrosoftOneDriveAsync(credentials, fileId);
            }
            else if (credentials.Provider == "Yandex")
            {
                return await DownloadFromYandexDiskAsync(credentials, fileId);
            }
            else
            {
                throw new NotSupportedException($"Provider {credentials.Provider} is not supported for file downloads.");
            }
        }

        private async Task<Stream> DownloadFromGoogleDriveAsync(AccountCredentials credentials, string fileId)
        {
            var credential = GoogleCredential.FromAccessToken(credentials.AccessToken);
            var service = new DriveService(new BaseClientService.Initializer
            {
                HttpClientInitializer = credential,
                ApplicationName = "MultiSych"
            });

            var request = service.Files.Get(fileId);
            var stream = new MemoryStream();
            var response = await request.DownloadAsync(stream);

            if (response.Status == Google.Apis.Download.DownloadStatus.Failed)
            {
                throw new Exception($"Google Drive download failed: {response.Exception?.Message}");
            }

            stream.Position = 0;
            return stream;
        }

        private async Task<Stream> DownloadFromMicrosoftOneDriveAsync(AccountCredentials credentials, string fileId)
        {
            var endpoint = $"https://graph.microsoft.com/v1.0/me/drive/items/{fileId}/content";

            using var httpClient = _httpClientFactory.CreateClient();
            httpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", credentials.AccessToken);

            var response = await httpClient.GetAsync(endpoint);
            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync();
                _logger.Error("Microsoft Graph API returned an error during download: {Error}", error);
                throw new Exception($"Microsoft Graph API download error: {response.StatusCode}");
            }

            var stream = new MemoryStream();
            await response.Content.CopyToAsync(stream);
            stream.Position = 0;
            return stream;
        }

        private async Task<Stream> DownloadFromYandexDiskAsync(AccountCredentials credentials, string fileId)
        {
            var linkEndpoint = $"https://cloud-api.yandex.net/v1/disk/resources/download?path={Uri.EscapeDataString(fileId)}";

            using var httpClient = _httpClientFactory.CreateClient();
            httpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("OAuth", credentials.AccessToken);

            var linkResponse = await httpClient.GetAsync(linkEndpoint);
            if (!linkResponse.IsSuccessStatusCode)
                throw new Exception($"Yandex Disk link generation error: {linkResponse.StatusCode}");

            var linkContent = await linkResponse.Content.ReadAsStringAsync();
            using var linkDoc = JsonDocument.Parse(linkContent);
            var downloadUrl = linkDoc.RootElement.GetProperty("href").GetString() ?? throw new Exception("Download href is null");

            var downloadResponse = await httpClient.GetAsync(downloadUrl);
            var stream = new MemoryStream();
            await downloadResponse.Content.CopyToAsync(stream);
            stream.Position = 0;
            return stream;
        }

        public async Task<bool> DeleteFileAsync(AccountCredentials credentials, string fileId)
        {
            _logger.Information("Deleting file {FileId} from {Provider} for account {Email}", fileId, credentials.Provider, credentials.Email);

            if (credentials.Provider == "Google")
            {
                return await DeleteFromGoogleDriveAsync(credentials, fileId);
            }
            else if (credentials.Provider == "Microsoft")
            {
                return await DeleteFromMicrosoftOneDriveAsync(credentials, fileId);
            }
            else if (credentials.Provider == "Yandex")
            {
                return await DeleteFromYandexDiskAsync(credentials, fileId);
            }
            else
            {
                throw new NotSupportedException($"Provider {credentials.Provider} is not supported for file deletion.");
            }
        }

        private async Task<bool> DeleteFromGoogleDriveAsync(AccountCredentials credentials, string fileId)
        {
            var credential = GoogleCredential.FromAccessToken(credentials.AccessToken);
            var service = new DriveService(new BaseClientService.Initializer
            {
                HttpClientInitializer = credential,
                ApplicationName = "MultiSych"
            });

            try
            {
                await service.Files.Delete(fileId).ExecuteAsync();
                return true;
            }
            catch (Exception ex)
            {
                throw new Exception($"Google Drive delete failed: {ex.Message}");
            }
        }

        private async Task<bool> DeleteFromMicrosoftOneDriveAsync(AccountCredentials credentials, string fileId)
        {
            var endpoint = $"https://graph.microsoft.com/v1.0/me/drive/items/{fileId}";

            using var httpClient = _httpClientFactory.CreateClient();
            httpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", credentials.AccessToken);

            var response = await httpClient.DeleteAsync(endpoint);
            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync();
                _logger.Error("Microsoft Graph API returned an error during delete: {Error}", error);
                throw new Exception($"Microsoft Graph API delete error: {response.StatusCode}");
            }

            return true;
        }

        private async Task<bool> DeleteFromYandexDiskAsync(AccountCredentials credentials, string fileId)
        {
            var endpoint = $"https://cloud-api.yandex.net/v1/disk/resources?path={Uri.EscapeDataString(fileId)}&permanently=true";

            using var httpClient = _httpClientFactory.CreateClient();
            httpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("OAuth", credentials.AccessToken);

            var response = await httpClient.DeleteAsync(endpoint);
            // Yandex Disk returns 204 Accepted or 202 if it's processing async, 200 OK otherwise
            if (!response.IsSuccessStatusCode)
                throw new Exception($"Yandex Disk delete error: {response.StatusCode}");

            return true;
        }

        // Diğer IStorageService metotları için geçici fırlatmalar (İhtiyaç oldukça dolduracağız)
        public Task<CloudFile> GetFileAsync(AccountCredentials credentials, string fileId) => throw new NotImplementedException();
        
        public async Task SyncStorageAsync(AccountCredentials credentials)
        {
            _logger.Information("Starting storage sync for provider {Provider}, account {Email}", credentials.Provider, credentials.Email);
            
            try
            {
                var localDrivePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "MultiSych", "Drives", credentials.AccountId ?? "Unknown");
                if (!Directory.Exists(localDrivePath))
                {
                    Directory.CreateDirectory(localDrivePath);
                }

                await SyncFolderRecursiveAsync(credentials, "root", localDrivePath);
                
                _logger.Information("Storage sync completed for {Email}", credentials.Email);
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error during storage sync for {Email}", credentials.Email);
            }
        }

        private async Task SyncFolderRecursiveAsync(AccountCredentials credentials, string folderId, string localFolderPath)
        {
            var files = await ListFilesAsync(credentials, folderId);
            foreach (var file in files)
            {
                var filePath = Path.Combine(localFolderPath, file.FileName ?? "Unknown");
                if (file.IsDirectory)
                {
                    if (!Directory.Exists(filePath))
                        Directory.CreateDirectory(filePath);
                    
                    // Klasör bulunduysa metod kendi kendini bu klasörün ID'si ile tekrar çağırıyor
                    await SyncFolderRecursiveAsync(credentials, file.FileId ?? string.Empty, filePath);
                }
                else
                {
                    if (!File.Exists(filePath))
                    {
                        _logger.Information("Syncing new file {FileName} to local drive...", file.FileName);
                        try
                        {
                            using var cloudStream = await DownloadFileAsync(credentials, file.FileId ?? string.Empty);
                            using var localStream = new FileStream(filePath, FileMode.Create, FileAccess.Write);
                            await cloudStream.CopyToAsync(localStream);
                        }
                        catch (Exception ex)
                        {
                            _logger.Error(ex, "Failed to download file {FileName} ({FileId})", file.FileName, file.FileId);
                        }
                    }
                }
            }
        }

        public Task<List<CloudFile>> SearchFilesAsync(AccountCredentials credentials, string query) => throw new NotImplementedException();
    }
}