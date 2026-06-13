using System;

namespace MultiSych.Services.Data;

public class CalendarEventEntity : BaseEntity
{
    public string AccountId { get; set; } = string.Empty;
    public string EventId { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }
    public string Provider { get; set; } = string.Empty;
}
