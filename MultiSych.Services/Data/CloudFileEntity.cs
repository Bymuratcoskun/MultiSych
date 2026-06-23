using System;

namespace MultiSych.Services.Data;

public class CloudFileEntity : BaseEntity
{
    public string AccountId { get; set; } = string.Empty;
    public string FileId { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public string Path { get; set; } = string.Empty;
    public string? ParentId { get; set; }
    public string MimeType { get; set; } = string.Empty;
    public long FileSize { get; set; }
    public bool IsDirectory { get; set; }
    public string Provider { get; set; } = string.Empty;
}
