using System;
using System.Diagnostics;
using System.IO;
using Gtk;
using Adw;
using Gio;
using Microsoft.Extensions.DependencyInjection;
using MultiSych.Desktop.ViewModels;
using MultiSych.Services.Interfaces;

namespace MultiSych.Desktop.Views;

public class MainWindow : Adw.ApplicationWindow
{
    public static MainWindow? Instance { get; private set; }

    private MainWindowViewModel? _viewModel;
    private Gtk.Label? _ramLabel;
    private Gtk.Stack? _contentStack;
    private Gtk.ListBox? _navigationList;
    private System.Threading.Timer? _ramTimer;
    private bool _reallyExit = false;

    // View caching
    private DashboardView? _dashboardView;
    private SyncView? _syncView;
    private SettingsView? _settingsView;
    private AccountsView? _accountsView;
    private EmailView? _emailView;
    private ChatView? _chatView;
    private FileExplorerView? _fileExplorerView;
    private AIOverviewView? _aiOverviewView;
    private DocumentAnalyzerView? _documentAnalyzerView;
    private DocumentsView? _documentsView;
    private ErrorReportView? _errorReportView;

    public MainWindowViewModel? DataContext
    {
        get => _viewModel;
        set
        {
            if (ReferenceEquals(_viewModel, value)) return;
            _viewModel = value;
            if (_viewModel != null)
            {
                InitializeBindings();
            }
        }
    }

    public MainWindow() : base()
    {
        Instance = this;

        SetTitle("MultiSych - Cloud AI Platform");
        SetDefaultSize(1200, 750);

        BuildUi();

        // Close request handling (close to tray / background)
        OnCloseRequest += (sender, args) =>
        {
            if (!_reallyExit)
            {
                ShowExitConfirmationAsync();
                return true; // Cancel close request immediately
            }
            return false; // Allow close
        };

        // RAM update timer (runs every 2 seconds)
        _ramTimer = new System.Threading.Timer(UpdateRamUsage, null, TimeSpan.Zero, TimeSpan.FromSeconds(2));

        // Cleanup on destroy
        OnDestroy += (s, e) => { _ramTimer?.Dispose(); };
    }

