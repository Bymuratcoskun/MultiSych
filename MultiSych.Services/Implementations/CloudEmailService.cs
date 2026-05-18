using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Threading.Tasks;
using Google.Apis.Auth.OAuth2;
using Google.Apis.Gmail.v1;
using Google.Apis.Services;
using MailKit;
using MailKit.Search;
using MailKit.Net.Imap;
using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;
using MimeKit.Text;
using MultiSych.Services.Interfaces;
using MultiSych.Services.Models;
using Serilog;

namespace MultiSych.Services.Implementations
{
    public class CloudEmailService : IEmailService
    {
        private readonly ILogger _logger = Log.ForContext<CloudEmailService>();
        private readonly IHttpClientFactory _httpClientFactory;

        public CloudEmailService(IHttpClientFactory httpClientFactory)
        {
            _httpClientFactory = httpClientFactory;
        }

        public async Task<List<EmailMessage>> GetEmailsAsync(AccountCredentials credentials, int maxResults = 10)
        {
            _logger.Information("Fetching real emails from {Provider} for account {Email}", credentials.Provider, credentials.Email);

            if (credentials.Provider == "Google")
            {
                return await GetGoogleEmailsAsync(credentials, maxResults);
            }
            else if (credentials.Provider == "Microsoft")
            {
                return await GetMicrosoftEmailsAsync(credentials, maxResults);
            }
            else if (credentials.Provider == "Yandex")
            {
                return await GetYandexEmailsAsync(credentials, maxResults);
            }

            return new List<EmailMessage>();
        }

        private async Task<List<EmailMessage>> GetGoogleEmailsAsync(AccountCredentials credentials, int maxResults)
        {
            var credential = GoogleCredential.FromAccessToken(credentials.AccessToken);
            var service = new GmailService(new BaseClientService.Initializer
            {
                HttpClientInitializer = credential,
                ApplicationName = "MultiSych"
            });

            var request = service.Users.Messages.List("me");
            request.MaxResults = maxResults;
            var response = await request.ExecuteAsync();

            var emails = new List<EmailMessage>();
            if (response.Messages == null) return emails;

            foreach (var msgItem in response.Messages)
            {
                try
                {
                    var msg = await service.Users.Messages.Get("me", msgItem.Id).ExecuteAsync();
                    var headers = msg.Payload.Headers;

                    var subject = headers.FirstOrDefault(h => h.Name == "Subject")?.Value ?? "No Subject";
                    var from = headers.FirstOrDefault(h => h.Name == "From")?.Value ?? "Unknown";
                    var to = headers.FirstOrDefault(h => h.Name == "To")?.Value ?? string.Empty;

                    emails.Add(new EmailMessage
                    {
                        MessageId = msg.Id ?? string.Empty,
                        Subject = subject,
                        From = from,
                        To = to.Split(',').Select(t => t.Trim()).ToList(),
                        Body = msg.Snippet ?? string.Empty, // Gösterim için snippet (önizleme) kullanıyoruz
                        ReceivedDate = msg.InternalDate.HasValue 
                            ? DateTimeOffset.FromUnixTimeMilliseconds(msg.InternalDate.Value).UtcDateTime 
                            : DateTime.UtcNow,
                        Provider = "Google",
                        AccountId = credentials.AccountId ?? string.Empty
                    });
                }
                catch (Exception ex)
                {
                    _logger.Warning(ex, "Failed to fetch Gmail message {MessageId}", msgItem.Id);
                }
            }

            return emails;
        }

        private async Task<List<EmailMessage>> GetMicrosoftEmailsAsync(AccountCredentials credentials, int maxResults)
        {
            var endpoint = $"https://graph.microsoft.com/v1.0/me/messages?$top={maxResults}&$select=id,subject,from,toRecipients,bodyPreview,receivedDateTime";

            using var httpClient = _httpClientFactory.CreateClient();
            httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", credentials.AccessToken);

            var response = await httpClient.GetAsync(endpoint);
            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync();
                _logger.Error("Microsoft Graph API returned an error: {Error}", error);
                throw new Exception($"Microsoft Graph API error: {response.StatusCode}");
            }

            var content = await response.Content.ReadAsStringAsync();
            using var document = JsonDocument.Parse(content);
            var emails = new List<EmailMessage>();

