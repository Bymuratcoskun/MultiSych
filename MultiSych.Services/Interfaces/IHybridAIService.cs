using System.Collections.Generic;
using System.Threading.Tasks;
using MultiSych.Services.Models;
using MultiSych.Services.Data;

namespace MultiSych.Services.Interfaces;

public interface IHybridAIService
{
    Task AnalyzeUnprocessedEmailsAsync(string? accountId = null);
    Task<List<CalendarEvent>> AnalyzeEmailsForEventsAsync(List<EmailMessageEntity> emails);
    Task<string> GenerateDailySummaryAsync();
}