using System;

namespace MultiSych.Desktop.Services;

public class StatusUpdate
{
    public string Message { get; set; } = string.Empty;
    public int? TotalAccounts { get; set; }
    public int? TotalEmails { get; set; }
    public int? TotalEvents { get; set; }
    public int? TotalFiles { get; set; }
    public bool IsSyncing { get; set; }
    // Sync Progress Overlay alanları
    public string? ProgressFileName { get; set; }
    public double? ProgressPercent { get; set; }   // 0–100
    public string? TransferSpeed { get; set; }      // "1.2 MB/s" gibi insan okunabilir
}

public interface IAppStatusService
{
    event Action<StatusUpdate>? StatusChanged;
    void PostUpdate(string message, bool isSyncing = false);
    void PostDatabaseCounts(int accounts, int emails, int events, int files);
    void PostProgress(string fileName, double percent, string transferSpeed = "");
    void ClearProgress();
}
