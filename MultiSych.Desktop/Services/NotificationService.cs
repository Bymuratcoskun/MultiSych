using Avalonia.Threading;
using MultiSych.Services.Interfaces;
using MultiSych.Desktop.Views;

namespace MultiSych.Desktop.Services;

public class NotificationService : INotificationService
{
    public void ShowNotification(string title, string message, string type = "Info")
    {
        Dispatcher.UIThread.Post(() =>
        {
            var soundType = type switch
            {
                "Success" => NotificationSound.Success,
                "Error" => NotificationSound.Error,
                "Email" => NotificationSound.Email,
                "Event" => NotificationSound.Event,
                _ => NotificationSound.Default
            };
            
            var toast = new ToastNotificationWindow(title, message, soundType);
            toast.Show();
        });
    }
}