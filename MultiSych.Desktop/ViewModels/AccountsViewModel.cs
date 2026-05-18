using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using System.Windows.Input;
using Avalonia.Controls.Notifications;
using MultiSych.Desktop.Services;
using MultiSych.Services.Configuration;
using MultiSych.Services.Interfaces;
using MultiSych.Services.Models;

namespace MultiSych.Desktop.ViewModels;

public sealed class AccountsViewModel : ViewModelBase
{
    private readonly IAccountStore _accountStore;
    private readonly IVirtualDriveService _virtualDriveService;
    private readonly IWindowService _windowService;
    private readonly IAuthenticationService _authService;
    private readonly MultiSychConfig _config;
    private string _statusMessage = "Loading accounts...";

    public AccountsViewModel(IAccountStore accountStore, IVirtualDriveService virtualDriveService, IWindowService windowService, IAuthenticationService authService, MultiSychConfig config)
    {
        _accountStore = accountStore;
        _virtualDriveService = virtualDriveService;
        _windowService = windowService;
        _authService = authService;
        _config = config;
        Accounts = new ObservableCollection<AccountCredentials>();
        MountedDrives = new ObservableCollection<VirtualDriveInfo>();
        RefreshCommand = new RelayCommand(async _ => await RefreshAsync());
        MountDriveCommand = new RelayCommand(async param => await MountDriveAsync(param));
        UnmountDriveCommand = new RelayCommand(async param => await UnmountDriveAsync(param));
        AddGoogleAccountCommand = new RelayCommand(async _ => await AddAccountAsync("Google"));
        AddMicrosoftAccountCommand = new RelayCommand(async _ => await AddAccountAsync("Microsoft"));
        AddYandexAccountCommand = new RelayCommand(async _ => await AddAccountAsync("Yandex"));
        _ = RefreshAsync();
    }

    public ObservableCollection<AccountCredentials> Accounts { get; }
    public ObservableCollection<VirtualDriveInfo> MountedDrives { get; }
    public ICommand RefreshCommand { get; }
    public ICommand MountDriveCommand { get; }
    public ICommand UnmountDriveCommand { get; }
    public ICommand AddGoogleAccountCommand { get; }
    public ICommand AddMicrosoftAccountCommand { get; }
    public ICommand AddYandexAccountCommand { get; }

    public string StatusMessage
    {
        get => _statusMessage;
        set => SetProperty(ref _statusMessage, value);
    }

    private async Task RefreshAsync()
    {
        var accounts = await _accountStore.GetAccountsAsync();
        Accounts.Clear();
        foreach (var account in accounts)
            Accounts.Add(account);

        MountedDrives.Clear();
        var drives = await _virtualDriveService.GetMountedDrivesAsync();
        foreach (var drive in drives)
            MountedDrives.Add(drive);

        StatusMessage = Accounts.Count > 0
            ? $"{Accounts.Count} account(s) loaded." 
            : "No connected accounts found.";
    }

    private async Task MountDriveAsync(object? parameter)
    {
        if (parameter is not AccountCredentials account)
            return;

        await _virtualDriveService.MountDriveAsync(account);
        await RefreshAsync();
        StatusMessage = $"Mounted drive for {account.Provider}.";
        _windowService.ShowNotification("Drive Mounted", $"Virtual drive mounted for {account.Provider}.", NotificationType.Success);
    }

    private async Task UnmountDriveAsync(object? parameter)
    {
        if (parameter is not AccountCredentials account)
            return;

        bool isConfirmed = await _windowService.ShowConfirmationDialogAsync($"Are you sure you want to unmount the {account.Provider} virtual drive for {account.Email}?");
        if (!isConfirmed)
            return;

        var result = await _virtualDriveService.UnmountDriveAsync(account.AccountId ?? string.Empty);
        await RefreshAsync();
        StatusMessage = result ? $"Unmounted {account.Provider} drive." : "Nothing to unmount.";
        if (result)
        {
            _windowService.ShowNotification("Drive Unmounted", $"Virtual drive disconnected for {account.Provider}.", NotificationType.Information);
        }
    }

    private async Task AddAccountAsync(string provider)
    {
        StatusMessage = $"Authenticating with {provider}... Please check your browser.";
        try
        {
            AccountCredentials? credentials = null;
            
            if (provider == "Google")
            {
                if (string.IsNullOrWhiteSpace(_config.Google?.ClientId) || string.IsNullOrWhiteSpace(_config.Google?.ClientSecret))
                    throw new Exception("Google client credentials are not configured in the environment (.env).");
                var redirect = string.IsNullOrWhiteSpace(_config.Google.RedirectUrl) ? "http://localhost:5000/" : _config.Google.RedirectUrl;
                credentials = await _authService.AuthenticateGoogleAsync(_config.Google.ClientId, _config.Google.ClientSecret, redirect);
            }
            else if (provider == "Microsoft")
            {
                if (string.IsNullOrWhiteSpace(_config.Microsoft?.ClientId) || string.IsNullOrWhiteSpace(_config.Microsoft?.ClientSecret))
                    throw new Exception("Microsoft client credentials are not configured in the environment (.env).");
                var redirect = string.IsNullOrWhiteSpace(_config.Microsoft.RedirectUrl) ? "http://localhost:5001/" : _config.Microsoft.RedirectUrl;
                credentials = await _authService.AuthenticateMicrosoftAsync(_config.Microsoft.ClientId, _config.Microsoft.ClientSecret, redirect, _config.Microsoft.TenantId);
            }
            else if (provider == "Yandex")
            {
                if (string.IsNullOrWhiteSpace(_config.Yandex?.ClientId) || string.IsNullOrWhiteSpace(_config.Yandex?.ClientSecret))
                    throw new Exception("Yandex client credentials are not configured in the environment (.env).");
                var redirect = string.IsNullOrWhiteSpace(_config.Yandex.RedirectUrl) ? "http://localhost:5002/" : _config.Yandex.RedirectUrl;
                credentials = await _authService.AuthenticateYandexAsync(_config.Yandex.ClientId, _config.Yandex.ClientSecret, redirect);
            }

            if (credentials != null)
            {
                await _accountStore.SaveAccountAsync(credentials);
                await RefreshAsync();
                StatusMessage = $"Successfully added {provider} account: {credentials.Email}";
                _windowService.ShowNotification("Account Added", $"Successfully linked {credentials.Email}", NotificationType.Success);
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"Authentication failed: {ex.Message}";
            _windowService.ShowNotification("Error", $"Could not add {provider} account: {ex.Message}", NotificationType.Error);
        }
    }
}
