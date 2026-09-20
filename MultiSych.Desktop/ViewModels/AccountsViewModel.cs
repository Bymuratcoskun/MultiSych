using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using System.Windows.Input;
using Avalonia.Threading;
using Microsoft.Extensions.DependencyInjection;
using MultiSych.Desktop.Services;
using IWindowService = MultiSych.Desktop.Services.IWindowService;
using MultiSych.Services.Interfaces;
using Serilog;

namespace MultiSych.Desktop.ViewModels;

public class AccountItemViewModel : ViewModelBase
{
    private bool _isMounted;

    public string AccountId { get; init; } = string.Empty;
    public string Email { get; init; } = string.Empty;
    public string Provider { get; init; } = string.Empty;

    public bool IsMounted
    {
        get => _isMounted;
        set
        {
            SetProperty(ref _isMounted, value);
            OnPropertyChanged(nameof(MountStateColor));
            OnPropertyChanged(nameof(MountStateIcon));
            OnPropertyChanged(nameof(Status));
        }
    }

    // Bağlı ise Yeşil (🟢), değilse Gri (⚪)
    public string MountStateColor => IsMounted ? "#4CAF50" : "#9E9E9E";
    public string MountStateIcon => IsMounted ? "🟢" : "⚪";
    public string Status => IsMounted ? "🟢 Sanal Sürücü Bağlı" : "⚪ Sürücü Bağlı Değil";
}

public class AccountsViewModel : ViewModelBase
{
    private readonly IAccountStore _accountStore;
    private readonly IVirtualDriveService _virtualDriveService;
    private readonly IWindowService _windowService;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IAppStatusService _appStatusService;
    private readonly ILogger _logger;

    public ObservableCollection<AccountItemViewModel> Accounts { get; } = new();

    public ICommand LoadAccountsCommand { get; }
    public ICommand AddAccountCommand { get; }
    public ICommand MountCommand { get; }
    public ICommand UnmountCommand { get; }
    public ICommand DeleteAccountCommand { get; }
    public ICommand SyncAccountCommand { get; }

    public AccountsViewModel(
        IAccountStore accountStore, 
        IVirtualDriveService virtualDriveService, 
        IWindowService windowService,
        IServiceScopeFactory scopeFactory,
        IAppStatusService appStatusService)
    {
        _accountStore = accountStore;
        _virtualDriveService = virtualDriveService;
        _windowService = windowService;
        _scopeFactory = scopeFactory;
        _appStatusService = appStatusService;
        _logger = Log.ForContext<AccountsViewModel>();

        LoadAccountsCommand = new RelayCommand(async _ => await LoadAccountsAsync());
        AddAccountCommand = new RelayCommand(_ => AddAccount());
        MountCommand = new RelayCommand<AccountItemViewModel?>(async item => await MountDriveAsync(item));
        UnmountCommand = new RelayCommand<AccountItemViewModel?>(async item => await UnmountDriveAsync(item));
        DeleteAccountCommand = new RelayCommand<AccountItemViewModel?>(async item => await DeleteAccountAsync(item));
        SyncAccountCommand = new RelayCommand<AccountItemViewModel?>(async item => await SyncAccountAsync(item));

        Task.Run(LoadAccountsAsync); // Başlangıçta hesapları yükle
    }

