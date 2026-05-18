using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using System.Threading.Tasks;
using Google.Apis.Auth.OAuth2;
using Google.Apis.Calendar.v3;
using Google.Apis.Services;
using MultiSych.Services.Interfaces;
using MultiSych.Services.Models;
using Serilog;

namespace MultiSych.Services.Implementations
{
    public class CloudCalendarService : ICalendarService
    {
        private readonly ILogger _logger = Log.ForContext<CloudCalendarService>();
        private readonly IHttpClientFactory _httpClientFactory;

        public CloudCalendarService(IHttpClientFactory httpClientFactory)
        {
            _httpClientFactory = httpClientFactory;
        }

        public async Task<List<CalendarEvent>> GetEventsAsync(AccountCredentials credentials, DateTime startDate, DateTime endDate)
        {
            _logger.Information("Fetching events from {Provider} for account {Email}", credentials.Provider, credentials.Email);

            if (credentials.Provider == "Google")
                return await GetGoogleEventsAsync(credentials, startDate, endDate);
            if (credentials.Provider == "Microsoft")
                return await GetMicrosoftEventsAsync(credentials, startDate, endDate);
            if (credentials.Provider == "Yandex")
                return await GetYandexEventsAsync(credentials, startDate, endDate);

            return new List<CalendarEvent>();
        }

        public async Task<CalendarEvent> GetEventAsync(AccountCredentials credentials, string eventId)
        {
            if (credentials.Provider == "Google")
                return await GetGoogleEventAsync(credentials, eventId);
            if (credentials.Provider == "Microsoft")
                return await GetMicrosoftEventAsync(credentials, eventId);
            if (credentials.Provider == "Yandex")
                return await GetYandexEventAsync(credentials, eventId);
                
            throw new NotSupportedException($"Provider {credentials.Provider} is not supported for calendar events.");
        }

        public async Task<string> CreateEventAsync(AccountCredentials credentials, CalendarEvent @event)
        {
            _logger.Information("Creating event in {Provider} for account {Email}", credentials.Provider, credentials.Email);

            if (credentials.Provider == "Google")
                return await CreateGoogleEventAsync(credentials, @event);
            if (credentials.Provider == "Microsoft")
                return await CreateMicrosoftEventAsync(credentials, @event);
            if (credentials.Provider == "Yandex")
                return await CreateYandexEventAsync(credentials, @event);
                
            throw new NotSupportedException($"Provider {credentials.Provider} is not supported for creating events.");
        }

        public async Task UpdateEventAsync(AccountCredentials credentials, CalendarEvent @event)
        {
            _logger.Information("Updating event {EventId} in {Provider}", @event.EventId, credentials.Provider);

            if (credentials.Provider == "Google")
                await UpdateGoogleEventAsync(credentials, @event);
            else if (credentials.Provider == "Microsoft")
                await UpdateMicrosoftEventAsync(credentials, @event);
            else if (credentials.Provider == "Yandex")
                await UpdateYandexEventAsync(credentials, @event);
            else
                throw new NotSupportedException($"Provider {credentials.Provider} is not supported for updating events.");
        }

        public async Task<bool> DeleteEventAsync(AccountCredentials credentials, string eventId)
        {
            _logger.Information("Deleting event {EventId} from {Provider}", eventId, credentials.Provider);

            if (credentials.Provider == "Google")
                return await DeleteGoogleEventAsync(credentials, eventId);
            if (credentials.Provider == "Microsoft")
                return await DeleteMicrosoftEventAsync(credentials, eventId);
            if (credentials.Provider == "Yandex")
                return await DeleteYandexEventAsync(credentials, eventId);
                
            throw new NotSupportedException($"Provider {credentials.Provider} is not supported for deleting events.");
        }

        public Task SyncEventsAsync(AccountCredentials credentials) => Task.CompletedTask;

        // --- Google Calendar Implementations ---

        private CalendarService GetGoogleCalendarService(AccountCredentials credentials)
        {
            var credential = GoogleCredential.FromAccessToken(credentials.AccessToken);
            return new CalendarService(new BaseClientService.Initializer
            {
                HttpClientInitializer = credential,
                ApplicationName = "MultiSych"
            });
        }

