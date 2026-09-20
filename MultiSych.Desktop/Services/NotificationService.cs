using System;
using Microsoft.Extensions.DependencyInjection;
using MultiSych.Services.Interfaces;

namespace MultiSych.Desktop.Services;

public class NotificationService : INotificationService
{
    public void ShowNotification(string title, string message, string type = "Info")
    {
        var soundType = type switch
        {
            "Success" => NotificationSound.Success,
            "Error" => NotificationSound.Error,
            "Email" => NotificationSound.Email,
            "Event" => NotificationSound.Event,
            _ => NotificationSound.Default
        };
        
        var windowService = Program.ServiceProvider.GetRequiredService<IWindowService>();
        windowService.ShowNotification(title, message, soundType);
    }
}