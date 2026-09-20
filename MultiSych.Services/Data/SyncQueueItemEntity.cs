using System;

namespace MultiSych.Services.Data;

public class SyncQueueItemEntity : BaseEntity
{
    public string AccountId { get; set; } = string.Empty;
    public string Action { get; set; } = string.Empty; // "Upload", "Delete", "Move"
    public string FileId { get; set; } = string.Empty;
    public string LocalFilePath { get; set; } = string.Empty;
    public string TargetFolderId { get; set; } = string.Empty;
    public string NewFileName { get; set; } = string.Empty;
    public bool IsProcessed { get; set; }
    public int RetryCount { get; set; }
    public string? ErrorMessage { get; set; }
}
