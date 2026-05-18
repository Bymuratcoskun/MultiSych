using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using MultiSych.Services.Interfaces;
using MultiSych.Services.Models;
using Serilog;

namespace MultiSych.Services.Implementations
{
    public class EmailServiceStub : IEmailService
    {
        private readonly ILogger _logger;

        public EmailServiceStub()
        {
            _logger = Log.ForContext<EmailServiceStub>();
        }

        public Task<List<EmailMessage>> GetEmailsAsync(AccountCredentials credentials, int maxResults = 10)
        {
            _logger.Information("[EmailServiceStub] GetEmailsAsync called for provider {Provider}.", credentials.Provider);
            return Task.FromResult(new List<EmailMessage>());
        }

        public Task<EmailMessage> GetEmailAsync(AccountCredentials credentials, string messageId)
        {
            _logger.Information("[EmailServiceStub] GetEmailAsync called for provider {Provider}, messageId={MessageId}.", credentials.Provider, messageId);
            return Task.FromResult(new EmailMessage
            {
                MessageId = messageId,
                Subject = "Placeholder email",
                From = credentials.Email,
                To = new List<string> { credentials.Email ?? string.Empty },
                Body = "This is a placeholder email message because the selected provider integration is not yet implemented.",
                ReceivedDate = DateTime.UtcNow,
                Provider = credentials.Provider,
                AccountId = credentials.AccountId
            });
        }

        public Task SendEmailAsync(AccountCredentials credentials, EmailMessage message)
        {
            _logger.Information("[EmailServiceStub] SendEmailAsync called for provider {Provider}. Message Subject: {Subject}", credentials.Provider, message.Subject);
            return Task.CompletedTask;
        }

        public Task<bool> DeleteEmailAsync(AccountCredentials credentials, string messageId)
        {
            _logger.Information("[EmailServiceStub] DeleteEmailAsync called for provider {Provider}, messageId={MessageId}.", credentials.Provider, messageId);
            return Task.FromResult(true);
        }

        public Task MarkAsReadAsync(AccountCredentials credentials, string messageId)
        {
            _logger.Information("[EmailServiceStub] MarkAsReadAsync called for provider {Provider}, messageId={MessageId}.", credentials.Provider, messageId);
            return Task.CompletedTask;
        }

        public Task MarkAsUnreadAsync(AccountCredentials credentials, string messageId)
        {
            _logger.Information("[EmailServiceStub] MarkAsUnreadAsync called for provider {Provider}, messageId={MessageId}.", credentials.Provider, messageId);
            return Task.CompletedTask;
        }

        public Task SyncEmailsAsync(AccountCredentials credentials)
        {
            _logger.Information("[EmailServiceStub] SyncEmailsAsync called for provider {Provider}.", credentials.Provider);
            return Task.CompletedTask;
        }
    }
}
