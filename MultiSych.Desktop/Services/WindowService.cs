using System;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Controls.Notifications;
using Microsoft.Extensions.DependencyInjection;
using MultiSych.Desktop.Views;
using MultiSych.Desktop.ViewModels;

namespace MultiSych.Desktop.Services;

public class WindowService(IServiceProvider serviceProvider) : IWindowService
{
    public void ShowAddAccountDialog()
    {
        if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop && desktop.MainWindow != null)
        {
            var dialog = new AddAccountWindow
            {
                DataContext = serviceProvider.GetRequiredService<AddAccountViewModel>()
            };
            
            // Pencereyi ana pencerenin üzerinde "Dialog (Modal)" olarak açar
            dialog.ShowDialog(desktop.MainWindow);
        }
    }

    public void ShowAIChat(string provider)
    {
        // AI Chat ekranı MainWindow içerisine gömülü olduğu için bu metot ileride ayrı pencere yapmak istenirse diye bırakıldı.
    }

    public Task<bool> ShowConfirmationDialogAsync(string message)
    {
        // Şimdilik doğrulama kutusu gerçek bir kullanıcı interaksiyonu yerine her zaman onay döndürür.
        return Task.FromResult(true);
    }

    public async Task<string?> OpenFileDialogAsync(string title, string[]? extensions = null)
    {
        if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop && desktop.MainWindow != null)
        {
            var options = new Avalonia.Platform.Storage.FilePickerOpenOptions
            {
                Title = title,
                AllowMultiple = false
            };

            if (extensions != null && extensions.Length > 0)
            {
                var filter = new Avalonia.Platform.Storage.FilePickerFileType("Allowed Files") { Patterns = extensions };
                options.FileTypeFilter = [filter];
            }

            var files = await desktop.MainWindow.StorageProvider.OpenFilePickerAsync(options);
            if (files != null && files.Count > 0)
                return files[0].Path.LocalPath;
        }

        return null;
    }

#pragma warning disable CA1822 // Member does not access instance data and can be marked as static
    public async Task<string?> SaveFileDialogAsync(string title, string defaultExtension)
    {
        if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop && desktop.MainWindow != null)
        {
            var options = new Avalonia.Platform.Storage.FilePickerSaveOptions
            {
                Title = title,
                DefaultExtension = defaultExtension,
                SuggestedFileName = $"MultiSych_Export_{DateTime.Now:yyyyMMdd}.{defaultExtension}"
            };

            var file = await desktop.MainWindow.StorageProvider.SaveFilePickerAsync(options);
            return file?.Path.LocalPath;
        }
        return null;
    }
#pragma warning restore CA1822

    public void ShowNotification(string title, string message, NotificationSound sound = NotificationSound.Default)
    {
        // Yalnızca Avalonia'nın ana UI thread'inde (Dispatcher) çalışmasını garanti altına alıyoruz
        Avalonia.Threading.Dispatcher.UIThread.Post(() =>
        {
            // Modern ve animasyonlu Toast penceremizi, ses türüyle birlikte çağırıyoruz
            var toast = new ToastNotificationWindow(title, message, sound, durationSeconds: 7);
            toast.Show();
        });
    }
}
