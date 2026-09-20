using System;
using System.Threading.Tasks;
using Gtk;
using Adw;
using Gio;
using Microsoft.Extensions.DependencyInjection;
using MultiSych.Desktop.Views;
using MultiSych.Desktop.ViewModels;
using Task = System.Threading.Tasks.Task;

namespace MultiSych.Desktop.Services;

public class WindowService(IServiceProvider serviceProvider) : IWindowService
{
    public void ShowAddAccountDialog()
    {
        GLib.Functions.IdleAdd(0, () =>
        {
            if (MainWindow.Instance != null)
            {
                var dialog = new AddAccountWindow(MainWindow.Instance)
                {
                    DataContext = serviceProvider.GetRequiredService<AddAccountViewModel>()
                };
                dialog.Present();
            }
            return false;
        });
    }

    public void ShowAIChat(string provider)
    {
        GLib.Functions.IdleAdd(0, () =>
        {
            if (MainWindow.Instance != null)
            {
                var vm = new AIChatViewModel(provider, serviceProvider);
                var dialog = new AIChatWindow(MainWindow.Instance, vm);
                dialog.Present();
            }
            return false;
        });
    }

    public async Task<bool> ShowConfirmationDialogAsync(string message)
    {
        try
        {
            var dialog = new Gtk.AlertDialog
            {
                Message = "Onay",
                Detail = message,
                Buttons = new string[] { "Evet", "Hayır" },
                DefaultButton = 0,
                CancelButton = 1
            };
            var result = await dialog.ChooseAsync(MainWindow.Instance);
            return result == 0;
        }
        catch (Exception ex)
        {
            Serilog.Log.Error(ex, "ShowConfirmationDialogAsync failed");
            return false;
        }
    }

    public async Task<string?> OpenFileDialogAsync(string title, string[]? extensions = null)
    {
        try
        {
            var dialog = new Gtk.FileDialog();
            dialog.SetTitle(title);
            var file = await dialog.OpenAsync(MainWindow.Instance);
            return file?.GetPath();
        }
        catch (Exception ex)
        {
            Serilog.Log.Error(ex, "OpenFileDialogAsync failed");
            return null;
        }
    }

    public async Task<string?> SaveFileDialogAsync(string title, string defaultExtension)
    {
        try
        {
            var dialog = new Gtk.FileDialog();
            dialog.SetTitle(title);
            var file = await dialog.SaveAsync(MainWindow.Instance);
            return file?.GetPath();
        }
        catch (Exception ex)
        {
            Serilog.Log.Error(ex, "SaveFileDialogAsync failed");
            return null;
        }
    }

    public void ShowNotification(string title, string message, NotificationSound sound = NotificationSound.Default)
    {
        GLib.Functions.IdleAdd(0, () =>
        {
            try
            {
                var notification = Gio.Notification.New(title);
                notification.SetBody(message);
                
                var app = MainWindow.Instance?.Application;
                if (app != null)
                {
                    app.SendNotification($"multisych-{Guid.NewGuid():N}", notification);
                }
                else
                {
                    Serilog.Log.Warning("ShowNotification: MainWindow.Instance.Application null, bildirim gönderilemedi");
                }
            }
            catch (Exception ex)
            {
                Serilog.Log.Error(ex, "Failed to show notification");
            }
            return false;
        });
    }

    public async Task ShowMessageDialogAsync(string title, string message)
    {
        try
        {
            var dialog = new Gtk.AlertDialog
            {
                Message = title,
                Detail = message,
                Buttons = new string[] { "Tamam" }
            };
            await dialog.ChooseAsync(MainWindow.Instance);
        }
        catch (Exception ex)
        {
            Serilog.Log.Error(ex, "ShowMessageDialogAsync failed");
        }
    }

    public void ShowNewEmailDialog(string? defaultAccountId = null, string? to = null, string? subject = null, string? body = null, System.Collections.Generic.List<MultiSych.Services.Data.CloudFileEntity>? initialAttachments = null)
    {
        GLib.Functions.IdleAdd(0, () =>
        {
            if (MainWindow.Instance != null)
            {
                var vm = serviceProvider.GetRequiredService<NewEmailViewModel>();
                if (!string.IsNullOrEmpty(defaultAccountId)) vm.SelectedAccountId = defaultAccountId;
                if (!string.IsNullOrEmpty(to)) vm.ToAddress = to;
                if (!string.IsNullOrEmpty(subject)) vm.Subject = subject;
                if (!string.IsNullOrEmpty(body)) vm.BodyText = body;
                
                if (initialAttachments != null)
                {
                    foreach (var file in initialAttachments)
                    {
                        vm.Attachments.Add(new EmailAttachmentViewModel
                        {
                            FileId = file.FileId,
                            FileName = file.FileName,
                            MimeType = file.MimeType,
                            Size = file.FileSize
                        });
                    }
                }

                var dialog = new NewEmailWindow(MainWindow.Instance, vm);
                dialog.Present();
            }
            return false;
        });
    }
}
