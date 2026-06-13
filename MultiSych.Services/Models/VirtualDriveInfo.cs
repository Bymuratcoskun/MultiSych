namespace MultiSych.Services.Models
{
    public class VirtualDriveInfo
    {
        public string AccountId { get; set; } = string.Empty;
        public string Provider { get; set; } = string.Empty;
        public string DriveLabel { get; set; } = string.Empty;
        public string MountPoint { get; set; } = string.Empty;
        public bool IsMounted { get; set; }
        public string? StatusMessage { get; set; }
    }
}
