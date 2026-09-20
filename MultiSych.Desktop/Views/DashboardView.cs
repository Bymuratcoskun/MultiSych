using System;
using Gtk;
using MultiSych.Desktop.Localization;
using MultiSych.Desktop.ViewModels;

namespace MultiSych.Desktop.Views;

public class DashboardView : Gtk.Box
{
    private DashboardViewModel? _viewModel;
    private Gtk.Label? _aiSummaryLabel;
    private Gtk.Label? _accountsLabel;
    private Gtk.Label? _emailsLabel;
    private Gtk.Label? _eventsLabel;
    private Gtk.Label? _filesLabel;
    private Gtk.Label? _statusLabel;
    private Gtk.ListBox? _accountsList;
    private Gtk.ListBox? _logsList;

    public DashboardViewModel? DataContext
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

    public DashboardView() : base()
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
        var title = Gtk.Label.New(Loc.Get("dashboard.title"));
        title.SetHalign(Gtk.Align.Start);
        title.SetFontSize(24);
        title.SetFontWeight(Pango.Weight.Bold);
        Append(title);

        // --- AI Summary Card ---
        var aiCard = Gtk.Frame.New(null);
        var aiBox = Gtk.Box.New(Gtk.Orientation.Vertical, 8);
        aiBox.SetMarginStart(15);
        aiBox.SetMarginEnd(15);
        aiBox.SetMarginTop(15);
        aiBox.SetMarginBottom(15);

        var aiTitle = Gtk.Label.New(Loc.Get("dashboard.header.ai_summary"));
        aiTitle.SetHalign(Gtk.Align.Start);
        aiTitle.SetFontWeight(Pango.Weight.Bold);
        
        _aiSummaryLabel = Gtk.Label.New(Loc.Get("common.loading"));
        _aiSummaryLabel.SetHalign(Gtk.Align.Start);
        _aiSummaryLabel.SetWrap(true);

        aiBox.Append(aiTitle);
        aiBox.Append(_aiSummaryLabel);
        aiCard.SetChild(aiBox);
        Append(aiCard);

        // --- Counter Grid ---
        var grid = Gtk.Grid.New();
        grid.SetColumnSpacing(20);
        grid.SetRowSpacing(20);
        grid.SetColumnHomogeneous(true);

        _accountsLabel = CreateCounterCard(grid, 0, 0, "🔗 Bağlı Hesaplar");
        _emailsLabel = CreateCounterCard(grid, 1, 0, "📧 Toplam E-Posta");
        _eventsLabel = CreateCounterCard(grid, 0, 1, "📅 Toplam Etkinlik");
        _filesLabel = CreateCounterCard(grid, 1, 1, "📁 Toplam Dosya");

        Append(grid);

        // --- System Status Box ---
        var statusFrame = Gtk.Frame.New("Sistem Durumu");
        var statusBox = Gtk.Box.New(Gtk.Orientation.Horizontal, 10);
        statusBox.SetMarginStart(15);
        statusBox.SetMarginEnd(15);
        statusBox.SetMarginTop(15);
        statusBox.SetMarginBottom(15);

        _statusLabel = Gtk.Label.New(Loc.Get("common.ready"));
        _statusLabel.SetHalign(Gtk.Align.Start);
        statusBox.Append(_statusLabel);
        statusFrame.SetChild(statusBox);
        Append(statusFrame);

        // --- Connected Accounts & Logs ---
        var bottomGrid = Gtk.Grid.New();
        bottomGrid.SetColumnSpacing(20);
        bottomGrid.SetColumnHomogeneous(true);

        // Accounts list
        var accountsFrame = Gtk.Frame.New("Bağlı Hesapların Durumu");
        _accountsList = Gtk.ListBox.New();
        accountsFrame.SetChild(_accountsList);
        bottomGrid.Attach(accountsFrame, 0, 0, 1, 1);

        // Mini logs list
        var logsFrame = Gtk.Frame.New("Son İşlemler / Mini Log");
        _logsList = Gtk.ListBox.New();
        logsFrame.SetChild(_logsList);
        bottomGrid.Attach(logsFrame, 1, 0, 1, 1);

        Append(bottomGrid);
    }

    private Gtk.Label CreateCounterCard(Gtk.Grid grid, int col, int row, string titleText)
    {
        var frame = Gtk.Frame.New(null);
        var box = Gtk.Box.New(Gtk.Orientation.Vertical, 10);
        box.SetMarginStart(15);
        box.SetMarginEnd(15);
        box.SetMarginTop(15);
        box.SetMarginBottom(15);

        var title = Gtk.Label.New(titleText);
        title.SetHalign(Gtk.Align.Start);

        var countLabel = Gtk.Label.New("0");
        countLabel.SetHalign(Gtk.Align.Start);
        countLabel.SetFontSize(28);
        countLabel.SetFontWeight(Pango.Weight.Bold);

        box.Append(title);
        box.Append(countLabel);
        frame.SetChild(box);

        grid.Attach(frame, col, row, 1, 1);
        return countLabel;
    }

    private void InitializeBindings()
    {
        if (_viewModel == null) return;

        _viewModel.PropertyChanged += (s, e) =>
        {
            GLib.Functions.IdleAdd(0, () =>
            {
                UpdateValues();
                return false;
            });
        };

        UpdateValues();
    }

    private void UpdateValues()
    {
        if (_viewModel == null) return;

        _aiSummaryLabel?.SetText(_viewModel.DailyAiSummary ?? string.Empty);
        _accountsLabel?.SetText(_viewModel.TotalAccounts.ToString());
        _emailsLabel?.SetText(_viewModel.TotalEmails.ToString());
        _eventsLabel?.SetText(_viewModel.TotalEvents.ToString());
        _filesLabel?.SetText(_viewModel.TotalFiles.ToString());
        _statusLabel?.SetText(_viewModel.StatusMessage ?? "Ready.");

        // Update accounts list
        if (_accountsList != null)
        {
            var child = _accountsList.GetFirstChild();
            while (child != null)
            {
                _accountsList.Remove(child);
                child = _accountsList.GetFirstChild();
            }

            foreach (var status in _viewModel.AccountStatuses)
            {
                var row = Gtk.ListBoxRow.New();
                var box = Gtk.Box.New(Gtk.Orientation.Horizontal, 10);
                box.SetMarginStart(10);
                box.SetMarginEnd(10);
                box.SetMarginTop(5);
                box.SetMarginBottom(5);

                var provider = Gtk.Label.New(status.Provider);
                provider.SetFontWeight(Pango.Weight.Bold);
                
                var email = Gtk.Label.New(status.Email);
                email.SetHalign(Gtk.Align.Start);
                email.SetHexpand(true);

                var state = Gtk.Label.New(status.Status);
                state.SetHalign(Gtk.Align.End);

                box.Append(provider);
                box.Append(email);
                box.Append(state);
                row.SetChild(box);
                _accountsList.Append(row);
            }
        }

        // Update logs list
        if (_logsList != null)
        {
            var child = _logsList.GetFirstChild();
            while (child != null)
            {
                _logsList.Remove(child);
                child = _logsList.GetFirstChild();
            }

            foreach (var log in _viewModel.RecentLogs)
            {
                var row = Gtk.ListBoxRow.New();
                var label = Gtk.Label.New(log);
                label.SetHalign(Gtk.Align.Start);
                label.SetMarginStart(10);
                label.SetMarginEnd(10);
                label.SetMarginTop(5);
                label.SetMarginBottom(5);
                label.SetWrap(true);
                row.SetChild(label);
                _logsList.Append(row);
            }
        }
    }
}
