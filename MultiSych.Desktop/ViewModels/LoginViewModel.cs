using System;
using System.Windows.Input;
using MultiSych.Services.Configuration;
using MultiSych.Services.Security;

namespace MultiSych.Desktop.ViewModels;

public class LoginViewModel : ViewModelBase
{
    private string _password = string.Empty;
    private string _twoFactorCode = string.Empty;
    private string _errorMessage = string.Empty;
    private readonly SecuritySettings _security;
    private readonly Action<bool> _callback;

    public LoginViewModel(SecuritySettings security, Action<bool> callback)
    {
        _security = security ?? throw new ArgumentNullException(nameof(security));
        _callback = callback ?? throw new ArgumentNullException(nameof(callback));
        LoginCommand = new RelayCommand(_ => Login());
    }

    public string Password
    {
        get => _password;
        set => SetProperty(ref _password, value);
    }

    public string TwoFactorCode
    {
        get => _twoFactorCode;
        set => SetProperty(ref _twoFactorCode, value);
    }

    public bool RequiresTwoFactor => _security.EnableTwoFactorAuth;

    public string ErrorMessage
    {
        get => _errorMessage;
        set => SetProperty(ref _errorMessage, value);
    }

    public ICommand LoginCommand { get; }

    private void Login()
    {
        if (_security.RequireStartupPassword && !SecurityHelper.ValidatePassword(_security, Password))
        {
            ErrorMessage = "Hatalı parola";
            Password = string.Empty;
            return;
        }

        if (_security.EnableTwoFactorAuth && !SecurityHelper.ValidateTwoFactorCode(_security, TwoFactorCode))
        {
            ErrorMessage = "Hatalı 2FA kodu";
            TwoFactorCode = string.Empty;
            return;
        }

        ErrorMessage = string.Empty;
        _callback(true);
    }
}
