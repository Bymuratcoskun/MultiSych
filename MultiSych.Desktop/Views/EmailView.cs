using System;
using System.Collections.Generic;
using Gtk;
using MultiSych.Desktop.Localization;
using MultiSych.Desktop.ViewModels;
using MultiSych.Services.Models;

namespace MultiSych.Desktop.Views;

/// <summary>
/// Üç panelli e-posta ekranı: klasörler | e-posta listesi | okuma paneli (+ AI aksiyonları).
/// EmailViewModel'e bağlıdır.
/// </summary>
public class EmailView : Gtk.Box
{
    private EmailViewModel? _viewModel;
    private Gtk.ListBox? _emailList;
    private readonly List<EmailMessage> _renderedEmails = new();

    private Gtk.Label? _readSubject;
    private Gtk.Label? _readMeta;
    private Gtk.TextView? _readBody;
    private Gtk.Label? _summaryLabel;
    private Gtk.Entry? _searchEntry;

    public EmailViewModel? DataContext
    {
        get => _viewModel;
        set
        {
            if (ReferenceEquals(_viewModel, value)) return;
            _viewModel = value;
            if (_viewModel != null) InitializeBindings();
        }
    }

    public EmailView() : base()
    {
        ((Gtk.Orientable)this).Orientation = Gtk.Orientation.Horizontal;
        this.Spacing = 0;
        BuildUi();
    }

    private void BuildUi()
    {
        Append(BuildFolderColumn());
        Append(Gtk.Separator.New(Gtk.Orientation.Vertical));
        Append(BuildListColumn());
        Append(Gtk.Separator.New(Gtk.Orientation.Vertical));
        Append(BuildReadingColumn());
    }

    private Gtk.Widget BuildFolderColumn()
    {
        var col = Gtk.Box.New(Gtk.Orientation.Vertical, 8);
        col.SetSizeRequest(170, -1);
        col.SetMarginStart(12);
        col.SetMarginEnd(12);
        col.SetMarginTop(15);
        col.SetMarginBottom(15);

        var btnCompose = Gtk.Button.NewWithLabel(Loc.Get("email.compose_button"));
        btnCompose.AddCssClass("suggested-action");
        btnCompose.OnClicked += (_, _) => _viewModel?.ComposeEmailCommand.Execute(null);
        col.Append(btnCompose);

        var btnRefresh = Gtk.Button.NewWithLabel(Loc.Get("common.refresh_button"));
        btnRefresh.OnClicked += (_, _) => _viewModel?.RefreshCommand.Execute(null);
        col.Append(btnRefresh);

        col.Append(Gtk.Separator.New(Gtk.Orientation.Horizontal));

        foreach (var (id, label) in new[] { ("Inbox", "📥 Gelen Kutusu"), ("Sent", "📤 Gönderilenler"), ("Trash", "🗑️ Çöp"), ("Vault", "🔒 Kasa") })
        {
            var btn = Gtk.Button.NewWithLabel(label);
            btn.SetHalign(Gtk.Align.Fill);
            btn.OnClicked += (_, _) => _viewModel?.SelectFolderCommand.Execute(id);
            col.Append(btn);
        }

        return col;
    }

    private Gtk.Widget BuildListColumn()
    {
        var col = Gtk.Box.New(Gtk.Orientation.Vertical, 8);
        col.SetSizeRequest(340, -1);
        col.SetMarginStart(10);
        col.SetMarginEnd(10);
        col.SetMarginTop(15);
        col.SetMarginBottom(15);

        _searchEntry = Gtk.Entry.New();
        _searchEntry.SetPlaceholderText(Loc.Get("email.search_placeholder"));
        _searchEntry.OnNotify += (_, args) =>
        {
            if (args.Pspec.GetName() == "text" && _viewModel != null)
                _viewModel.SearchQuery = _searchEntry.GetText();
        };
        col.Append(_searchEntry);

        _emailList = Gtk.ListBox.New();
        _emailList.SetSelectionMode(Gtk.SelectionMode.Single);
        _emailList.OnRowSelected += (_, args) =>
        {
            var idx = args.Row?.GetIndex() ?? -1;
            if (_viewModel != null && idx >= 0 && idx < _renderedEmails.Count)
                _viewModel.SelectedEmail = _renderedEmails[idx];
        };

        var scroll = Gtk.ScrolledWindow.New();
        scroll.SetChild(_emailList);
        scroll.SetVexpand(true);
        col.Append(scroll);

        return col;
    }

