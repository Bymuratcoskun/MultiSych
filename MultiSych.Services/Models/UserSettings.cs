namespace MultiSych.Services.Models;

public class UserSettings
{
    public bool SkipExitConfirmation { get; set; } = false;
    public bool EnableNotificationSounds { get; set; } = true;
    public bool StartMinimized { get; set; } = false;
    public string Language { get; set; } = "English";
    public string Theme { get; set; } = "Modern";
}