    private async Task LoadAccountsAsync()
    {
        try
        {
            var accounts = await _accountStore.GetAccountsAsync();
            var items = new System.Collections.Generic.List<AccountItemViewModel>();

            foreach (var acc in accounts)
            {
                bool isMounted = await _virtualDriveService.IsMountedAsync(acc.AccountId ?? string.Empty);
                items.Add(new AccountItemViewModel
                {
                    AccountId = acc.AccountId ?? string.Empty,
                    Email = acc.Email ?? string.Empty,
                    Provider = acc.Provider ?? string.Empty,
                    IsMounted = isMounted
                });
            }

            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                Accounts.Clear();
                foreach (var item in items)
                {
                    Accounts.Add(item);
                }
            });
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Failed to load accounts.");
        }
    }

    private void AddAccount() => _windowService.ShowAddAccountDialog();

    private async Task MountDriveAsync(AccountItemViewModel? item)
    {
        if (item == null || string.IsNullOrEmpty(item.AccountId)) return;
        _appStatusService.PostUpdate("Sürücü bağlanıyor...", isSyncing: true);
        if (await _virtualDriveService.MountDriveAsync(item.AccountId))
        {
            await UpdateMountStateAsync(item.AccountId);
            _appStatusService.PostUpdate("Sürücü başarıyla bağlandı.", isSyncing: false);
        }
        else
        {
            _appStatusService.PostUpdate("Sürücü bağlanamadı.", isSyncing: false);
        }
    }

    private async Task UnmountDriveAsync(AccountItemViewModel? item)
    {
        if (item == null || string.IsNullOrEmpty(item.AccountId)) return;
        _appStatusService.PostUpdate("Sürücü bağlantısı kesiliyor...", isSyncing: true);
        if (await _virtualDriveService.UnmountDriveAsync(item.AccountId))
        {
            await UpdateMountStateAsync(item.AccountId);
            _appStatusService.PostUpdate("Sürücü bağlantısı kesildi.", isSyncing: false);
        }
        else
        {
            _appStatusService.PostUpdate("Sürücü bağlantısı kesilemedi.", isSyncing: false);
        }
    }

    private async Task DeleteAccountAsync(AccountItemViewModel? item)
    {
        if (item == null || string.IsNullOrEmpty(item.AccountId)) return;
        _appStatusService.PostUpdate("Hesap siliniyor...", isSyncing: true);
        await _virtualDriveService.UnmountDriveAsync(item.AccountId); // Varsa önce çıkart
        await _accountStore.DeleteAccountAsync(item.AccountId);       // Sonra veritabanından sil
        await LoadAccountsAsync();
        _appStatusService.PostUpdate("Hesap silindi.", isSyncing: false);
    }

    private async Task SyncAccountAsync(AccountItemViewModel? item)
    {
        if (item == null || string.IsNullOrEmpty(item.AccountId)) return;
        _appStatusService.PostUpdate("Hesap senkronizasyonu başlatılıyor...", isSyncing: true);
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var accountStore = scope.ServiceProvider.GetRequiredService<IAccountStore>();
            var emailService = scope.ServiceProvider.GetRequiredService<IEmailService>();
            var calendarService = scope.ServiceProvider.GetRequiredService<ICalendarService>();
            var storageService = scope.ServiceProvider.GetRequiredService<IStorageService>();
            var hybridAiService = scope.ServiceProvider.GetRequiredService<IHybridAIService>();

            var account = await accountStore.GetAccountAsync(item.AccountId);
            if (account != null)
            {
                _appStatusService.PostUpdate($"{account.Email} - E-Postalar senkronize ediliyor...", isSyncing: true);
                await emailService.SyncEmailsAsync(account);
                await hybridAiService.AnalyzeUnprocessedEmailsAsync(account.AccountId);

                _appStatusService.PostUpdate($"{account.Email} - Takvim senkronize ediliyor...", isSyncing: true);
                await calendarService.SyncEventsAsync(account);

                _appStatusService.PostUpdate($"{account.Email} - Dosyalar senkronize ediliyor...", isSyncing: true);
                await storageService.SyncStorageAsync(account);

                _appStatusService.PostUpdate("Hesap senkronizasyonu başarıyla tamamlandı.", isSyncing: false);
            }
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Failed to sync account {AccountId}", item.AccountId);
            _appStatusService.PostUpdate($"Senkronizasyon hatası: {ex.Message}", isSyncing: false);
        }
    }

    private async Task UpdateMountStateAsync(string accountId)
    {
        foreach (var acc in Accounts)
        {
            if (acc.AccountId == accountId)
            {
                acc.IsMounted = await _virtualDriveService.IsMountedAsync(accountId);
                break;
            }
        }
    }
}
