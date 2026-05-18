using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Windows.Input;
using MultiSych.Desktop.Services;
using MultiSych.Services.Configuration;
using MultiSych.Services.Interfaces;

namespace MultiSych.Desktop.ViewModels;

public class MainWindowViewModel : ViewModelBase
{
    private readonly IAuthenticationService _authenticationService;
    private readonly IAIService _aiService;
    private readonly IEmailService _emailService;
    private readonly ICalendarService _calendarService;
    private readonly IStorageService _storageService;
    private readonly IAccountStore _accountStore;
    private readonly IErrorReporter _errorReporter;
    private readonly IVirtualDriveService _virtualDriveService;
    private readonly IWindowService _windowService;
    private readonly MultiSychConfig _config;
    private readonly ISecureStorageService _secureStorage;
    private readonly ISyncSignalService _syncSignalService;

    private object _currentPageViewModel = null!;
    private string _selectedTheme = "Modern";
    private string _selectedIconStyle = "Modern";
    private string _statusMessage = "Ready.";
    private string _selectedSection = "Dashboard";

    public ObservableCollection<string> Themes { get; } = new() { "Modern", "Retro", "Sade" };
    public ObservableCollection<string> IconStyles { get; } = new() { "Modern", "Retro", "Sade" };
    public ObservableCollection<NavigationItem> NavigationItems { get; } = new();

    public ICommand RefreshCommand { get; }
    public ICommand NavigateCommand { get; }
    public ICommand OpenCopilotChatCommand { get; }
    public ICommand OpenGeminiChatCommand { get; }
    public ICommand OpenYandexChatCommand { get; }

    public DashboardViewModel DashboardPage { get; }
    public AccountsViewModel AccountsPage { get; }
    public SyncViewModel SyncPage { get; }
    public AIOverviewViewModel AIPage { get; }
    public DocumentAnalyzerViewModel DocumentAnalyzerPage { get; }
    public ErrorReportViewModel ErrorReportPage { get; }
    public SettingsViewModel SettingsPage { get; }

    public object CurrentPageViewModel
    {
        get => _currentPageViewModel;
        set => SetProperty(ref _currentPageViewModel, value);
    }

    public MainWindowViewModel(IServiceProvider services)
    {
        _authenticationService = services.GetService(typeof(IAuthenticationService)) as IAuthenticationService ?? throw new InvalidOperationException("Authentication service is missing");
        _aiService = services.GetService(typeof(IAIService)) as IAIService ?? throw new InvalidOperationException("AI service is missing");
        _emailService = services.GetService(typeof(IEmailService)) as IEmailService ?? throw new InvalidOperationException("Email service is missing");
        _calendarService = services.GetService(typeof(ICalendarService)) as ICalendarService ?? throw new InvalidOperationException("Calendar service is missing");
        _storageService = services.GetService(typeof(IStorageService)) as IStorageService ?? throw new InvalidOperationException("Storage service is missing");
        _accountStore = services.GetService(typeof(IAccountStore)) as IAccountStore ?? throw new InvalidOperationException("Account store service is missing");
        _errorReporter = services.GetService(typeof(IErrorReporter)) as IErrorReporter ?? throw new InvalidOperationException("Error reporter service is missing");
        _virtualDriveService = services.GetService(typeof(IVirtualDriveService)) as IVirtualDriveService ?? throw new InvalidOperationException("Virtual drive service is missing");
        _windowService = services.GetService(typeof(IWindowService)) as IWindowService ?? throw new InvalidOperationException("Window service is missing");
        _config = services.GetService(typeof(MultiSychConfig)) as MultiSychConfig ?? throw new InvalidOperationException("App config is missing");
        _secureStorage = services.GetService(typeof(ISecureStorageService)) as ISecureStorageService ?? throw new InvalidOperationException("Secure storage service is missing");
        _syncSignalService = services.GetService(typeof(ISyncSignalService)) as ISyncSignalService ?? throw new InvalidOperationException("Sync signal service is missing");

        DashboardPage = new DashboardViewModel();
        AccountsPage = new AccountsViewModel(_accountStore, _virtualDriveService, _windowService, _authenticationService, _config);
        SyncPage = new SyncViewModel(_emailService, _calendarService, _accountStore, _aiService, _windowService, _syncSignalService);
        AIPage = new AIOverviewViewModel(_windowService, _config);
        DocumentAnalyzerPage = new DocumentAnalyzerViewModel(_aiService, _windowService);
        ErrorReportPage = new ErrorReportViewModel(_config);
        SettingsPage = new SettingsViewModel(_config, _secureStorage);

        RefreshCommand = new RelayCommand(async _ => await RefreshCurrentPageAsync());
        NavigateCommand = new RelayCommand(section => Navigate(section?.ToString() ?? string.Empty));
        OpenCopilotChatCommand = new RelayCommand(_ => _windowService.ShowAIChat("copilot"));
        OpenGeminiChatCommand = new RelayCommand(_ => _windowService.ShowAIChat("gemini"));
        OpenYandexChatCommand = new RelayCommand(_ => _windowService.ShowAIChat("yandex"));

        UpdateNavigationItems();
        CurrentPageViewModel = DashboardPage;
    }

