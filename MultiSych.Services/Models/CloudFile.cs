namespace MultiSych.Services.Models
{
    public class CloudFile
    {
        public string? FileId { get; set; }
        public string? FileName { get; set; }
        public string? FilePath { get; set; }
        public long FileSize { get; set; }
        public string? MimeType { get; set; }
        public DateTime CreatedDate { get; set; }
        public DateTime ModifiedDate { get; set; }
        public string? Owner { get; set; }
        public bool IsDirectory { get; set; }
        public string? Provider { get; set; } // "Google", "Microsoft", "Yandex"
        public string? AccountId { get; set; }
        public Dictionary<string, object>? Metadata { get; set; }
    }
}
