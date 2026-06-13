using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MultiSych.Services.Data.Entities;
using MultiSych.Services.Data;
using MultiSych.Services.Interfaces;
using MultiSych.Services.Models;
using Serilog;

namespace MultiSych.Services.Implementations;

public class HybridAIService : IHybridAIService
{
    private readonly IAIService _aiService;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger _logger = Log.ForContext<HybridAIService>();

    public HybridAIService(IAIService aiService, IServiceScopeFactory scopeFactory)
    {
        _aiService = aiService;
        _scopeFactory = scopeFactory;
    }

    public async Task AnalyzeUnprocessedEmailsAsync(string? accountId = null)
    {
        using var scope = _scopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<LocalCacheDbContext>();
        var accountStore = scope.ServiceProvider.GetRequiredService<IAccountStore>();

        var accountsToProcess = new List<AccountCredentials>();
        if (string.IsNullOrEmpty(accountId))
        {
            accountsToProcess.AddRange(await accountStore.GetAccountsAsync());
            _logger.Information("Starting AI event analysis for all accounts.");
        }
        else
        {
            var account = await accountStore.GetAccountAsync(accountId);
            if (account != null) accountsToProcess.Add(account);
        }

        foreach (var account in accountsToProcess)
        {
            var unanalyzedEmails = await dbContext.CachedEmails
                .Where(e => e.AccountId == account.AccountId && !e.IsAnalyzedForEvents)
                .ToListAsync();

            if (!unanalyzedEmails.Any()) continue;

            _logger.Information("Found {Count} unanalyzed emails for {Email}. Sending to AI for analysis.", unanalyzedEmails.Count, account.Email);
            var foundEvents = await AnalyzeEmailsForEventsAsync(unanalyzedEmails);

            if (foundEvents.Any())
            {
                foreach (var foundEvent in foundEvents)
                {
                    var existingEvent = await dbContext.CachedEvents.FirstOrDefaultAsync(e => e.EventId == foundEvent.EventId && e.AccountId == account.AccountId);
                    if (existingEvent == null)
                    {
                        dbContext.CachedEvents.Add(new CalendarEventEntity
                        {
                            AccountId = account.AccountId, EventId = foundEvent.EventId, Title = foundEvent.Title,
                            Description = foundEvent.Description, Location = foundEvent.Location,
                            StartTime = foundEvent.StartTime, EndTime = foundEvent.EndTime,
                            IsAllDay = foundEvent.IsAllDay, CreatedAt = DateTime.UtcNow
                        });
                    }
                }
            }

            unanalyzedEmails.ForEach(e => e.IsAnalyzedForEvents = true);
            await dbContext.SaveChangesAsync();
            _logger.Information("Marked {Count} emails as analyzed and saved {EventCount} new events for {Email}.", unanalyzedEmails.Count, foundEvents.Count, account.Email);
        }
    }

