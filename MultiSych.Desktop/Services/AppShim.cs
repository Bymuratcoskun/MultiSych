using System;
using Adw;

namespace MultiSych.Desktop;

public static class App
{
    public static void ApplyTheme(string theme)
    {
        var manager = Adw.StyleManager.GetDefault();
        if (theme == "Sade")
        {
            manager.SetColorScheme(Adw.ColorScheme.ForceLight);
        }
        else
        {
            manager.SetColorScheme(Adw.ColorScheme.ForceDark);
        }
    }

    public static void ApplyAccentColor(string hex)
    {
        // Optional: Custom GTK CSS overrides can be applied here
    }

    public static void ApplyLanguage(string langCode)
    {
        // Stub for localization settings
    }
}
