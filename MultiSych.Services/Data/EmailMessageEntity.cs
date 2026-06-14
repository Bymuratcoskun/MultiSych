using System;

namespace MultiSych.Services.Data;

public class EmailMessageEntity : BaseEntity
{
    public string AccountId { get; set; } = string.Empty;
    public string MessageId { get; set; } = string.Empty;
    public string Subject { get; set; } = string.Empty;
    public string From { get; set; } = string.Empty;
    public string To { get; set; } = string.Empty; // Virgülle ayrılmış olarak saklanacak
    public string Snippet { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public DateTime ReceivedDate { get; set; }
    public bool IsRead { get; set; }
    public bool IsAnalyzedForEvents { get; set; }
    public DateTime ReceivedAt { get; set; }
    public string Provider { get; set; } = string.Empty;
}
