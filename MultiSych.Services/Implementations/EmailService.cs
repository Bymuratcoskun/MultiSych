using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MailKit;
using MailKit.Net.Imap;
using MailKit.Security;
using MailKit.Search;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MultiSych.Services.Data;
using MultiSych.Services.Interfaces;
using MultiSych.Services.Models;

namespace MultiSych.Services.Implementations;

public class EmailService : IEmailService
{
    private readonly ILogger<EmailService> _logger;
    private readonly IDbContextFactory<LocalCacheDbContext> _dbContextFactory;

    public EmailService(ILogger<EmailService> _log, IDbContextFactory<LocalCacheDbContext> dbContextFactory)
    {
        _logger = _log;
        _dbContextFactory = dbContextFactory;
    }

    public async Task SyncEmailsAsync(AccountCredentials credentials)
    {
        _logger.LogInformation("Starting email sync for {Email} via MailKit...", credentials.Email);
        
        try
        {
            var emails = await GetEmailsAsync(credentials, 15);
            
            // Çekilen e-postaları yerel veritabanına (Local Cache) kaydediyoruz
            await using var dbContext = await _dbContextFactory.CreateDbContextAsync();
            foreach (var email in emails)
            {
                var existing = await dbContext.CachedEmails.FindAsync(email.AccountId, email.MessageId);
                if (existing != null)
                {
                    existing.Subject = email.Subject ?? string.Empty;
                    existing.From = email.From ?? string.Empty;
                    existing.To = string.Join(",", email.To ?? new List<string>());
                    existing.Body = email.Body ?? string.Empty;
                    existing.ReceivedDate = email.ReceivedDate;
                    existing.IsRead = email.IsRead;
                    existing.UpdatedAt = DateTime.UtcNow;
                }
                else
                {
                    await dbContext.CachedEmails.AddAsync(new EmailMessageEntity
                    {
                        AccountId = email.AccountId ?? string.Empty,
                        MessageId = email.MessageId ?? string.Empty,
                        Subject = email.Subject ?? string.Empty,
                        From = email.From ?? string.Empty,
                        To = string.Join(",", email.To ?? new List<string>()),
                        Body = email.Body ?? string.Empty,
                        ReceivedDate = email.ReceivedDate,
                        IsRead = email.IsRead,
                        Provider = email.Provider ?? string.Empty,
                        CreatedAt = DateTime.UtcNow,
                        UpdatedAt = DateTime.UtcNow
                    });
                }
            }
            await dbContext.SaveChangesAsync();

            _logger.LogInformation("Successfully synced {Count} emails for {Email}", emails.Count, credentials.Email);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to sync emails for {Email}", credentials.Email);
        }
    }

    public async Task<List<EmailMessage>> GetEmailsAsync(AccountCredentials credentials, int maxResults = 10)
    {
        var emails = new List<EmailMessage>();
        using var client = new ImapClient();

        string host = GetImapHost(credentials.Provider);

        await client.ConnectAsync(host, 993, SecureSocketOptions.SslOnConnect);

        var oauth2 = new SaslMechanismOAuthBearer(credentials.Email ?? string.Empty, credentials.AccessToken ?? string.Empty);
        await client.AuthenticateAsync(oauth2);

        await client.Inbox!.OpenAsync(FolderAccess.ReadOnly);

        int count = client.Inbox!.Count;
        int startIndex = Math.Max(0, count - maxResults);

        for (int i = count - 1; i >= startIndex; i--)
        {
            try
            {
                var msg = await client.Inbox!.GetMessageAsync(i);
                
                // MailKit üzerinden okundu (Seen) bayrağını sorguluyoruz
                bool isRead = false;
                try
                {
                    var summaries = await client.Inbox!.FetchAsync(new[] { i }, MessageSummaryItems.Flags);
                    isRead = summaries?.FirstOrDefault()?.Flags?.HasFlag(MessageFlags.Seen) ?? false;
                }
                catch
                {
                    // Fallback
                }

                emails.Add(new EmailMessage
                {
                    MessageId = msg.MessageId ?? Guid.NewGuid().ToString(),
                    Subject = msg.Subject ?? "No Subject",
                    From = msg.From?.Mailboxes?.FirstOrDefault()?.Name ?? msg.From?.Mailboxes?.FirstOrDefault()?.Address ?? "Unknown",
                    To = msg.To?.Mailboxes?.Select(m => m.Address).ToList() ?? new List<string>(),
                    Cc = msg.Cc?.Mailboxes?.Select(m => m.Address).ToList() ?? new List<string>(),
                    Bcc = msg.Bcc?.Mailboxes?.Select(m => m.Address).ToList() ?? new List<string>(),
                    Body = !string.IsNullOrEmpty(msg.TextBody) ? msg.TextBody : (msg.HtmlBody ?? string.Empty),
                    ReceivedDate = msg.Date.UtcDateTime,
                    Provider = credentials.Provider,
                    AccountId = credentials.AccountId ?? string.Empty,
                    IsRead = isRead
                });
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to fetch message at index {Index}", i);
            }
        }

        await client.DisconnectAsync(true);
        return emails;
    }

