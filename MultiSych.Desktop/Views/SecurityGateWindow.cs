using System.ComponentModel;
using Adw;
using Gtk;
using MultiSych.Desktop.ViewModels;
using MultiSych.Services.Configuration;

namespace MultiSych.Desktop.Views;

public class SecurityGateWindow : Gtk.Window
{
    private readonly Adw.Application _application;
    private readonly LoginViewModel _viewModel;
    private bool _authenticationSucceeded;

    public SecurityGateWindow(
        Adw.Application application,
        SecuritySettings security,
        LoginViewModel viewModel)
    {
        _application = application;
        _viewModel = viewModel;

        SetTitle("MultiSych Güvenlik Doğrulaması");
        SetDefaultSize(420, 280);
        SetApplication(application);
        SetModal(true);

        BuildUi(security);

        OnCloseRequest += (_, _) => !_authenticationSucceeded;
    }

    public void CompleteAuthentication()
    {
        _authenticationSucceeded = true;
        Close();
    }

    private void BuildUi(SecuritySettings security)
    {
        var box = Gtk.Box.New(Gtk.Orientation.Vertical, 12);
        box.SetMarginStart(24);
        box.SetMarginEnd(24);
        box.SetMarginTop(24);
        box.SetMarginBottom(24);

        var title = Gtk.Label.New("Başlangıç Güvenlik Kontrolü");
        title.SetFontSize(16);
        title.SetFontWeight(Pango.Weight.Bold);
        box.Append(title);

        Gtk.PasswordEntry? passwordEntry = null;
        if (security.RequireStartupPassword)
        {
            var passwordLabel = Gtk.Label.New("Parola");
            passwordLabel.SetHalign(Gtk.Align.Start);
            box.Append(passwordLabel);

            passwordEntry = Gtk.PasswordEntry.New();
            passwordEntry.SetShowPeekIcon(true);
            passwordEntry.OnNotify += (_, args) =>
            {
                if (args.Pspec.GetName() == "text")
                    _viewModel.Password = passwordEntry.GetText();
            };
            box.Append(passwordEntry);
        }

        Gtk.Entry? twoFactorEntry = null;
        if (_viewModel.RequiresTwoFactor)
        {
            twoFactorEntry = Gtk.Entry.New();
            twoFactorEntry.SetPlaceholderText("6 haneli 2FA kodu");
            twoFactorEntry.SetMaxLength(6);
            twoFactorEntry.OnNotify += (_, args) =>
            {
                if (args.Pspec.GetName() == "text")
                    _viewModel.TwoFactorCode = twoFactorEntry.GetText();
            };
            box.Append(twoFactorEntry);
        }

        var errorLabel = Gtk.Label.New(string.Empty);
        errorLabel.SetHalign(Gtk.Align.Start);
        errorLabel.AddCssClass("error");
        box.Append(errorLabel);

        _viewModel.PropertyChanged += OnViewModelPropertyChanged;
        OnDestroy += (_, _) => _viewModel.PropertyChanged -= OnViewModelPropertyChanged;

        var buttonBox = Gtk.Box.New(Gtk.Orientation.Horizontal, 10);
        buttonBox.SetHalign(Gtk.Align.End);

        var exitButton = Gtk.Button.NewWithLabel("Çıkış");
        exitButton.OnClicked += (_, _) => _application.Quit();
        buttonBox.Append(exitButton);

        var loginButton = Gtk.Button.NewWithLabel("Giriş");
        loginButton.AddCssClass("suggested-action");
        loginButton.OnClicked += (_, _) => _viewModel.LoginCommand.Execute(null);
        buttonBox.Append(loginButton);

        box.Append(buttonBox);
        SetChild(box);

        void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs args)
        {
            if (args.PropertyName == nameof(LoginViewModel.ErrorMessage))
                errorLabel.SetText(_viewModel.ErrorMessage);
        }
    }
}
