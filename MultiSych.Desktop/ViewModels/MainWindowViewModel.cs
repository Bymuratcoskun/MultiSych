using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Windows.Input;
using Avalonia.Threading;
using ReactiveUI;
using IWindowService = MultiSych.Desktop.Services.IWindowService;
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
    private readonly IUserSettingsService _userSettingsService;

    private object _currentPageViewModel = null!;
    private string _selectedTheme;
    private string _selectedIconStyle = "Modern";
    private string _statusMessage = "Ready.";
    private string _selectedSection = "Dashboard";

    public ObservableCollection<string> Themes { get; } = ["Modern", "Retro", "Sade"];
    public ObservableCollection<string> IconStyles { get; } = ["Modern", "Retro", "Sade"];
    public ObservableCollection<NavigationItem> NavigationItems { get; } = [];

    public ICommand RefreshCommand { get; }
    public ICommand NavigateCommand { get; }
    public ICommand OpenCopilotChatCommand { get; }
    public ICommand OpenGeminiChatCommand { get; }
    public ICommand OpenYandexChatCommand { get; }
    public ICommand ToggleThemeCommand { get; }

    public DashboardViewModel DashboardPage { get; }
    public AccountsViewModel AccountsPage { get; }
    public SyncViewModel SyncPage { get; }
    public AIOverviewViewModel AIPage { get; }
    public DocumentAnalyzerViewModel DocumentAnalyzerPage { get; }
    public FileExplorerViewModel FileExplorerPage { get; }
    public ErrorReportViewModel ErrorReportPage { get; }
    public SettingsViewModel SettingsPage { get; }
    public ChatViewModel ChatPage { get; }

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
        _userSettingsService = services.GetService(typeof(IUserSettingsService)) as IUserSettingsService ?? throw new InvalidOperationException("UserSettings service is missing");

        DashboardPage = services.GetService(typeof(DashboardViewModel)) as DashboardViewModel ?? throw new InvalidOperationException("DashboardViewModel is missing in DI.");
        AccountsPage = services.GetService(typeof(AccountsViewModel)) as AccountsViewModel ?? throw new InvalidOperationException("AccountsViewModel is missing in DI.");
        SyncPage = services.GetService(typeof(SyncViewModel)) as SyncViewModel ?? throw new InvalidOperationException("SyncViewModel is missing in DI.");
        AIPage = services.GetService(typeof(AIOverviewViewModel)) as AIOverviewViewModel ?? throw new InvalidOperationException("AIOverviewViewModel is missing in DI.");
        DocumentAnalyzerPage = services.GetService(typeof(DocumentAnalyzerViewModel)) as DocumentAnalyzerViewModel ?? throw new InvalidOperationException("DocumentAnalyzerViewModel is missing in DI.");
        FileExplorerPage = services.GetService(typeof(FileExplorerViewModel)) as FileExplorerViewModel ?? throw new InvalidOperationException("FileExplorerViewModel is missing in DI.");
        ErrorReportPage = services.GetService(typeof(ErrorReportViewModel)) as ErrorReportViewModel ?? throw new InvalidOperationException("ErrorReportViewModel is missing in DI.");
        SettingsPage = services.GetService(typeof(SettingsViewModel)) as SettingsViewModel ?? throw new InvalidOperationException("SettingsViewModel is missing in DI.");
        ChatPage = services.GetService(typeof(ChatViewModel)) as ChatViewModel ?? throw new InvalidOperationException("ChatViewModel is missing in DI.");

        RefreshCommand = new RelayCommand(async _ => await RefreshCurrentPageAsync());
        NavigateCommand = new RelayCommand(section => Navigate(section?.ToString() ?? string.Empty));
        OpenCopilotChatCommand = new RelayCommand(_ => _windowService.ShowAIChat("copilot"));
        OpenGeminiChatCommand = new RelayCommand(_ => _windowService.ShowAIChat("gemini"));
        OpenYandexChatCommand = new RelayCommand(_ => _windowService.ShowAIChat("yandex"));
        ToggleThemeCommand = new RelayCommand(_ => SelectedTheme = SelectedTheme == "Sade" ? "Modern" : "Sade");

        _selectedTheme = _userSettingsService.Settings.Theme;
        MultiSych.Desktop.App.ApplyTheme(_selectedTheme);

        UpdateNavigationItems();
        CurrentPageViewModel = DashboardPage;

        // Arka plandan veya sesli asistandan gelen komutları dinleyerek sekmeyi ve bildirimleri güncelle
        MessageBus.Current.Listen<string>("NavigationIntent")
            .Subscribe(section => 
            {
                Dispatcher.UIThread.Post(() => Navigate(section));
            });
            
        MessageBus.Current.Listen<string>("NotificationIntent")
            .Subscribe(message => 
            {
                Dispatcher.UIThread.Post(() => 
                {
                    _windowService.ShowNotification("Sesli Komut", message, MultiSych.Desktop.Services.NotificationSound.Success);
                });
            });
    }

    public string SelectedTheme
    {
        get => _selectedTheme;
        set
        {
            if (SetProperty(ref _selectedTheme, value))
                MultiSych.Desktop.App.ApplyTheme(value);
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
            "Explorer" => FileExplorerPage,
            "Logs" => ErrorReportPage,
            "Settings" => SettingsPage,
            "Chat" => ChatPage,
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
            new NavigationItem("Explorer", GetIconGlyph("Explorer")),
            new NavigationItem("Logs", GetIconGlyph("Logs")),
            new NavigationItem("Settings", GetIconGlyph("Settings")),
            new NavigationItem("Chat", GetIconGlyph("Chat"))
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
                "Explorer" => "◫",
                "Logs" => "⚠",
                "Settings" => "⚙",
                "Chat" => "✉",
                _ => "★"
            },
            "Sade" => section switch
            {
                "Dashboard" => "▣",
                "Accounts" => "⊡",
                "Sync" => "⟳",
                "AI" => "★",
                "Analyzer" => "▤",
                "Explorer" => "▤",
                "Logs" => "▤",
                "Settings" => "⛭",
                "Chat" => "✉",
                _ => "•"
            },
            _ => section switch
            {
                "Dashboard" => "🏠",
                "Accounts" => "👤",
                "Sync" => "🔄",
                "AI" => "🤖",
                "Analyzer" => "📄",
                "Explorer" => "📁",
                "Logs" => "🐛",
                "Settings" => "⚙️",
                "Chat" => "💬",
                _ => "·"
            }
        };
    }

    private async Task RefreshCurrentPageAsync()
    {
        if (CurrentPageViewModel is AccountsViewModel accountsPage)
        {
            accountsPage.LoadAccountsCommand.Execute(null);
            StatusMessage = "Refreshing accounts page...";
            return;
        }

        StatusMessage = "Refresh is supported on the Accounts page only.";
        await Task.CompletedTask;
    }
}

