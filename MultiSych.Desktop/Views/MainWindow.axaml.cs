using System;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Threading;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MultiSych.Services.Data;
using MultiSych.Services.Interfaces;

namespace MultiSych.Desktop.Views;

public partial class MainWindow : Window
{
    private DispatcherTimer? _ramTimer;
    private DispatcherTimer? _badgeTimer;
    private readonly IUserSettingsService _userSettingsService;
    private bool _reallyExit = false;
    private bool _skipExitConfirmation = false;

    public MainWindow()
    {
        InitializeComponent();
        _userSettingsService = Program.ServiceProvider.GetRequiredService<IUserSettingsService>();

        // Her 2 saniyede bir RAM kullanımını ekranda güncelleyen sayaç
        _ramTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
        _ramTimer.Tick += (s, e) => UpdateRamUsage();
        _ramTimer.Start();

        // Her 1 dakikada bir TrayIcon Tooltip (Rozet) güncelleyen sayaç
        _badgeTimer = new DispatcherTimer { Interval = TimeSpan.FromMinutes(1) };
        _badgeTimer.Tick += async (s, e) => await UpdateTrayBadgeAsync();
        _badgeTimer.Start();

        // İlk açılışta rozeti güncelle
        _ = UpdateTrayBadgeAsync();

        // Kaydedilmiş "Çıkışta onay sorma" tercihini UserSettings üzerinden yükle
        _skipExitConfirmation = _userSettingsService.Settings.SkipExitConfirmation;

        if (_userSettingsService.Settings.StartMinimized)
        {
            this.WindowState = WindowState.Minimized;
            this.Hide();
        }
    }

    private void UpdateRamUsage()
    {
        var ramMB = Environment.WorkingSet / (1024 * 1024);
        var textBlock = this.FindControl<TextBlock>("RamUsageText");
        if (textBlock != null) textBlock.Text = $"{ramMB} MB";
    }

    private async Task UpdateTrayBadgeAsync()
    {
        try
        {
            // Veritabanı sorgusunu UI thread'ini dondurmamak için arka planda (Task) çalıştırıyoruz
            await Task.Run(async () =>
            {
                var scopeFactory = Program.ServiceProvider.GetRequiredService<IServiceScopeFactory>();
                using var scope = scopeFactory.CreateScope();
                var dbContext = scope.ServiceProvider.GetRequiredService<LocalCacheDbContext>();
                
                var now = DateTime.UtcNow;
                var upcomingLimit = now.AddHours(24); // Önümüzdeki 24 saat
                
                // Yaklaşan etkinlikleri ve yeni/işlenmemiş e-postaları say
                var eventCount = await dbContext.CachedEvents.CountAsync(ev => ev.StartTime > now && ev.StartTime <= upcomingLimit);
                var newEmails = await dbContext.CachedEmails.CountAsync(em => !em.IsRead);

                // UI işlemini Dispatcher ile ana thread'e geri yolluyoruz
                Dispatcher.UIThread.Post(() => 
                {
                    var trayIcon = TrayIcon.GetIcons(this)?.FirstOrDefault();
                    if (trayIcon != null)
                    {
                        if (eventCount > 0 || newEmails > 0)
                            trayIcon.ToolTipText = $"MultiSych | {newEmails} Yeni E-Posta, {eventCount} Yaklaşan Etkinlik";
                        else
                            trayIcon.ToolTipText = "MultiSych | Senkronize ve Güncel";
                    }
                });
            });
        }
        catch
        {
            // Uygulama kapanırken veya veritabanı kilitliyken oluşan anlık hataları yoksay
        }
    }

    private async void Window_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        if (!_reallyExit)
        {
            e.Cancel = true; // Kapanmayı durdur
            
            if (!this.IsVisible) return; // Pencere zaten gizliyse dialog çıkarma

            if (_skipExitConfirmation)
            {
                this.Hide();
                return;
            }

            var dialog = new ConfirmationDialog();
            var result = await dialog.ShowAsync(this, "MultiSych arka planda çalışmaya ve senkronizasyon yapmaya devam edecektir.\n\nPencereyi gizlemek istediğinize emin misiniz?");
            
            if (result)
            {
                if (dialog.DontShowAgain)
                {
                    _skipExitConfirmation = true;
                    _ = SaveSkipExitSettingAsync(); // Ayarı kalıcı olarak kaydet
                }
                this.Hide(); // Kullanıcı onaylarsa gizle
            }
        }
    }

    private async Task SaveSkipExitSettingAsync()
    {
        try
        {
            _userSettingsService.Settings.SkipExitConfirmation = true;
            await _userSettingsService.SaveAsync();
        }
        catch (Exception ex)
        {
            Serilog.Log.Error(ex, "Failed to save 'skip exit confirmation' setting.");
        }
    }

    private void TrayIcon_Show_Click(object? sender, EventArgs e)
    {
        this.Show();
        this.WindowState = WindowState.Normal;
        this.Activate();
    }

    private void TrayIcon_Settings_Click(object? sender, EventArgs e)
    {
        TrayIcon_Show_Click(sender, e); // Önce pencereyi görünür yapıyoruz
        if (DataContext is ViewModels.MainWindowViewModel vm)
        {
            vm.SelectedSection = "Settings"; // Ayarlar sekmesini aktifleştir
        }
    }

    private void TrayIcon_Sync_Click(object? sender, EventArgs e)
    {
        TrayIcon_Show_Click(sender, e);
        if (DataContext is ViewModels.MainWindowViewModel vm)
        {
            vm.SelectedSection = "Sync";
            // Senkronizasyon sekmesine geçip doğrudan arka plan işlemini tetikleyebiliriz
            vm.SyncPage.TriggerSyncCommand.Execute(null);
        }
    }

    private void TrayIcon_Exit_Click(object? sender, EventArgs e)
    {
        _reallyExit = true;
        this.Close();
    }
}
