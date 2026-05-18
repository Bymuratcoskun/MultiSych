using MultiSych.Services.Models;

namespace MultiSych.Services.Interfaces
{
    public interface IEmailService
    {
        Task<List<EmailMessage>> GetEmailsAsync(AccountCredentials credentials, int maxResults = 10);
        Task<EmailMessage> GetEmailAsync(AccountCredentials credentials, string messageId);
        Task SendEmailAsync(AccountCredentials credentials, EmailMessage message);
        Task<bool> DeleteEmailAsync(AccountCredentials credentials, string messageId);
        Task MarkAsReadAsync(AccountCredentials credentials, string messageId);
        Task MarkAsUnreadAsync(AccountCredentials credentials, string messageId);
        Task SyncEmailsAsync(AccountCredentials credentials);
    }
}
