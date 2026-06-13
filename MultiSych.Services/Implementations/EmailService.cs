using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MailKit;
using MailKit.Net.Imap;
using MailKit.Security;
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

    public EmailService(ILogger<EmailService> logger, IDbContextFactory<LocalCacheDbContext> dbContextFactory)
    {
        _logger = logger;
        _dbContextFactory = dbContextFactory;
    }

    public async Task SyncEmailsAsync(AccountCredentials credentials)
    {
        _logger.LogInformation("Starting email sync for {Email} via MailKit...", credentials.Email);
        
        try
        {
            var emails = await GetEmailsAsync(credentials, 10);
            
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

        string host = credentials.Provider switch
        {
            "Google" => "imap.gmail.com",
            "Microsoft" => "outlook.office365.com",
            "Yandex" => "imap.yandex.com",
            _ => throw new NotSupportedException($"Provider {credentials.Provider} is not supported.")
        };

        await client.ConnectAsync(host, 993, SecureSocketOptions.SslOnConnect);

        // Şifre yerine daha güvenli olan OAuth2 AccessToken yöntemi kullanılıyor
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
                emails.Add(new EmailMessage
                {
                    MessageId = msg.MessageId ?? Guid.NewGuid().ToString(),
                    Subject = msg.Subject ?? "No Subject",
                    From = msg.From?.Mailboxes?.FirstOrDefault()?.Name ?? msg.From?.Mailboxes?.FirstOrDefault()?.Address ?? "Unknown",
                    To = msg.To?.Mailboxes?.Select(m => m.Address).ToList() ?? new List<string>(),
                    Body = !string.IsNullOrEmpty(msg.TextBody) ? msg.TextBody : (msg.HtmlBody ?? string.Empty),
                    ReceivedDate = msg.Date.UtcDateTime,
                    Provider = credentials.Provider,
                    AccountId = credentials.AccountId ?? string.Empty
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

    public Task<EmailMessage> GetEmailAsync(AccountCredentials credentials, string messageId) => throw new NotImplementedException();
    public Task SendEmailAsync(AccountCredentials credentials, EmailMessage message) => throw new NotImplementedException();
    public Task<bool> DeleteEmailAsync(AccountCredentials credentials, string messageId) => throw new NotImplementedException();
    public Task MarkAsReadAsync(AccountCredentials credentials, string messageId) => throw new NotImplementedException();
    public Task MarkAsUnreadAsync(AccountCredentials credentials, string messageId) => throw new NotImplementedException();
}
