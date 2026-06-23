using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using MultiSych.Services.Data;
using MultiSych.Services.Interfaces;

using MultiSych.Services.Models;
namespace MultiSych.Services.Implementations;

/// <summary>
/// Yandex Cloud Service - provides Mail, Disk, Calendar, and Contacts integration
/// </summary>
public class CloudYandexService : ICloudService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<CloudYandexService> _logger;
    private readonly OAuthToken _token;

    private const string ApiBase = "https://cloud-api.yandex.net/v1";
    private const string MailApiBase = "https://mail.yandex.com/api/v1";
    private const string DiskApiBase = "https://cloud-api.yandex.net/v1/disk";
    private const string ContactsApiBase = "https://cloud-api.yandex.net/v1/contacts";

    public CloudYandexService(
        IHttpClientFactory httpClientFactory,
        ILogger<CloudYandexService> logger,
        OAuthToken token)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
        _token = token ?? throw new ArgumentNullException(nameof(token));
    }

    public string ProviderName => "Yandex";

    #region Mail Operations

    /// <summary>
    /// Get list of mailboxes
    /// </summary>
    public async Task<List<MailboxInfo>> GetMailboxesAsync()
    {
        try
        {
            using var client = _httpClientFactory.CreateClient();
            var request = new HttpRequestMessage(HttpMethod.Get, $"{MailApiBase}/user/mailboxes");
            request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("OAuth", _token.AccessToken);

            var response = await client.SendAsync(request);
            response.EnsureSuccessStatusCode();

            var content = await response.Content.ReadAsStringAsync();
            var data = JsonSerializer.Deserialize<JsonElement>(content);

            var mailboxes = new List<MailboxInfo>();

            if (data.TryGetProperty("mailboxes", out var mailboxesArray))
            {
                foreach (var mailbox in mailboxesArray.EnumerateArray())
                {
                    var email = mailbox.GetProperty("email").GetString() ?? string.Empty;
                    mailboxes.Add(new MailboxInfo
                    {
                        Email = email,
                        Name = email,
                        IsDefault = mailbox.TryGetProperty("is_default", out var isDefault) && 
                                   isDefault.GetBoolean(),
                        FolderCount = mailbox.TryGetProperty("folder_count", out var count) 
                            ? count.GetInt32() 
                            : 0
                    });
                }
            }

            return mailboxes;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get Yandex mailboxes");
            throw;
        }
    }

    /// <summary>
    /// Get recent emails from inbox
    /// </summary>
    public async Task<List<EmailInfo>> GetRecentEmailsAsync(int limit = 50)
    {
        try
        {
            using var client = _httpClientFactory.CreateClient();
            var request = new HttpRequestMessage(
                HttpMethod.Get, 
                $"{MailApiBase}/user/folders/1/messages?limit={Math.Min(limit, 100)}&offset=0&with_attachments=true");
            request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("OAuth", _token.AccessToken);

            var response = await client.SendAsync(request);
            response.EnsureSuccessStatusCode();

            var content = await response.Content.ReadAsStringAsync();
            var data = JsonSerializer.Deserialize<JsonElement>(content);

            var emails = new List<EmailInfo>();

            if (data.TryGetProperty("messages", out var messagesArray))
            {
                foreach (var message in messagesArray.EnumerateArray())
                {
                    var from = message.TryGetProperty("from", out var fromProp) ? fromProp.GetString() : "Unknown";
                    var subject = message.TryGetProperty("subject", out var subjectProp) ? subjectProp.GetString() : "(No subject)";
                    var date = message.TryGetProperty("date", out var dateProp) ? dateProp.GetString() : DateTime.UtcNow.ToString();
                    var messageId = message.GetProperty("id").GetString() ?? string.Empty;

                    emails.Add(new EmailInfo
                    {
                        Id = messageId,
                        From = from ?? string.Empty,
                        Subject = subject ?? string.Empty,
                        ReceivedAt = DateTime.TryParse(date, out var parsedDate) ? parsedDate : DateTime.UtcNow,
                        HasAttachments = message.TryGetProperty("attachments", out var attachments) && 
                                        attachments.GetArrayLength() > 0
                    });
                }
            }

            return emails;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get recent Yandex emails");
            throw;
        }
    }

    #endregion

    #region Disk Operations

    /// <summary>
    /// Get disk usage and space info
    /// </summary>
    public async Task<StorageQuota> GetStorageQuotaAsync()
    {
        try
        {
            using var client = _httpClientFactory.CreateClient();
            var request = new HttpRequestMessage(HttpMethod.Get, DiskApiBase);
            request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("OAuth", _token.AccessToken);

            var response = await client.SendAsync(request);
            response.EnsureSuccessStatusCode();

            var content = await response.Content.ReadAsStringAsync();
            var data = JsonSerializer.Deserialize<JsonElement>(content);

            var usedSpace = data.TryGetProperty("used_space", out var used) ? used.GetInt64() : 0;
            var totalSpace = data.TryGetProperty("total_space", out var total) ? total.GetInt64() : 0;

            return new StorageQuota
            {
                UsedBytes = usedSpace,
                TotalBytes = totalSpace,
                PercentUsed = totalSpace > 0 ? (usedSpace * 100) / totalSpace : 0
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get Yandex disk quota");
            throw;
        }
    }

    /// <summary>
    /// List files in a directory
    /// </summary>
    public async Task<List<FileInfo>> ListFilesAsync(string path = "/")
    {
        try
        {
            using var client = _httpClientFactory.CreateClient();
            var encodedPath = Uri.EscapeDataString(path);
            var request = new HttpRequestMessage(
                HttpMethod.Get, 
                $"{DiskApiBase}/resources?path={encodedPath}&fields=_embedded.resource(_embedded.resource(name,type,size,created,modified),name,type,size,created,modified)");
            request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("OAuth", _token.AccessToken);

            var response = await client.SendAsync(request);
            response.EnsureSuccessStatusCode();

            var content = await response.Content.ReadAsStringAsync();
            var data = JsonSerializer.Deserialize<JsonElement>(content);

            var files = new List<FileInfo>();

            if (data.TryGetProperty("_embedded", out var embedded) && 
                embedded.TryGetProperty("items", out var itemsArray))
            {
                foreach (var item in itemsArray.EnumerateArray())
                {
                    var name = item.GetProperty("name").GetString();
                    var type = item.GetProperty("type").GetString();
                    var size = item.TryGetProperty("size", out var sizeVal) ? sizeVal.GetInt64() : 0;
                    var modified = item.TryGetProperty("modified", out var modVal) ? modVal.GetString() : DateTime.UtcNow.ToString();
                    var fileName = name ?? string.Empty;
                    var fileType = type ?? string.Empty;

                    files.Add(new FileInfo
                    {
                        Name = fileName,
                        Path = $"{path.TrimEnd('/')}/{fileName}",
                        Size = size,
                        IsDirectory = fileType == "dir",
                        ModifiedAt = DateTime.TryParse(modified, out var parsedDate) ? parsedDate : DateTime.UtcNow,
                        Type = fileType
                    });
                }
            }

            return files;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to list Yandex disk files");
            throw;
        }
    }

    #endregion

    #region Calendar Operations

    /// <summary>
    /// Get list of calendars
    /// </summary>
    public async Task<List<CalendarInfo>> GetCalendarsAsync()
    {
        try
        {
            using var client = _httpClientFactory.CreateClient();
            var request = new HttpRequestMessage(HttpMethod.Get, $"{ApiBase}/calendars");
            request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("OAuth", _token.AccessToken);

            var response = await client.SendAsync(request);
            
            // If endpoint not available, return empty list
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Yandex calendars API not available");
                return new List<CalendarInfo>();
            }

            var content = await response.Content.ReadAsStringAsync();
            var data = JsonSerializer.Deserialize<JsonElement>(content);

            var calendars = new List<CalendarInfo>();

            // Parse response based on actual Yandex API structure
            if (data.TryGetProperty("calendars", out var calendarsArray))
            {
                foreach (var cal in calendarsArray.EnumerateArray())
                {
                    calendars.Add(new CalendarInfo
                    {
                        Id = cal.GetProperty("id").GetString() ?? string.Empty,
                        Name = cal.GetProperty("name").GetString() ?? string.Empty,
                        Description = cal.TryGetProperty("description", out var desc) 
                            ? desc.GetString() ?? string.Empty
                            : "",
                        IsReadOnly = cal.TryGetProperty("is_read_only", out var ro) && ro.GetBoolean()
                    });
                }
            }

            return calendars;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to get Yandex calendars");
            return new List<CalendarInfo>();
        }
    }

    #endregion

    /// <summary>
    /// Sync contacts (future implementation)
    /// </summary>
    public async Task<int> SyncContactsAsync()
    {
        try
        {
            // Placeholder for future contact sync
            _logger.LogInformation("Yandex contacts sync not yet implemented");
            return 0;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to sync Yandex contacts");
            throw;
        }
    }
}

public class MailboxInfo
{
    public string Email { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public bool IsDefault { get; set; }
    public int FolderCount { get; set; }
}

public class EmailInfo
{
    public string Id { get; set; } = string.Empty;
    public string From { get; set; } = string.Empty;
    public string Subject { get; set; } = string.Empty;
    public DateTime ReceivedAt { get; set; }
    public bool HasAttachments { get; set; }
}

public class StorageQuota
{
    public long UsedBytes { get; set; }
    public long TotalBytes { get; set; }
    public long PercentUsed { get; set; }
}

public class FileInfo
{
    public string Name { get; set; } = string.Empty;
    public string Path { get; set; } = string.Empty;
    public long Size { get; set; }
    public bool IsDirectory { get; set; }
    public DateTime ModifiedAt { get; set; }
    public string Type { get; set; } = string.Empty;
}

public class CalendarInfo
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public bool IsReadOnly { get; set; }
}
