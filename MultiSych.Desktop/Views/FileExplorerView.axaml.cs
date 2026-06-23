using System.Linq;
using Avalonia.Controls;
using Avalonia.Input;
using MultiSych.Desktop.ViewModels;
using MultiSych.Services.Data;
using MultiSych.Services.Models;
using Avalonia.Platform.Storage;

namespace MultiSych.Desktop.Views;

public partial class FileExplorerView : UserControl
{
    public FileExplorerView()
    {
        InitializeComponent();
        AddHandler(DragDrop.DropEvent, Drop);
        AddHandler(DragDrop.DragOverEvent, DragOver);
        AddHandler(DragDrop.DragEnterEvent, DragEnter);
        AddHandler(DragDrop.DragLeaveEvent, DragLeave);
    }

    private void DragEnter(object? sender, DragEventArgs e)
    {
        if (e.DataTransfer.TryGetFiles() is not null)
        {
            var overlay = this.FindControl<Border>("DragOverlay");
            if (overlay != null) overlay.IsVisible = true;
        }
    }

    private void DragLeave(object? sender, DragEventArgs e)
    {
        var overlay = this.FindControl<Border>("DragOverlay");
        if (overlay != null) overlay.IsVisible = false;
    }

    private void DragOver(object? sender, DragEventArgs e)
    {
        if (e.DataTransfer.TryGetFiles() is not null)
            e.DragEffects = DragDropEffects.Copy;
        else
            e.DragEffects = DragDropEffects.None;
    }

    private void Drop(object? sender, DragEventArgs e)
    {
        var overlay = this.FindControl<Border>("DragOverlay");
        if (overlay != null) overlay.IsVisible = false;

        var files = e.DataTransfer.TryGetFiles();
        if (files is not null)
        {
            if (DataContext is FileExplorerViewModel vm)
            {
                var filePaths = files.Select(x => x.TryGetLocalPath()).Where(x => x != null).Cast<string>().ToList();
                if (filePaths.Any() && vm.UploadFilesCommand.CanExecute(filePaths))
                {
                    vm.UploadFilesCommand.Execute(filePaths);
                }
            }
        }
    }

    private void OnRowDoubleTapped(object? sender, TappedEventArgs e)
    {
        // Tablodaki bir satıra çift tıklandığında eğer bu bir klasörse içine gir (Navigate)
        if (sender is DataGrid grid && grid.SelectedItem is CloudFileEntity file)
        {
            if (DataContext is FileExplorerViewModel vm && vm.OpenFolderCommand.CanExecute(file))
                vm.OpenFolderCommand.Execute(file);
        }
    }

    private void OnListBoxDoubleTapped(object? sender, TappedEventArgs e)
    {
        // Izgara görünümündeki bir karta çift tıklandığında eğer bu bir klasörse içine gir
        if (sender is ListBox listBox && listBox.SelectedItem is CloudFileEntity file)
        {
            if (DataContext is FileExplorerViewModel vm && vm.OpenFolderCommand.CanExecute(file))
                vm.OpenFolderCommand.Execute(file);
        }
    }
}