    public async Task<List<CalendarEvent>> AnalyzeEmailsForEventsAsync(List<EmailMessageEntity> emails)
    {
        _logger.Information("Analyzing {EmailCount} emails for potential events...", emails.Count);
        var foundEvents = new List<CalendarEvent>();

        foreach (var email in emails)
        {
            try
            {
                _logger.Information("Sending email '{Subject}' to Gemini AI for event extraction...", email.Subject);
                
                var prompt = $@"
Lütfen aşağıdaki e-posta içeriğini analiz et ve herhangi bir takvim etkinliği (toplantı, randevu, uçuş vb.) olup olmadığını kontrol et. 
Eğer etkinlik bulursan, SADECE aşağıdaki formatta bir JSON dizisi (array) döndür. Ekstra metin veya markdown bloğu ekleme:
[{{
    ""title"": ""Etkinlik Başlığı"",
    ""description"": ""Kısa açıklama"",
    ""location"": ""Yer veya Online"",
    ""startTime"": ""YYYY-MM-DDTHH:mm:ss"",
    ""endTime"": ""YYYY-MM-DDTHH:mm:ss"",
    ""isAllDay"": false
}}]
Eğer etkinlik yoksa sadece boş bir dizi [] döndür.

E-posta Konusu: {email.Subject}
E-posta İçeriği: {email.Body}";

                var aiResponse = await _aiService.SendMessageAsync(prompt, new List<string>(), "gemini");
                
                var jsonStr = aiResponse.Trim();
                if (jsonStr.StartsWith("```json", StringComparison.OrdinalIgnoreCase)) jsonStr = jsonStr.Substring(7);
                if (jsonStr.StartsWith("```")) jsonStr = jsonStr.Substring(3);
                if (jsonStr.EndsWith("```")) jsonStr = jsonStr.Substring(0, jsonStr.Length - 3);
                jsonStr = jsonStr.Trim();

                if (string.IsNullOrWhiteSpace(jsonStr) || jsonStr == "[]") continue;

                using var doc = System.Text.Json.JsonDocument.Parse(jsonStr);
                foreach (var element in doc.RootElement.EnumerateArray())
                {
                    var calendarEvent = new CalendarEvent
                    {
                        EventId = Guid.NewGuid().ToString(),
                        Title = element.TryGetProperty("title", out var t) ? t.GetString() ?? "Bilinmeyen Etkinlik" : "Bilinmeyen Etkinlik",
                        Description = element.TryGetProperty("description", out var d) ? d.GetString() : string.Empty,
                        Location = element.TryGetProperty("location", out var l) ? l.GetString() : string.Empty,
                        StartTime = element.TryGetProperty("startTime", out var st) && DateTime.TryParse(st.GetString(), out var sdt) ? sdt : DateTime.UtcNow,
                        EndTime = element.TryGetProperty("endTime", out var et) && DateTime.TryParse(et.GetString(), out var edt) ? edt : DateTime.UtcNow.AddHours(1),
                        IsAllDay = element.TryGetProperty("isAllDay", out var iad) && iad.GetBoolean()
                    };
                    foundEvents.Add(calendarEvent);
                    _logger.Information("AI successfully extracted event: {Title} at {StartTime}", calendarEvent.Title, calendarEvent.StartTime);
                }
            }
            catch (Exception ex)
            {
                _logger.Warning(ex, "Failed to analyze email for events. Subject: {Subject}", email.Subject);
            }
        }

        return await Task.FromResult(foundEvents);
    }

    public async Task<string> GenerateDailySummaryAsync()
    {
        using var scope = _scopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<LocalCacheDbContext>();
        
        var today = DateTime.UtcNow.Date;
        var todaysEmails = await dbContext.CachedEmails
            .Where(e => e.ReceivedAt >= today)
            .OrderByDescending(e => e.ReceivedAt)
            .Take(20) // Token sınırını aşmamak için son 20 maili alıyoruz
            .ToListAsync();

        if (!todaysEmails.Any())
            return "Bugün henüz yeni bir e-posta almadınız, posta kutunuz oldukça sakin.";

        var promptBuilder = new System.Text.StringBuilder();
        promptBuilder.AppendLine("Aşağıdaki e-posta konularını ve özetlerini inceleyerek kullanıcının bugünkü e-posta trafiğinin genel bir özetini SADECE TEK BİR CÜMLE ile yaz:");
        foreach (var e in todaysEmails)
        {
            promptBuilder.AppendLine($"- {e.Subject}: {e.Snippet}");
        }

        try
        {
            var response = await _aiService.SendMessageAsync(promptBuilder.ToString(), new List<string>(), "hybrid");
            return response.Trim().Replace("\"", "").Replace("*", "");
        }
        catch (Exception ex)
        {
            _logger.Warning(ex, "Failed to generate daily summary.");
            return "Yapay zeka hizmetine erişilemediği için özet oluşturulamadı.";
        }
    }
}
