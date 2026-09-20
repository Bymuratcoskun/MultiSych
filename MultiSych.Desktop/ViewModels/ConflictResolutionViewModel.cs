using System;
using System.Collections.ObjectModel;
using System.Windows.Input;
using MultiSych.Services.Interfaces;

namespace MultiSych.Desktop.ViewModels;

public class ConflictItem : ViewModelBase
{
    public string FileId { get; init; } = string.Empty;
    public string AccountId { get; init; } = string.Empty;
    public string FileName { get; init; } = string.Empty;
    public string LocalPath { get; init; } = string.Empty;
    public DateTime LocalModifiedAt { get; init; }
    public DateTime CloudModifiedAt { get; init; }
    public string LocalModifiedDisplay => LocalModifiedAt.ToString("dd.MM.yyyy HH:mm");
    public string CloudModifiedDisplay => CloudModifiedAt.ToString("dd.MM.yyyy HH:mm");

    private string _resolution = string.Empty;
    public string Resolution
    {
        get => _resolution;
        set => SetProperty(ref _resolution, value);
    }
    public bool IsResolved => !string.IsNullOrEmpty(Resolution);
}

public class ConflictResolutionViewModel : ViewModelBase
{
    private readonly IEventBus _eventBus;

    public ObservableCollection<ConflictItem> Conflicts { get; } = new();
    public bool HasItems => Conflicts.Count > 0;

    public ICommand KeepLocalCommand { get; }
    public ICommand KeepCloudCommand { get; }
    public ICommand KeepBothCommand { get; }
    public ICommand DismissAllCommand { get; }

    public ConflictResolutionViewModel(IEventBus eventBus)
    {
        _eventBus = eventBus;

        KeepLocalCommand = new RelayCommand<ConflictItem>(item =>
        {
            if (item == null) return;
            item.Resolution = "KeepLocal";
            _eventBus.Publish(new ConflictResolvedEvent(item.AccountId, item.FileId, "KeepLocal"));
            TryRemove(item);
        });

        KeepCloudCommand = new RelayCommand<ConflictItem>(item =>
        {
            if (item == null) return;
            item.Resolution = "KeepCloud";
            _eventBus.Publish(new ConflictResolvedEvent(item.AccountId, item.FileId, "KeepCloud"));
            TryRemove(item);
        });

        KeepBothCommand = new RelayCommand<ConflictItem>(item =>
        {
            if (item == null) return;
            item.Resolution = "KeepBoth";
            _eventBus.Publish(new ConflictResolvedEvent(item.AccountId, item.FileId, "KeepBoth"));
            TryRemove(item);
        });

        DismissAllCommand = new RelayCommand(_ =>
        {
            Conflicts.Clear();
            OnPropertyChanged(nameof(HasItems));
        });
    }

    public void AddConflict(FileConflictDetectedEvent ev)
    {
        // Aynı dosya için zaten bekleyen çakışma varsa güncelle
        for (int i = 0; i < Conflicts.Count; i++)
        {
            if (Conflicts[i].FileId == ev.FileId && Conflicts[i].AccountId == ev.AccountId)
            {
                Conflicts.RemoveAt(i);
                break;
            }
        }

        Conflicts.Add(new ConflictItem
        {
            FileId = ev.FileId,
            AccountId = ev.AccountId,
            FileName = ev.FileName,
            LocalPath = ev.LocalPath,
            LocalModifiedAt = ev.LocalModifiedAt,
            CloudModifiedAt = ev.CloudModifiedAt
        });
        OnPropertyChanged(nameof(HasItems));
    }

    private void TryRemove(ConflictItem item)
    {
        Conflicts.Remove(item);
        OnPropertyChanged(nameof(HasItems));
    }
}