        private async Task<List<CalendarEvent>> GetGoogleEventsAsync(AccountCredentials credentials, DateTime startDate, DateTime endDate)
        {
            var service = GetGoogleCalendarService(credentials);
            var request = service.Events.List("primary");
            request.TimeMinDateTimeOffset = startDate;
            request.TimeMaxDateTimeOffset = endDate;
            request.SingleEvents = true;
            request.OrderBy = EventsResource.ListRequest.OrderByEnum.StartTime;

            var response = await request.ExecuteAsync();
            var events = new List<CalendarEvent>();

            if (response.Items != null)
            {
                foreach (var item in response.Items)
                {
                    events.Add(MapGoogleEventToCalendarEvent(item, credentials));
                }
            }

            return events;
        }

        private async Task<CalendarEvent> GetGoogleEventAsync(AccountCredentials credentials, string eventId)
        {
            var service = GetGoogleCalendarService(credentials);
            var item = await service.Events.Get("primary", eventId).ExecuteAsync();
            return MapGoogleEventToCalendarEvent(item, credentials);
        }

        private async Task<string> CreateGoogleEventAsync(AccountCredentials credentials, CalendarEvent @event)
        {
            var service = GetGoogleCalendarService(credentials);
            var newEvent = new Google.Apis.Calendar.v3.Data.Event
            {
                Summary = @event.Title,
                Description = @event.Description,
                Start = new Google.Apis.Calendar.v3.Data.EventDateTime { DateTimeDateTimeOffset = @event.StartTime },
                End = new Google.Apis.Calendar.v3.Data.EventDateTime { DateTimeDateTimeOffset = @event.EndTime }
            };

            var createdEvent = await service.Events.Insert(newEvent, "primary").ExecuteAsync();
            return createdEvent.Id;
        }

        private async Task UpdateGoogleEventAsync(AccountCredentials credentials, CalendarEvent @event)
        {
            var service = GetGoogleCalendarService(credentials);
            var existingEvent = await service.Events.Get("primary", @event.EventId).ExecuteAsync();
            
            existingEvent.Summary = @event.Title;
            existingEvent.Description = @event.Description;
            existingEvent.Start = new Google.Apis.Calendar.v3.Data.EventDateTime { DateTimeDateTimeOffset = @event.StartTime };
            existingEvent.End = new Google.Apis.Calendar.v3.Data.EventDateTime { DateTimeDateTimeOffset = @event.EndTime };

            await service.Events.Update(existingEvent, "primary", @event.EventId).ExecuteAsync();
        }

        private async Task<bool> DeleteGoogleEventAsync(AccountCredentials credentials, string eventId)
        {
            var service = GetGoogleCalendarService(credentials);
            await service.Events.Delete("primary", eventId).ExecuteAsync();
            return true;
        }

        private CalendarEvent MapGoogleEventToCalendarEvent(Google.Apis.Calendar.v3.Data.Event item, AccountCredentials credentials)
        {
            return new CalendarEvent
            {
                EventId = item.Id,
                Title = item.Summary ?? "No Title",
                Description = item.Description ?? string.Empty,
                StartTime = item.Start?.DateTimeDateTimeOffset?.UtcDateTime ?? (DateTime.TryParse(item.Start?.Date, out var sDt) ? sDt.ToUniversalTime() : DateTime.UtcNow),
                EndTime = item.End?.DateTimeDateTimeOffset?.UtcDateTime ?? (DateTime.TryParse(item.End?.Date, out var eDt) ? eDt.ToUniversalTime() : DateTime.UtcNow),
                Provider = "Google",
                AccountId = credentials.AccountId,
                CreatedAt = item.CreatedDateTimeOffset?.UtcDateTime ?? DateTime.UtcNow,
                UpdatedAt = item.UpdatedDateTimeOffset?.UtcDateTime ?? DateTime.UtcNow
            };
        }

        // --- Microsoft Graph API Implementations ---