            if (document.RootElement.TryGetProperty("value", out var items))
            {
                foreach (var item in items.EnumerateArray())
                {
                    var id = item.GetProperty("id").GetString() ?? string.Empty;
                    var subject = item.TryGetProperty("subject", out var subj) ? subj.GetString() : "No Subject";
                    
                    var from = "Unknown";
                    if (item.TryGetProperty("from", out var fromProp) && fromProp.TryGetProperty("emailAddress", out var fromEmail))
                    {
                        from = fromEmail.TryGetProperty("address", out var addr) ? addr.GetString() : "Unknown";
                    }

                    var toList = new List<string>();
                    if (item.TryGetProperty("toRecipients", out var toRecips))
                    {
                        foreach (var recipient in toRecips.EnumerateArray())
                        {
                            if (recipient.TryGetProperty("emailAddress", out var toEmail) && toEmail.TryGetProperty("address", out var addr))
                            {
                                var addrStr = addr.GetString();
                                if (!string.IsNullOrEmpty(addrStr)) toList.Add(addrStr);
                            }
                        }
                    }

                    var bodyPreview = item.TryGetProperty("bodyPreview", out var body) ? body.GetString() : string.Empty;
                    var receivedDate = item.TryGetProperty("receivedDateTime", out var recDate) ? recDate.GetDateTime() : DateTime.UtcNow;

                    emails.Add(new EmailMessage
                    {
                        MessageId = id,
                        Subject = subject ?? "No Subject",
                        From = from ?? "Unknown",
                        To = toList,
                        Body = bodyPreview ?? string.Empty,
                        ReceivedDate = receivedDate,
                        Provider = "Microsoft",
                        AccountId = credentials.AccountId ?? string.Empty
                    });
                }
            }

            return emails;
        }

        private async Task<List<EmailMessage>> GetYandexEmailsAsync(AccountCredentials credentials, int maxResults)
        {
            var emails = new List<EmailMessage>();
            using var client = new ImapClient();

            await client.ConnectAsync("imap.yandex.com", 993, SecureSocketOptions.SslOnConnect);

            var oauth2 = new SaslMechanismOAuthBearer(credentials.Email ?? string.Empty, credentials.AccessToken ?? string.Empty);
            await client.AuthenticateAsync(oauth2);

            await client.Inbox!.OpenAsync(MailKit.FolderAccess.ReadOnly);

            int count = client.Inbox.Count;
            int startIndex = Math.Max(0, count - maxResults);

            for (int i = count - 1; i >= startIndex; i--)
            {
                try
                {
                    var msg = await client.Inbox.GetMessageAsync(i);
                    emails.Add(new EmailMessage
                    {
                        MessageId = msg.MessageId ?? Guid.NewGuid().ToString(),
                        Subject = msg.Subject ?? "No Subject",
                        From = msg.From.Mailboxes.FirstOrDefault()?.Address ?? "Unknown",
                        To = msg.To.Mailboxes.Select(m => m.Address).ToList(),
                        Body = !string.IsNullOrEmpty(msg.TextBody) ? msg.TextBody : (msg.HtmlBody ?? string.Empty),
                        ReceivedDate = msg.Date.UtcDateTime,
                        Provider = "Yandex",
                        AccountId = credentials.AccountId ?? string.Empty
                    });
                }
                catch (Exception ex)
                {
                    _logger.Warning(ex, "Failed to fetch Yandex message at index {Index}", i);
                }
            }

            await client.DisconnectAsync(true);
            return emails;
        }

        public async Task SendEmailAsync(AccountCredentials credentials, EmailMessage message)
        {
            _logger.Information("Sending email via {Provider} for account {Email}", credentials.Provider, credentials.Email);

            if (credentials.Provider == "Google")
            {
                await SendGoogleEmailAsync(credentials, message);
            }
            else if (credentials.Provider == "Microsoft")
            {
                await SendMicrosoftEmailAsync(credentials, message);
            }
            else if (credentials.Provider == "Yandex")
            {
                await SendYandexEmailAsync(credentials, message);
            }
            else
            {
                throw new NotSupportedException($"Provider {credentials.Provider} is not supported for sending emails.");
            }
        }

