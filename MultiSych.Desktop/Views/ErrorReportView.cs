using System;
using Gtk;
using MultiSych.Desktop.ViewModels;

namespace MultiSych.Desktop.Views;

public class ErrorReportView : Gtk.Box
{
    private ErrorReportViewModel? _viewModel;
    private Gtk.Entry? _titleEntry;
    private Gtk.TextView? _descriptionView;
    private Gtk.Button? _submitButton;

    public ErrorReportViewModel? DataContext
    {
        get => _viewModel;
        set
        {
            if (ReferenceEquals(_viewModel, value)) return;
            _viewModel = value;
            if (_viewModel != null) InitializeBindings();
        }
    }

    public ErrorReportView() : base()
    {
        ((Gtk.Orientable)this).Orientation = Gtk.Orientation.Vertical;
        Spacing = 15;
        SetMarginStart(20);
        SetMarginEnd(20);
        SetMarginTop(20);
        SetMarginBottom(20);
        BuildUi();
    }

    private void BuildUi()
    {
        var title = Gtk.Label.New("Hata Bildirimi / GitHub Issue Aç");
        title.SetHalign(Gtk.Align.Start);
        title.SetFontSize(24);
        title.SetFontWeight(Pango.Weight.Bold);
        Append(title);

        var explanation = Gtk.Label.New("Başlığı ve ayrıntıları girin. Gönder düğmesi GitHub issue sayfasını tarayıcıda açar.");
        explanation.SetHalign(Gtk.Align.Start);
        explanation.SetWrap(true);
        explanation.AddCssClass("dim-label");
        Append(explanation);

        _titleEntry = Gtk.Entry.New();
        _titleEntry.SetPlaceholderText("Kısa ve açıklayıcı başlık");
        _titleEntry.OnNotify += (_, args) =>
        {
            if (args.Pspec.GetName() == "text" && _viewModel != null)
                _viewModel.IssueTitle = _titleEntry.GetText();
        };
        Append(_titleEntry);

        _descriptionView = Gtk.TextView.New();
        _descriptionView.SetWrapMode(Gtk.WrapMode.Word);
        _descriptionView.SetSizeRequest(-1, 240);
        _descriptionView.Buffer!.OnNotify += (_, args) =>
        {
            if (args.Pspec.GetName() == "text" && _viewModel != null)
                _viewModel.IssueDescription = _descriptionView.Buffer!.Text ?? string.Empty;
        };
        var scroll = Gtk.ScrolledWindow.New();
        scroll.SetChild(_descriptionView);
        scroll.SetVexpand(true);
        Append(scroll);

        _submitButton = Gtk.Button.NewWithLabel("GitHub'da Issue Aç");
        _submitButton.AddCssClass("suggested-action");
        _submitButton.SetHalign(Gtk.Align.End);
        _submitButton.OnClicked += (_, _) =>
        {
            if (_viewModel?.SubmitGitHubIssueCommand.CanExecute(null) == true)
                _viewModel.SubmitGitHubIssueCommand.Execute(null);
        };
        Append(_submitButton);
    }

    private void InitializeBindings()
    {
        if (_viewModel == null) return;
        _viewModel.PropertyChanged += (_, _) =>
            GLib.Functions.IdleAdd(0, () => { UpdateValues(); return false; });
        UpdateValues();
    }

    private void UpdateValues()
    {
        if (_viewModel == null) return;
        if (_titleEntry != null && _titleEntry.GetText() != _viewModel.IssueTitle)
            _titleEntry.SetText(_viewModel.IssueTitle);
        if (_descriptionView?.Buffer != null && _descriptionView.Buffer.Text != _viewModel.IssueDescription)
            _descriptionView.Buffer.Text = _viewModel.IssueDescription;
        _submitButton?.SetSensitive(_viewModel.SubmitGitHubIssueCommand.CanExecute(null));
    }
}
