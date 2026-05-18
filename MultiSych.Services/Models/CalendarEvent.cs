namespace MultiSych.Services.Models
{
    public class CalendarEvent
    {
        public string? EventId { get; set; }
        public string? Title { get; set; }
        public string? Description { get; set; }
        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }
        public string? Location { get; set; }
        public List<string>? Attendees { get; set; }
        public bool IsAllDay { get; set; }
        public string? CalendarId { get; set; }
        public string? Provider { get; set; } // "Google", "Microsoft", "Yandex"
        public string? AccountId { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
    }
}