    private Gtk.Widget BuildReadingColumn()
    {
        var col = Gtk.Box.New(Gtk.Orientation.Vertical, 10);
        col.SetHexpand(true);
        col.SetVexpand(true);
        col.SetMarginStart(15);
        col.SetMarginEnd(15);
        col.SetMarginTop(15);
        col.SetMarginBottom(15);

        _readSubject = Gtk.Label.New(Loc.Get("email.select_prompt"));
        _readSubject.SetHalign(Gtk.Align.Start);
        _readSubject.SetFontSize(18);
        _readSubject.SetFontWeight(Pango.Weight.Bold);
        _readSubject.SetWrap(true);
        col.Append(_readSubject);

        _readMeta = Gtk.Label.New(string.Empty);
        _readMeta.SetHalign(Gtk.Align.Start);
        _readMeta.SetFontSize(11);
        col.Append(_readMeta);

        // AI aksiyon çubuğu
        var actions = Gtk.Box.New(Gtk.Orientation.Horizontal, 8);
        AddAction(actions, "🧠 Özetle", () => _viewModel?.SummarizeEmailCommand.Execute(null));
        AddAction(actions, "↩️ Akıllı Yanıt", () => _viewModel?.GenerateSmartReplyCommand.Execute(null));
        AddAction(actions, "📁 Arşivle", () => _viewModel?.ArchiveEmailCommand.Execute(null));
        AddAction(actions, "✓ Okundu/Okunmadı", () => _viewModel?.ToggleReadStatusCommand.Execute(null));
        var btnDelete = Gtk.Button.NewWithLabel(Loc.Get("email.delete_button"));
        btnDelete.AddCssClass("destructive-action");
        btnDelete.OnClicked += (_, _) => _viewModel?.DeleteEmailCommand.Execute(null);
        actions.Append(btnDelete);
        col.Append(actions);

        _summaryLabel = Gtk.Label.New(string.Empty);
        _summaryLabel.SetHalign(Gtk.Align.Start);
        _summaryLabel.SetWrap(true);
        _summaryLabel.AddCssClass("dim-label");
        col.Append(_summaryLabel);

        _readBody = Gtk.TextView.New();
        _readBody.SetEditable(false);
        _readBody.SetWrapMode(Gtk.WrapMode.Word);
        var scroll = Gtk.ScrolledWindow.New();
        scroll.SetChild(_readBody);
        scroll.SetVexpand(true);
        col.Append(scroll);

        return col;
    }

    private static void AddAction(Gtk.Box parent, string label, Action onClick)
    {
        var btn = Gtk.Button.NewWithLabel(label);
        btn.OnClicked += (_, _) => onClick();
        parent.Append(btn);
    }

    private void InitializeBindings()
    {
        if (_viewModel == null) return;

        _viewModel.Emails.CollectionChanged += (_, _) =>
            GLib.Functions.IdleAdd(0, () => { PopulateEmails(); return false; });

        _viewModel.PropertyChanged += (_, args) =>
            GLib.Functions.IdleAdd(0, () =>
            {
                switch (args.PropertyName)
                {
                    case nameof(EmailViewModel.SelectedEmail):
                        UpdateReadingPane();
                        break;
                    case nameof(EmailViewModel.SelectedEmailSummary):
                        if (_summaryLabel != null)
                            _summaryLabel.SetText(_viewModel!.SelectedEmailSummary ?? string.Empty);
                        break;
                }
                return false;
            });

        PopulateEmails();
    }

    private void PopulateEmails()
    {
        if (_viewModel == null || _emailList == null) return;

        var child = _emailList.GetFirstChild();
        while (child != null)
        {
            _emailList.Remove(child);
            child = _emailList.GetFirstChild();
        }
        _renderedEmails.Clear();

        foreach (var email in _viewModel.Emails)
        {
            _renderedEmails.Add(email);

            var row = Gtk.ListBoxRow.New();
            var box = Gtk.Box.New(Gtk.Orientation.Vertical, 2);
            box.SetMarginStart(10);
            box.SetMarginEnd(10);
            box.SetMarginTop(8);
            box.SetMarginBottom(8);

            var subject = Gtk.Label.New(string.IsNullOrWhiteSpace(email.Subject) ? "(konu yok)" : email.Subject);
            subject.SetHalign(Gtk.Align.Start);
            subject.SetEllipsize(Pango.EllipsizeMode.End);
            if (!email.IsRead) subject.SetFontWeight(Pango.Weight.Bold);
            box.Append(subject);

            var meta = Gtk.Label.New($"{email.From}  ·  {email.ReceivedDate.ToLocalTime():dd.MM.yyyy HH:mm}");
            meta.SetHalign(Gtk.Align.Start);
            meta.SetFontSize(10);
            meta.SetEllipsize(Pango.EllipsizeMode.End);
            meta.AddCssClass("dim-label");
            box.Append(meta);

            row.SetChild(box);
            _emailList.Append(row);
        }
    }

    private void UpdateReadingPane()
    {
        if (_viewModel == null) return;
        var email = _viewModel.SelectedEmail;

        if (email == null)
        {
            _readSubject?.SetText("Bir e-posta seçin");
            _readMeta?.SetText(string.Empty);
            if (_readBody?.Buffer != null) _readBody.Buffer.Text = string.Empty;
            _summaryLabel?.SetText(string.Empty);
            return;
        }

        _readSubject?.SetText(string.IsNullOrWhiteSpace(email.Subject) ? "(konu yok)" : email.Subject!);
        _readMeta?.SetText($"{email.From}  ·  {email.ReceivedDate.ToLocalTime():dd.MM.yyyy HH:mm}  ·  {email.Provider}");
        if (_readBody?.Buffer != null) _readBody.Buffer.Text = email.Body ?? string.Empty;
        _summaryLabel?.SetText(email.AiSummary ?? string.Empty);
    }
}
