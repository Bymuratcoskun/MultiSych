using System;
using System.Threading.Tasks;
using System.Windows.Input;
using Avalonia.Threading;
using MultiSych.Desktop.Services;
using MultiSych.Services.Configuration;
using MultiSych.Services.Interfaces;

namespace MultiSych.Desktop.ViewModels;

public class AddAccountViewModel : ViewModelBase
{
    private readonly IAuthenticationService _authService;
    private readonly IAccountStore _accountStore;
    private readonly IWindowService _windowService;
    private readonly MultiSychConfig _config;

    public AddAccountViewModel(IAuthenticationService authService, IAccountStore accountStore, IWindowService windowService, MultiSychConfig config)
    {
        _authService = authService;
        _accountStore = accountStore;
        _windowService = windowService;
        _config = config;

        AddGoogleCommand = new RelayCommand(async _ => await AuthenticateAsync("Google"));
        AddMicrosoftCommand = new RelayCommand(async _ => await AuthenticateAsync("Microsoft"));
        AddYandexCommand = new RelayCommand(async _ => await AuthenticateAsync("Yandex"));
    }

    public ICommand AddGoogleCommand { get; }
    public ICommand AddMicrosoftCommand { get; }
    public ICommand AddYandexCommand { get; }

    private async Task AuthenticateAsync(string provider)
    {
        try
        {
            Services.Models.AccountCredentials? credentials = null;

            if (provider == "Google")
                credentials = await _authService.AuthenticateGoogleAsync(_config.Google?.ClientId ?? "", _config.Google?.ClientSecret ?? "", _config.Google?.RedirectUrl ?? "http://localhost:5000/");
            else if (provider == "Microsoft")
                credentials = await _authService.AuthenticateMicrosoftAsync(_config.Microsoft?.ClientId ?? "", _config.Microsoft?.ClientSecret ?? "", _config.Microsoft?.RedirectUrl ?? "http://localhost:5000/", _config.Microsoft?.TenantId);
            else if (provider == "Yandex")
                credentials = await _authService.AuthenticateYandexAsync(_config.Yandex?.ClientId ?? "", _config.Yandex?.ClientSecret ?? "", _config.Yandex?.RedirectUrl ?? "http://localhost:5000/");

            if (credentials != null)
            {
                await _accountStore.SaveAccountAsync(credentials);

                Dispatcher.UIThread.Post(() => _windowService.ShowNotification("Başarılı", $"{provider} hesabı başarıyla bağlandı.", NotificationSound.Success));
            }
        }
        catch (Exception ex)
        {
            Dispatcher.UIThread.Post(() => _windowService.ShowNotification("Hata", $"{provider} bağlantısı başarısız: {ex.Message}", NotificationSound.Error));
        }
    }
}
