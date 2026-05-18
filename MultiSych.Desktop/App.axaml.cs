using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using Avalonia.Styling;

namespace MultiSych.Desktop;

public partial class App : Application
{
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        // ViewModel ve View'ları otomatik eşleştiren Locator'ı sisteme kaydediyoruz
        DataTemplates.Add(new ViewLocator());

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var mainWindow = new MainWindow();
            mainWindow.DataContext = new ViewModels.MainWindowViewModel(Program.ServiceProvider);
            desktop.MainWindow = mainWindow;
        }

        ApplyTheme("Modern");
        base.OnFrameworkInitializationCompleted();
    }

    public static void ApplyTheme(string themeName)
    {
        if (Current == null)
            return;

        // Avalonia 11 için ana aydınlık/karanlık mod geçişi
        Current.RequestedThemeVariant = themeName == "Sade" ? ThemeVariant.Light : ThemeVariant.Dark;

        if (Current.Resources is not IResourceDictionary resources)
            return;

        switch (themeName)
        {
            case "Retro":
                SetRetroTheme(resources);
                break;
            case "Sade":
                SetSadeTheme(resources);
                break;
            default:
                SetModernTheme(resources);
                break;
        }
    }

    private static void SetModernTheme(IResourceDictionary resources)
    {
        resources["PrimaryBackgroundBrush"] = new SolidColorBrush(Color.Parse("#FF0F172A"));
        resources["SecondaryBackgroundBrush"] = new SolidColorBrush(Color.Parse("#FF111827"));
        resources["SidebarBackgroundBrush"] = new SolidColorBrush(Color.Parse("#FF111827"));
        resources["CardBackgroundBrush"] = new SolidColorBrush(Color.Parse("#FF1E293B"));
        resources["AccentBrush"] = new SolidColorBrush(Color.Parse("#FF38BDF8"));
        resources["AccentTextBrush"] = new SolidColorBrush(Color.Parse("#FFFFFFFF"));
        resources["TextBrush"] = new SolidColorBrush(Color.Parse("#FFF8FAFC"));
        resources["BorderBrush"] = new SolidColorBrush(Color.Parse("#FF334155"));
        resources["ControlBackgroundBrush"] = new SolidColorBrush(Color.Parse("#FF111827"));
    }

    private static void SetRetroTheme(IResourceDictionary resources)
    {
        resources["PrimaryBackgroundBrush"] = new SolidColorBrush(Color.Parse("#FF1A1A2E"));
        resources["SecondaryBackgroundBrush"] = new SolidColorBrush(Color.Parse("#FF16213E"));
        resources["SidebarBackgroundBrush"] = new SolidColorBrush(Color.Parse("#FF16213E"));
        resources["CardBackgroundBrush"] = new SolidColorBrush(Color.Parse("#FF0F3460"));
        resources["AccentBrush"] = new SolidColorBrush(Color.Parse("#FFE94560"));
        resources["AccentTextBrush"] = new SolidColorBrush(Color.Parse("#FFF8EDEB"));
        resources["TextBrush"] = new SolidColorBrush(Color.Parse("#FFF8EDEB"));
        resources["BorderBrush"] = new SolidColorBrush(Color.Parse("#FF53354A"));
        resources["ControlBackgroundBrush"] = new SolidColorBrush(Color.Parse("#FF16213E"));
    }

    private static void SetSadeTheme(IResourceDictionary resources)
    {
        resources["PrimaryBackgroundBrush"] = new SolidColorBrush(Color.Parse("#FFF8FAFC"));
        resources["SecondaryBackgroundBrush"] = new SolidColorBrush(Color.Parse("#FFEEF2FF"));
        resources["SidebarBackgroundBrush"] = new SolidColorBrush(Color.Parse("#FFF1F5F9"));
        resources["CardBackgroundBrush"] = new SolidColorBrush(Color.Parse("#FFFFFFFF"));
        resources["AccentBrush"] = new SolidColorBrush(Color.Parse("#FF2563EB"));
        resources["AccentTextBrush"] = new SolidColorBrush(Color.Parse("#FFFFFFFF"));
        resources["TextBrush"] = new SolidColorBrush(Color.Parse("#FF0F172A"));
        resources["BorderBrush"] = new SolidColorBrush(Color.Parse("#FFCBD5E1"));
        resources["ControlBackgroundBrush"] = new SolidColorBrush(Color.Parse("#FFF1F5F9"));
    }
}
