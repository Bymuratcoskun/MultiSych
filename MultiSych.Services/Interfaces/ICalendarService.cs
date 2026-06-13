using MultiSych.Services.Models;

namespace MultiSych.Services.Interfaces
{
    public interface ICalendarService
    {
        Task<List<CalendarEvent>> GetEventsAsync(AccountCredentials credentials, DateTime startDate, DateTime endDate);
        Task<CalendarEvent> GetEventAsync(AccountCredentials credentials, string eventId);
        Task<string> CreateEventAsync(AccountCredentials credentials, CalendarEvent @event);
        Task UpdateEventAsync(AccountCredentials credentials, CalendarEvent @event);
        Task<bool> DeleteEventAsync(AccountCredentials credentials, string eventId);
        Task SyncEventsAsync(AccountCredentials credentials);
    }
}
