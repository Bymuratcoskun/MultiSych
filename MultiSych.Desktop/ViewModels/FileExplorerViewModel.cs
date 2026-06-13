using System;
using System.Collections.ObjectModel;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using Avalonia.Threading;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MultiSych.Services.Data;
using MultiSych.Services.Data.Entities;
using MultiSych.Services.Interfaces;

namespace MultiSych.Desktop.ViewModels;

public class FileExplorerViewModel : ViewModelBase
{
    private readonly IServiceScopeFactory _scopeFactory;
    private string _currentPath = "/";
    private string _searchQuery = string.Empty;
    private string? _selectedAccountId;
    private bool _isLoading;

    public ObservableCollection<CloudFileEntity> Files { get; } = new();
    public ObservableCollection<Services.Models.AccountCredentials> Accounts { get; } = new();

    public FileExplorerViewModel(IServiceScopeFactory scopeFactory, IAccountStore accountStore)
    {
        _scopeFactory = scopeFactory;
        NavigateUpCommand = new RelayCommand(async _ => await NavigateUpAsync(), _ => CurrentPath != "/");
        RefreshCommand = new RelayCommand(async _ => await LoadFilesAsync());
        OpenFolderCommand = new RelayCommand<CloudFileEntity>(async file => 
        {
            if (file != null && file.IsDirectory)
            {
                CurrentPath = file.Path;
                await LoadFilesAsync();
            }
        });

        UploadFilesCommand = new RelayCommand<List<string>>(async paths => await UploadFilesAsync(paths), paths => !IsLoading && !string.IsNullOrEmpty(SelectedAccountId));

        Task.Run(async () =>
        {
            var accounts = await accountStore.GetAccountsAsync();
            Dispatcher.UIThread.Post(() => {
                foreach (var acc in accounts) Accounts.Add(acc);
                SelectedAccountId = Accounts.FirstOrDefault()?.AccountId;
            });
        });
    }

    public string CurrentPath
    {
        get => _currentPath;
        set { if (SetProperty(ref _currentPath, value)) (NavigateUpCommand as RelayCommand)?.RaiseCanExecuteChanged(); }
    }

    public string SearchQuery
    {
        get => _searchQuery;
        set { if (SetProperty(ref _searchQuery, value)) Task.Run(LoadFilesAsync); }
    }

    public string? SelectedAccountId
    {
        get => _selectedAccountId;
        set { if (SetProperty(ref _selectedAccountId, value)) { CurrentPath = "/"; Task.Run(LoadFilesAsync); } }
    }

    public bool IsLoading { get => _isLoading; set => SetProperty(ref _isLoading, value); }

    public ICommand NavigateUpCommand { get; }
    public ICommand RefreshCommand { get; }
    public ICommand OpenFolderCommand { get; }
    public ICommand UploadFilesCommand { get; }

    private async Task LoadFilesAsync()
    {
        if (string.IsNullOrEmpty(SelectedAccountId)) return;

        IsLoading = true;
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<LocalCacheDbContext>();

            List<CloudFileEntity> files;

            // Eğer arama yapılıyorsa klasör hiyerarşisini (ParentId) göz ardı et ve tümünde ara
            if (!string.IsNullOrWhiteSpace(SearchQuery))
            {
                files = await dbContext.CloudFiles
                    .Where(f => f.AccountId == SelectedAccountId && EF.Functions.Like(f.FileName, $"%{SearchQuery}%"))
                    .OrderByDescending(f => f.IsDirectory).ThenBy(f => f.FileName).ToListAsync();
            }
            else
            {
                // Arama yoksa standart klasör gezinme modunu kullan
                string? parentId = CurrentPath != "/" 
                    ? (await dbContext.CloudFiles.FirstOrDefaultAsync(f => f.AccountId == SelectedAccountId && f.Path == CurrentPath && f.IsDirectory))?.FileId 
                    : null;

                files = await dbContext.CloudFiles
                    .Where(f => f.AccountId == SelectedAccountId && f.ParentId == parentId)
                    .OrderByDescending(f => f.IsDirectory).ThenBy(f => f.FileName).ToListAsync();
            }
            Dispatcher.UIThread.Post(() => { Files.Clear(); foreach (var file in files) Files.Add(file); });
        }
        finally { IsLoading = false; }
    }

    private async Task NavigateUpAsync()
    {
        if (CurrentPath == "/") return;
        var lastSlash = CurrentPath.LastIndexOf('/');
        CurrentPath = lastSlash <= 0 ? "/" : CurrentPath.Substring(0, lastSlash);
        await LoadFilesAsync();
    }

    private async Task UploadFilesAsync(List<string>? filePaths)
    {
        if (filePaths == null || !filePaths.Any() || string.IsNullOrEmpty(SelectedAccountId)) return;

        IsLoading = true;
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var storageService = scope.ServiceProvider.GetRequiredService<IStorageService>();
            var accountStore = scope.ServiceProvider.GetRequiredService<IAccountStore>();
            var appStatusService = scope.ServiceProvider.GetRequiredService<MultiSych.Desktop.Services.IAppStatusService>();
            var dbContext = scope.ServiceProvider.GetRequiredService<LocalCacheDbContext>();

            var account = await accountStore.GetAccountAsync(SelectedAccountId);
            if (account == null) return;

            string folderId = "root";
            if (CurrentPath != "/")
            {
                var parentDir = await dbContext.CloudFiles.FirstOrDefaultAsync(f => f.AccountId == SelectedAccountId && f.Path == CurrentPath && f.IsDirectory);
                if (parentDir != null) folderId = parentDir.FileId;
            }

            foreach (var path in filePaths)
            {
                if (string.IsNullOrEmpty(path)) continue;
                appStatusService.PostUpdate($"Yükleniyor: {System.IO.Path.GetFileName(path)}", true);
                await storageService.UploadFileAsync(account, path, folderId);
            }

            appStatusService.PostUpdate("Yükleme tamamlandı. Liste güncelleniyor...", true);
            
            // Buluttaki değişiklikleri yerel önbelleğe çekip arayüzü güncelle
            await storageService.ListFilesAsync(account, folderId);
            await LoadFilesAsync();
            appStatusService.PostUpdate("Dosyalar güncellendi.", false);
        }
        catch (Exception ex)
        {
            using var scope = _scopeFactory.CreateScope();
            var appStatusService = scope.ServiceProvider.GetRequiredService<MultiSych.Desktop.Services.IAppStatusService>();
            appStatusService.PostUpdate($"Yükleme hatası: {ex.Message}", false);
        }
        finally
        {
            IsLoading = false;
        }
    }
}
