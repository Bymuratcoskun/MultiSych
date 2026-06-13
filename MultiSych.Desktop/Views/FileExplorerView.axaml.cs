using System.Linq;
using Avalonia.Controls;
using Avalonia.Input;
using MultiSych.Desktop.ViewModels;
using MultiSych.Services.Data.Entities;

namespace MultiSych.Desktop.Views;

public partial class FileExplorerView : UserControl
{
    public FileExplorerView()
    {
        InitializeComponent();
        AddHandler(DragDrop.DropEvent, Drop);
        AddHandler(DragDrop.DragOverEvent, DragOver);
    }

    private void DragOver(object? sender, DragEventArgs e)
    {
        if (e.Data.Contains(DataFormats.Files))
            e.DragEffects = DragDropEffects.Copy;
        else
            e.DragEffects = DragDropEffects.None;
    }

    private void Drop(object? sender, DragEventArgs e)
    {
        if (e.Data.Contains(DataFormats.Files))
        {
            var files = e.Data.GetFiles();
            if (files != null && DataContext is FileExplorerViewModel vm)
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
}
