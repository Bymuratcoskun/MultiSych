using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using System.Windows.Input;
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
        }
    }

    // Bağlı ise Yeşil (🟢), değilse Gri (⚪)
    public string MountStateColor => IsMounted ? "#4CAF50" : "#9E9E9E";
    public string MountStateIcon => IsMounted ? "🟢" : "⚪";
}

public class AccountsViewModel : ViewModelBase
{
    private readonly IAccountStore _accountStore;
    private readonly IVirtualDriveService _virtualDriveService;
    private readonly IWindowService _windowService;
    private readonly ILogger _logger;

    public ObservableCollection<AccountItemViewModel> Accounts { get; } = new();

    public ICommand LoadAccountsCommand { get; }
    public ICommand AddAccountCommand { get; }
    public ICommand MountDriveCommand { get; }
    public ICommand UnmountDriveCommand { get; }
    public ICommand DeleteAccountCommand { get; }

    public AccountsViewModel(IAccountStore accountStore, IVirtualDriveService virtualDriveService, IWindowService windowService)
    {
        _accountStore = accountStore;
        _virtualDriveService = virtualDriveService;
        _windowService = windowService;
        _logger = Log.ForContext<AccountsViewModel>();

        LoadAccountsCommand = new RelayCommand(async _ => await LoadAccountsAsync());
        AddAccountCommand = new RelayCommand(_ => AddAccount());
        MountDriveCommand = new RelayCommand<string?>(async id => await MountDriveAsync(id ?? string.Empty));
        UnmountDriveCommand = new RelayCommand<string?>(async id => await UnmountDriveAsync(id ?? string.Empty));
        DeleteAccountCommand = new RelayCommand<string?>(async id => await DeleteAccountAsync(id ?? string.Empty));

        Task.Run(LoadAccountsAsync); // Başlangıçta hesapları yükle
    }

    private async Task LoadAccountsAsync()
    {
        try
        {
            var accounts = await _accountStore.GetAccountsAsync();
            Accounts.Clear();

            foreach (var acc in accounts)
            {
                bool isMounted = await _virtualDriveService.IsMountedAsync(acc.AccountId ?? string.Empty);
                Accounts.Add(new AccountItemViewModel
                {
                    AccountId = acc.AccountId ?? string.Empty,
                    Email = acc.Email ?? string.Empty,
                    Provider = acc.Provider ?? string.Empty,
                    IsMounted = isMounted
                });
            }
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Failed to load accounts.");
        }
    }

    private void AddAccount() => _windowService.ShowAddAccountDialog();

    private async Task MountDriveAsync(string accountId)
    {
        if (string.IsNullOrEmpty(accountId)) return;
        if (await _virtualDriveService.MountDriveAsync(accountId))
            await UpdateMountStateAsync(accountId);
    }

    private async Task UnmountDriveAsync(string accountId)
    {
        if (string.IsNullOrEmpty(accountId)) return;
        if (await _virtualDriveService.UnmountDriveAsync(accountId))
            await UpdateMountStateAsync(accountId);
    }

    private async Task DeleteAccountAsync(string accountId)
    {
        if (string.IsNullOrEmpty(accountId)) return;
        await _virtualDriveService.UnmountDriveAsync(accountId); // Varsa önce çıkart
        await _accountStore.DeleteAccountAsync(accountId);       // Sonra veritabanından sil
        await LoadAccountsAsync();
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
