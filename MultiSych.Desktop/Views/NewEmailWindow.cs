using System;
using Gtk;
using MultiSych.Desktop.ViewModels;

namespace MultiSych.Desktop.Views;

public class NewEmailWindow : Gtk.Window
{
    private NewEmailViewModel? _viewModel;

    public NewEmailViewModel? DataContext
    {
        get => _viewModel;
        set => _viewModel = value;
    }

    public NewEmailWindow(Gtk.Window parent, NewEmailViewModel viewModel)
    {
        _viewModel = viewModel;
        
        SetTitle("Yeni E-Posta Oluştur");
        SetDefaultSize(600, 500);
        SetTransientFor(parent);
        SetModal(true);

        BuildUi();
    }

    private void BuildUi()
    {
        var box = Gtk.Box.New(Gtk.Orientation.Vertical, 10);
        box.SetMarginStart(15);
        box.SetMarginEnd(15);
        box.SetMarginTop(15);
        box.SetMarginBottom(15);

        // To address
        var toBox = Gtk.Box.New(Gtk.Orientation.Horizontal, 10);
        var toLabel = Gtk.Label.New("Kime:");
        toLabel.SetSizeRequest(60, -1);
        var toEntry = Gtk.Entry.New();
        toEntry.SetHexpand(true);
        if (_viewModel != null) toEntry.SetText(_viewModel.ToAddress ?? string.Empty);
        toEntry.OnNotify += (s, e) => {
            if (e.Pspec.GetName() == "text" && _viewModel != null) _viewModel.ToAddress = toEntry.GetText();
        };
        toBox.Append(toLabel);
        toBox.Append(toEntry);
        box.Append(toBox);

        // Subject
        var subBox = Gtk.Box.New(Gtk.Orientation.Horizontal, 10);
        var subLabel = Gtk.Label.New("Konu:");
        subLabel.SetSizeRequest(60, -1);
        var subEntry = Gtk.Entry.New();
        subEntry.SetHexpand(true);
        if (_viewModel != null) subEntry.SetText(_viewModel.Subject ?? string.Empty);
        subEntry.OnNotify += (s, e) => {
            if (e.Pspec.GetName() == "text" && _viewModel != null) _viewModel.Subject = subEntry.GetText();
        };
        subBox.Append(subLabel);
        subBox.Append(subEntry);
        box.Append(subBox);

        // Body Text
        var bodyTextView = Gtk.TextView.New();
        bodyTextView.SetWrapMode(Gtk.WrapMode.Word);
        if (_viewModel != null) bodyTextView.Buffer!.Text = _viewModel.BodyText ?? string.Empty;
        bodyTextView.Buffer!.OnNotify += (s, e) => {
            if (e.Pspec.GetName() == "text" && _viewModel != null) _viewModel.BodyText = bodyTextView.Buffer!.Text ?? string.Empty;
        };

        var scroll = Gtk.ScrolledWindow.New();
        scroll.SetChild(bodyTextView);
        scroll.SetVexpand(true);
        box.Append(scroll);

        // Buttons
        var btnBox = Gtk.Box.New(Gtk.Orientation.Horizontal, 10);
        var btnSend = Gtk.Button.NewWithLabel("Gönder ✉️");
        btnSend.AddCssClass("suggested-action");
        btnSend.OnClicked += (s, e) =>
        {
            _viewModel?.SendEmailCommand.Execute(null);
            this.Close();
        };

        var btnCancel = Gtk.Button.NewWithLabel("İptal");
        btnCancel.OnClicked += (s, e) => this.Close();

        btnBox.Append(btnSend);
        btnBox.Append(btnCancel);
        box.Append(btnBox);

        SetChild(box);
    }
}
