using System;
using Gtk;
using MultiSych.Desktop.Localization;
using MultiSych.Desktop.ViewModels;

namespace MultiSych.Desktop.Views;

public class AIOverviewView : Gtk.Box
{
    private AIOverviewViewModel? _viewModel;
    private Gtk.Box? _providerBox;
    private Gtk.Label? _statusLabel;

    public AIOverviewViewModel? DataContext
    {
        get => _viewModel;
        set
        {
            if (ReferenceEquals(_viewModel, value)) return;
            _viewModel = value;
            if (_viewModel != null) InitializeBindings();
        }
    }

    public AIOverviewView() : base()
    {
        ((Gtk.Orientable)this).Orientation = Gtk.Orientation.Vertical;
        Spacing = 20;
        SetMarginStart(20);
        SetMarginEnd(20);
        SetMarginTop(20);
        SetMarginBottom(20);
        BuildUi();
    }

    private void BuildUi()
    {
        var title = Gtk.Label.New(Loc.Get("ai_overview.title"));
        title.SetHalign(Gtk.Align.Start);
        title.SetFontSize(24);
        title.SetFontWeight(Pango.Weight.Bold);
        Append(title);

        _providerBox = Gtk.Box.New(Gtk.Orientation.Horizontal, 15);
        _providerBox.SetHalign(Gtk.Align.Center);
        _providerBox.SetVexpand(true);
        _providerBox.SetValign(Gtk.Align.Center);
        Append(_providerBox);

        _statusLabel = Gtk.Label.New(string.Empty);
        _statusLabel.SetHalign(Gtk.Align.Center);
        _statusLabel.SetWrap(true);
        _statusLabel.AddCssClass("dim-label");
        Append(_statusLabel);
    }

    private void InitializeBindings()
    {
        if (_viewModel == null) return;

        _viewModel.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName == nameof(AIOverviewViewModel.StatusMessage))
                GLib.Functions.IdleAdd(0, () => { UpdateStatus(); return false; });
        };

        PopulateProviders();
        UpdateStatus();
    }

    private void PopulateProviders()
    {
        if (_viewModel == null || _providerBox == null) return;

        var child = _providerBox.GetFirstChild();
        while (child != null)
        {
            _providerBox.Remove(child);
            child = _providerBox.GetFirstChild();
        }

        foreach (var provider in _viewModel.ProviderButtons)
        {
            var button = Gtk.Button.NewWithLabel($"🤖  {provider}\nSohbeti Aç");
            button.SetSizeRequest(190, 150);
            button.OnClicked += (_, _) =>
            {
                if (_viewModel.OpenChatCommand.CanExecute(provider))
                    _viewModel.OpenChatCommand.Execute(provider);
            };
            _providerBox.Append(button);
        }
    }

    private void UpdateStatus() => _statusLabel?.SetText(_viewModel?.StatusMessage ?? string.Empty);
}