    private void BuildUi()
    {
        // Main split box
        var mainBox = Gtk.Box.New(Gtk.Orientation.Horizontal, 0);

        // --- Sidebar ---
        var sidebarBox = Gtk.Box.New(Gtk.Orientation.Vertical, 10);
        sidebarBox.SetSizeRequest(260, -1);
        sidebarBox.AddCssClass("background"); // GTK4 styling class

        // Logo and Title
        var logoBox = Gtk.Box.New(Gtk.Orientation.Horizontal, 12);
        logoBox.SetMarginStart(20);
        logoBox.SetMarginEnd(20);
        logoBox.SetMarginTop(30);
        logoBox.SetMarginBottom(30);

        var logoLabel = Gtk.Label.New("☁️");
        logoLabel.SetFontSize(32);
        var titleLabel = Gtk.Label.New("MultiSych");
        titleLabel.SetFontSize(24);
        titleLabel.SetFontWeight(Pango.Weight.Bold);

        logoBox.Append(logoLabel);
        logoBox.Append(titleLabel);
        sidebarBox.Append(logoBox);

        // Navigation list box
        _navigationList = Gtk.ListBox.New();
        _navigationList.SetMarginStart(10);
        _navigationList.SetMarginEnd(10);
        _navigationList.OnRowSelected += NavigationList_OnRowSelected;

        // Navigasyon öğeleri (id'ler MainWindowViewModel.UpdateCurrentPage switch'iyle eşleşmeli)
        AddNavigationRow("Dashboard", "📊 Panel");
        AddNavigationRow("Accounts", "🔗 Hesaplar");
        AddNavigationRow("Mail", "✉️ E-posta");
        AddNavigationRow("Chat", "💬 Sohbet");
        AddNavigationRow("Explorer", "📁 Dosyalar");
        AddNavigationRow("Documents", "📄 Belgeler");
        AddNavigationRow("Analyzer", "🔍 Belge Analizi");
        AddNavigationRow("AI", "🧠 AI Genel Bakış");
        AddNavigationRow("Sync", "🔄 Senkronizasyon");
        AddNavigationRow("Logs", "📋 Loglar");
        AddNavigationRow("Settings", "⚙️ Ayarlar");

        var scroll = Gtk.ScrolledWindow.New();
        scroll.SetVexpand(true);
        scroll.SetChild(_navigationList);
        sidebarBox.Append(scroll);

        // AI Shortcuts
        var aiShortcutsBox = Gtk.Box.New(Gtk.Orientation.Vertical, 8);
        aiShortcutsBox.SetMarginStart(20);
        aiShortcutsBox.SetMarginEnd(20);
        aiShortcutsBox.SetMarginBottom(20);

        var aiTitle = Gtk.Label.New("AI ASSISTANTS");
        aiTitle.SetHalign(Gtk.Align.Start);
        aiTitle.SetFontSize(10);
        aiTitle.SetFontWeight(Pango.Weight.Bold);
        aiShortcutsBox.Append(aiTitle);

        var btnCopilot = Gtk.Button.NewWithLabel("🤖 Microsoft Copilot");
        btnCopilot.OnClicked += (s, e) => _viewModel?.OpenCopilotChatCommand.Execute(null);
        aiShortcutsBox.Append(btnCopilot);

        var btnGemini = Gtk.Button.NewWithLabel("✨ Google Gemini");
        btnGemini.OnClicked += (s, e) => _viewModel?.OpenGeminiChatCommand.Execute(null);
        aiShortcutsBox.Append(btnGemini);

        var btnYandex = Gtk.Button.NewWithLabel("🧠 Yandex AI");
        btnYandex.OnClicked += (s, e) => _viewModel?.OpenYandexChatCommand.Execute(null);
        aiShortcutsBox.Append(btnYandex);

        sidebarBox.Append(aiShortcutsBox);

        // RAM Usage / Status Info
        var statusBox = Gtk.Box.New(Gtk.Orientation.Vertical, 4);
        statusBox.SetMarginStart(20);
        statusBox.SetMarginEnd(20);
        statusBox.SetMarginBottom(20);

        var versionLabel = Gtk.Label.New("MultiSych v1.0.0-beta");
        versionLabel.SetHalign(Gtk.Align.Start);
        versionLabel.SetFontSize(10);

        var ramBox = Gtk.Box.New(Gtk.Orientation.Horizontal, 5);
        var ramTitle = Gtk.Label.New("RAM Usage:");
        ramTitle.SetFontSize(10);
        _ramLabel = Gtk.Label.New("Calculating...");
        _ramLabel.SetFontSize(10);
        _ramLabel.SetFontWeight(Pango.Weight.Bold);

        ramBox.Append(ramTitle);
        ramBox.Append(_ramLabel);

        statusBox.Append(versionLabel);
        statusBox.Append(ramBox);
        sidebarBox.Append(statusBox);

        mainBox.Append(sidebarBox);

        // Separator
        var separator = Gtk.Separator.New(Gtk.Orientation.Vertical);
        mainBox.Append(separator);

        // --- Main Content Area ---
        var rightAreaBox = Gtk.Box.New(Gtk.Orientation.Vertical, 0);
        rightAreaBox.SetHexpand(true);
        rightAreaBox.SetVexpand(true);

        // Header bar / Theme button overlay
        var headerBar = Adw.HeaderBar.New();
        var btnTheme = Gtk.Button.NewWithLabel("🌓");
        btnTheme.OnClicked += (s, e) => ToggleColorTheme();
        headerBar.PackEnd(btnTheme);
        rightAreaBox.Append(headerBar);

        _contentStack = Gtk.Stack.New();
        _contentStack.SetTransitionType(Gtk.StackTransitionType.SlideLeftRight);
        _contentStack.SetTransitionDuration(350);

        rightAreaBox.Append(_contentStack);
        mainBox.Append(rightAreaBox);

        SetContent(mainBox);
    }

