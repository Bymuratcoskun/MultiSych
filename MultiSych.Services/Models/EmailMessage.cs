namespace MultiSych.Services.Models
{
    public class EmailMessage
    {
        public string? MessageId { get; set; }
        public string? Subject { get; set; }
        public string? From { get; set; }
        public List<string>? To { get; set; }
        public List<string>? Cc { get; set; }
        public List<string>? Bcc { get; set; }
        public string? Body { get; set; }
        public bool IsHtml { get; set; }
        public DateTime ReceivedDate { get; set; }
        public List<EmailAttachment>? Attachments { get; set; }
        public string? Provider { get; set; } // "Google", "Microsoft", "Yandex"
        public string? AccountId { get; set; }
    }

    public class EmailAttachment
    {
        public string? FileName { get; set; }
        public string? MimeType { get; set; }
        public long Size { get; set; }
        public byte[]? Content { get; set; }
    }
}
