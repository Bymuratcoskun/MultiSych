using System.ComponentModel.DataAnnotations;

namespace MultiSych.Services.Data.Entities;

public class CalendarEventEntity
{
    [Required]
    public string AccountId { get; set; } = string.Empty;

    [Required]
    public string EventId { get; set; } = string.Empty;

    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? Location { get; set; }
    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }
    public bool IsAllDay { get; set; }
    public DateTime CreatedAt { get; set; }
}