public sealed class NavigationItem(string label, string iconGlyph) : ViewModelBase
{
    private bool _isSelected;

    public string Label { get; } = label;
    public string IconGlyph { get; } = iconGlyph;
    public string Section { get; } = label;

    public bool IsSelected
    {
        get => _isSelected;
        set => SetProperty(ref _isSelected, value);
    }
}

public sealed class RelayCommand(Action<object?> execute, Func<object?, bool>? canExecute = null) : ICommand
{
    private readonly Action<object?> _execute = execute ?? throw new ArgumentNullException(nameof(execute));
    private readonly Func<object?, bool>? _canExecute = canExecute;

    public bool CanExecute(object? parameter) => _canExecute?.Invoke(parameter) ?? true;

    public void Execute(object? parameter) => _execute(parameter);

    public event EventHandler? CanExecuteChanged;

    public void RaiseCanExecuteChanged() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
}

public sealed class RelayCommand<T>(Action<T?> execute, Func<T?, bool>? canExecute = null) : ICommand
{
    private readonly Action<T?> _execute = execute ?? throw new ArgumentNullException(nameof(execute));
    private readonly Func<T?, bool>? _canExecute = canExecute;

    public bool CanExecute(object? parameter)
    {
        return _canExecute?.Invoke(parameter is T typed ? typed : default) ?? true;
    }

    public void Execute(object? parameter)
    {
        _execute(parameter is T typed ? typed : default);
    }

    public event EventHandler? CanExecuteChanged;

    public void RaiseCanExecuteChanged() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
}