        private HttpClient CreateMicrosoftHttpClient(AccountCredentials credentials)
        {
            var httpClient = _httpClientFactory.CreateClient();
            httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", credentials.AccessToken);
            return httpClient;
        }

        private async Task<List<CalendarEvent>> GetMicrosoftEventsAsync(AccountCredentials credentials, DateTime startDate, DateTime endDate)
        {
            var endpoint = $"https://graph.microsoft.com/v1.0/me/calendarView?startDateTime={startDate:yyyy-MM-ddTHH:mm:ssZ}&endDateTime={endDate:yyyy-MM-ddTHH:mm:ssZ}";
            using var httpClient = CreateMicrosoftHttpClient(credentials);
            var response = await httpClient.GetAsync(endpoint);
            response.EnsureSuccessStatusCode();

            var content = await response.Content.ReadAsStringAsync();
            using var document = JsonDocument.Parse(content);
            var events = new List<CalendarEvent>();

            if (document.RootElement.TryGetProperty("value", out var items))
            {
                foreach (var item in items.EnumerateArray())
                {
                    events.Add(MapMicrosoftEventToCalendarEvent(item, credentials));
                }
            }
            return events;
        }

        private async Task<CalendarEvent> GetMicrosoftEventAsync(AccountCredentials credentials, string eventId)
        {
            var endpoint = $"https://graph.microsoft.com/v1.0/me/events/{eventId}";
            using var httpClient = CreateMicrosoftHttpClient(credentials);
            var response = await httpClient.GetAsync(endpoint);
            response.EnsureSuccessStatusCode();

            var content = await response.Content.ReadAsStringAsync();
            using var document = JsonDocument.Parse(content);
            return MapMicrosoftEventToCalendarEvent(document.RootElement, credentials);
        }

        private async Task<string> CreateMicrosoftEventAsync(AccountCredentials credentials, CalendarEvent @event)
        {
            var endpoint = "https://graph.microsoft.com/v1.0/me/events";
            var payload = new
            {
                subject = @event.Title,
                body = new { contentType = "Text", content = @event.Description },
                start = new { dateTime = @event.StartTime.ToString("o"), timeZone = "UTC" },
                end = new { dateTime = @event.EndTime.ToString("o"), timeZone = "UTC" }
            };

            using var httpClient = CreateMicrosoftHttpClient(credentials);
            var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
            var response = await httpClient.PostAsync(endpoint, content);
            response.EnsureSuccessStatusCode();

            var responseBody = await response.Content.ReadAsStringAsync();
            using var document = JsonDocument.Parse(responseBody);
            return document.RootElement.GetProperty("id").GetString() ?? string.Empty;
        }

        private async Task UpdateMicrosoftEventAsync(AccountCredentials credentials, CalendarEvent @event)
        {
            var endpoint = $"https://graph.microsoft.com/v1.0/me/events/{@event.EventId}";
            var payload = new
            {
                subject = @event.Title,
                body = new { contentType = "Text", content = @event.Description },
                start = new { dateTime = @event.StartTime.ToString("o"), timeZone = "UTC" },
                end = new { dateTime = @event.EndTime.ToString("o"), timeZone = "UTC" }
            };

            using var httpClient = CreateMicrosoftHttpClient(credentials);
            var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
            var request = new HttpRequestMessage(new HttpMethod("PATCH"), endpoint) { Content = content };
            var response = await httpClient.SendAsync(request);
            response.EnsureSuccessStatusCode();
        }

        private async Task<bool> DeleteMicrosoftEventAsync(AccountCredentials credentials, string eventId)
        {
            var endpoint = $"https://graph.microsoft.com/v1.0/me/events/{eventId}";
            using var httpClient = CreateMicrosoftHttpClient(credentials);
            var response = await httpClient.DeleteAsync(endpoint);
            response.EnsureSuccessStatusCode();
            return true;
        }

