using System;
using System.Linq;
using Gtk;
using MultiSych.Desktop.ViewModels;

namespace MultiSych.Desktop.Views;

public class DocumentAnalyzerView : Gtk.Box
{
    private DocumentAnalyzerViewModel? _viewModel;
    private Gtk.DropDown? _providerDropDown;
    private Gtk.TextView? _documentContent;
    private Gtk.TextView? _summaryResult;
    private Gtk.Entry? _emailFrom;
    private Gtk.Entry? _emailSubject;
    private Gtk.TextView? _emailBody;
    private Gtk.TextView? _emailResult;
    private Gtk.PasswordEntry? _exportPassword;
    private Gtk.Label? _statusLabel;
    private Gtk.Button? _summarizeButton;
    private Gtk.Button? _analyzeEmailButton;

    public DocumentAnalyzerViewModel? DataContext
    {
        get => _viewModel;
        set
        {
            if (ReferenceEquals(_viewModel, value)) return;
            _viewModel = value;
            if (_viewModel != null) InitializeBindings();
        }
    }

    public DocumentAnalyzerView() : base()
    {
        ((Gtk.Orientable)this).Orientation = Gtk.Orientation.Vertical;
        Spacing = 0;
        BuildUi();
    }

    private void BuildUi()
    {
        var content = Gtk.Box.New(Gtk.Orientation.Vertical, 16);
        content.SetMarginStart(20);
        content.SetMarginEnd(20);
        content.SetMarginTop(20);
        content.SetMarginBottom(20);

        var title = Gtk.Label.New("Belge ve E-Posta Analizi (AI)");
        title.SetHalign(Gtk.Align.Start);
        title.SetFontSize(24);
        title.SetFontWeight(Pango.Weight.Bold);
        content.Append(title);

        var controls = Gtk.Box.New(Gtk.Orientation.Horizontal, 10);
        _providerDropDown = Gtk.DropDown.NewFromStrings(["hybrid"]);
        _providerDropDown.OnNotify += (_, args) =>
        {
            if (args.Pspec.GetName() != "selected" || _viewModel == null) return;
            var index = (int)_providerDropDown.GetSelected();
            if (index >= 0 && index < _viewModel.AvailableProviders.Count)
                _viewModel.SelectedProvider = _viewModel.AvailableProviders[index];
        };
        controls.Append(_providerDropDown);

        var loadButton = Gtk.Button.NewWithLabel("Dosya Yükle");
        loadButton.OnClicked += (_, _) => Execute(_viewModel?.LoadFileCommand);
        controls.Append(loadButton);

        _summarizeButton = Gtk.Button.NewWithLabel("Özetle");
        _summarizeButton.AddCssClass("suggested-action");
        _summarizeButton.OnClicked += (_, _) => Execute(_viewModel?.SummarizeCommand);
        controls.Append(_summarizeButton);

        _statusLabel = Gtk.Label.New(string.Empty);
        _statusLabel.SetHalign(Gtk.Align.Start);
        _statusLabel.SetHexpand(true);
        _statusLabel.SetWrap(true);
        controls.Append(_statusLabel);
        content.Append(controls);

        _documentContent = CreateTextView(editable: true, minHeight: 180);
        _documentContent.Buffer!.OnNotify += (_, args) =>
        {
            if (args.Pspec.GetName() == "text" && _viewModel != null)
                _viewModel.DocumentContent = _documentContent.Buffer!.Text ?? string.Empty;
        };
        content.Append(CreateFrame("Belge İçeriği", _documentContent));

        var exportRow = Gtk.Box.New(Gtk.Orientation.Horizontal, 8);
        var exportLabel = Gtk.Label.New("Dışa aktarma parolası:");
        exportLabel.SetHalign(Gtk.Align.Start);
        exportRow.Append(exportLabel);
        _exportPassword = Gtk.PasswordEntry.New();
        _exportPassword.SetShowPeekIcon(true);
        _exportPassword.SetHexpand(true);
        _exportPassword.OnNotify += (_, args) =>
        {
            if (args.Pspec.GetName() == "text" && _viewModel != null)
                _viewModel.ExportPassword = _exportPassword.GetText();
        };
        exportRow.Append(_exportPassword);
        content.Append(exportRow);

        _summaryResult = CreateTextView(editable: false, minHeight: 150);
        content.Append(CreateFrame("Özet Sonucu", _summaryResult));

        var emailMeta = Gtk.Box.New(Gtk.Orientation.Horizontal, 10);
        _emailFrom = Gtk.Entry.New();
        _emailFrom.SetPlaceholderText("Kimden");
        _emailFrom.SetHexpand(true);
        _emailFrom.OnNotify += (_, args) =>
        {
            if (args.Pspec.GetName() == "text" && _viewModel != null)
                _viewModel.EmailFrom = _emailFrom.GetText();
        };
        emailMeta.Append(_emailFrom);
        _emailSubject = Gtk.Entry.New();
        _emailSubject.SetPlaceholderText("Konu");
        _emailSubject.SetHexpand(true);
        _emailSubject.OnNotify += (_, args) =>
        {
            if (args.Pspec.GetName() == "text" && _viewModel != null)
                _viewModel.EmailSubject = _emailSubject.GetText();
        };
        emailMeta.Append(_emailSubject);
        content.Append(emailMeta);

        _emailBody = CreateTextView(editable: true, minHeight: 160);
        _emailBody.Buffer!.OnNotify += (_, args) =>
        {
            if (args.Pspec.GetName() == "text" && _viewModel != null)
                _viewModel.EmailBody = _emailBody.Buffer!.Text ?? string.Empty;
        };
        content.Append(CreateFrame("E-Posta İçeriği", _emailBody));

        _analyzeEmailButton = Gtk.Button.NewWithLabel("E-Postayı Analiz Et");
        _analyzeEmailButton.AddCssClass("suggested-action");
        _analyzeEmailButton.SetHalign(Gtk.Align.End);
        _analyzeEmailButton.OnClicked += (_, _) => Execute(_viewModel?.AnalyzeEmailCommand);
        content.Append(_analyzeEmailButton);

        _emailResult = CreateTextView(editable: false, minHeight: 150);
        content.Append(CreateFrame("E-Posta Analiz Sonucu", _emailResult));

        var scroll = Gtk.ScrolledWindow.New();
        scroll.SetChild(content);
        scroll.SetVexpand(true);
        scroll.SetHexpand(true);
        Append(scroll);
    }

