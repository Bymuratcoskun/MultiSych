using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace MultiSych.Desktop.Localization;

/// <summary>
/// Basit JSON tabanlı kaynak sözlüğü (docs/KARARLAR.md K12). Karar: gettext değil
/// JSON — proje zaten JSON/CSV tabanlı veri katmanı kullanıyor
/// (MultiSych.Services/Data), tutarlılık için.
///
/// Dil değişimi anlık DEĞİL — View'lar metinlerini yalnız KENDİ oluşturuldukları
/// anda okur (GTK widget'ları statik metinle inşa ediliyor, canlı yeniden
/// bağlama altyapısı yok). Bu yüzden SetLanguage uygulama açılışında, İLK View
/// inşa edilmeden ÖNCE bir kez çağrılmalı (bkz. Program.cs). Kullanıcı Ayarlar'dan
/// dili değiştirirse etkisi bir sonraki başlatmada görünür — bu bilerek böyle,
/// yanlış "anında değişti" izlenimi vermemek için (bkz. K8 dersi).
/// </summary>
public static class Loc
{
    private static Dictionary<string, string> _strings = new();
    private static readonly ConcurrentDictionary<string, byte> _warnedMissingKeys = new();
    private static string _currentLangCode = "tr-TR";

    public static string CurrentLanguage => _currentLangCode;

    public static void SetLanguage(string langCode)
    {
        _currentLangCode = langCode;
        var fileName = langCode.StartsWith("tr", StringComparison.OrdinalIgnoreCase) ? "tr.json" : "en.json";
        var path = Path.Combine(AppContext.BaseDirectory, "Localization", fileName);

        try
        {
            var json = File.ReadAllText(path);
            _strings = JsonSerializer.Deserialize<Dictionary<string, string>>(json) ?? new();
        }
        catch (Exception ex)
        {
            Serilog.Log.Error(ex, "Loc.SetLanguage: {Path} okunamadı, kaynak sözlük boş kalacak", path);
            _strings = new();
        }
    }

    /// <summary>
    /// Anahtarı çevirir. Anahtar bulunamazsa uygulama ÇÖKMEZ (bir UI metni eksikliği
    /// bir çökme sebebi değildir) ama SESSİZCE de geçilmez: eksik anahtar bir kez
    /// loglanır (aynı anahtar için tekrar tekrar loglamaz) ve anahtarın kendisi
    /// döner — böylece ekranda "app.title" gibi bir şey görünürse bu bariz bir
    /// eksik çeviri sinyali olur, sahte bir metinle gizlenmez.
    /// </summary>
    public static string Get(string key)
    {
        if (_strings.TryGetValue(key, out var value))
            return value;

        if (_warnedMissingKeys.TryAdd(key, 0))
            Serilog.Log.Warning("Loc.Get: çeviri anahtarı eksik: {Key} (dil: {Lang})", key, _currentLangCode);

        return key;
    }
}
