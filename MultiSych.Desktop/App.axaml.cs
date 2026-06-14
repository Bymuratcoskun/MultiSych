using System;
using System.IO;
using System.Threading.Tasks;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Styling;
using Avalonia.Markup.Xaml.Styling;
using Microsoft.Extensions.DependencyInjection;
using MultiSych.Services.Configuration;
using MultiSych.Desktop.ViewModels;
using MultiSych.Desktop.Views;
using MultiSych.Desktop.Services;
using MultiSych.Services.Interfaces;
using Avalonia.Themes.Fluent;
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
                var msgDialog = new MessageDialog
                {
                    Title = "Kurulum Tamamlandı",
                    Message = "Ana şifre başarıyla ayarlandı. Lütfen uygulamayı yeniden başlatın."
                };

                var msgTcs = new TaskCompletionSource();
                msgDialog.Closed += (s, e) => msgTcs.SetResult();
                msgDialog.Show();
                await msgTcs.Task;

                desktop.Shutdown();
                return;
            }

            var config = Program.ServiceProvider.GetRequiredService<MultiSychConfig>();
            var secureStorage = Program.ServiceProvider.GetRequiredService<ISecureStorageService>();

            if (config.Security != null && (config.Security.RequireStartupPassword || config.Security.EnableTwoFactorAuth))
            {
                var rememberedUntilStr = await secureStorage.GetSecretAsync("REMEMBER_ME_UNTIL");
                bool skipAuth = false;
                if (DateTime.TryParse(rememberedUntilStr, out var rememberedUntil) && rememberedUntil > DateTime.UtcNow)
                {
                    skipAuth = true;
                }

                var isAuthenticated = skipAuth;

                if (!skipAuth)
                {
                    var authWindow = new AuthWindow();
                    authWindow.DataContext = new AuthViewModel(config, secureStorage, result =>
                    {
                        isAuthenticated = result;
                        authWindow.Close(result);
                    });

                    var authTcs = new TaskCompletionSource();
                    authWindow.Closed += (s, e) => authTcs.SetResult();
                    authWindow.Show();
                    await authTcs.Task;
                }

                if (!isAuthenticated)
                {
                    desktop.Shutdown();
                    return;
                }
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
#pragma warning disable CA1416
            var source = new Squirrel.Sources.GithubSource("https://github.com/Bymuratt/MultiSych", string.Empty, false);
            using var mgr = new UpdateManager(source);
            
            if (mgr.IsInstalledApp)
            {
                var release = await mgr.UpdateApp();
                if (release != null)
                {
                    var windowService = Program.ServiceProvider.GetRequiredService<Desktop.Services.IWindowService>();
                    windowService.ShowNotification("Güncelleme Hazır", $"MultiSych v{release.Version} indirildi. Yeniden başlatın.", NotificationSound.Success);
                }
            }
#pragma warning restore CA1416
        }
        catch (Exception ex)
        {
            Serilog.Log.Error(ex, "Failed to check for updates");
        }
    }

    public static void ApplyTheme(string themeName)
    {
        if (Current == null) return;
        
        Current.RequestedThemeVariant = themeName switch
        {
            "Sade" => ThemeVariant.Light,
            _ => ThemeVariant.Dark
        };
    }

    public static void ApplyLanguage(string culture)
    {
        // TODO: Implement resource dictionary switching for localization.
        if (Current == null) return;

        // Örn: culture değişkeni "de-DE", "es-ES", "en-US" vb. değerler alabilir
        var resourceUri = new Uri($"avares://MultiSych.Desktop/Assets/Languages/{culture}.axaml");
        
        try
        {
#pragma warning disable IL2026
            var dictionary = new ResourceInclude(resourceUri) { Source = resourceUri };
#pragma warning restore IL2026
            
            // Yüklü olan dil sözlüğünü bul (Eski dili tespit et)
            var existingDict = Current.Resources.MergedDictionaries
                .FirstOrDefault(x => x is ResourceInclude ri && ri.Source != null && ri.Source.AbsoluteUri.Contains("/Languages/"));

            // Eski dili kaldırıp yenisini ekleyerek tüm DynamicResource'ları anında güncelliyoruz
            if (existingDict != null)
            {
                Current.Resources.MergedDictionaries.Remove(existingDict);
            }
            
            Current.Resources.MergedDictionaries.Add(dictionary);
        }
        catch (Exception ex)
        {
            Serilog.Log.Error(ex, "Failed to load language dictionary for {Culture}", culture);
        }
    }
}
