using System.Collections.ObjectModel;
using System.Windows.Input;
using MultiSych.Services.Configuration;
using MultiSych.Services.Security;
using MultiSych.Services.Interfaces;
using System.Threading.Tasks;

namespace MultiSych.Desktop.ViewModels;

public sealed class SettingsViewModel : ViewModelBase
{
    private readonly MultiSychConfig _config;
    private readonly ISecureStorageService _secureStorage;
    private string _statusMessage = "Customize theme, icons and AI provider settings here.";
    private string _selectedTheme = "Modern";
    private string _selectedIconStyle = "Modern";
    private bool _isDarkMode = true;
    private string _copilotApiKey = string.Empty;
    private string _geminiApiKey = string.Empty;
    private string _yandexApiKey = string.Empty;

    public SettingsViewModel(MultiSychConfig config, ISecureStorageService secureStorage)
    {
        _config = config;
        _secureStorage = secureStorage;
        Themes = new ObservableCollection<string> { "Modern", "Retro", "Sade" };
        IconStyles = new ObservableCollection<string> { "Modern", "Retro", "Sade" };
        SaveThemeCommand = new RelayCommand(_ => SaveTheme());
        SaveApiKeysCommand = new RelayCommand(async _ => await SaveApiKeysAsync());

        CopilotApiKey = _config.AI?.CopilotApiKey ?? string.Empty;
        GeminiApiKey = _config.AI?.GeminiApiKey ?? string.Empty;
        YandexApiKey = _config.AI?.YandexAiApiKey ?? string.Empty;
    }

    public ObservableCollection<string> Themes { get; }
    public ObservableCollection<string> IconStyles { get; }
    public ICommand SaveThemeCommand { get; }
    public ICommand SaveApiKeysCommand { get; }

    public bool IsDarkMode
    {
        get => _isDarkMode;
        set
        {
            if (SetProperty(ref _isDarkMode, value))
            {
                SelectedTheme = value ? "Modern" : "Sade";
            }
        }
    }

    public string SelectedTheme
    {
        get => _selectedTheme;
        set
        {
            if (SetProperty(ref _selectedTheme, value))
            {
                MultiSych.Desktop.App.ApplyTheme(value);
                _isDarkMode = value != "Sade";
                OnPropertyChanged(nameof(IsDarkMode));
            }
        }
    }

    public string SelectedIconStyle
    {
        get => _selectedIconStyle;
        set => SetProperty(ref _selectedIconStyle, value);
    }

    public string CopilotApiKey
    {
        get => _copilotApiKey;
        set => SetProperty(ref _copilotApiKey, value);
    }

    public string GeminiApiKey
    {
        get => _geminiApiKey;
        set => SetProperty(ref _geminiApiKey, value);
    }

    public string YandexApiKey
    {
        get => _yandexApiKey;
        set => SetProperty(ref _yandexApiKey, value);
    }

    public string StatusMessage
    {
        get => _statusMessage;
        set => SetProperty(ref _statusMessage, value);
    }

    private void SaveTheme()
    {
        SettingsStatus = $"Selected theme '{SelectedTheme}' and icon style '{SelectedIconStyle}'.";
    }

    private async Task SaveApiKeysAsync()
    {
        if (_config.AI is not null)
        {
            _config.AI.CopilotApiKey = CopilotApiKey;
            _config.AI.GeminiApiKey = GeminiApiKey;
            _config.AI.YandexAiApiKey = YandexApiKey;
            await _secureStorage.SaveSecretAsync("COPILOT_API_KEY", CopilotApiKey);
            await _secureStorage.SaveSecretAsync("GEMINI_API_KEY", GeminiApiKey);
            await _secureStorage.SaveSecretAsync("YANDEX_AI_API_KEY", YandexApiKey);
            SettingsStatus = "AI keys updated in application config and .env.";
            return;
        }

        SettingsStatus = "Unable to save AI keys. Configuration is missing.";
    }

    private string _settingsStatus = string.Empty;
    public string SettingsStatus
    {
        get => _settingsStatus;
        set => SetProperty(ref _settingsStatus, value);
    }
}
