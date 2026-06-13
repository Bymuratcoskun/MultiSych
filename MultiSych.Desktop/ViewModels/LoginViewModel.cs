using System;
using System.Windows.Input;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using MultiSych.Desktop.Views;

namespace MultiSych.Desktop.ViewModels;

public class LoginViewModel : ViewModelBase
{
    private string _password = string.Empty;
    private string _errorMessage = string.Empty;
    private readonly IServiceProvider _serviceProvider;

    public LoginViewModel(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
        LoginCommand = new RelayCommand(_ => Login());
    }

    public string Password
    {
        get => _password;
        set => SetProperty(ref _password, value);
    }

    public string ErrorMessage
    {
        get => _errorMessage;
        set => SetProperty(ref _errorMessage, value);
    }

    public ICommand LoginCommand { get; }

    private void Login()
    {
        var storedPassword = Environment.GetEnvironmentVariable("MULTISYCH_STARTUP_PASSWORD") ?? string.Empty;
        
        if (Password == storedPassword || string.IsNullOrEmpty(storedPassword))
        {
            if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                var mainWindow = new MainWindow { DataContext = new MainWindowViewModel(_serviceProvider) };
                var oldWindow = desktop.MainWindow;
                desktop.MainWindow = mainWindow;
                mainWindow.Show();
                oldWindow?.Close();
            }
        }
        else
        {
            ErrorMessage = "Hatalı şifre. Lütfen tekrar deneyin.";
            Password = string.Empty;
        }
    }
}