        private async Task SendGoogleEmailAsync(AccountCredentials credentials, EmailMessage message)
        {
            var credential = GoogleCredential.FromAccessToken(credentials.AccessToken);
            var service = new GmailService(new BaseClientService.Initializer
            {
                HttpClientInitializer = credential,
                ApplicationName = "MultiSych"
            });

            // RFC 2822 email construction
            var rawMessage = $"To: {string.Join(",", message.To ?? new List<string>())}\r\n" +
                             $"Subject: {message.Subject ?? "No Subject"}\r\n" +
                             "Content-Type: text/plain; charset=utf-8\r\n\r\n" +
                             $"{message.Body ?? string.Empty}";

            // Gmail API requires Base64Url encoding
            var base64UrlEncoded = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(rawMessage))
                .Replace('+', '-')
                .Replace('/', '_')
                .Replace("=", "");

            var gmailMessage = new Google.Apis.Gmail.v1.Data.Message { Raw = base64UrlEncoded };
            await service.Users.Messages.Send(gmailMessage, "me").ExecuteAsync();
            _logger.Information("Successfully sent email via Google for {Email}", credentials.Email);
        }

        private async Task SendMicrosoftEmailAsync(AccountCredentials credentials, EmailMessage message)
        {
            var endpoint = "https://graph.microsoft.com/v1.0/me/sendMail";

            var payload = new
            {
                message = new
                {
                    subject = message.Subject ?? "No Subject",
                    body = new { contentType = "Text", content = message.Body ?? string.Empty },
                    toRecipients = message.To?.Select(t => new { emailAddress = new { address = t ?? string.Empty } }).ToList() ?? new()
                },
                saveToSentItems = "true"
            };

            using var httpClient = _httpClientFactory.CreateClient();
            httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", credentials.AccessToken);

            var content = new StringContent(JsonSerializer.Serialize(payload), System.Text.Encoding.UTF8, "application/json");
            var response = await httpClient.PostAsync(endpoint, content);

            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync();
                _logger.Error("Microsoft Graph API returned an error while sending email: {Error}", error);
                throw new Exception($"Microsoft Graph API error: {response.StatusCode}");
            }

            _logger.Information("Successfully sent email via Microsoft for {Email}", credentials.Email);
        }

        private async Task SendYandexEmailAsync(AccountCredentials credentials, EmailMessage message)
        {
            var mimeMessage = new MimeMessage();
            mimeMessage.From.Add(new MailboxAddress("", credentials.Email ?? string.Empty));

            foreach (var to in message.To ?? new List<string>())
            {
                mimeMessage.To.Add(new MailboxAddress("", to ?? string.Empty));
            }

            mimeMessage.Subject = message.Subject ?? "No Subject";
            mimeMessage.Body = new TextPart(TextFormat.Plain) { Text = message.Body ?? string.Empty };

            using var client = new SmtpClient();
            await client.ConnectAsync("smtp.yandex.com", 465, SecureSocketOptions.SslOnConnect);

            var oauth2 = new SaslMechanismOAuthBearer(credentials.Email ?? string.Empty, credentials.AccessToken ?? string.Empty);
            await client.AuthenticateAsync(oauth2);

            await client.SendAsync(mimeMessage);
            await client.DisconnectAsync(true);

            _logger.Information("Successfully sent email via Yandex for {Email}", credentials.Email);
        }

        private string DecodeBase64Url(string base64Url)
        {
            if (string.IsNullOrEmpty(base64Url)) return string.Empty;
            var base64 = base64Url.Replace('-', '+').Replace('_', '/');
            switch (base64.Length % 4)
            {
                case 2: base64 += "=="; break;
                case 3: base64 += "="; break;
            }
            return System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(base64));
        }

        // Diğer IEmailService metotları için geçici fırlatmalar (İhtiyaç oldukça dolduracağız)
        public async Task<EmailMessage> GetEmailAsync(AccountCredentials credentials, string messageId)
        {
            _logger.Information("Fetching email {MessageId} details from {Provider} for account {Email}", messageId, credentials.Provider, credentials.Email);

            if (credentials.Provider == "Google")
            {
                return await GetGoogleEmailAsync(credentials, messageId);
            }
            else if (credentials.Provider == "Microsoft")
            {
                return await GetMicrosoftEmailAsync(credentials, messageId);
            }
            else if (credentials.Provider == "Yandex")
            {
                return await GetYandexEmailAsync(credentials, messageId);
            }
            else
            {
                throw new NotSupportedException($"Provider {credentials.Provider} is not supported for fetching email details.");
            }
        }

        private async Task<EmailMessage> GetGoogleEmailAsync(AccountCredentials credentials, string messageId)
        {
            var credential = GoogleCredential.FromAccessToken(credentials.AccessToken);
            var service = new GmailService(new BaseClientService.Initializer
            {
                HttpClientInitializer = credential,
                ApplicationName = "MultiSych"
            });

            var msg = await service.Users.Messages.Get("me", messageId).ExecuteAsync();
            var headers = msg.Payload.Headers;

            var subject = headers.FirstOrDefault(h => h.Name == "Subject")?.Value ?? "No Subject";
            var from = headers.FirstOrDefault(h => h.Name == "From")?.Value ?? "Unknown";
            var to = headers.FirstOrDefault(h => h.Name == "To")?.Value ?? string.Empty;

            var body = msg.Snippet ?? string.Empty;
            
            if (msg.Payload.Parts != null)
            {
                var textPart = msg.Payload.Parts.FirstOrDefault(p => p.MimeType == "text/html") 
                            ?? msg.Payload.Parts.FirstOrDefault(p => p.MimeType == "text/plain");
                
                if (textPart?.Body?.Data != null)
                {
                    body = DecodeBase64Url(textPart.Body.Data);
                }
            }
            else if (msg.Payload.Body?.Data != null)
            {
                body = DecodeBase64Url(msg.Payload.Body.Data);
            }

            return new EmailMessage
            {
                MessageId = msg.Id ?? string.Empty,
                Subject = subject,
                From = from,
                To = to.Split(',').Select(t => t.Trim()).ToList(),
                Body = body,
                ReceivedDate = msg.InternalDate.HasValue 
                    ? DateTimeOffset.FromUnixTimeMilliseconds(msg.InternalDate.Value).UtcDateTime 
                    : DateTime.UtcNow,
                Provider = "Google",
                AccountId = credentials.AccountId ?? string.Empty
            };
        }

        private async Task<EmailMessage> GetMicrosoftEmailAsync(AccountCredentials credentials, string messageId)
        {
            var endpoint = $"https://graph.microsoft.com/v1.0/me/messages/{messageId}?$select=id,subject,from,toRecipients,body,receivedDateTime";

            using var httpClient = _httpClientFactory.CreateClient();
            httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", credentials.AccessToken);

            var response = await httpClient.GetAsync(endpoint);
            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync();
                _logger.Error("Microsoft Graph API returned an error reading email details: {Error}", error);
                throw new Exception($"Microsoft Graph API error: {response.StatusCode}");
            }

            var content = await response.Content.ReadAsStringAsync();
            using var document = JsonDocument.Parse(content);
            var item = document.RootElement;

            var id = item.GetProperty("id").GetString() ?? string.Empty;
            var subject = item.TryGetProperty("subject", out var subj) ? subj.GetString() : "No Subject";
            
            var from = "Unknown";
            if (item.TryGetProperty("from", out var fromProp) && fromProp.TryGetProperty("emailAddress", out var fromEmail))
                from = fromEmail.TryGetProperty("address", out var addr) ? addr.GetString() : "Unknown";

            var toList = new List<string>();
            if (item.TryGetProperty("toRecipients", out var toRecips))
            {
                foreach (var recipient in toRecips.EnumerateArray())
                {
                    if (recipient.TryGetProperty("emailAddress", out var toEmail) && toEmail.TryGetProperty("address", out var addr))
                    {
                        var addrStr = addr.GetString();
                        if (!string.IsNullOrEmpty(addrStr)) toList.Add(addrStr);
                    }
                }
            }

            var bodyContent = string.Empty;
            if (item.TryGetProperty("body", out var bodyObj) && bodyObj.TryGetProperty("content", out var contentProp))
                bodyContent = contentProp.GetString() ?? string.Empty;

            var receivedDate = item.TryGetProperty("receivedDateTime", out var recDate) ? recDate.GetDateTime() : DateTime.UtcNow;

            return new EmailMessage
            {
                MessageId = id,
                Subject = subject ?? "No Subject",
                From = from ?? "Unknown",
                To = toList,
                Body = bodyContent,
                ReceivedDate = receivedDate,
                Provider = "Microsoft",
                AccountId = credentials.AccountId ?? string.Empty
            };
        }

        private async Task<EmailMessage> GetYandexEmailAsync(AccountCredentials credentials, string messageId)
        {
            using var client = new ImapClient();
            await client.ConnectAsync("imap.yandex.com", 993, SecureSocketOptions.SslOnConnect);

            var oauth2 = new SaslMechanismOAuthBearer(credentials.Email ?? string.Empty, credentials.AccessToken ?? string.Empty);
            await client.AuthenticateAsync(oauth2);

            await client.Inbox!.OpenAsync(MailKit.FolderAccess.ReadOnly);
            var uids = await client.Inbox.SearchAsync(SearchQuery.HeaderContains("Message-Id", messageId ?? string.Empty));

            if (uids.Count == 0)
            {
                await client.DisconnectAsync(true);
                throw new Exception($"Yandex email not found for MessageId: {messageId}");
            }

            var msg = await client.Inbox.GetMessageAsync(uids[0]);
            var emailMessage = new EmailMessage
            {
                MessageId = msg.MessageId ?? messageId,
                Subject = msg.Subject ?? "No Subject",
                From = msg.From.Mailboxes.FirstOrDefault()?.Address ?? "Unknown",
                To = msg.To.Mailboxes.Select(m => m.Address).ToList(),
                Body = !string.IsNullOrEmpty(msg.HtmlBody) ? msg.HtmlBody : (msg.TextBody ?? string.Empty),
                ReceivedDate = msg.Date.UtcDateTime,
                Provider = "Yandex",
                AccountId = credentials.AccountId ?? string.Empty
            };

            await client.DisconnectAsync(true);
            return emailMessage;
        }

        public async Task<bool> DeleteEmailAsync(AccountCredentials credentials, string messageId)
        {
            _logger.Information("Deleting email {MessageId} from {Provider} for account {Email}", messageId, credentials.Provider, credentials.Email);

            if (credentials.Provider == "Google")
            {
                return await DeleteGoogleEmailAsync(credentials, messageId);
            }
            else if (credentials.Provider == "Microsoft")
            {
                return await DeleteMicrosoftEmailAsync(credentials, messageId);
            }
            else if (credentials.Provider == "Yandex")
            {
                return await DeleteYandexEmailAsync(credentials, messageId);
            }
            else
            {
                throw new NotSupportedException($"Provider {credentials.Provider} is not supported for deleting emails.");
            }
        }

        private async Task<bool> DeleteGoogleEmailAsync(AccountCredentials credentials, string messageId)
        {
            var credential = GoogleCredential.FromAccessToken(credentials.AccessToken);
            var service = new GmailService(new BaseClientService.Initializer
            {
                HttpClientInitializer = credential,
                ApplicationName = "MultiSych"
            });

            await service.Users.Messages.Delete("me", messageId).ExecuteAsync();
            return true;
        }

        private async Task<bool> DeleteMicrosoftEmailAsync(AccountCredentials credentials, string messageId)
        {
            var endpoint = $"https://graph.microsoft.com/v1.0/me/messages/{messageId}";

            using var httpClient = _httpClientFactory.CreateClient();
            httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", credentials.AccessToken);

            var response = await httpClient.DeleteAsync(endpoint);
            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync();
                _logger.Error("Microsoft Graph API returned an error during email delete: {Error}", error);
                throw new Exception($"Microsoft Graph API delete error: {response.StatusCode}");
            }

            return true;
        }

        private async Task<bool> DeleteYandexEmailAsync(AccountCredentials credentials, string messageId)
        {
            using var client = new ImapClient();
            await client.ConnectAsync("imap.yandex.com", 993, SecureSocketOptions.SslOnConnect);

            var oauth2 = new SaslMechanismOAuthBearer(credentials.Email ?? string.Empty, credentials.AccessToken ?? string.Empty);
            await client.AuthenticateAsync(oauth2);

            await client.Inbox!.OpenAsync(MailKit.FolderAccess.ReadWrite);
            var uids = await client.Inbox.SearchAsync(SearchQuery.HeaderContains("Message-Id", messageId ?? string.Empty));

            if (uids.Count > 0)
            {
                await client.Inbox.AddFlagsAsync(uids, MessageFlags.Deleted, true);
                await client.Inbox.ExpungeAsync();
            }

            await client.DisconnectAsync(true);
            return true;
        }

        public async Task MarkAsReadAsync(AccountCredentials credentials, string messageId)
        {
            _logger.Information("Marking email {MessageId} as read from {Provider} for account {Email}", messageId, credentials.Provider, credentials.Email);
            await SetEmailReadStatusAsync(credentials, messageId, true);
        }

        public async Task MarkAsUnreadAsync(AccountCredentials credentials, string messageId)
        {
            _logger.Information("Marking email {MessageId} as unread from {Provider} for account {Email}", messageId, credentials.Provider, credentials.Email);
            await SetEmailReadStatusAsync(credentials, messageId, false);
        }

        private async Task SetEmailReadStatusAsync(AccountCredentials credentials, string messageId, bool isRead)
        {
            if (credentials.Provider == "Google")
            {
                await SetGoogleEmailReadStatusAsync(credentials, messageId, isRead);
            }
            else if (credentials.Provider == "Microsoft")
            {
                await SetMicrosoftEmailReadStatusAsync(credentials, messageId, isRead);
            }
            else if (credentials.Provider == "Yandex")
            {
                await SetYandexEmailReadStatusAsync(credentials, messageId, isRead);
            }
            else
            {
                throw new NotSupportedException($"Provider {credentials.Provider} is not supported for updating email status.");
            }
        }

        private async Task SetGoogleEmailReadStatusAsync(AccountCredentials credentials, string messageId, bool isRead)
        {
            var credential = GoogleCredential.FromAccessToken(credentials.AccessToken);
            var service = new GmailService(new BaseClientService.Initializer
            {
                HttpClientInitializer = credential,
                ApplicationName = "MultiSych"
            });

            var mods = new Google.Apis.Gmail.v1.Data.ModifyMessageRequest();
            if (isRead)
                mods.RemoveLabelIds = new List<string> { "UNREAD" };
            else
                mods.AddLabelIds = new List<string> { "UNREAD" };

            await service.Users.Messages.Modify(mods, "me", messageId).ExecuteAsync();
        }

        private async Task SetMicrosoftEmailReadStatusAsync(AccountCredentials credentials, string messageId, bool isRead)
        {
            var endpoint = $"https://graph.microsoft.com/v1.0/me/messages/{messageId}";
            
            using var httpClient = _httpClientFactory.CreateClient();
            httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", credentials.AccessToken);

            var payload = new { isRead = isRead };
            var content = new StringContent(JsonSerializer.Serialize(payload), System.Text.Encoding.UTF8, "application/json");
            
            var request = new HttpRequestMessage(new HttpMethod("PATCH"), endpoint) { Content = content };
            var response = await httpClient.SendAsync(request);

            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync();
                _logger.Error("Microsoft Graph API returned an error updating read status: {Error}", error);
                throw new Exception($"Microsoft Graph API error: {response.StatusCode}");
            }
        }

        private async Task SetYandexEmailReadStatusAsync(AccountCredentials credentials, string messageId, bool isRead)
        {
            using var client = new ImapClient();
            await client.ConnectAsync("imap.yandex.com", 993, SecureSocketOptions.SslOnConnect);

            var oauth2 = new SaslMechanismOAuthBearer(credentials.Email ?? string.Empty, credentials.AccessToken ?? string.Empty);
            await client.AuthenticateAsync(oauth2);

            await client.Inbox!.OpenAsync(MailKit.FolderAccess.ReadWrite);
            var uids = await client.Inbox.SearchAsync(SearchQuery.HeaderContains("Message-Id", messageId ?? string.Empty));

            if (uids.Count > 0)
            {
                if (isRead)
                    await client.Inbox.AddFlagsAsync(uids, MessageFlags.Seen, true);
                else
                    await client.Inbox.RemoveFlagsAsync(uids, MessageFlags.Seen, true);
            }

            await client.DisconnectAsync(true);
        }

        public Task SyncEmailsAsync(AccountCredentials credentials) => Task.CompletedTask;
    }
}