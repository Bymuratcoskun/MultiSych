using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Interactivity;
using Avalonia.Threading;
using MultiSych.Desktop.Services;

namespace MultiSych.Desktop.Views;

public partial class ToastNotificationWindow : Window
{
    private DispatcherTimer? _timer;
    private static readonly List<ToastNotificationWindow> ActiveToasts = [];

    public ToastNotificationWindow()
    {
        InitializeComponent();
    }

    // Parametreli Constructor: Başlık, Mesaj ve Ekranda Kalma Süresi (Saniye)
    public ToastNotificationWindow(string title, string message, NotificationSound soundType = NotificationSound.Default, int durationSeconds = 5) : this()
    {
        var titleText = this.FindControl<TextBlock>("TitleText");
        var messageText = this.FindControl<TextBlock>("MessageText");
        
        if (titleText != null) titleText.Text = title;
        if (messageText != null) messageText.Text = message;

        var iconText = this.FindControl<TextBlock>("IconText");
        if (iconText != null)
        {
            iconText.Text = soundType switch
            {
                NotificationSound.Email => "📧",
                NotificationSound.Event => "⏰",
                NotificationSound.Success => "✅",
                NotificationSound.Error => "❌",
                _ => "🔔"
            };
        }

        // Otomatik kapanış için DispatcherTimer
        _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(durationSeconds) };
        _timer.Tick += async (s, e) => await CloseToastAsync();
        _timer.Start();

        // Bildirim penceresi açılırken ses çal
        SoundPlayerService.PlayNotificationSound(soundType);

        this.Opened += (s, e) => this.Opacity = 1; // Açıldığında yavaşça belir (Fade-in)

        ActiveToasts.Add(this);
        RearrangeToasts();
    }

    private static void RearrangeToasts()
    {
        // Aktif olan tüm bildirimleri sırayla yukarıya doğru yığ (Stacking)
        
        if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop && desktop.MainWindow != null)
        {
            var screens = desktop.MainWindow.Screens;
            var workingArea = screens.Primary?.WorkingArea ?? new PixelRect(0, 0, 1920, 1080);
        
            for (int i = 0; i < ActiveToasts.Count; i++)
            {
                var toast = ActiveToasts[i];
                var x = workingArea.Right - toast.Width - 10;
                var y = workingArea.Bottom - ((toast.Height + 10) * (i + 1));
                toast.Position = new PixelPoint((int)x, (int)y);
            }
        }
    }

    private async void CloseButton_Click(object? sender, RoutedEventArgs e) => await CloseToastAsync();

    private async Task CloseToastAsync()
    {
        _timer?.Stop();
        this.Opacity = 0; // Kapanmadan önce yavaşça kaybol (Fade-out)
        await Task.Delay(500); // Animasyon süresi (0.5 sn) bitene kadar bekle
        
        ActiveToasts.Remove(this);
        RearrangeToasts(); // Kapanan bildirimin altındaki boşluğu doldur
        Close();
    }
}