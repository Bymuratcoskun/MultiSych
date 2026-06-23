using System;
using System.Reactive.Subjects;

namespace MultiSych.Desktop.Services;

public class AppStatusService : IAppStatusService
{
    private readonly Subject<StatusUpdate> _statusSubject = new();

    public IObservable<StatusUpdate> StatusChanged => _statusSubject;

    public void PostUpdate(string message, bool isSyncing = false)
    {
        _statusSubject.OnNext(new StatusUpdate
        {
            Message = message,
            IsSyncing = isSyncing
        });
    }

    public void PostDatabaseCounts(int accounts, int emails, int events, int files)
    {
        _statusSubject.OnNext(new StatusUpdate
        {
            Message = "Dashboard data loaded.",
            TotalAccounts = accounts,
            TotalEmails = emails,
            TotalEvents = events,
            TotalFiles = files
        });
    }
}
