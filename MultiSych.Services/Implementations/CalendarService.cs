using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Threading.Tasks;
using Google.Apis.Auth.OAuth2;
using Google.Apis.Calendar.v3;
using Google.Apis.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MultiSych.Services.Data;
using MultiSych.Services.Interfaces;
using MultiSych.Services.Models;

namespace MultiSych.Services.Implementations;

public class CalendarService : ICalendarService
{
    private readonly ILogger<CalendarService> _logger;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IDbContextFactory<LocalCacheDbContext> _dbContextFactory;

    public CalendarService(ILogger<CalendarService> logger, IHttpClientFactory httpClientFactory, IDbContextFactory<LocalCacheDbContext> dbContextFactory)
    {
        _logger = logger;
        _httpClientFactory = httpClientFactory;
        _dbContextFactory = dbContextFactory;
    }

    public async Task SyncEventsAsync(AccountCredentials credentials)
    {
        _logger.LogInformation("Starting calendar sync for provider: {Provider}", credentials.Provider);

        try
        {
            if (credentials.Provider == "Google")
            {
                await SyncGoogleCalendarAsync(credentials);
            }
            else if (credentials.Provider == "Microsoft")
            {
                await SyncOutlookCalendarAsync(credentials);
            }
            else
            {
                _logger.LogWarning("Calendar sync is not yet implemented for {Provider}", credentials.Provider);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to sync calendar for {Email}", credentials.Email);
        }
    }

    private async Task SyncGoogleCalendarAsync(AccountCredentials account)
    {
        _logger.LogInformation("Fetching events from Google Calendar for {Email}...", account.Email);

        var credential = GoogleCredential.FromAccessToken(account.AccessToken);
        // İsim çakışmasını önlemek için Google SDK'sındaki sınıfın tam adını yazıyoruz
        var service = new Google.Apis.Calendar.v3.CalendarService(new BaseClientService.Initializer
        {
            HttpClientInitializer = credential,
            ApplicationName = "MultiSych"
        });

        var request = service.Events.List("primary");
        request.TimeMinDateTimeOffset = DateTimeOffset.UtcNow; // Sadece şimdiden sonraki etkinlikler
        request.ShowDeleted = false;
        request.SingleEvents = true;
        request.MaxResults = 10;
        request.OrderBy = EventsResource.ListRequest.OrderByEnum.StartTime;

        var events = await request.ExecuteAsync();
        
        if (events.Items != null && events.Items.Count > 0)
        {
            await using var dbContext = await _dbContextFactory.CreateDbContextAsync();
            foreach (var eventItem in events.Items)
            {
                string when = eventItem.Start?.DateTimeDateTimeOffset?.ToString() ?? eventItem.Start?.Date ?? "Unknown Time";
                _logger.LogInformation("Found Google Calendar Event: {Summary} at {When}", eventItem.Summary, when);

                var calEvent = new CalendarEvent
                {
                    AccountId = account.AccountId ?? string.Empty,
                    EventId = eventItem.Id ?? Guid.NewGuid().ToString(),
                    Title = eventItem.Summary ?? "No Title",
                    Description = eventItem.Description ?? string.Empty,
                    StartTime = eventItem.Start?.DateTimeDateTimeOffset?.UtcDateTime ?? (DateTime.TryParse(eventItem.Start?.Date, out var sDt) ? sDt.ToUniversalTime() : DateTime.UtcNow),
                    EndTime = eventItem.End?.DateTimeDateTimeOffset?.UtcDateTime ?? (DateTime.TryParse(eventItem.End?.Date, out var eDt) ? eDt.ToUniversalTime() : DateTime.UtcNow),
                    Provider = "Google",
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };

                var existing = await dbContext.CachedEvents.FindAsync(calEvent.AccountId, calEvent.EventId);
                if (existing != null)
                {
                    existing.Title = calEvent.Title;
                    existing.Description = calEvent.Description;
                    existing.StartTime = calEvent.StartTime;
                    existing.EndTime = calEvent.EndTime;
                    existing.UpdatedAt = DateTime.UtcNow;
                }
                else await dbContext.CachedEvents.AddAsync(new CalendarEventEntity
                {
                    AccountId = calEvent.AccountId,
                    EventId = calEvent.EventId,
                    Title = calEvent.Title,
                    Description = calEvent.Description,
                    StartTime = calEvent.StartTime,
                    EndTime = calEvent.EndTime,
                    Provider = calEvent.Provider,
                    CreatedAt = calEvent.CreatedAt,
                    UpdatedAt = calEvent.UpdatedAt
                });
            }
            await dbContext.SaveChangesAsync();
            _logger.LogInformation("Successfully listed and synced {Count} events from Google Calendar.", events.Items.Count);
        }
        else
        {
            _logger.LogInformation("No upcoming events found in Google Calendar.");
        }
    }

    private async Task SyncOutlookCalendarAsync(AccountCredentials account)
    {
        _logger.LogInformation("Fetching events from Outlook Calendar for {Email}...", account.Email);

        var endpoint = "https://graph.microsoft.com/v1.0/me/calendar/events?$select=subject,start,end&$top=10";
        using var httpClient = _httpClientFactory.CreateClient();
        httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", account.AccessToken);

        var response = await httpClient.GetAsync(endpoint);
        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync();
            _logger.LogError("Microsoft Graph API returned an error: {Error}", error);
            throw new Exception($"Microsoft Graph API error: {response.StatusCode}");
        }

        var content = await response.Content.ReadAsStringAsync();
        using var document = JsonDocument.Parse(content);

        if (document.RootElement.TryGetProperty("value", out var items))
        {
            var count = items.GetArrayLength();
            await using var dbContext = await _dbContextFactory.CreateDbContextAsync();
            foreach (var item in items.EnumerateArray())
            {
                var subject = item.TryGetProperty("subject", out var subjProp) ? subjProp.GetString() : "No Subject";
                var start = item.TryGetProperty("start", out var startProp) && startProp.TryGetProperty("dateTime", out var dtProp) ? dtProp.GetString() : "Unknown Time";
                var eventId = item.TryGetProperty("id", out var idProp) ? idProp.GetString() : Guid.NewGuid().ToString();
                
                _logger.LogInformation("Found Outlook Calendar Event: {Subject} starting at {Start}", subject, start);

                var calEvent = new CalendarEvent
                {
                    AccountId = account.AccountId ?? string.Empty,
                    EventId = eventId ?? Guid.NewGuid().ToString(),
                    Title = subject ?? "No Title",
                    Description = item.TryGetProperty("bodyPreview", out var bodyProp) ? bodyProp.GetString() ?? string.Empty : string.Empty,
                    StartTime = DateTime.TryParse(start, out var parsedStart) ? parsedStart.ToUniversalTime() : DateTime.UtcNow,
                    EndTime = item.TryGetProperty("end", out var endProp) && endProp.TryGetProperty("dateTime", out var endDtProp) && DateTime.TryParse(endDtProp.GetString(), out var parsedEnd) ? parsedEnd.ToUniversalTime() : DateTime.UtcNow,
                    Provider = "Microsoft",
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };

                var existing = await dbContext.CachedEvents.FindAsync(calEvent.AccountId, calEvent.EventId);
                if (existing != null)
                {
                    existing.Title = calEvent.Title;
                    existing.Description = calEvent.Description;
                    existing.StartTime = calEvent.StartTime;
                    existing.EndTime = calEvent.EndTime;
                    existing.UpdatedAt = DateTime.UtcNow;
                }
                else await dbContext.CachedEvents.AddAsync(new CalendarEventEntity
                {
                    AccountId = calEvent.AccountId,
                    EventId = calEvent.EventId,
                    Title = calEvent.Title,
                    Description = calEvent.Description,
                    StartTime = calEvent.StartTime,
                    EndTime = calEvent.EndTime,
                    Provider = calEvent.Provider,
                    CreatedAt = calEvent.CreatedAt,
                    UpdatedAt = calEvent.UpdatedAt
                });
            }
            await dbContext.SaveChangesAsync();
            _logger.LogInformation("Successfully listed and synced {Count} events from Outlook Calendar.", count);
        }
        else
        {
            _logger.LogInformation("No upcoming events found in Outlook Calendar.");
        }
    }

    // --- Other ICalendarService Methods (İleride doldurulacak) ---
    public Task<List<CalendarEvent>> GetEventsAsync(AccountCredentials credentials, DateTime startDate, DateTime endDate) => throw new NotImplementedException();
    public Task<CalendarEvent> GetEventAsync(AccountCredentials credentials, string eventId) => throw new NotImplementedException();
    public Task<string> CreateEventAsync(AccountCredentials credentials, CalendarEvent @event) => throw new NotImplementedException();
    public Task UpdateEventAsync(AccountCredentials credentials, CalendarEvent @event) => throw new NotImplementedException();
    public Task<bool> DeleteEventAsync(AccountCredentials credentials, string eventId) => throw new NotImplementedException();
}
