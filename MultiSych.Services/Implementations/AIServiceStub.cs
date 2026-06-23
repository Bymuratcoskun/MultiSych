using System.Collections.Generic;
using System.Threading.Tasks;
using MultiSych.Services.Interfaces;
using MultiSych.Services.Models;

namespace MultiSych.Services.Implementations;

public class AIServiceStub : IAIService
{
    public Task<string> SendMessageAsync(string prompt, List<string> history, string provider)
    {
        return Task.FromResult($"[AIServiceStub using {provider}]: I received your message. Real AI integrations will be linked here.");
    }

    public Task<string> GetResponseAsync(string prompt, string provider)
    {
        return Task.FromResult($"[AIServiceStub using {provider}]: GetResponseAsync stub response.");
    }

    public Task<string> GetResponseAsync(string prompt, string provider, AccountCredentials? account)
    {
        return Task.FromResult($"[AIServiceStub using {provider}]: GetResponseAsync stub response for account {account?.Email}.");
    }

    public Task<string> AnalyzeEmailAsync(EmailMessage email, string provider)
    {
        return Task.FromResult($"[AIServiceStub using {provider}]: Analyzed email '{email.Subject}'.");
    }

    public Task<List<CalendarEvent>> GenerateCalendarSuggestionsAsync(List<EmailMessage> emails, string provider)
    {
        return Task.FromResult(new List<CalendarEvent>());
    }

    public Task<string> SummarizeDocumentAsync(string content, string provider)
    {
        return Task.FromResult($"[AIServiceStub using {provider}]: Document summarized.");
    }
}
