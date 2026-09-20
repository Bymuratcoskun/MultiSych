using System;
using System.Linq;
using Gtk;
using Adw;
using MultiSych.Desktop.Localization;
using MultiSych.Desktop.ViewModels;

namespace MultiSych.Desktop.Views;

public class SyncView : Gtk.Box
{
    private SyncViewModel? _viewModel;
    private Gtk.DropDown? _accountDropDown;
    private Gtk.DropDown? _syncTypeDropDown;
    private Gtk.Button? _btnSync;
    private Gtk.Button? _btnAnalyze;

    public SyncViewModel? DataContext
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

    public SyncView() : base()
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
        var title = Gtk.Label.New(Loc.Get("sync.title"));
        title.SetHalign(Gtk.Align.Start);
        title.SetFontSize(24);
        title.SetFontWeight(Pango.Weight.Bold);
        Append(title);

        var prefGroup = Adw.PreferencesGroup.New();
        prefGroup.SetTitle(Loc.Get("sync.manual_options_title"));

        // Accounts dropdown
        var rowAccount = Adw.ActionRow.New();
        rowAccount.SetTitle(Loc.Get("sync.account_selection_title"));
        rowAccount.SetSubtitle("Senkronize edilecek hesabı seçin");

        // Empty default setup (will be populated dynamically)
        _accountDropDown = Gtk.DropDown.NewFromStrings(new string[] { "Yükleniyor..." });
        _accountDropDown.OnNotify += (s, e) =>
        {
            if (e.Pspec.GetName() == "selected" && _viewModel != null)
            {
                var index = (int)_accountDropDown.GetSelected();
                if (index >= 0 && index < _viewModel.Accounts.Count)
                {
                    _viewModel.SelectedAccountId = _viewModel.Accounts[index].AccountId;
                }
            }
        };
        rowAccount.AddSuffix(_accountDropDown);
        prefGroup.Add(rowAccount);

        // Sync Type dropdown
        var rowType = Adw.ActionRow.New();
        rowType.SetTitle(Loc.Get("sync.type_title"));
        rowType.SetSubtitle("Veri kategorisi filtresi");

        string[] syncTypes = ["Tümü", "E-Posta", "Takvim", "Dosyalar"];
        _syncTypeDropDown = Gtk.DropDown.NewFromStrings(syncTypes);
        _syncTypeDropDown.OnNotify += (s, e) =>
        {
            if (e.Pspec.GetName() == "selected" && _viewModel != null)
            {
                var index = (int)_syncTypeDropDown.GetSelected();
                if (index >= 0 && index < syncTypes.Length)
                {
                    _viewModel.SelectedSyncType = syncTypes[index];
                }
            }
        };
        rowType.AddSuffix(_syncTypeDropDown);
        prefGroup.Add(rowType);

        Append(prefGroup);

        // Control Buttons
        var actionBox = Gtk.Box.New(Gtk.Orientation.Horizontal, 15);
        actionBox.SetMarginTop(10);

        _btnSync = Gtk.Button.NewWithLabel(Loc.Get("sync.start_button"));
        _btnSync.OnClicked += (s, e) => _viewModel?.TriggerSyncCommand.Execute(null);
        _btnSync.AddCssClass("suggested-action");

        _btnAnalyze = Gtk.Button.NewWithLabel(Loc.Get("sync.analyze_emails_button"));
        _btnAnalyze.OnClicked += (s, e) => _viewModel?.AnalyzeEmailsCommand.Execute(null);

        actionBox.Append(_btnSync);
        actionBox.Append(_btnAnalyze);
        Append(actionBox);
    }

    private void InitializeBindings()
    {
        if (_viewModel == null) return;

        _viewModel.PropertyChanged += (s, e) =>
        {
            GLib.Functions.IdleAdd(0, () =>
            {
                UpdateUiState();
                return false;
            });
        };

        _viewModel.Accounts.CollectionChanged += (s, e) =>
        {
            GLib.Functions.IdleAdd(0, () =>
            {
                PopulateAccounts();
                return false;
            });
        };

        PopulateAccounts();
        UpdateUiState();
    }

    private void PopulateAccounts()
    {
        if (_viewModel == null || _accountDropDown == null) return;

        var accountNames = _viewModel.Accounts.Select(a => $"{a.Provider}: {a.Email}").ToArray();
        if (accountNames.Length == 0) return;

        // Note: To dynamically change DropDown items in GTK4, we can set a new StringList model.
        // Let's create a new StringList using Gtk.StringList.New
        var stringList = Gtk.StringList.New(accountNames);
        _accountDropDown.SetModel(stringList);
    }

    private void UpdateUiState()
    {
        if (_viewModel == null) return;

        var enabled = !_viewModel.IsBusy;
        _btnSync?.SetSensitive(enabled);
        _btnAnalyze?.SetSensitive(enabled);
        _accountDropDown?.SetSensitive(enabled);
        _syncTypeDropDown?.SetSensitive(enabled);
    }
}
