using System;
using System.Runtime.InteropServices;
using Adw;

namespace MultiSych.Desktop;

public static class App
{
    // glibc setlocale — GTK'nın yerleşik widget'ları (örn. Gtk.Calendar'ın ay/gün
    // isimleri) bizim Loc.Get() sistemimize DEĞİL, doğrudan işletim sistemi
    // yereline (LC_TIME) bakıyor. 2026-09-22'de operatör gerçek kullanımda
    // yakaladı: uygulama "English" ayarındayken takvim widget'ı hâlâ Türkçe
    // ay/gün isimleri gösteriyordu (makinenin LC_TIME'ı tr_TR.UTF-8 olduğu
    // için). Loc.SetLanguage yalnız bizim JSON sözlüğümüzü değiştiriyordu,
    // native widget'lara dokunmuyordu — ayrı bir katman.
    [DllImport("libc", EntryPoint = "setlocale")]
    private static extern IntPtr native_setlocale(int category, string? locale);

    private const int LC_ALL = 6;
    private const int LC_TIME = 2;
    private const int LC_MESSAGES = 5;

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
        MultiSych.Desktop.Localization.Loc.SetLanguage(langCode);

        var glibcLocale = langCode.StartsWith("tr", StringComparison.OrdinalIgnoreCase)
            ? "tr_TR.UTF-8"
            : "en_US.UTF-8";

        try
        {
            // İki deneme (2026-09-22, yalnız LC_TIME + Environment.SetEnvironmentVariable)
            // işe yaramadı — operatör gerçek kullanımda hâlâ Türkçe gördü. Kök sebep
            // KESİN BİLİNMİYOR (GTK'nın ay/gün isimleri strftime/LC_TIME'dan mı yoksa
            // gtk40.mo çeviri kataloğundan/LC_MESSAGES'ten mi geliyor, doğrulanamadı —
            // bu makinede ölçmenin bir yolu bulunamadı). Bu yüzden ARTIK TÜM locale
            // kategorilerini (LC_ALL) ve ilgili tüm ortam değişkenlerini birlikte
            // zorluyoruz — hangisi etkiliyse etkilesin kapsanmış olsun.
            foreach (var name in new[] { "LC_ALL", "LANG", "LANGUAGE", "LC_TIME", "LC_MESSAGES", "LC_NUMERIC", "LC_COLLATE" })
            {
                Environment.SetEnvironmentVariable(name, glibcLocale);
            }

            var result = native_setlocale(LC_ALL, glibcLocale);
            if (result == IntPtr.Zero)
            {
                Serilog.Log.Warning("ApplyLanguage: setlocale(LC_ALL, '{Locale}') NULL döndü — sistemde kurulu değil ya da desteklenmiyor.", glibcLocale);
            }

            // Teşhis: bir sonraki denemede körlemesine tahmin etmemek için gerçekte
            // hangi locale'in AKTİF olduğunu logluyoruz.
            var activeTime = Marshal.PtrToStringAnsi(native_setlocale(LC_TIME, null)) ?? "?";
            var activeMessages = Marshal.PtrToStringAnsi(native_setlocale(LC_MESSAGES, null)) ?? "?";
            Serilog.Log.Information("ApplyLanguage: setlocale sonrası aktif LC_TIME={LcTime} LC_MESSAGES={LcMessages} (hedef: {Hedef})", activeTime, activeMessages, glibcLocale);
        }
        catch (Exception ex)
        {
            Serilog.Log.Warning(ex, "ApplyLanguage: setlocale çağrısı başarısız oldu, native widget dili değişmeyebilir.");
        }
    }
}
