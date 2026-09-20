using System;
using Gtk;
using MultiSych.Desktop.ViewModels;

namespace MultiSych.Desktop.Views;

public class AddAccountWindow : Gtk.Window
{
    private AddAccountViewModel? _viewModel;

    public AddAccountViewModel? DataContext
    {
        get => _viewModel;
        set => _viewModel = value;
    }

    public AddAccountWindow(Gtk.Window parent)
    {
        SetTitle("Yeni Hesap Ekle");
        SetDefaultSize(400, 250);
        SetTransientFor(parent);
        SetModal(true);

        BuildUi();
    }

    private void BuildUi()
    {
        var box = Gtk.Box.New(Gtk.Orientation.Vertical, 15);
        box.SetMarginStart(20);
        box.SetMarginEnd(20);
        box.SetMarginTop(20);
        box.SetMarginBottom(20);

        var title = Gtk.Label.New("Bulut Hesabı Ekle");
        title.SetFontSize(16);
        title.SetFontWeight(Pango.Weight.Bold);
        box.Append(title);

        var subtitle = Gtk.Label.New("Eklemek istediğiniz bulut sağlayıcısını seçin:");
        subtitle.SetHalign(Gtk.Align.Start);
        box.Append(subtitle);

        var btnGoogle = Gtk.Button.NewWithLabel("🤖 Google Drive / Gmail");
        btnGoogle.OnClicked += (s, e) => {
            _viewModel?.AddGoogleCommand.Execute(null);
            this.Close();
        };
        box.Append(btnGoogle);

        var btnMicrosoft = Gtk.Button.NewWithLabel("✨ Microsoft OneDrive / Outlook");
        btnMicrosoft.OnClicked += (s, e) => {
            _viewModel?.AddMicrosoftCommand.Execute(null);
            this.Close();
        };
        box.Append(btnMicrosoft);

        var btnYandex = Gtk.Button.NewWithLabel("🧠 Yandex Disk / Mail");
        btnYandex.OnClicked += (s, e) => {
            _viewModel?.AddYandexCommand.Execute(null);
            this.Close();
        };
        box.Append(btnYandex);

        SetChild(box);
    }
}