        private CalendarEvent MapMicrosoftEventToCalendarEvent(JsonElement item, AccountCredentials credentials)
        {
            return new CalendarEvent
            {
                EventId = item.GetProperty("id").GetString() ?? string.Empty,
                Title = item.TryGetProperty("subject", out var subj) ? subj.GetString() ?? "No Title" : "No Title",
                Description = item.TryGetProperty("bodyPreview", out var body) ? body.GetString() ?? string.Empty : string.Empty,
                StartTime = item.TryGetProperty("start", out var startToken) && startToken.TryGetProperty("dateTime", out var startDt) ? DateTime.Parse(startDt.GetString() ?? string.Empty).ToUniversalTime() : DateTime.UtcNow,
                EndTime = item.TryGetProperty("end", out var endToken) && endToken.TryGetProperty("dateTime", out var endDt) ? DateTime.Parse(endDt.GetString() ?? string.Empty).ToUniversalTime() : DateTime.UtcNow,
                Provider = "Microsoft",
                AccountId = credentials.AccountId,
                CreatedAt = item.TryGetProperty("createdDateTime", out var created) ? created.GetDateTime() : DateTime.UtcNow,
                UpdatedAt = item.TryGetProperty("lastModifiedDateTime", out var modified) ? modified.GetDateTime() : DateTime.UtcNow
            };
        }

        // --- Yandex CalDAV API Implementations ---

        private HttpClient CreateYandexCalDavClient(AccountCredentials credentials)
        {
            var client = _httpClientFactory.CreateClient();
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("OAuth", credentials.AccessToken);
            return client;
        }

        private string GetYandexCalendarUrl(string? email)
        {
            // Yandex uses 'events' as the default calendar for CalDAV.
            return $"https://caldav.yandex.ru/calendars/{email ?? "unknown"}/events/";
        }

        private async Task<List<CalendarEvent>> GetYandexEventsAsync(AccountCredentials credentials, DateTime startDate, DateTime endDate)
        {
            var url = GetYandexCalendarUrl(credentials.Email);
            var xmlQuery = $@"<?xml version=""1.0"" encoding=""utf-8"" ?>
<c:calendar-query xmlns:d=""DAV:"" xmlns:c=""urn:ietf:params:xml:ns:caldav"">
  <d:prop>
    <c:calendar-data />
  </d:prop>
  <c:filter>
    <c:comp-filter name=""VCALENDAR"">
      <c:comp-filter name=""VEVENT"">
        <c:time-range start=""{startDate:yyyyMMddTHHmmssZ}"" end=""{endDate:yyyyMMddTHHmmssZ}"" />
      </c:comp-filter>
    </c:comp-filter>
  </c:filter>
</c:calendar-query>";

            using var client = CreateYandexCalDavClient(credentials);
            var request = new HttpRequestMessage(new HttpMethod("REPORT"), url)
            {
                Content = new StringContent(xmlQuery, Encoding.UTF8, "application/xml")
            };
            request.Headers.Add("Depth", "1");

            var response = await client.SendAsync(request);
            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync();
                _logger.Error("Yandex CalDAV REPORT failed: {Error}", error);
                throw new Exception($"Yandex CalDAV error: {response.StatusCode}");
            }

            var xmlResponse = await response.Content.ReadAsStringAsync();
            var events = new List<CalendarEvent>();

