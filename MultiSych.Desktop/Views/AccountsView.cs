using System;
using Gtk;
using MultiSych.Desktop.ViewModels;

namespace MultiSych.Desktop.Views;

public class AccountsView : Gtk.Box
{
    private AccountsViewModel? _viewModel;
    private Gtk.ListBox? _accountsList;
    private Gtk.Button? _btnAddAccount;

    public AccountsViewModel? DataContext
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

    public AccountsView() : base()
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
        // Header Box
        var header = Gtk.Box.New(Gtk.Orientation.Horizontal, 10);
        
        var title = Gtk.Label.New("Bağlı Hesaplar");
        title.SetHalign(Gtk.Align.Start);
        title.SetFontSize(24);
        title.SetFontWeight(Pango.Weight.Bold);
        title.SetHexpand(true);
        header.Append(title);

        _btnAddAccount = Gtk.Button.NewWithLabel("Yeni Hesap Ekle ➕");
        _btnAddAccount.OnClicked += (s, e) => _viewModel?.AddAccountCommand.Execute(null);
        _btnAddAccount.AddCssClass("suggested-action");
        header.Append(_btnAddAccount);

        Append(header);

        // Accounts List Box Frame
        var listFrame = Gtk.Frame.New(null);
        _accountsList = Gtk.ListBox.New();
        _accountsList.SetSelectionMode(Gtk.SelectionMode.None);
        listFrame.SetChild(_accountsList);
        Append(listFrame);
    }

    private void InitializeBindings()
    {
        if (_viewModel == null) return;

        _viewModel.Accounts.CollectionChanged += (s, e) =>
        {
            GLib.Functions.IdleAdd(0, () =>
            {
                PopulateAccounts();
                return false;
            });
        };

        PopulateAccounts();
    }

    private void PopulateAccounts()
    {
        if (_viewModel == null || _accountsList == null) return;

        // Clear list
        var child = _accountsList.GetFirstChild();
        while (child != null)
        {
            _accountsList.Remove(child);
            child = _accountsList.GetFirstChild();
        }

        foreach (var item in _viewModel.Accounts)
        {
            var row = Gtk.ListBoxRow.New();
            
            var box = Gtk.Box.New(Gtk.Orientation.Horizontal, 15);
            box.SetMarginStart(15);
            box.SetMarginEnd(15);
            box.SetMarginTop(10);
            box.SetMarginBottom(10);

            // Provider Icon/Label
            var provider = Gtk.Label.New(item.Provider);
            provider.SetFontWeight(Pango.Weight.Bold);
            provider.SetFontSize(14);
            box.Append(provider);

            // Email details
            var emailBox = Gtk.Box.New(Gtk.Orientation.Vertical, 2);
            emailBox.SetHexpand(true);
            var email = Gtk.Label.New(item.Email);
            email.SetHalign(Gtk.Align.Start);
            var status = Gtk.Label.New(item.Status);
            status.SetHalign(Gtk.Align.Start);
            status.SetFontSize(10);
            
            emailBox.Append(email);
            emailBox.Append(status);
            box.Append(emailBox);

            // Mount actions
            if (item.IsMounted)
            {
                var btnUnmount = Gtk.Button.NewWithLabel("Sürücüyü Ayır");
                btnUnmount.OnClicked += (s, e) => _viewModel.UnmountCommand.Execute(item);
                box.Append(btnUnmount);
            }
            else
            {
                var btnMount = Gtk.Button.NewWithLabel("Sürücüyü Bağla");
                btnMount.OnClicked += (s, e) => _viewModel.MountCommand.Execute(item);
                box.Append(btnMount);
            }

            // Sync account action
            var btnSync = Gtk.Button.NewWithLabel("Senkronize Et 🔄");
            btnSync.OnClicked += (s, e) => _viewModel.SyncAccountCommand.Execute(item);
            box.Append(btnSync);

            // Delete action
            var btnDelete = Gtk.Button.NewWithLabel("Kaldır 🗑️");
            btnDelete.OnClicked += (s, e) => _viewModel.DeleteAccountCommand.Execute(item);
            btnDelete.AddCssClass("destructive-action");
            box.Append(btnDelete);

            row.SetChild(box);
            _accountsList.Append(row);
        }
    }
}
