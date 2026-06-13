using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using MultiSych.Services.Interfaces;
using MultiSych.Services.Models;
using Serilog;

namespace MultiSych.Services.Implementations
{
    public class CalendarServiceStub : ICalendarService
    {
        private readonly ILogger _logger;

        public CalendarServiceStub()
        {
            _logger = Log.ForContext<CalendarServiceStub>();
        }

        public Task<List<CalendarEvent>> GetEventsAsync(AccountCredentials credentials, DateTime startDate, DateTime endDate)
        {
            _logger.Information("[CalendarServiceStub] GetEventsAsync called for provider {Provider}.", credentials.Provider);
            return Task.FromResult(new List<CalendarEvent>());
        }

        public Task<CalendarEvent> GetEventAsync(AccountCredentials credentials, string eventId)
        {
            _logger.Information("[CalendarServiceStub] GetEventAsync called for provider {Provider}, eventId={EventId}.", credentials.Provider, eventId);
            return Task.FromResult(new CalendarEvent
            {
                EventId = eventId,
                Title = "Placeholder event",
                Description = "This is a placeholder calendar event because provider integration is not yet implemented.",
                StartTime = DateTime.UtcNow.AddHours(1),
                EndTime = DateTime.UtcNow.AddHours(2),
                Provider = credentials.Provider,
                AccountId = credentials.AccountId,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            });
        }

        public Task<string> CreateEventAsync(AccountCredentials credentials, CalendarEvent @event)
        {
            _logger.Information("[CalendarServiceStub] CreateEventAsync called for provider {Provider}. Event title: {Title}", credentials.Provider, @event.Title);
            return Task.FromResult(Guid.NewGuid().ToString());
        }

        public Task UpdateEventAsync(AccountCredentials credentials, CalendarEvent @event)
        {
            _logger.Information("[CalendarServiceStub] UpdateEventAsync called for provider {Provider}. EventId: {EventId}", credentials.Provider, @event.EventId);
            return Task.CompletedTask;
        }

        public Task<bool> DeleteEventAsync(AccountCredentials credentials, string eventId)
        {
            _logger.Information("[CalendarServiceStub] DeleteEventAsync called for provider {Provider}, eventId={EventId}.", credentials.Provider, eventId);
            return Task.FromResult(true);
        }

        public Task SyncEventsAsync(AccountCredentials credentials)
        {
            _logger.Information("[CalendarServiceStub] SyncEventsAsync called for provider {Provider}.", credentials.Provider);
            return Task.CompletedTask;
        }
    }
}
