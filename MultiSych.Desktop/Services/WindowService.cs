using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Controls.Notifications;
using Avalonia.Platform.Storage;
using MultiSych.Desktop.ViewModels;
using MultiSych.Desktop.Views;

namespace MultiSych.Desktop.Services;

public class WindowService : IWindowService
{
    private readonly IServiceProvider _services;
    private WindowNotificationManager? _notificationManager;

    public WindowService(IServiceProvider services)
    {
        _services = services;
    }

    public void ShowAIChat(string provider)
    {
        var window = new AIChatWindow(provider, _services);
        window.Show();
    }

    public async Task<bool> ShowConfirmationDialogAsync(string message)
    {
        if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop && desktop.MainWindow != null)
        {
            var dialog = new ConfirmationDialog();
            return await dialog.ShowAsync(desktop.MainWindow, message);
        }
        
        return false; // Ana pencere bulunamazsa varsayılan olarak işlemi iptal et
    }

    public async Task<string?> OpenFileDialogAsync(string title, string[]? extensions = null)
    {
        if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop && desktop.MainWindow != null)
        {
            var topLevel = TopLevel.GetTopLevel(desktop.MainWindow);
            if (topLevel != null)
            {
                var options = new FilePickerOpenOptions { Title = title, AllowMultiple = false };
                if (extensions != null && extensions.Length > 0)
                {
                    options.FileTypeFilter = new List<FilePickerFileType>
                    {
                        new FilePickerFileType("Supported Files") { Patterns = extensions }
                    };
                }

                var files = await topLevel.StorageProvider.OpenFilePickerAsync(options);
                if (files.Count > 0)
                    return files[0].Path.LocalPath;
            }
        }
        return null;
    }

    public void ShowNotification(string title, string message, NotificationType type = NotificationType.Information)
    {
        if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop && desktop.MainWindow != null)
        {
            if (_notificationManager == null)
            {
                var topLevel = TopLevel.GetTopLevel(desktop.MainWindow);
                if (topLevel != null)
                {
                    _notificationManager = new WindowNotificationManager(topLevel) { Position = NotificationPosition.BottomRight, MaxItems = 3 };
                }
            }
            
            _notificationManager?.Show(new Notification(title, message, type));
        }
    }
}
