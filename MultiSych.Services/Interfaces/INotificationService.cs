namespace MultiSych.Services.Interfaces;

public interface INotificationService
{
    void ShowNotification(string title, string message, string type = "Info");
}