            try
            {
                var doc = XDocument.Parse(xmlResponse);
                var calDataNodes = doc.Descendants().Where(x => x.Name.LocalName == "calendar-data");
                foreach (var node in calDataNodes)
                {
                    var ical = node.Value;
                    var uid = ExtractIcalValue(ical, "UID");
                    if (string.IsNullOrEmpty(uid)) continue;

                    var dtStartStr = ExtractIcalValue(ical, "DTSTART");
                    var dtEndStr = ExtractIcalValue(ical, "DTEND");

                    events.Add(new CalendarEvent
                    {
                        EventId = uid,
                        Title = ExtractIcalValue(ical, "SUMMARY") ?? "No Title",
                        Description = ExtractIcalValue(ical, "DESCRIPTION") ?? string.Empty,
                        StartTime = ParseIcalDateTime(dtStartStr) ?? DateTime.UtcNow,
                        EndTime = ParseIcalDateTime(dtEndStr) ?? DateTime.UtcNow.AddHours(1),
                        Provider = "Yandex",
                        AccountId = credentials.AccountId,
                        CreatedAt = DateTime.UtcNow,
                        UpdatedAt = DateTime.UtcNow
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to parse Yandex CalDAV XML response.");
            }

            return events;
        }

        private async Task<CalendarEvent> GetYandexEventAsync(AccountCredentials credentials, string eventId)
        {
            var url = $"{GetYandexCalendarUrl(credentials.Email)}{eventId}.ics";
            using var client = CreateYandexCalDavClient(credentials);
            var response = await client.GetAsync(url);
            response.EnsureSuccessStatusCode();

            var ical = await response.Content.ReadAsStringAsync();
            return new CalendarEvent
            {
                EventId = eventId,
                Title = ExtractIcalValue(ical, "SUMMARY") ?? "No Title",
                Description = ExtractIcalValue(ical, "DESCRIPTION") ?? string.Empty,
                StartTime = ParseIcalDateTime(ExtractIcalValue(ical, "DTSTART")) ?? DateTime.UtcNow,
                EndTime = ParseIcalDateTime(ExtractIcalValue(ical, "DTEND")) ?? DateTime.UtcNow.AddHours(1),
                Provider = "Yandex",
                AccountId = credentials.AccountId,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
        }

        private async Task<string> CreateYandexEventAsync(AccountCredentials credentials, CalendarEvent @event)
        {
            var uid = string.IsNullOrEmpty(@event.EventId) ? Guid.NewGuid().ToString() : @event.EventId;
            var url = $"{GetYandexCalendarUrl(credentials.Email)}{uid}.ics";

            var ical = $@"BEGIN:VCALENDAR
VERSION:2.0
PRODID:-//MultiSych//DesktopClient//EN
BEGIN:VEVENT
UID:{uid}
DTSTAMP:{DateTime.UtcNow:yyyyMMddTHHmmssZ}
DTSTART:{@event.StartTime:yyyyMMddTHHmmssZ}
DTEND:{@event.EndTime:yyyyMMddTHHmmssZ}
SUMMARY:{@event.Title}
DESCRIPTION:{@event.Description?.Replace("\r", "")?.Replace("\n", "\\n")}
END:VEVENT
END:VCALENDAR";

            using var client = CreateYandexCalDavClient(credentials);
            var request = new HttpRequestMessage(HttpMethod.Put, url)
            {
                Content = new StringContent(ical, Encoding.UTF8, "text/calendar")
            };

            var response = await client.SendAsync(request);
            response.EnsureSuccessStatusCode();
            return uid;
        }

        private async Task UpdateYandexEventAsync(AccountCredentials credentials, CalendarEvent @event)
        {
            await CreateYandexEventAsync(credentials, @event); // CalDAV uses PUT for updates too
        }

        private async Task<bool> DeleteYandexEventAsync(AccountCredentials credentials, string eventId)
        {
            var url = $"{GetYandexCalendarUrl(credentials.Email)}{eventId}.ics";
            using var client = CreateYandexCalDavClient(credentials);
            var response = await client.DeleteAsync(url);
            return response.IsSuccessStatusCode || response.StatusCode == System.Net.HttpStatusCode.NotFound;
        }

        private string? ExtractIcalValue(string ical, string key)
        {
            var match = Regex.Match(ical, $@"{key}[^:]*:(.*?)\r?\n", RegexOptions.IgnoreCase);
            return match.Success ? match.Groups[1].Value.Trim() : null;
        }

        private DateTime? ParseIcalDateTime(string? dtStr)
        {
            if (string.IsNullOrEmpty(dtStr)) return null;
            if (DateTime.TryParseExact(dtStr, "yyyyMMddTHHmmssZ", null, System.Globalization.DateTimeStyles.AssumeUniversal, out var dt))
                return dt;
            if (DateTime.TryParseExact(dtStr, "yyyyMMddTHHmmss", null, System.Globalization.DateTimeStyles.AssumeUniversal, out var dtLocal))
                return dtLocal;
            if (DateTime.TryParseExact(dtStr, "yyyyMMdd", null, System.Globalization.DateTimeStyles.AssumeUniversal, out var date))
                return date;
            return null;
        }
    }
}