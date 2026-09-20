using System;
using System.Threading.Tasks;

namespace MultiSych.Desktop.Services;

public interface IWindowService
{
    void ShowAIChat(string provider);
    void ShowAddAccountDialog();
    Task<bool> ShowConfirmationDialogAsync(string message);
    Task<string?> OpenFileDialogAsync(string title, string[]? extensions = null);
    Task<string?> SaveFileDialogAsync(string title, string defaultExtension);
    void ShowNotification(string title, string message, NotificationSound sound = NotificationSound.Default);
    Task ShowMessageDialogAsync(string title, string message);
    void ShowNewEmailDialog(string? defaultAccountId = null, string? to = null, string? subject = null, string? body = null, System.Collections.Generic.List<MultiSych.Services.Data.CloudFileEntity>? initialAttachments = null);
}

