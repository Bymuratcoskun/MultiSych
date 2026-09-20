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
using MultiSych.Services.Configuration;
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
                
                string parentPath = "/";
                string? dbParentId = folderId == "root" ? null : folderId;

                if (dbParentId != null)
                {
                    var parentDir = await dbContext.CloudFiles.FirstOrDefaultAsync(f => f.AccountId == credentials.AccountId && f.FileId == dbParentId);
                    if (parentDir != null)
                    {
                        parentPath = parentDir.Path;
                        if (!parentPath.EndsWith("/")) parentPath += "/";
                    }
                }

                foreach (var file in files)
                {
                    string filePath;
                    if (credentials.Provider == "Yandex")
                    {
                        filePath = (file.FileId ?? string.Empty).Replace("disk:", "");
                        if (!filePath.StartsWith("/")) filePath = "/" + filePath;
                    }
                    else
                    {
                        filePath = parentPath == "/" ? $"/{file.FileName}" : $"{parentPath}{file.FileName}";
                    }

                    var existing = await dbContext.CloudFiles.FirstOrDefaultAsync(f => f.AccountId == file.AccountId && f.FileId == file.FileId);
                    if (existing != null)
                    {
                        // Cache Invalidation: Eğer dosya boyutu veya buluttaki son güncellenme tarihi değiştiyse yerel önbellek dosyasını sil
                        if (existing.FileSize != file.FileSize || existing.UpdatedAt < file.ModifiedDate)
                        {
                            var cacheFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "MultiSych", "Cache", credentials.AccountId ?? string.Empty);
                            var localCachePath = Path.Combine(cacheFolder, file.FileId ?? string.Empty);
                            if (File.Exists(localCachePath))
                            {
                                try { File.Delete(localCachePath); } catch { }
                            }
                        }

                        existing.FileName = file.FileName ?? string.Empty;
                        existing.MimeType = file.MimeType ?? string.Empty;
                        existing.FileSize = file.FileSize;
                        existing.IsDirectory = file.IsDirectory;
                        existing.UpdatedAt = DateTime.UtcNow;
                        existing.ParentId = dbParentId;
                        existing.Path = filePath;
                        existing.WebEditUrl = file.WebEditUrl;
                    }
                    else
                        dbContext.CloudFiles.Add(new CloudFileEntity
                        {
                            AccountId = file.AccountId ?? string.Empty,
                            FileId = file.FileId ?? string.Empty,
                            FileName = file.FileName ?? string.Empty,
                            MimeType = file.MimeType ?? string.Empty,
                            FileSize = file.FileSize,
                            IsDirectory = file.IsDirectory,
                            Provider = file.Provider ?? string.Empty,
                            CreatedAt = file.CreatedDate,
                            UpdatedAt = file.ModifiedDate,
                            ParentId = dbParentId,
                            Path = filePath,
                            WebEditUrl = file.WebEditUrl
                        });
                }

                // Bulutta silinmiş olan dosyaları yerel önbellekten temizle
                var activeFileIds = files.Select(f => f.FileId).ToHashSet();
                var cachedFiles = await dbContext.CloudFiles
                    .Where(f => f.AccountId == credentials.AccountId && f.ParentId == dbParentId)
                    .ToListAsync();

                foreach (var cachedFile in cachedFiles)
                {
                    if (!activeFileIds.Contains(cachedFile.FileId))
                    {
                        dbContext.CloudFiles.Remove(cachedFile);
                    }
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
                var entities = await dbContext.CloudFiles
                    .Where(f => f.AccountId == credentials.AccountId)
                    .ToListAsync();

                files = entities.Select(e => new CloudFile
                {
                    AccountId = e.AccountId,
                    FileId = e.FileId,
                    FileName = e.FileName,
                    MimeType = e.MimeType,
                    FileSize = e.FileSize,
                    IsDirectory = e.IsDirectory,
                    Provider = e.Provider
                }).ToList();
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
            request.Fields = "files(id, name, mimeType, size, createdTime, modifiedTime, owners, webViewLink)";

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
                AccountId = credentials.AccountId ?? string.Empty,
                WebEditUrl = f.WebViewLink
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
                        AccountId = credentials.AccountId,
                        WebEditUrl = item.TryGetProperty("webUrl", out var wUrl) ? wUrl.GetString() : null
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
                    var pathStr = item.GetProperty("path").GetString() ?? string.Empty;
                    var cleanPath = pathStr.Replace("disk:", "");
                    
                    files.Add(new CloudFile
                    {
                        FileId = pathStr,
                        FileName = item.GetProperty("name").GetString() ?? string.Empty,
                        MimeType = item.TryGetProperty("mime_type", out var mime) ? (mime.GetString() ?? "application/octet-stream") : (isFolder ? "folder" : "application/octet-stream"),
                        FileSize = item.TryGetProperty("size", out var size) ? size.GetInt64() : 0,
                        CreatedDate = item.TryGetProperty("created", out var cDate) ? cDate.GetDateTime() : DateTime.UtcNow,
                        ModifiedDate = item.TryGetProperty("modified", out var mDate) ? mDate.GetDateTime() : DateTime.UtcNow,
                        IsDirectory = isFolder,
                        Provider = "Yandex",
                        AccountId = credentials.AccountId,
                        WebEditUrl = item.TryGetProperty("public_url", out var pUrl) ? pUrl.GetString() : $"https://disk.yandex.com/client/disk{cleanPath}"
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
            
            // Klasörde aynı isimde dosya var mı kontrol et (Duplicate önleme)
            var listRequest = service.Files.List();
            listRequest.Q = $"name = '{fileName.Replace("'", "\\'")}' and '{destinationFolderId}' in parents and trashed = false";
            listRequest.Fields = "files(id)";
            var listResponse = await listRequest.ExecuteAsync();
            var existingFile = listResponse.Files?.FirstOrDefault();

            using var baseStream = new FileStream(filePath, FileMode.Open, FileAccess.Read);
            using var scope = _scopeFactory.CreateScope();
            var settings = scope.ServiceProvider.GetService<RuntimeSyncSettings>();
            using Stream stream = (settings != null && settings.MaxUploadSpeedKbps > 0)
                ? new ThrottledStream(baseStream, settings.MaxUploadSpeedKbps * 1024)
                : baseStream;

            if (existingFile != null)
            {
                // Var olan dosyayı güncelle (Update)
                var fileMetadata = new Google.Apis.Drive.v3.Data.File();
                var updateRequest = service.Files.Update(fileMetadata, existingFile.Id, stream, "application/octet-stream");
                updateRequest.Fields = "id";
                var response = await updateRequest.UploadAsync();
                if (response.Status != Google.Apis.Upload.UploadStatus.Completed)
                {
                    throw new Exception($"Google Drive update failed: {response.Exception?.Message}");
                }
                return existingFile.Id;
            }
            else
            {
                // Yeni dosya oluştur (Create)
                var fileMetadata = new Google.Apis.Drive.v3.Data.File
                {
                    Name = fileName,
                    Parents = new List<string> { destinationFolderId }
                };
                var request = service.Files.Create(fileMetadata, stream, "application/octet-stream");
                request.Fields = "id";
                var response = await request.UploadAsync();
                if (response.Status != Google.Apis.Upload.UploadStatus.Completed)
                {
                    throw new Exception($"Google Drive upload failed: {response.Exception?.Message}");
                }
                return request.ResponseBody?.Id ?? string.Empty;
            }
        }

        private async Task<string> UploadToMicrosoftOneDriveAsync(AccountCredentials credentials, string filePath, string destinationFolderId)
        {
            var fileName = Path.GetFileName(filePath);
            var endpoint = destinationFolderId == "root"
                ? $"https://graph.microsoft.com/v1.0/me/drive/root:/{Uri.EscapeDataString(fileName)}:/content"
                : $"https://graph.microsoft.com/v1.0/me/drive/items/{destinationFolderId}:/{Uri.EscapeDataString(fileName)}:/content";

            using var httpClient = _httpClientFactory.CreateClient();
            httpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", credentials.AccessToken);

            using var baseStream = new FileStream(filePath, FileMode.Open, FileAccess.Read);
            using var scope = _scopeFactory.CreateScope();
            var settings = scope.ServiceProvider.GetService<RuntimeSyncSettings>();
            using Stream stream = (settings != null && settings.MaxUploadSpeedKbps > 0)
                ? new ThrottledStream(baseStream, settings.MaxUploadSpeedKbps * 1024)
                : baseStream;
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

            using var baseStream = new FileStream(filePath, FileMode.Open, FileAccess.Read);
            using var scope = _scopeFactory.CreateScope();
            var settings = scope.ServiceProvider.GetService<RuntimeSyncSettings>();
            using Stream stream = (settings != null && settings.MaxUploadSpeedKbps > 0)
                ? new ThrottledStream(baseStream, settings.MaxUploadSpeedKbps * 1024)
                : baseStream;
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

            Stream baseStream;
            if (credentials.Provider == "Google")
            {
                baseStream = await DownloadFromGoogleDriveAsync(credentials, fileId);
            }
            else if (credentials.Provider == "Microsoft")
            {
                baseStream = await DownloadFromMicrosoftOneDriveAsync(credentials, fileId);
            }
            else if (credentials.Provider == "Yandex")
            {
                baseStream = await DownloadFromYandexDiskAsync(credentials, fileId);
            }
            else
            {
                throw new NotSupportedException($"Provider {credentials.Provider} is not supported for file downloads.");
            }

            using var scope = _scopeFactory.CreateScope();
            var settings = scope.ServiceProvider.GetService<RuntimeSyncSettings>();
            if (settings != null && settings.MaxDownloadSpeedKbps > 0)
            {
                return new ThrottledStream(baseStream, settings.MaxDownloadSpeedKbps * 1024);
            }

            return baseStream;
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

        public async Task<bool> MoveFileAsync(AccountCredentials credentials, string fileId, string newParentId, string? newFileName = null)
        {
            _logger.Information("Moving file {FileId} for account {Email}. New Parent: {ParentId}, New Name: {NewName}", fileId, credentials.Email, newParentId, newFileName);

            if (credentials.Provider == "Google")
            {
                return await MoveGoogleDriveFileAsync(credentials, fileId, newParentId, newFileName);
            }
            if (credentials.Provider == "Microsoft")
            {
                return await MoveMicrosoftOneDriveFileAsync(credentials, fileId, newParentId, newFileName);
            }
            
            throw new NotSupportedException($"Moving files is not supported for provider {credentials.Provider}.");
        }

        private async Task<bool> MoveGoogleDriveFileAsync(AccountCredentials credentials, string fileId, string newParentId, string? newFileName)
        {
            var credential = GoogleCredential.FromAccessToken(credentials.AccessToken);
            var service = new DriveService(new BaseClientService.Initializer
            {
                HttpClientInitializer = credential,
                ApplicationName = "MultiSych"
            });
    
            var getRequest = service.Files.Get(fileId);
            getRequest.Fields = "parents";
            var file = await getRequest.ExecuteAsync();
            var previousParents = string.Join(",", file.Parents);

            var fileUpdate = new Google.Apis.Drive.v3.Data.File();
            if (!string.IsNullOrEmpty(newFileName))
            {
                fileUpdate.Name = newFileName;
            }

            var updateRequest = service.Files.Update(fileUpdate, fileId);
            updateRequest.AddParents = newParentId;
            updateRequest.RemoveParents = previousParents;
            updateRequest.Fields = "id";

            await updateRequest.ExecuteAsync();
            return true;
        }

        private async Task<bool> MoveMicrosoftOneDriveFileAsync(AccountCredentials credentials, string fileId, string newParentId, string? newFileName)
        {
            var endpoint = $"https://graph.microsoft.com/v1.0/me/drive/items/{fileId}";
            using var httpClient = _httpClientFactory.CreateClient();
            httpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", credentials.AccessToken);

            var payloadData = new Dictionary<string, object>
            {
                { "parentReference", new { id = newParentId } }
            };
            if (!string.IsNullOrEmpty(newFileName))
            {
                payloadData["name"] = newFileName;
            }

            var content = new StringContent(JsonSerializer.Serialize(payloadData), System.Text.Encoding.UTF8, "application/json");
            var request = new HttpRequestMessage(new HttpMethod("PATCH"), endpoint) { Content = content };
            var response = await httpClient.SendAsync(request);
            response.EnsureSuccessStatusCode();
            return true;
        }

        public async Task<CloudFile> GetFileAsync(AccountCredentials credentials, string fileId)
        {
            _logger.Information("Fetching file details for {FileId} from {Provider} for account {Email}", fileId, credentials.Provider, credentials.Email);

            if (credentials.Provider == "Google")
            {
                var credential = GoogleCredential.FromAccessToken(credentials.AccessToken);
                var service = new DriveService(new BaseClientService.Initializer
                {
                    HttpClientInitializer = credential,
                    ApplicationName = "MultiSych"
                });

                var request = service.Files.Get(fileId);
                request.Fields = "id, name, mimeType, size, createdTime, modifiedTime";
                var f = await request.ExecuteAsync();

                return new CloudFile
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
                };
            }
            else if (credentials.Provider == "Microsoft")
            {
                var endpoint = $"https://graph.microsoft.com/v1.0/me/drive/items/{fileId}";

                using var httpClient = _httpClientFactory.CreateClient();
                httpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", credentials.AccessToken);

                var response = await httpClient.GetAsync(endpoint);
                if (!response.IsSuccessStatusCode)
                {
                    var error = await response.Content.ReadAsStringAsync();
                    _logger.Error("Microsoft Graph API returned an error in GetFileAsync: {Error}", error);
                    throw new Exception($"Microsoft Graph API error: {response.StatusCode}");
                }

                var content = await response.Content.ReadAsStringAsync();
                using var document = JsonDocument.Parse(content);
                var item = document.RootElement;
                var isFolder = item.TryGetProperty("folder", out _);

                return new CloudFile
                {
                    FileId = item.GetProperty("id").GetString() ?? string.Empty,
                    FileName = item.GetProperty("name").GetString() ?? string.Empty,
                    MimeType = isFolder ? "folder" : (item.TryGetProperty("file", out var f) && f.TryGetProperty("mimeType", out var m) ? m.GetString() ?? "application/octet-stream" : "application/octet-stream"),
                    FileSize = item.TryGetProperty("size", out var size) ? size.GetInt64() : 0,
                    CreatedDate = item.TryGetProperty("createdDateTime", out var cDate) ? cDate.GetDateTime() : DateTime.UtcNow,
                    ModifiedDate = item.TryGetProperty("lastModifiedDateTime", out var mDate) ? mDate.GetDateTime() : DateTime.UtcNow,
                    IsDirectory = isFolder,
                    Provider = "Microsoft",
                    AccountId = credentials.AccountId ?? string.Empty
                };
            }
            else if (credentials.Provider == "Yandex")
            {
                var endpoint = $"https://cloud-api.yandex.net/v1/disk/resources?path={Uri.EscapeDataString(fileId)}";

                using var httpClient = _httpClientFactory.CreateClient();
                httpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("OAuth", credentials.AccessToken);

                var response = await httpClient.GetAsync(endpoint);
                if (!response.IsSuccessStatusCode)
                {
                    var error = await response.Content.ReadAsStringAsync();
                    _logger.Error("Yandex Disk API returned an error in GetFileAsync: {Error}", error);
                    throw new Exception($"Yandex Disk API error: {response.StatusCode}");
                }

                var content = await response.Content.ReadAsStringAsync();
                using var document = JsonDocument.Parse(content);
                var item = document.RootElement;
                var isFolder = item.TryGetProperty("type", out var type) && type.GetString() == "dir";

                return new CloudFile
                {
                    FileId = item.GetProperty("path").GetString() ?? string.Empty,
                    FileName = item.GetProperty("name").GetString() ?? string.Empty,
                    MimeType = item.TryGetProperty("mime_type", out var mime) ? (mime.GetString() ?? "application/octet-stream") : (isFolder ? "folder" : "application/octet-stream"),
                    FileSize = item.TryGetProperty("size", out var size) ? size.GetInt64() : 0,
                    CreatedDate = item.TryGetProperty("created", out var cDate) ? cDate.GetDateTime() : DateTime.UtcNow,
                    ModifiedDate = item.TryGetProperty("modified", out var mDate) ? mDate.GetDateTime() : DateTime.UtcNow,
                    IsDirectory = isFolder,
                    Provider = "Yandex",
                    AccountId = credentials.AccountId ?? string.Empty
                };
            }
            else
            {
                throw new NotSupportedException($"Provider {credentials.Provider} is not supported for GetFileAsync.");
            }
        }

        public async Task SyncStorageAsync(AccountCredentials credentials)
        {
            _logger.Information("Starting storage metadata sync for provider {Provider}, account {Email}", credentials.Provider, credentials.Email);
            
            try
            {
                if (credentials.Provider == "Google")
                {
                    await SyncGoogleDriveIncrementalAsync(credentials);
                }
                else if (credentials.Provider == "Microsoft")
                {
                    await SyncMicrosoftOneDriveIncrementalAsync(credentials);
                }
                else
                {
                    // Yandex Disk or other fallback: Optimize recursive sync by checking folder timestamps
                    await SyncFolderRecursiveOptimizedAsync(credentials, "root");
                }
                _logger.Information("Storage metadata sync completed for {Email}", credentials.Email);
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error during storage metadata sync for {Email}", credentials.Email);
            }
        }

        private async Task SyncGoogleDriveIncrementalAsync(AccountCredentials credentials)
        {
            _logger.Information("Performing Google Drive incremental sync for account {Email}", credentials.Email);

            var lastSyncKey = "GoogleLastSyncTime_" + credentials.AccountId;
            DateTime? lastSyncTime = null;
            if (credentials.AdditionalProperties != null && credentials.AdditionalProperties.TryGetValue(lastSyncKey, out var valueObj))
            {
                if (DateTime.TryParse(valueObj.ToString(), out var dt))
                {
                    lastSyncTime = dt.ToUniversalTime();
                }
            }

            if (lastSyncTime == null)
            {
                _logger.Information("No last sync time found for Google Drive account. Performing full sync...");
                await SyncFolderRecursiveOptimizedAsync(credentials, "root");
                await SaveLastSyncTimeAsync(credentials, lastSyncKey, DateTime.UtcNow);
                return;
            }

            var credential = GoogleCredential.FromAccessToken(credentials.AccessToken);
            var service = new DriveService(new BaseClientService.Initializer
            {
                HttpClientInitializer = credential,
                ApplicationName = "MultiSych"
            });

            using var scope = _scopeFactory.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<LocalCacheDbContext>();

            // 1. Get modified/added files
            var listRequest = service.Files.List();
            listRequest.Q = $"modifiedTime > '{lastSyncTime.Value:yyyy-MM-ddTHH:mm:ss.fffZ}' and trashed = false";
            listRequest.Fields = "files(id, name, mimeType, size, createdTime, modifiedTime, parents)";
            
            var modifiedResponse = await listRequest.ExecuteAsync();
            var modifiedFiles = modifiedResponse.Files;

            if (modifiedFiles != null && modifiedFiles.Count > 0)
            {
                foreach (var f in modifiedFiles)
                {
                    var parentId = f.Parents?.FirstOrDefault();
                    var isDir = f.MimeType == "application/vnd.google-apps.folder";
                    
                    var path = await ResolveGooglePathRecursiveAsync(dbContext, credentials, parentId, f.Name ?? "Untitled", service);

                    var existing = await dbContext.CloudFiles.FirstOrDefaultAsync(x => x.AccountId == credentials.AccountId && x.FileId == f.Id);
                    if (existing != null)
                    {
                        if (existing.FileSize != (f.Size ?? 0) || existing.UpdatedAt < (f.ModifiedTimeDateTimeOffset?.UtcDateTime ?? DateTime.UtcNow))
                        {
                            EvictLocalCacheFile(credentials.AccountId ?? string.Empty, f.Id ?? string.Empty);
                        }

                        existing.FileName = f.Name ?? string.Empty;
                        existing.MimeType = f.MimeType ?? string.Empty;
                        existing.FileSize = f.Size ?? 0;
                        existing.IsDirectory = isDir;
                        existing.UpdatedAt = DateTime.UtcNow;
                        existing.ParentId = parentId;
                        existing.Path = path;
                    }
                    else
                    {
                        dbContext.CloudFiles.Add(new CloudFileEntity
                        {
                            AccountId = credentials.AccountId ?? string.Empty,
                            FileId = f.Id ?? string.Empty,
                            FileName = f.Name ?? string.Empty,
                            MimeType = f.MimeType ?? string.Empty,
                            FileSize = f.Size ?? 0,
                            IsDirectory = isDir,
                            Provider = "Google",
                            CreatedAt = f.CreatedTimeDateTimeOffset?.UtcDateTime ?? DateTime.UtcNow,
                            UpdatedAt = f.ModifiedTimeDateTimeOffset?.UtcDateTime ?? DateTime.UtcNow,
                            ParentId = parentId,
                            Path = path
                        });
                    }
                }
                await dbContext.SaveChangesAsync();
            }

            // 2. Get deleted/trashed files
            var deleteRequest = service.Files.List();
            deleteRequest.Q = $"modifiedTime > '{lastSyncTime.Value:yyyy-MM-ddTHH:mm:ss.fffZ}' and trashed = true";
            deleteRequest.Fields = "files(id)";
            var deleteResponse = await deleteRequest.ExecuteAsync();
            var deletedFiles = deleteResponse.Files;

            if (deletedFiles != null && deletedFiles.Count > 0)
            {
                foreach (var d in deletedFiles)
                {
                    var existing = await dbContext.CloudFiles.FirstOrDefaultAsync(x => x.AccountId == credentials.AccountId && x.FileId == d.Id);
                    if (existing != null)
                    {
                        dbContext.CloudFiles.Remove(existing);
                        EvictLocalCacheFile(credentials.AccountId ?? string.Empty, d.Id ?? string.Empty);
                    }
                }
                await dbContext.SaveChangesAsync();
            }

            await SaveLastSyncTimeAsync(credentials, lastSyncKey, DateTime.UtcNow);
        }

        private async Task<string> ResolveGooglePathRecursiveAsync(LocalCacheDbContext dbContext, AccountCredentials credentials, string? parentId, string fileName, DriveService service)
        {
            if (string.IsNullOrEmpty(parentId) || parentId == "root")
            {
                return "/" + fileName;
            }

            var parent = await dbContext.CloudFiles.FirstOrDefaultAsync(f => f.AccountId == credentials.AccountId && f.FileId == parentId);
            if (parent != null)
            {
                var parentPath = parent.Path;
                if (!parentPath.EndsWith("/")) parentPath += "/";
                return parentPath + fileName;
            }

            try
            {
                var req = service.Files.Get(parentId);
                req.Fields = "id, name, parents, mimeType";
                var pFile = await req.ExecuteAsync();
                var gpParentId = pFile.Parents?.FirstOrDefault();
                var pPath = await ResolveGooglePathRecursiveAsync(dbContext, credentials, gpParentId, pFile.Name, service);

                var pEntity = new CloudFileEntity
                {
                    AccountId = credentials.AccountId ?? string.Empty,
                    FileId = pFile.Id,
                    FileName = pFile.Name,
                    Path = pPath,
                    ParentId = gpParentId,
                    MimeType = pFile.MimeType ?? "application/vnd.google-apps.folder",
                    IsDirectory = pFile.MimeType == "application/vnd.google-apps.folder",
                    Provider = "Google",
                    FileSize = 0
                };
                dbContext.CloudFiles.Add(pEntity);
                await dbContext.SaveChangesAsync();

                if (!pPath.EndsWith("/")) pPath += "/";
                return pPath + fileName;
            }
            catch
            {
                return "/" + fileName;
            }
        }

        private async Task SyncMicrosoftOneDriveIncrementalAsync(AccountCredentials credentials)
        {
            _logger.Information("Performing Microsoft OneDrive incremental sync using delta for account {Email}", credentials.Email);

            var deltaLinkKey = "OneDriveDeltaLink_" + credentials.AccountId;
            string? deltaUrl = null;
            if (credentials.AdditionalProperties != null && credentials.AdditionalProperties.TryGetValue(deltaLinkKey, out var val))
            {
                deltaUrl = val?.ToString();
            }

            if (string.IsNullOrEmpty(deltaUrl))
            {
                deltaUrl = "https://graph.microsoft.com/v1.0/me/drive/root/delta";
            }

            using var httpClient = _httpClientFactory.CreateClient();
            httpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", credentials.AccessToken);

            using var scope = _scopeFactory.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<LocalCacheDbContext>();

            string? nextUrl = deltaUrl;
            string? latestDeltaLink = null;

            try
            {
                while (!string.IsNullOrEmpty(nextUrl))
                {
                    var response = await httpClient.GetAsync(nextUrl);
                    if (response.StatusCode == System.Net.HttpStatusCode.Gone)
                    {
                        _logger.Warning("OneDrive delta link expired. Falling back to initial delta sync.");
                        nextUrl = "https://graph.microsoft.com/v1.0/me/drive/root/delta";
                        continue;
                    }

                    response.EnsureSuccessStatusCode();

                    var content = await response.Content.ReadAsStringAsync();
                    using var document = JsonDocument.Parse(content);

                    if (document.RootElement.TryGetProperty("value", out var items))
                    {
                        foreach (var item in items.EnumerateArray())
                        {
                            var id = item.GetProperty("id").GetString() ?? string.Empty;
                            
                            if (item.TryGetProperty("deleted", out _))
                            {
                                var existing = await dbContext.CloudFiles.FirstOrDefaultAsync(x => x.AccountId == credentials.AccountId && x.FileId == id);
                                if (existing != null)
                                {
                                    dbContext.CloudFiles.Remove(existing);
                                    EvictLocalCacheFile(credentials.AccountId ?? string.Empty, id);
                                }
                                continue;
                            }

                            var name = item.GetProperty("name").GetString() ?? string.Empty;
                            var isFolder = item.TryGetProperty("folder", out _);
                            var size = item.TryGetProperty("size", out var sizeEl) ? sizeEl.GetInt64() : 0;
                            var mDate = item.TryGetProperty("lastModifiedDateTime", out var mDateEl) ? mDateEl.GetDateTime() : DateTime.UtcNow;
                            var cDate = item.TryGetProperty("createdDateTime", out var cDateEl) ? cDateEl.GetDateTime() : DateTime.UtcNow;

                            string parentPath = "/";
                            string? parentId = null;
                            if (item.TryGetProperty("parentReference", out var parentRef))
                            {
                                parentId = parentRef.TryGetProperty("id", out var pIdEl) ? pIdEl.GetString() : null;
                                if (parentRef.TryGetProperty("path", out var pPathEl))
                                {
                                    var pPathStr = pPathEl.GetString();
                                    if (!string.IsNullOrEmpty(pPathStr))
                                    {
                                        int colonIdx = pPathStr.IndexOf(':');
                                        if (colonIdx >= 0)
                                        {
                                            parentPath = pPathStr.Substring(colonIdx + 1);
                                        }
                                    }
                                }
                            }

                            if (!parentPath.StartsWith("/")) parentPath = "/" + parentPath;
                            if (!parentPath.EndsWith("/")) parentPath += "/";
                            var itemPath = parentPath + name;

                            var existingEntity = await dbContext.CloudFiles.FirstOrDefaultAsync(x => x.AccountId == credentials.AccountId && x.FileId == id);
                            if (existingEntity != null)
                            {
                                if (existingEntity.FileSize != size || existingEntity.UpdatedAt < mDate)
                                {
                                    EvictLocalCacheFile(credentials.AccountId ?? string.Empty, id);
                                }

                                existingEntity.FileName = name;
                                existingEntity.MimeType = isFolder ? "folder" : (item.TryGetProperty("file", out var f) && f.TryGetProperty("mimeType", out var m) ? m.GetString() ?? "application/octet-stream" : "application/octet-stream");
                                existingEntity.FileSize = size;
                                existingEntity.IsDirectory = isFolder;
                                existingEntity.UpdatedAt = DateTime.UtcNow;
                                existingEntity.ParentId = parentId;
                                existingEntity.Path = itemPath;
                            }
                            else
                            {
                                dbContext.CloudFiles.Add(new CloudFileEntity
                                {
                                    AccountId = credentials.AccountId ?? string.Empty,
                                    FileId = id,
                                    FileName = name,
                                    Path = itemPath,
                                    ParentId = parentId,
                                    MimeType = isFolder ? "folder" : (item.TryGetProperty("file", out var f) && f.TryGetProperty("mimeType", out var m) ? m.GetString() ?? "application/octet-stream" : "application/octet-stream"),
                                    FileSize = size,
                                    IsDirectory = isFolder,
                                    Provider = "Microsoft",
                                    CreatedAt = cDate,
                                    UpdatedAt = mDate
                                });
                            }
                        }
                        await dbContext.SaveChangesAsync();
                    }

                    nextUrl = document.RootElement.TryGetProperty("@odata.nextLink", out var nextLinkEl) ? nextLinkEl.GetString() : null;
                    latestDeltaLink = document.RootElement.TryGetProperty("@odata.deltaLink", out var deltaLinkEl) ? deltaLinkEl.GetString() : null;
                }

                if (!string.IsNullOrEmpty(latestDeltaLink))
                {
                    await SaveLastSyncTimeAsync(credentials, deltaLinkKey, latestDeltaLink);
                }
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error performing Microsoft OneDrive delta sync.");
                throw;
            }
        }

        private async Task SyncFolderRecursiveOptimizedAsync(AccountCredentials credentials, string folderId)
        {
            using var scope = _scopeFactory.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<LocalCacheDbContext>();

            var files = await ListFilesAsync(credentials, folderId);

            foreach (var file in files)
            {
                if (file.IsDirectory)
                {
                    var cachedSubfolder = await dbContext.CloudFiles.FirstOrDefaultAsync(f => f.AccountId == credentials.AccountId && f.FileId == file.FileId);
                    if (cachedSubfolder != null && file.ModifiedDate <= cachedSubfolder.UpdatedAt)
                    {
                        _logger.Information("Skipping folder sync for {FolderName} as it is up-to-date.", file.FileName);
                        continue;
                    }

                    await SyncFolderRecursiveOptimizedAsync(credentials, file.FileId ?? string.Empty);
                }
            }
        }

        private void EvictLocalCacheFile(string accountId, string fileId)
        {
            var cacheFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "MultiSych", "Cache", accountId);
            var localCachePath = Path.Combine(cacheFolder, fileId);
            if (File.Exists(localCachePath))
            {
                try { File.Delete(localCachePath); } catch { }
            }
        }

        private async Task SaveLastSyncTimeAsync(AccountCredentials credentials, string key, object value)
        {
            if (credentials.AdditionalProperties == null)
            {
                credentials.AdditionalProperties = new Dictionary<string, object>();
            }
            credentials.AdditionalProperties[key] = value;

            using var scope = _scopeFactory.CreateScope();
            var accountStore = scope.ServiceProvider.GetRequiredService<IAccountStore>();
            await accountStore.SaveAccountAsync(credentials);
        }

        /// <summary>
        /// Yerel önbellekte (daha önce ListFilesAsync ile listelenmiş dosyalarda) dosya adına
        /// göre arar. Sağlayıcının canlı arama API'sini ÇAĞIRMAZ — üç sağlayıcının (Google
        /// Drive, OneDrive, Yandex Disk) ayrı arama uç noktalarını test edecek gerçek hesap
        /// kimlik bilgisi bu ortamda yoktu, bu yüzden bilinçli olarak dürüst bir kapsamla
        /// sınırlandı: yalnız daha önce görülmüş dosyaları arar, buluttaki HER dosyayı değil.
        /// Bu sınır docs/YOL-HARITASI.md'de ayrıca not edilmiştir.
        /// </summary>
        public async Task<List<CloudFile>> SearchFilesAsync(AccountCredentials credentials, string query)
        {
            if (string.IsNullOrWhiteSpace(query))
                return new List<CloudFile>();

            using var scope = _scopeFactory.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<LocalCacheDbContext>();

            var matches = await dbContext.CloudFiles
                .Where(f => f.AccountId == credentials.AccountId && f.FileName.Contains(query))
                .ToListAsync();

            return matches.Select(e => new CloudFile
            {
                AccountId = e.AccountId,
                FileId = e.FileId,
                FileName = e.FileName,
                MimeType = e.MimeType,
                FileSize = e.FileSize,
                IsDirectory = e.IsDirectory,
                Provider = e.Provider,
                WebEditUrl = e.WebEditUrl
            }).ToList();
        }
    }
}