    public async Task<EmailMessage> GetEmailAsync(AccountCredentials credentials, string messageId)
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync();
        var cached = await dbContext.CachedEmails.FindAsync(credentials.AccountId, messageId);
        if (cached != null)
        {
            return new EmailMessage
            {
                MessageId = cached.MessageId,
                Subject = cached.Subject,
                From = cached.From,
                To = cached.To.Split(',').ToList(),
                Body = cached.Body,
                ReceivedDate = cached.ReceivedDate,
                Provider = cached.Provider,
                AccountId = cached.AccountId,
                IsRead = cached.IsRead
            };
        }

        using var client = new ImapClient();
        string host = GetImapHost(credentials.Provider);
        await client.ConnectAsync(host, 993, SecureSocketOptions.SslOnConnect);
        
        var oauth2 = new SaslMechanismOAuthBearer(credentials.Email ?? string.Empty, credentials.AccessToken ?? string.Empty);
        await client.AuthenticateAsync(oauth2);
        await client.Inbox!.OpenAsync(FolderAccess.ReadOnly);
        
        var uids = await client.Inbox.SearchAsync(SearchQuery.HeaderContains("Message-Id", messageId ?? string.Empty));
        if (uids.Any())
        {
            var msg = await client.Inbox.GetMessageAsync(uids.First());
            
            bool isRead = false;
            try
            {
                var summaries = await client.Inbox.FetchAsync(new[] { uids.First() }, MessageSummaryItems.Flags);
                isRead = summaries?.FirstOrDefault()?.Flags?.HasFlag(MessageFlags.Seen) ?? false;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "IMAP okundu bayrağı sorgulanamadı: {MessageId}", messageId);
            }

