using System;

namespace MultiSych.Services.Data;

public class DocumentChatMessageEntity : BaseEntity
{
    public string AccountId { get; set; } = string.Empty;
    public string FileId { get; set; } = string.Empty;
    public string Text { get; set; } = string.Empty;
    public bool IsUser { get; set; }
    public string Time { get; set; } = string.Empty;
}
