using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Windows.Input;
using Microsoft.Extensions.DependencyInjection;
using MultiSych.Services.Interfaces;
using MultiSych.Services.Security;

namespace MultiSych.Desktop.ViewModels;

public class SetupViewModel : ViewModelBase
{
    private readonly Action<bool> _closeAction;
    private string _password = string.Empty;
    private string _confirmPassword = string.Empty;
    private string _errorMessage = string.Empty;
    private string _selectedLanguage = "Türkçe";
    private string _selectedTheme = "Modern";

    public SetupViewModel(Action<bool> closeAction)
    {
        _closeAction = closeAction;
        AvailableLanguages = new ObservableCollection<string> { "English", "Türkçe" };
        AvailableThemes = new ObservableCollection<string> { "Modern", "Retro", "Sade" };
        SaveCommand = new RelayCommand(SavePassword, CanSave);
    }

    public ObservableCollection<string> AvailableLanguages { get; }
    public ObservableCollection<string> AvailableThemes { get; }

    public string SelectedLanguage { get => _selectedLanguage; set => SetProperty(ref _selectedLanguage, value); }
    public string SelectedTheme { get => _selectedTheme; set => SetProperty(ref _selectedTheme, value); }

    public string Password
    {
        get => _password;
        set { if (SetProperty(ref _password, value)) (SaveCommand as RelayCommand)?.RaiseCanExecuteChanged(); }
    }

    public string ConfirmPassword
    {
        get => _confirmPassword;
        set { if (SetProperty(ref _confirmPassword, value)) (SaveCommand as RelayCommand)?.RaiseCanExecuteChanged(); }
    }

    public string ErrorMessage
    {
        get => _errorMessage;
        private set => SetProperty(ref _errorMessage, value);
    }

    public ICommand SaveCommand { get; }

    private bool CanSave(object? _) => !string.IsNullOrWhiteSpace(Password) && !string.IsNullOrWhiteSpace(ConfirmPassword);

    private void SavePassword(object? _)
    {
        if (Password.Length < 8) { ErrorMessage = "Şifre en az 8 karakter olmalıdır."; return; }
        if (Password != ConfirmPassword) { ErrorMessage = "Girilen şifreler eşleşmiyor."; return; }

        try
        {
            SecurityHelper.SaveEnvironmentVariable("MULTISYCH_STORAGE_PASSWORD", Password);

            var envPath = Path.Combine(Directory.GetCurrentDirectory(), ".env");
            var lines = new List<string>();
            if (File.Exists(envPath))
            {
                lines = new List<string>(File.ReadAllLines(envPath));
            }
            
            void UpdateOrAddEnv(string key, string value)
            {
                var index = lines.FindIndex(l => l.StartsWith(key + "="));
                if (index >= 0) lines[index] = $"{key}={value}";
                else lines.Add($"{key}={value}");
            }

            File.WriteAllLines(envPath, lines);
            
            var userSettings = Program.ServiceProvider.GetRequiredService<IUserSettingsService>();
            userSettings.Settings.Language = SelectedLanguage;
            userSettings.Settings.Theme = SelectedTheme;
            userSettings.SaveAsync().GetAwaiter().GetResult();

            ErrorMessage = string.Empty;
            _closeAction(true); // Başarı sinyali gönder ve pencereyi kapat
        }
        catch (Exception ex) { ErrorMessage = $"Şifre kaydedilirken hata oluştu: {ex.Message}"; }
    }
}