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
}

public interface IAppStatusService
{
    IObservable<StatusUpdate> StatusChanged { get; }
    void PostUpdate(string message, bool isSyncing = false);
    void PostDatabaseCounts(int accounts, int emails, int events, int files);
}
