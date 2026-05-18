using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using Avalonia.Controls.Notifications;
using MultiSych.Desktop.Services;
using MultiSych.Services.Interfaces;
using MultiSych.Services.Models;

namespace MultiSych.Desktop.ViewModels;

public sealed class SyncViewModel : ViewModelBase
{
    private readonly IEmailService _emailService;
    private readonly ICalendarService _calendarService;
    private readonly IAccountStore _accountStore;
    private readonly IAIService _aiService;
    private readonly IWindowService _windowService;
    private readonly ISyncSignalService _syncSignalService;
    private string _syncStatus = "Use the button below to trigger a sync cycle for all connected accounts.";

    public SyncViewModel(IEmailService emailService, ICalendarService calendarService, IAccountStore accountStore, IAIService aiService, IWindowService windowService, ISyncSignalService syncSignalService)
    {
        _emailService = emailService;
        _calendarService = calendarService;
        _accountStore = accountStore;
        _aiService = aiService;
        _windowService = windowService;
        _syncSignalService = syncSignalService;
        
        TriggerSyncCommand = new RelayCommand(async _ => await TriggerSyncAsync());
        GenerateSuggestionsCommand = new RelayCommand(async _ => await GenerateSuggestionsAsync());
        AddSuggestionToCalendarCommand = new RelayCommand(async param => await AddSuggestionToCalendarAsync(param));
    }

    public ICommand TriggerSyncCommand { get; }
    public ICommand GenerateSuggestionsCommand { get; }
    public ICommand AddSuggestionToCalendarCommand { get; }
    public ObservableCollection<CalendarEvent> AiSuggestions { get; } = new();

    public string SyncStatus
    {
        get => _syncStatus;
        set => SetProperty(ref _syncStatus, value);
    }

    private async Task TriggerSyncAsync()
    {
        // Senkronizasyon yükünü UI/ViewModel thread'inden alıp,
        // arka planda çalışan AutoSyncBackgroundService'e iletiyoruz.
        SyncStatus = "Sync request sent to background service...";
        _syncSignalService.TriggerSync();
        
        _windowService.ShowNotification("Sync Queued", "A synchronization cycle has been queued in the background.", NotificationType.Information);
    }

    private async Task GenerateSuggestionsAsync()
    {
        var accounts = await _accountStore.GetAccountsAsync();
        var emailAccount = accounts.FirstOrDefault(a => a.Provider == "Google" || a.Provider == "Microsoft" || a.Provider == "Yandex");
        
        if (emailAccount == null)
        {
            SyncStatus = "No connected accounts available to analyze emails.";
            return;
        }

        SyncStatus = $"Analyzing recent emails from {emailAccount.Email} to suggest events...";
        AiSuggestions.Clear();

        try
        {
            // En son 5 e-postayı analiz için çekiyoruz
            var emails = await _emailService.GetEmailsAsync(emailAccount, maxResults: 5);
            if (emails.Count == 0)
            {
                SyncStatus = "No recent emails found to analyze.";
                return;
            }

            var suggestions = await _aiService.GenerateCalendarSuggestionsAsync(emails, "hybrid");
            foreach (var suggestion in suggestions)
            {
                AiSuggestions.Add(suggestion);
            }

            SyncStatus = $"AI successfully generated {suggestions.Count} calendar suggestions.";
        }
        catch (Exception ex)
        {
            SyncStatus = $"AI suggestion error: {ex.Message}";
        }
    }

    private async Task AddSuggestionToCalendarAsync(object? parameter)
    {
        if (parameter is not CalendarEvent suggestion)
            return;
            
        var accounts = await _accountStore.GetAccountsAsync();
        var calendarAccount = accounts.FirstOrDefault(a => a.Provider == "Google" || a.Provider == "Microsoft" || a.Provider == "Yandex");
        
        if (calendarAccount == null)
        {
            SyncStatus = "No connected cloud account available to add the calendar event.";
            return;
        }

        try
        {
            SyncStatus = $"Adding event to {calendarAccount.Provider} calendar...";
            await _calendarService.CreateEventAsync(calendarAccount, suggestion);
            AiSuggestions.Remove(suggestion); // Başarıyla eklenen öneriyi listeden kaldır
            SyncStatus = $"Event '{suggestion.Title}' successfully added to {calendarAccount.Provider} calendar!";
            _windowService.ShowNotification("Event Added", $"'{suggestion.Title}' was added to your {calendarAccount.Provider} calendar.", NotificationType.Success);
        }
        catch (Exception ex)
        {
            SyncStatus = $"Failed to add event: {ex.Message}";
        }
    }
}
