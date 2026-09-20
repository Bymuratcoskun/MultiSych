using System;
using Gtk;
using Adw;
using MultiSych.Desktop.Localization;
using MultiSych.Desktop.ViewModels;

namespace MultiSych.Desktop.Views;

public class SettingsView : Gtk.Box
{
    private SettingsViewModel? _viewModel;
    private Gtk.DropDown? _langDropDown;
    private Gtk.DropDown? _themeDropDown;
    private Gtk.TextView? _logsTextView;

    public SettingsViewModel? DataContext
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

    public SettingsView() : base()
    {
        ((Gtk.Orientable)this).Orientation = Gtk.Orientation.Vertical;
        this.Spacing = 20;

        SetMarginStart(20);
        SetMarginEnd(20);
        SetMarginTop(20);
        SetMarginBottom(20);

        BuildUi();
    }

    private void BuildUi()
    {
        // Title
        var title = Gtk.Label.New(Loc.Get("settings.title"));
        title.SetHalign(Gtk.Align.Start);
        title.SetFontSize(24);
        title.SetFontWeight(Pango.Weight.Bold);
        Append(title);

        // Preference Groups
        var prefGroup = Adw.PreferencesGroup.New();
        prefGroup.SetTitle(Loc.Get("settings.general_title"));

        // Language Dropdown
        var rowLang = Adw.ActionRow.New();
        rowLang.SetTitle(Loc.Get("settings.language_title"));
        rowLang.SetSubtitle("Uygulama arayüz dilini değiştirin");
        
        string[] languages = ["English", "Türkçe"];
        _langDropDown = Gtk.DropDown.NewFromStrings(languages);
        rowLang.AddSuffix(_langDropDown);
        prefGroup.Add(rowLang);

        // Theme Dropdown
        var rowTheme = Adw.ActionRow.New();
        rowTheme.SetTitle(Loc.Get("settings.theme_title"));
        rowTheme.SetSubtitle("Arayüz temasını değiştirin");
        
        string[] themes = ["Modern", "Retro", "Sade"];
        _themeDropDown = Gtk.DropDown.NewFromStrings(themes);
        rowTheme.AddSuffix(_themeDropDown);
        prefGroup.Add(rowTheme);

        Append(prefGroup);

        // Actions Group
        var actGroup = Adw.PreferencesGroup.New();
        actGroup.SetTitle(Loc.Get("settings.data_maintenance_title"));

        var rowBackup = Adw.ActionRow.New();
        rowBackup.SetTitle(Loc.Get("settings.database_backup_title"));
        var btnBackup = Gtk.Button.NewWithLabel(Loc.Get("settings.backup_button"));
        btnBackup.OnClicked += (s, e) => _viewModel?.BackupDatabaseCommand.Execute(null);
        rowBackup.AddSuffix(btnBackup);
        actGroup.Add(rowBackup);

        var rowRestore = Adw.ActionRow.New();
        rowRestore.SetTitle(Loc.Get("settings.database_restore_title"));
        var btnRestore = Gtk.Button.NewWithLabel(Loc.Get("settings.restore_button"));
        btnRestore.OnClicked += (s, e) => _viewModel?.RestoreDatabaseCommand.Execute(null);
        rowRestore.AddSuffix(btnRestore);
        actGroup.Add(rowRestore);

        var rowClearCache = Adw.ActionRow.New();
        rowClearCache.SetTitle(Loc.Get("settings.cache_cleanup_title"));
        var btnClearCache = Gtk.Button.NewWithLabel(Loc.Get("settings.clear_cache_button"));
        btnClearCache.OnClicked += (s, e) => _viewModel?.ClearCacheCommand.Execute(null);
        rowClearCache.AddSuffix(btnClearCache);
        actGroup.Add(rowClearCache);

        Append(actGroup);

        // Live logs box
        var logsFrame = Gtk.Frame.New("Canlı Loglar");
        var logsBox = Gtk.Box.New(Gtk.Orientation.Vertical, 10);
        logsBox.SetMarginStart(15);
        logsBox.SetMarginEnd(15);
        logsBox.SetMarginTop(15);
        logsBox.SetMarginBottom(15);

        _logsTextView = Gtk.TextView.New();
        _logsTextView.SetEditable(false);
        _logsTextView.SetSizeRequest(-1, 150);

        var scroll = Gtk.ScrolledWindow.New();
        scroll.SetChild(_logsTextView);
        scroll.SetVexpand(true);
        logsBox.Append(scroll);

        // Log controls
        var btnBox = Gtk.Box.New(Gtk.Orientation.Horizontal, 10);
        var btnPause = Gtk.Button.NewWithLabel(Loc.Get("settings.toggle_logs_button"));
        btnPause.OnClicked += (s, e) => _viewModel?.ToggleLogPauseCommand.Execute(null);
        var btnClear = Gtk.Button.NewWithLabel(Loc.Get("settings.clear_logs_button"));
        btnClear.OnClicked += (s, e) => _viewModel?.ClearLogCommand.Execute(null);

        btnBox.Append(btnPause);
        btnBox.Append(btnClear);
        logsBox.Append(btnBox);

        logsFrame.SetChild(logsBox);
        Append(logsFrame);
    }

    private void InitializeBindings()
    {
        if (_viewModel == null) return;

        _viewModel.PropertyChanged += (s, e) =>
        {
            GLib.Functions.IdleAdd(0, () =>
            {
                UpdateLogs();
                return false;
            });
        };

        UpdateLogs();
    }

    private void UpdateLogs()
    {
        if (_viewModel == null || _logsTextView == null) return;
        _logsTextView.Buffer!.Text = _viewModel.LiveLogs;
    }
}