    private void AddNavigationRow(string id, string labelText)
    {
        var row = Gtk.ListBoxRow.New();
        var label = Gtk.Label.New(labelText);
        label.SetHalign(Gtk.Align.Start);
        label.SetMarginStart(15);
        label.SetMarginTop(10);
        label.SetMarginBottom(10);
        row.SetChild(label);
        
        row.SetName(id);
        _navigationList?.Append(row);
    }

    private void NavigationList_OnRowSelected(Gtk.ListBox box, Gtk.ListBox.RowSelectedSignalArgs args)
    {
        var row = args.Row;
        if (row == null || _viewModel == null) return;
        var section = row.GetName();
        _viewModel.NavigateCommand.Execute(section);
    }

    private void InitializeBindings()
    {
        if (_viewModel == null) return;

        _viewModel.PropertyChanged += (sender, args) =>
        {
            if (args.PropertyName == nameof(MainWindowViewModel.CurrentPageViewModel))
            {
                GLib.Functions.IdleAdd(0, () =>
                {
                    UpdateActiveView();
                    return false;
                });
            }
        };

        // Initialize first view
        UpdateActiveView();
    }

    private void UpdateActiveView()
    {
        if (_viewModel == null || _contentStack == null) return;

        var currentVm = _viewModel.CurrentPageViewModel;

        if (currentVm is DashboardViewModel dashboardVm)
        {
            if (_dashboardView == null)
            {
                _dashboardView = new DashboardView();
                _contentStack.AddNamed(_dashboardView, "Dashboard");
            }
            _dashboardView.DataContext = dashboardVm;
            _contentStack.SetVisibleChildName("Dashboard");
        }
        else if (currentVm is SyncViewModel syncVm)
        {
            if (_syncView == null)
            {
                _syncView = new SyncView();
                _contentStack.AddNamed(_syncView, "Sync");
            }
            _syncView.DataContext = syncVm;
            _contentStack.SetVisibleChildName("Sync");
        }
        else if (currentVm is SettingsViewModel settingsVm)
        {
            if (_settingsView == null)
            {
                _settingsView = new SettingsView();
                _contentStack.AddNamed(_settingsView, "Settings");
            }
            _settingsView.DataContext = settingsVm;
            _contentStack.SetVisibleChildName("Settings");
        }
        else if (currentVm is AccountsViewModel accountsVm)
        {
            if (_accountsView == null)
            {
                _accountsView = new AccountsView();
                _contentStack.AddNamed(_accountsView, "Accounts");
            }
            _accountsView.DataContext = accountsVm;
            _contentStack.SetVisibleChildName("Accounts");
        }
        else if (currentVm is EmailViewModel emailVm)
        {
            if (_emailView == null)
            {
                _emailView = new EmailView();
                _contentStack.AddNamed(_emailView, "Mail");
            }
            _emailView.DataContext = emailVm;
            _contentStack.SetVisibleChildName("Mail");
        }
        else if (currentVm is ChatViewModel chatVm)
        {
            if (_chatView == null)
            {
                _chatView = new ChatView();
                _contentStack.AddNamed(_chatView, "Chat");
            }
            _chatView.DataContext = chatVm;
            _contentStack.SetVisibleChildName("Chat");
        }
        else if (currentVm is FileExplorerViewModel explorerVm)
        {
            if (_fileExplorerView == null)
            {
                _fileExplorerView = new FileExplorerView();
                _contentStack.AddNamed(_fileExplorerView, "Explorer");
            }
            _fileExplorerView.DataContext = explorerVm;
            _contentStack.SetVisibleChildName("Explorer");
        }
        else if (currentVm is AIOverviewViewModel aiOverviewVm)
        {
            if (_aiOverviewView == null)
            {
                _aiOverviewView = new AIOverviewView();
                _contentStack.AddNamed(_aiOverviewView, "AI");
            }
            _aiOverviewView.DataContext = aiOverviewVm;
            _contentStack.SetVisibleChildName("AI");
        }
        else if (currentVm is DocumentAnalyzerViewModel documentAnalyzerVm)
        {
            if (_documentAnalyzerView == null)
            {
                _documentAnalyzerView = new DocumentAnalyzerView();
                _contentStack.AddNamed(_documentAnalyzerView, "Analyzer");
            }
            _documentAnalyzerView.DataContext = documentAnalyzerVm;
            _contentStack.SetVisibleChildName("Analyzer");
        }
        else if (currentVm is DocumentsViewModel documentsVm)
        {
            if (_documentsView == null)
            {
                _documentsView = new DocumentsView();
                _contentStack.AddNamed(_documentsView, "Documents");
            }
            _documentsView.DataContext = documentsVm;
            _contentStack.SetVisibleChildName("Documents");
        }
        else if (currentVm is ErrorReportViewModel errorReportVm)
        {
            if (_errorReportView == null)
            {
                _errorReportView = new ErrorReportView();
                _contentStack.AddNamed(_errorReportView, "Logs");
            }
            _errorReportView.DataContext = errorReportVm;
            _contentStack.SetVisibleChildName("Logs");
        }
        else
        {
            // Henüz özel GTK view'i olmayan bölümler için geçici placeholder.
            ShowPlaceholder(currentVm?.GetType().Name ?? "Bilinmeyen");
        }
    }

