using System;
using Gtk;
using MultiSych.Desktop.Localization;
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
        SetTitle(Loc.Get("add_account.window_title"));
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

        var title = Gtk.Label.New(Loc.Get("add_account.title"));
        title.SetFontSize(16);
        title.SetFontWeight(Pango.Weight.Bold);
        box.Append(title);

        var subtitle = Gtk.Label.New(Loc.Get("add_account.provider_prompt"));
        subtitle.SetHalign(Gtk.Align.Start);
        box.Append(subtitle);

        var btnGoogle = Gtk.Button.NewWithLabel(Loc.Get("add_account.google_button"));
        btnGoogle.OnClicked += (s, e) => {
            _viewModel?.AddGoogleCommand.Execute(null);
            this.Close();
        };
        box.Append(btnGoogle);

        var btnMicrosoft = Gtk.Button.NewWithLabel(Loc.Get("add_account.microsoft_button"));
        btnMicrosoft.OnClicked += (s, e) => {
            _viewModel?.AddMicrosoftCommand.Execute(null);
            this.Close();
        };
        box.Append(btnMicrosoft);

        var btnYandex = Gtk.Button.NewWithLabel(Loc.Get("add_account.yandex_button"));
        btnYandex.OnClicked += (s, e) => {
            _viewModel?.AddYandexCommand.Execute(null);
            this.Close();
        };
        box.Append(btnYandex);

        SetChild(box);
    }
}
