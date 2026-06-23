using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using System.Windows.Input;
using Avalonia.Threading;
using Microsoft.Extensions.DependencyInjection;
using MultiSych.Desktop.Services;
using MultiSych.Services.Interfaces;
using MultiSych.Services.Models;

namespace MultiSych.Desktop.ViewModels;

public class SyncViewModel : ViewModelBase
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IAppStatusService _appStatusService;
    private readonly ISyncSignalService _syncSignalService;
    private bool _isBusy;
    private string? _selectedAccountId;
    private string _selectedSyncType = "Tümü";

    public ObservableCollection<AccountCredentials> Accounts { get; } = new();
    public ObservableCollection<string> SyncTypes { get; } = new() { "Tümü", "E-Posta", "Takvim", "Dosyalar" };

    public SyncViewModel(IServiceScopeFactory scopeFactory, IAppStatusService appStatusService, ISyncSignalService syncSignalService)
    {
        _scopeFactory = scopeFactory;
        _appStatusService = appStatusService;
        _syncSignalService = syncSignalService;
        
        AnalyzeEmailsCommand = new RelayCommand(async _ => await AnalyzeEmailsAsync(), _ => !IsBusy);
        TriggerSyncCommand = new RelayCommand(async _ => await TriggerSyncAsync(), _ => !IsBusy);
        
        Task.Run(LoadAccountsAsync);
    }

    public ICommand AnalyzeEmailsCommand { get; }
    public ICommand TriggerSyncCommand { get; }

    public bool IsBusy { get => _isBusy; set => SetProperty(ref _isBusy, value); }
    public string? SelectedAccountId { get => _selectedAccountId; set => SetProperty(ref _selectedAccountId, value); }
    public string SelectedSyncType { get => _selectedSyncType; set => SetProperty(ref _selectedSyncType, value); }

    private async Task LoadAccountsAsync()
    {
        using var scope = _scopeFactory.CreateScope();
        var accountStore = scope.ServiceProvider.GetRequiredService<IAccountStore>();
        var accounts = await accountStore.GetAccountsAsync();

        Dispatcher.UIThread.Post(() =>
        {
            Accounts.Clear();
            Accounts.Add(new AccountCredentials { AccountId = "", Email = "Tüm Hesaplar", Provider = "Hepsi" });
            foreach (var acc in accounts) Accounts.Add(acc);
            SelectedAccountId = "";
        });
    }

    private async Task TriggerSyncAsync()
    {
        IsBusy = true;
        _appStatusService.PostUpdate("Filtrelenmiş manuel senkronizasyon başlatılıyor...", isSyncing: true);
        
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var accountStore = scope.ServiceProvider.GetRequiredService<IAccountStore>();
            var emailService = scope.ServiceProvider.GetRequiredService<IEmailService>();
            var calendarService = scope.ServiceProvider.GetRequiredService<ICalendarService>();
            var storageService = scope.ServiceProvider.GetRequiredService<IStorageService>();
            var hybridAiService = scope.ServiceProvider.GetRequiredService<IHybridAIService>();

            var accountsToSync = new List<AccountCredentials>();
            if (string.IsNullOrEmpty(SelectedAccountId))
            {
                accountsToSync.AddRange(await accountStore.GetAccountsAsync());
            }
            else
            {
                var acc = await accountStore.GetAccountAsync(SelectedAccountId);
                if (acc != null) accountsToSync.Add(acc);
            }

            foreach (var account in accountsToSync)
            {
                if (SelectedSyncType == "Tümü" || SelectedSyncType == "E-Posta")
                {
                    _appStatusService.PostUpdate($"{account.Email} - E-Postalar senkronize ediliyor...", isSyncing: true);
                    await emailService.SyncEmailsAsync(account);
                    await hybridAiService.AnalyzeUnprocessedEmailsAsync(account.AccountId);
                }

                if (SelectedSyncType == "Tümü" || SelectedSyncType == "Takvim")
                {
                    _appStatusService.PostUpdate($"{account.Email} - Takvim senkronize ediliyor...", isSyncing: true);
                    await calendarService.SyncEventsAsync(account);
                }

                if (SelectedSyncType == "Tümü" || SelectedSyncType == "Dosyalar")
                {
                    _appStatusService.PostUpdate($"{account.Email} - Dosyalar senkronize ediliyor...", isSyncing: true);
                    await storageService.SyncStorageAsync(account);
                }
            }

            _appStatusService.PostUpdate("Senkronizasyon başarıyla tamamlandı.", isSyncing: false);
        }
        catch (Exception ex)
        {
            _appStatusService.PostUpdate($"Senkronizasyon hatası: {ex.Message}", isSyncing: false);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task AnalyzeEmailsAsync()
    {
        IsBusy = true;
        _appStatusService.PostUpdate("Manuel e-posta analizi başlatılıyor...", isSyncing: true);
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var aiService = scope.ServiceProvider.GetRequiredService<IHybridAIService>();
            await aiService.AnalyzeUnprocessedEmailsAsync();
            _appStatusService.PostUpdate("E-posta analizi tamamlandı.", isSyncing: false);
        }
        catch (Exception ex)
        {
            _appStatusService.PostUpdate($"E-posta analizi hatası: {ex.Message}", isSyncing: false);
        }
        finally
        {
            IsBusy = false;
        }
    }
}