    private void InitializeBindings()
    {
        if (_viewModel == null || _providerDropDown == null) return;

        _providerDropDown.SetModel(Gtk.StringList.New(_viewModel.AvailableProviders.ToArray()));
        var selectedIndex = _viewModel.AvailableProviders.IndexOf(_viewModel.SelectedProvider);
        if (selectedIndex >= 0) _providerDropDown.SetSelected((uint)selectedIndex);

        _viewModel.PropertyChanged += (_, _) =>
            GLib.Functions.IdleAdd(0, () => { UpdateValues(); return false; });
        UpdateValues();
    }

    private void UpdateValues()
    {
        if (_viewModel == null) return;
        SetText(_documentContent, _viewModel.DocumentContent);
        SetText(_summaryResult, _viewModel.SummaryResult);
        SetEntry(_emailFrom, _viewModel.EmailFrom);
        SetEntry(_emailSubject, _viewModel.EmailSubject);
        SetText(_emailBody, _viewModel.EmailBody);
        SetText(_emailResult, _viewModel.EmailAnalysisResult);
        _statusLabel?.SetText(_viewModel.StatusMessage);
        _summarizeButton?.SetSensitive(_viewModel.SummarizeCommand.CanExecute(null));
        _analyzeEmailButton?.SetSensitive(_viewModel.AnalyzeEmailCommand.CanExecute(null));
        _providerDropDown?.SetSensitive(!_viewModel.IsAnalyzing);
    }

    private static Gtk.TextView CreateTextView(bool editable, int minHeight)
    {
        var view = Gtk.TextView.New();
        view.SetEditable(editable);
        view.SetWrapMode(Gtk.WrapMode.Word);
        view.SetSizeRequest(-1, minHeight);
        return view;
    }

    private static Gtk.Widget CreateFrame(string title, Gtk.Widget child)
    {
        var frame = Gtk.Frame.New(title);
        var scroll = Gtk.ScrolledWindow.New();
        scroll.SetChild(child);
        frame.SetChild(scroll);
        return frame;
    }

    private static void Execute(System.Windows.Input.ICommand? command)
    {
        if (command?.CanExecute(null) == true) command.Execute(null);
    }

    private static void SetEntry(Gtk.Entry? entry, string value)
    {
        if (entry != null && entry.GetText() != value) entry.SetText(value);
    }

    private static void SetText(Gtk.TextView? view, string value)
    {
        if (view?.Buffer != null && view.Buffer.Text != value) view.Buffer.Text = value;
    }
}