            await client.DisconnectAsync(true);
            return new EmailMessage
            {
                MessageId = msg.MessageId ?? messageId,
                Subject = msg.Subject ?? "No Subject",
                From = msg.From?.Mailboxes?.FirstOrDefault()?.Address ?? "Unknown",
                To = msg.To?.Mailboxes?.Select(m => m.Address).ToList() ?? new List<string>(),
                Cc = msg.Cc?.Mailboxes?.Select(m => m.Address).ToList() ?? new List<string>(),
                Bcc = msg.Bcc?.Mailboxes?.Select(m => m.Address).ToList() ?? new List<string>(),
                Body = !string.IsNullOrEmpty(msg.TextBody) ? msg.TextBody : (msg.HtmlBody ?? string.Empty),
                ReceivedDate = msg.Date.UtcDateTime,
                Provider = credentials.Provider,
                AccountId = credentials.AccountId ?? string.Empty,
                IsRead = isRead
            };
        }
        await client.DisconnectAsync(true);
        throw new KeyNotFoundException("Email message not found on IMAP server.");
    }

    public async Task SendEmailAsync(AccountCredentials credentials, EmailMessage message)
    {
        _logger.LogInformation("Sending email to {To} via SMTP...", string.Join(",", message.To ?? new List<string>()));
        
        var mimeMessage = new MimeKit.MimeMessage();
        mimeMessage.From.Add(new MimeKit.MailboxAddress(credentials.Email ?? string.Empty, credentials.Email ?? string.Empty));
        
        if (message.To != null)
        {
            foreach (var to in message.To)
            {
                mimeMessage.To.Add(MimeKit.MailboxAddress.Parse(to));
            }
        }
        
        mimeMessage.Subject = message.Subject ?? "No Subject";
        
        var bodyBuilder = new MimeKit.BodyBuilder();
        if (message.IsHtml)
            bodyBuilder.HtmlBody = message.Body;
        else
            bodyBuilder.TextBody = message.Body;
            
        if (message.Attachments != null)
        {
            foreach (var att in message.Attachments)
            {
                if (att.Content != null)
                {
                    bodyBuilder.Attachments.Add(att.FileName ?? "Attachment", att.Content);
                }
            }
        }
            
        mimeMessage.Body = bodyBuilder.ToMessageBody();
        
        using var client = new MailKit.Net.Smtp.SmtpClient();
        string host = credentials.Provider switch
        {
            "Google" => "smtp.gmail.com",
            "Microsoft" => "smtp.office365.com",
            "Yandex" => "smtp.yandex.com",
            _ => throw new NotSupportedException($"Provider {credentials.Provider} is not supported.")
        };
        
        int port = credentials.Provider == "Yandex" ? 465 : 587;
        var secureSocketOptions = credentials.Provider == "Yandex" ? SecureSocketOptions.SslOnConnect : SecureSocketOptions.StartTls;
        
        await client.ConnectAsync(host, port, secureSocketOptions);
        
        var oauth2 = new SaslMechanismOAuthBearer(credentials.Email ?? string.Empty, credentials.AccessToken ?? string.Empty);
        await client.AuthenticateAsync(oauth2);
        
        await client.SendAsync(mimeMessage);
        await client.DisconnectAsync(true);
        
        _logger.LogInformation("Email sent successfully.");
    }

    public async Task<bool> DeleteEmailFromServerOnlyAsync(AccountCredentials credentials, string messageId)
    {
        _logger.LogInformation("Deleting email {MessageId} from server only...", messageId);
        
        bool deletedFromServer = false;
        try
        {
            using var client = new ImapClient();
            string host = GetImapHost(credentials.Provider);
            await client.ConnectAsync(host, 993, SecureSocketOptions.SslOnConnect);
            
            var oauth2 = new SaslMechanismOAuthBearer(credentials.Email ?? string.Empty, credentials.AccessToken ?? string.Empty);
            await client.AuthenticateAsync(oauth2);
            
            await client.Inbox!.OpenAsync(FolderAccess.ReadWrite);
            
            var uids = await client.Inbox.SearchAsync(SearchQuery.HeaderContains("Message-Id", messageId ?? string.Empty));
            if (uids.Any())
            {
                await client.Inbox.AddFlagsAsync(uids.First(), MessageFlags.Deleted, true);
                await client.Inbox.ExpungeAsync();
                deletedFromServer = true;
            }
            await client.DisconnectAsync(true);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete email {MessageId} from server.", messageId);
        }
        
        return deletedFromServer;
    }

    public async Task<bool> DeleteEmailAsync(AccountCredentials credentials, string messageId)
    {
        _logger.LogInformation("Deleting email {MessageId} from server and cache...", messageId);
        
        bool deletedFromServer = await DeleteEmailFromServerOnlyAsync(credentials, messageId);
        
        try
        {
            await using var dbContext = await _dbContextFactory.CreateDbContextAsync();
            var existing = await dbContext.CachedEmails.FindAsync(credentials.AccountId, messageId);
            if (existing != null)
            {
                dbContext.CachedEmails.Remove(existing);
                await dbContext.SaveChangesAsync();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to remove email {MessageId} from local cache.", messageId);
        }
        
        return deletedFromServer;
    }

    public async Task MarkAsReadAsync(AccountCredentials credentials, string messageId)
    {
        _logger.LogInformation("Marking email {MessageId} as read...", messageId);
        
        try
        {
            using var client = new ImapClient();
            string host = GetImapHost(credentials.Provider);
            await client.ConnectAsync(host, 993, SecureSocketOptions.SslOnConnect);
            
            var oauth2 = new SaslMechanismOAuthBearer(credentials.Email ?? string.Empty, credentials.AccessToken ?? string.Empty);
            await client.AuthenticateAsync(oauth2);
            
            await client.Inbox!.OpenAsync(FolderAccess.ReadWrite);
            
            var uids = await client.Inbox.SearchAsync(SearchQuery.HeaderContains("Message-Id", messageId ?? string.Empty));
            if (uids.Any())
            {
                await client.Inbox.AddFlagsAsync(uids.First(), MessageFlags.Seen, true);
            }
            await client.DisconnectAsync(true);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to mark email {MessageId} as read on server.", messageId);
        }
        
        try
        {
            await using var dbContext = await _dbContextFactory.CreateDbContextAsync();
            var existing = await dbContext.CachedEmails.FindAsync(credentials.AccountId, messageId);
            if (existing != null)
            {
                existing.IsRead = true;
                existing.UpdatedAt = DateTime.UtcNow;
                await dbContext.SaveChangesAsync();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update read state of email {MessageId} in local cache.", messageId);
        }
    }

    public async Task MarkAsUnreadAsync(AccountCredentials credentials, string messageId)
    {
        _logger.LogInformation("Marking email {MessageId} as unread...", messageId);
        
        try
        {
            using var client = new ImapClient();
            string host = GetImapHost(credentials.Provider);
            await client.ConnectAsync(host, 993, SecureSocketOptions.SslOnConnect);
            
            var oauth2 = new SaslMechanismOAuthBearer(credentials.Email ?? string.Empty, credentials.AccessToken ?? string.Empty);
            await client.AuthenticateAsync(oauth2);
            
            await client.Inbox!.OpenAsync(FolderAccess.ReadWrite);
            
            var uids = await client.Inbox.SearchAsync(SearchQuery.HeaderContains("Message-Id", messageId ?? string.Empty));
            if (uids.Any())
            {
                await client.Inbox.RemoveFlagsAsync(uids.First(), MessageFlags.Seen, true);
            }
            await client.DisconnectAsync(true);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to mark email {MessageId} as unread on server.", messageId);
        }
        
        try
        {
            await using var dbContext = await _dbContextFactory.CreateDbContextAsync();
            var existing = await dbContext.CachedEmails.FindAsync(credentials.AccountId, messageId);
            if (existing != null)
            {
                existing.IsRead = false;
                existing.UpdatedAt = DateTime.UtcNow;
                await dbContext.SaveChangesAsync();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update unread state of email {MessageId} in local cache.", messageId);
        }
    }

    private string GetImapHost(string? provider)
    {
        return provider switch
        {
            "Google" => "imap.gmail.com",
            "Microsoft" => "outlook.office365.com",
            "Yandex" => "imap.yandex.com",
            _ => throw new NotSupportedException($"Provider {provider} is not supported.")
        };
    }
}
