using System;
using System.Threading.Tasks;
using Avalonia.Controls.Notifications;

namespace MultiSych.Desktop.Services;

public interface IWindowService
{
    void ShowAIChat(string provider);
    Task<bool> ShowConfirmationDialogAsync(string message);
    Task<string?> OpenFileDialogAsync(string title, string[]? extensions = null);
    void ShowNotification(string title, string message, NotificationType type = NotificationType.Information);
}