    private readonly System.Collections.Generic.HashSet<string> _placeholderNames = new();

    private void ShowPlaceholder(string vmName)
    {
        if (_contentStack == null) return;
        var stackName = "ph_" + vmName;
        if (_placeholderNames.Add(stackName))
        {
            var box = Gtk.Box.New(Gtk.Orientation.Vertical, 10);
            box.SetValign(Gtk.Align.Center);
            box.SetHalign(Gtk.Align.Center);
            var label = Gtk.Label.New($"🚧 Bu ekran yakında ({vmName})");
            label.SetFontSize(16);
            box.Append(label);
            _contentStack.AddNamed(box, stackName);
        }
        _contentStack.SetVisibleChildName(stackName);
    }

    private void ToggleColorTheme()
    {
        var manager = Adw.StyleManager.GetDefault();
        if (manager.GetDark())
        {
            manager.SetColorScheme(Adw.ColorScheme.ForceLight);
        }
        else
        {
            manager.SetColorScheme(Adw.ColorScheme.ForceDark);
        }
    }

    private void UpdateRamUsage(object? state)
    {
        var ramMB = Environment.WorkingSet / (1024 * 1024);
        GLib.Functions.IdleAdd(0, () =>
        {
            if (_ramLabel != null)
            {
                _ramLabel.SetText($"{ramMB} MB");
            }
            return false;
        });
    }

    private async void ShowExitConfirmationAsync()
    {
        var dialog = new Gtk.AlertDialog
        {
            Message = "Çıkış Onayı",
            Detail = "MultiSych kapatılacak ve arka plan senkronizasyonu duracak. Emin misiniz?",
            Buttons = new string[] { "Evet", "Hayır" },
            DefaultButton = 0,
            CancelButton = 1
        };

        var result = await dialog.ChooseAsync(this);
        if (result == 0)
        {
            _reallyExit = true;
            this.Close();
        }
    }
}

// Widget Extensions to support quick layout bindings similar to WPF/Avalonia
public static class WidgetExtensions
{
    public static void SetFontSize(this Gtk.Label label, int size)
    {
        label.SetMarkup($"<span size=\"{size * 1000}\">{GLib.Markup.EscapeText(label.GetText())}</span>");
    }

    public static void SetFontWeight(this Gtk.Label label, Pango.Weight weight)
    {
        // Simple helper
    }
}
