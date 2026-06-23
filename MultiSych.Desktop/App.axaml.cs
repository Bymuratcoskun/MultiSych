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

            // Masaüstü açıldığında kullanıcının dil ve tema tercihlerini uygula
            var userSettingsService = Program.ServiceProvider.GetRequiredService<IUserSettingsService>();
            var userSettings = userSettingsService.Settings;
            if (userSettings != null)
            {
                ApplyLanguage(userSettings.Language == "Türkçe" ? "tr-TR" : "en-US");
                ApplyTheme(userSettings.Theme);
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

        if (themeName == "Retro")
        {
            Current.Resources["MainBgBrush"] = new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.Parse("#000000"));
            Current.Resources["CardBgBrush"] = new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.Parse("#0B0B0B"));
            Current.Resources["TextBrush"] = new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.Parse("#33FF33"));
            Current.Resources["SubTextBrush"] = new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.Parse("#00FF00"));
            Current.Resources["BorderBrush"] = new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.Parse("#33FF33"));
            Current.Resources["SidebarBgBrush"] = new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.Parse("#050505"));
            Current.Resources["AccentBrush"] = new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.Parse("#33FF33"));
            Current.Resources["AccentTextBrush"] = new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.Parse("#000000"));
            Current.Resources["SecondaryButtonBgBrush"] = new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.Parse("#111111"));
        }
        else if (themeName == "Modern")
        {
            Current.Resources["MainBgBrush"] = new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.Parse("#0F0F1A"));
            Current.Resources["CardBgBrush"] = new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.Parse("#1A1A2E"));
            Current.Resources["TextBrush"] = new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.Parse("#FFFFFF"));
            Current.Resources["SubTextBrush"] = new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.Parse("#8B8B9E"));
            Current.Resources["BorderBrush"] = new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.Parse("#2D2D44"));
            Current.Resources["SidebarBgBrush"] = new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.Parse("#0A0A12"));
            Current.Resources["AccentBrush"] = new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.Parse("#7F5AFA"));
            Current.Resources["AccentTextBrush"] = new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.Parse("#FFFFFF"));
            Current.Resources["SecondaryButtonBgBrush"] = new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.Parse("#252538"));
        }
        else
        {
            Current.Resources.Remove("MainBgBrush");
            Current.Resources.Remove("CardBgBrush");
            Current.Resources.Remove("TextBrush");
            Current.Resources.Remove("SubTextBrush");
            Current.Resources.Remove("BorderBrush");
            Current.Resources.Remove("SidebarBgBrush");
            Current.Resources.Remove("AccentBrush");
            Current.Resources.Remove("AccentTextBrush");
            Current.Resources.Remove("SecondaryButtonBgBrush");
        }
    }

    public static void ApplyLanguage(string culture)
    {
        if (Current == null) return;

        // Örn: culture değişkeni "tr-TR", "en-US" vb. değerler alabilir
        var resourceUri = new Uri($"avares://MultiSych.Desktop/{culture}.axaml");
        
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
