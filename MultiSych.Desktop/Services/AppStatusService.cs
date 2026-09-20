using System;

namespace MultiSych.Desktop.Services;

public class AppStatusService : IAppStatusService
{
    public event Action<StatusUpdate>? StatusChanged;

    public void PostUpdate(string message, bool isSyncing = false)
    {
        StatusChanged?.Invoke(new StatusUpdate
        {
            Message = message,
            IsSyncing = isSyncing
        });
    }

    public void PostDatabaseCounts(int accounts, int emails, int events, int files)
    {
        StatusChanged?.Invoke(new StatusUpdate
        {
            Message = "Dashboard data loaded.",
            TotalAccounts = accounts,
            TotalEmails = emails,
            TotalEvents = events,
            TotalFiles = files
        });
    }

    public void PostProgress(string fileName, double percent, string transferSpeed = "")
    {
        StatusChanged?.Invoke(new StatusUpdate
        {
            Message = $"{fileName} senkronize ediliyor...",
            IsSyncing = true,
            ProgressFileName = fileName,
            ProgressPercent = Math.Clamp(percent, 0, 100),
            TransferSpeed = transferSpeed
        });
    }

    public void ClearProgress()
    {
        StatusChanged?.Invoke(new StatusUpdate
        {
            Message = "Senkronizasyon tamamlandı.",
            IsSyncing = false,
            ProgressPercent = null
        });
    }
}