    public string SelectedTheme
    {
        get => _selectedTheme;
        set
        {
            if (SetProperty(ref _selectedTheme, value))
                App.ApplyTheme(value);
        }
    }

    public string SelectedIconStyle
    {
        get => _selectedIconStyle;
        set
        {
            if (SetProperty(ref _selectedIconStyle, value))
                UpdateNavigationItems();
        }
    }

    public string StatusMessage
    {
        get => _statusMessage;
        set => SetProperty(ref _statusMessage, value);
    }

    public string SelectedSection
    {
        get => _selectedSection;
        set
        {
            if (SetProperty(ref _selectedSection, value))
            {
                StatusMessage = $"Selected {value}.";
                UpdateCurrentPage();
            }
        }
    }

    private void Navigate(string section)
    {
        if (string.IsNullOrWhiteSpace(section))
            return;

        SelectedSection = section;
    }

    private void UpdateCurrentPage()
    {
        CurrentPageViewModel = SelectedSection switch
        {
            "Dashboard" => DashboardPage,
            "Accounts" => AccountsPage,
            "Sync" => SyncPage,
            "AI" => AIPage,
            "Analyzer" => DocumentAnalyzerPage,
            "Logs" => ErrorReportPage,
            "Settings" => SettingsPage,
            _ => DashboardPage
        };
    }

    private void UpdateNavigationItems()
    {
        NavigationItems.Clear();
        var items = new[]
        {
            new NavigationItem("Dashboard", GetIconGlyph("Dashboard")),
            new NavigationItem("Accounts", GetIconGlyph("Accounts")),
            new NavigationItem("Sync", GetIconGlyph("Sync")),
            new NavigationItem("AI", GetIconGlyph("AI")),
            new NavigationItem("Analyzer", GetIconGlyph("Analyzer")),
            new NavigationItem("Logs", GetIconGlyph("Logs")),
            new NavigationItem("Settings", GetIconGlyph("Settings"))
        };

        foreach (var item in items)
        {
            NavigationItems.Add(item);
        }

        UpdateSelectedNavigation();
    }

    private void UpdateSelectedNavigation()
    {
        foreach (var item in NavigationItems)
            item.IsSelected = string.Equals(item.Section, SelectedSection, StringComparison.OrdinalIgnoreCase);
    }

    private string GetIconGlyph(string section)
    {
        return _selectedIconStyle switch
        {
            "Retro" => section switch
            {
                "Dashboard" => "⌂",
                "Accounts" => "☺",
                "Sync" => "↻",
                "AI" => "⚡",
                "Analyzer" => "▤",
                "Logs" => "⚠",
                "Settings" => "⚙",
                _ => "★"
            },
            "Sade" => section switch
            {
                "Dashboard" => "▣",
                "Accounts" => "⊡",
                "Sync" => "⟳",
                "AI" => "★",
                "Analyzer" => "▤",
                "Logs" => "▤",
                "Settings" => "⛭",
                _ => "•"
            },
            _ => section switch
            {
                "Dashboard" => "🏠",
                "Accounts" => "👤",
                "Sync" => "🔄",
                "AI" => "🤖",
                "Analyzer" => "📄",
                "Logs" => "🐛",
                "Settings" => "⚙️",
                _ => "·"
            }
        };
    }

    private async Task RefreshCurrentPageAsync()
    {
        if (CurrentPageViewModel is Avalonia.Controls.Control control && control.DataContext is AccountsViewModel accountsPage)
        {
            accountsPage.RefreshCommand.Execute(null);
            StatusMessage = "Refreshing accounts page...";
            return;
        }

        StatusMessage = "Refresh is supported on the Accounts page only.";
        await Task.CompletedTask;
    }
}

public sealed class NavigationItem : ViewModelBase
{
    private bool _isSelected;

    public NavigationItem(string label, string iconGlyph)
    {
        Label = label;
        IconGlyph = iconGlyph;
        Section = label;
    }

    public string Label { get; }
    public string IconGlyph { get; }
    public string Section { get; }

    public bool IsSelected
    {
        get => _isSelected;
        set => SetProperty(ref _isSelected, value);
    }
}

public sealed class RelayCommand : ICommand
{
    private readonly Action<object?> _execute;
    private readonly Func<object?, bool>? _canExecute;

    public RelayCommand(Action<object?> execute, Func<object?, bool>? canExecute = null)
    {
        _execute = execute ?? throw new ArgumentNullException(nameof(execute));
        _canExecute = canExecute;
    }

    public bool CanExecute(object? parameter) => _canExecute?.Invoke(parameter) ?? true;

    public void Execute(object? parameter) => _execute(parameter);

    public event EventHandler? CanExecuteChanged;

    public void RaiseCanExecuteChanged() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
}
