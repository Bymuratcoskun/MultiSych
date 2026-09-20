using System;
using System.Collections.Generic;
using Gtk;
using MultiSych.Desktop.Localization;
using MultiSych.Desktop.ViewModels;
using MultiSych.Services.Data;

namespace MultiSych.Desktop.Views;

/// <summary>
/// Bulut dosya gezgini. FileExplorerViewModel'e bağlıdır; hesap seçimi, yol
/// gezinme ve klasöre girme (çift tıklama) sağlar.
/// </summary>
public class FileExplorerView : Gtk.Box
{
    private FileExplorerViewModel? _viewModel;
    private Gtk.ListBox? _fileList;
    private Gtk.Label? _pathLabel;
    private Gtk.Box? _accountBar;
    private readonly List<CloudFileEntity> _rendered = new();

    public FileExplorerViewModel? DataContext
    {
        get => _viewModel;
        set
        {
            if (ReferenceEquals(_viewModel, value)) return;
            _viewModel = value;
            if (_viewModel != null) InitializeBindings();
        }
    }

    public FileExplorerView() : base()
    {
        ((Gtk.Orientable)this).Orientation = Gtk.Orientation.Vertical;
        this.Spacing = 10;
        SetMarginStart(15);
        SetMarginEnd(15);
        SetMarginTop(15);
        SetMarginBottom(15);
        BuildUi();
    }

    private void BuildUi()
    {
        // Araç çubuğu
        var toolbar = Gtk.Box.New(Gtk.Orientation.Horizontal, 8);

        var btnUp = Gtk.Button.NewWithLabel(Loc.Get("file_explorer.up_button"));
        btnUp.OnClicked += (_, _) => _viewModel?.NavigateUpCommand.Execute(null);
        toolbar.Append(btnUp);

        var btnRefresh = Gtk.Button.NewWithLabel(Loc.Get("common.refresh_button"));
        btnRefresh.OnClicked += (_, _) => _viewModel?.RefreshCommand.Execute(null);
        toolbar.Append(btnRefresh);

        _pathLabel = Gtk.Label.New("/");
        _pathLabel.SetHalign(Gtk.Align.Start);
        _pathLabel.SetHexpand(true);
        _pathLabel.SetEllipsize(Pango.EllipsizeMode.Middle);
        toolbar.Append(_pathLabel);

        Append(toolbar);

        // Hesap seçim çubuğu
        _accountBar = Gtk.Box.New(Gtk.Orientation.Horizontal, 6);
        Append(_accountBar);

        // Dosya listesi
        _fileList = Gtk.ListBox.New();
        _fileList.SetSelectionMode(Gtk.SelectionMode.Single);
        _fileList.OnRowActivated += (_, args) =>
        {
            var idx = args.Row?.GetIndex() ?? -1;
            if (_viewModel != null && idx >= 0 && idx < _rendered.Count)
            {
                var file = _rendered[idx];
                if (file.IsDirectory) _viewModel.OpenFolderCommand.Execute(file);
            }
        };

        var scroll = Gtk.ScrolledWindow.New();
        scroll.SetChild(_fileList);
        scroll.SetVexpand(true);
        Append(scroll);
    }

    private void InitializeBindings()
    {
        if (_viewModel == null) return;

        _viewModel.Files.CollectionChanged += (_, _) =>
            GLib.Functions.IdleAdd(0, () => { PopulateFiles(); return false; });

        _viewModel.Accounts.CollectionChanged += (_, _) =>
            GLib.Functions.IdleAdd(0, () => { PopulateAccounts(); return false; });

        _viewModel.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName == nameof(FileExplorerViewModel.CurrentPath))
                GLib.Functions.IdleAdd(0, () =>
                {
                    _pathLabel?.SetText(string.IsNullOrEmpty(_viewModel!.CurrentPath) ? "/" : _viewModel.CurrentPath);
                    return false;
                });
        };

        PopulateAccounts();
        PopulateFiles();
    }

    private void PopulateAccounts()
    {
        if (_viewModel == null || _accountBar == null) return;

        var child = _accountBar.GetFirstChild();
        while (child != null)
        {
            _accountBar.Remove(child);
            child = _accountBar.GetFirstChild();
        }

        foreach (var acc in _viewModel.Accounts)
        {
            var btn = Gtk.Button.NewWithLabel($"{acc.Provider}: {acc.Email}");
            btn.OnClicked += (_, _) =>
            {
                _viewModel.SelectedAccountId = acc.AccountId;
                _viewModel.RefreshCommand.Execute(null);
            };
            _accountBar.Append(btn);
        }
    }

    private void PopulateFiles()
    {
        if (_viewModel == null || _fileList == null) return;

        var child = _fileList.GetFirstChild();
        while (child != null)
        {
            _fileList.Remove(child);
            child = _fileList.GetFirstChild();
        }
        _rendered.Clear();

        foreach (var file in _viewModel.Files)
        {
            _rendered.Add(file);

            var row = Gtk.ListBoxRow.New();
            var box = Gtk.Box.New(Gtk.Orientation.Horizontal, 12);
            box.SetMarginStart(12);
            box.SetMarginEnd(12);
            box.SetMarginTop(8);
            box.SetMarginBottom(8);

            var icon = Gtk.Label.New(file.IsDirectory ? "📁" : "📄");
            box.Append(icon);

            var name = Gtk.Label.New(file.FileName);
            name.SetHalign(Gtk.Align.Start);
            name.SetHexpand(true);
            name.SetEllipsize(Pango.EllipsizeMode.End);
            box.Append(name);

            if (!file.IsDirectory)
            {
                var size = Gtk.Label.New(FormatSize(file.FileSize));
                size.SetFontSize(10);
                size.AddCssClass("dim-label");
                box.Append(size);
            }

            row.SetChild(box);
            _fileList.Append(row);
        }
    }

    private static string FormatSize(long bytes)
    {
        string[] units = { "B", "KB", "MB", "GB", "TB" };
        double size = bytes;
        int unit = 0;
        while (size >= 1024 && unit < units.Length - 1)
        {
            size /= 1024;
            unit++;
        }
        return $"{size:0.#} {units[unit]}";
    }
}
