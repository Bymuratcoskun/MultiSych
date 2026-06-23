using System.ComponentModel.DataAnnotations;

namespace MultiSych.Services.Data.Entities;

public class EmailMessageEntity
{
    [Required]
    public string AccountId { get; set; } = string.Empty;

    [Required]
    public string MessageId { get; set; } = string.Empty;

    public string Subject { get; set; } = string.Empty;
    public string From { get; set; } = string.Empty;
    public string To { get; set; } = string.Empty;
    public string Snippet { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public DateTime ReceivedAt { get; set; }
    public bool IsRead { get; set; }

    /// <summary>
    /// Bu e-postanın yapay zeka tarafından etkinlik bulmak için analiz edilip edilmediğini belirtir.
    /// </summary>
    public bool IsAnalyzedForEvents { get; set; } = false;
}
