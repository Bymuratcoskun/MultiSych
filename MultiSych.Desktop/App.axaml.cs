using System;
using System.IO;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Styling;
using Microsoft.Extensions.DependencyInjection;
using MultiSych.Desktop.ViewModels;
using MultiSych.Desktop.Views;
using MultiSych.Desktop.Services;
using MultiSych.Services.Security;
using Squirrel;

namespace MultiSych.Desktop;

public partial class App : Application
{
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override async void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            // Arka planda güncelleme kontrolünü başlat
            _ = Task.Run(CheckForUpdates);

            var encryptStorage = string.Equals(Environment.GetEnvironmentVariable("MULTISYCH_ENCRYPT_STORAGE"), "true", StringComparison.OrdinalIgnoreCase);
            var storagePassword = Environment.GetEnvironmentVariable("MULTISYCH_STORAGE_PASSWORD");

            var envPath = Path.Combine(Directory.GetCurrentDirectory(), ".env");

            if (!File.Exists(envPath) || (encryptStorage && string.IsNullOrWhiteSpace(storagePassword)))
            {
                var setupWindow = new SetupWindow();
                var passwordWasSet = false;
                setupWindow.DataContext = new SetupViewModel(result =>
                {
                    passwordWasSet = result;
                    setupWindow.Close(result);
                });

                var tcs = new TaskCompletionSource();
                setupWindow.Closed += (s, e) => tcs.SetResult();
                setupWindow.Show();
                await tcs.Task;

                if (!passwordWasSet)
                {
                    desktop.Shutdown();
                    return;
                }
                
                // Şifre ayarlandıktan sonra, veritabanı bağlantısının yeni şifreyle kurulabilmesi için
                // uygulamanın yeniden başlatılması gerektiğini kullanıcıya bildiriyoruz.
                var msgBox = new MessageBox.Avalonia.MessageBoxManager.MessageBoxStandardWindow("Kurulum Tamamlandı", "Ana şifre başarıyla ayarlandı. Lütfen uygulamayı yeniden başlatın.");
                await msgBox.Show();
                desktop.Shutdown();
                return;
            }

            // Ana pencereyi oluştur ve göster
            desktop.MainWindow = new MainWindow
            {
                DataContext = Program.ServiceProvider.GetRequiredService<MainWindowViewModel>()
            };
        }

        base.OnFrameworkInitializationCompleted();
    }

    private async Task CheckForUpdates()
    {
        try
        {
            // ÖNEMLİ: Bu URL'yi kendi GitHub projenizin "Releases" bölümünün URL'si ile değiştirin.
            const string updateUrl = "https://github.com/yourusername/MultiSych";

            using (var mgr = await UpdateManager.GitHubUpdateManager(updateUrl))
            {
                var release = await mgr.UpdateApp();
                if (release != null)
                {
                    var windowService = Program.ServiceProvider.GetRequiredService<IWindowService>();
                    windowService.ShowNotification("Güncelleme Hazır", $"MultiSych v{release.Version} indirildi. Yeniden başlatın.", NotificationSound.Success);
                }
            }
        }
        catch (Exception ex)
        {
            // Güncelleme kontrolü başarısız olursa uygulamanın çökmesini engelle, sadece logla.
            Serilog.Log.Warning(ex, "Failed to check for updates.");
        }
    }

    public static void ApplyTheme(string themeName)
    {
        if (Current?.Styles[0] is not FluentTheme fluentTheme) return;
        
        fluentTheme.Mode = themeName switch
        {
            "Sade" => FluentThemeMode.Light,
            _ => FluentThemeMode.Dark
        };
    }

    public static void ApplyLanguage(string culture)
    {
        // TODO: Implement resource dictionary switching for localization.
    }
}