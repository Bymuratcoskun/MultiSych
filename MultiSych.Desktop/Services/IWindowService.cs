using System;
using System.Threading.Tasks;

namespace MultiSych.Desktop.Services;

public interface IWindowService
{
    void ShowAIChat(string provider);
    void ShowAddAccountDialog();
    Task<bool> ShowConfirmationDialogAsync(string message);
    Task<string?> OpenFileDialogAsync(string title, string[]? extensions = null);
    void ShowNotification(string title, string message, NotificationSound sound = NotificationSound.Default);
}
