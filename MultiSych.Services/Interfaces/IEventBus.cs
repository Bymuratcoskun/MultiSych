using System;
using System.Threading.Tasks;

namespace MultiSych.Services.Interfaces;

public interface IEventBus
{
    void Publish<TEvent>(TEvent @event) where TEvent : class;
    IDisposable Subscribe<TEvent>(Func<TEvent, Task> handler) where TEvent : class;
    IDisposable Subscribe<TEvent>(Action<TEvent> handler) where TEvent : class;
}

// Built-in domain events
public record SyncCompletedEvent(string AccountId, string Provider, int ItemsSynced, DateTime CompletedAt);
public record AuthTokenRefreshedEvent(string AccountId, string Provider, DateTime ExpiresAt);
public record NewEmailReceivedEvent(string AccountId, string EmailId, string Subject, string From);
public record CalendarEventChangedEvent(string AccountId, string EventId, string ChangeType);
public record StorageFileChangedEvent(string AccountId, string FileId, string ChangeType);
public record AppErrorEvent(string Source, string Message, Exception? Exception);
public record FileConflictDetectedEvent(
    string AccountId,
    string FileId,
    string FileName,
    string LocalPath,
    DateTime LocalModifiedAt,
    DateTime CloudModifiedAt);

/// <summary>Kullanıcı çakışmayı çözdüğünde yayınlanır. Strategy: KeepLocal | KeepCloud | KeepBoth</summary>
public record ConflictResolvedEvent(string AccountId, string FileId, string Strategy);

/// <summary>Offline sync kuyruğu işlenirken dosya başına ilerleme. FileName boşsa sync tamamlandı demektir.</summary>
public record SyncProgressEvent(string FileName, double Percent, int TotalItems, int ProcessedItems);

/// <summary>Sesli komut / arka plan bir sekmeye geçiş istediğinde yayınlanır (ör. "Dashboard", "Settings").</summary>
public record NavigationIntentEvent(string Section);

/// <summary>Kullanıcıya gösterilecek bir bildirim mesajı yayınlanır.</summary>
public record NotificationIntentEvent(string Message);

/// <summary>Gerçek zamanlı transkripsiyon sırasında kısmi metin parçası yayınlanır.</summary>
public record PartialTranscriptionEvent(string Text);

/// <summary>Konuşmacı ayrıştırmalı (diarization) ayrıntılı transkripsiyon çıktısı yayınlanır.</summary>
public record DetailedTranscriptionEvent(string Text);
