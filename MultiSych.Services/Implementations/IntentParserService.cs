using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MultiSych.Services.Interfaces;
using Serilog;

namespace MultiSych.Services.Implementations;

public class IntentParserService : IIntentParserService
{
    private readonly IAIService _aiService;
    private readonly ILogger _logger = Log.ForContext<IntentParserService>();

    private static readonly Dictionary<string, string[]> SamplePhrases = new(StringComparer.OrdinalIgnoreCase)
    {
        { "Sync", new[] { 
            "senkronize et", "eşitle", "senkronizasyonu başlat", "yenile", "verileri güncelle", "sync yap", "dosyaları senkronize et",
            "bulutu eşitle", "hesapları senkronize et", "senkronize etmeyi başlat", "bulutla eşitle", "verilerimi eşitle"
        } },
        { "Summarize", new[] { 
            "belgeyi özetle", "özet çıkar", "doküman analizi yap", "metni kısalt", "özet oluştur", "mail özetle", "yazıyı özetle",
            "özetini ver", "dosyayı analiz et", "dokümanı özetle", "özetle", "özet çıkart"
        } },
        { "Calendar", new[] { 
            "takvimi aç", "etkinlikleri göster", "toplantılarımı listele", "randevulara bak", "takvime git", "takvim sekmesine geç",
            "ajandamı aç", "etkinlik listesi", "planları göster", "takvimimi aç"
        } },
        { "Dashboard", new[] { 
            "genel bakış", "ana ekrana git", "giriş panelini aç", "paneli göster", "dashboard aç", 
            "ana ekrana geç", "genel bakışa geç", "başlangıç ekranı", "multi sych ana ekran"
        } },
        { "Accounts", new[] { 
            "bağlı hesaplar", "hesap ekleme ekranı", "hesaplarımı yönet", "hesaplara git", "hesapları aç", "hesap sekmesine geç",
            "hesapları göster", "yeni hesap ekle", "bulut hesapları", "hesap yönetimi"
        } },
        { "AI", new[] { 
            "yapay zeka asistanı", "asistan ekranına geç", "ai genel bakış", "asistanı aç", "yapay zekaya git",
            "botu göster", "ai asistanı", "yapay zeka panelini aç", "robot asistan"
        } },
        { "Explorer", new[] { 
            "dosya gezgini", "sanal sürücüyü aç", "dosyaları göster", "klasörleri listele", "explorer sekmesine geç",
            "dosya gezginine git", "sürücü dosyaları", "dosya listesini aç", "klasör gezgini", "dosyalarımı aç"
        } },
        { "Logs", new[] { 
            "sistem loglarını aç", "hata günlükleri", "mini logları göster", "log ekranına git", "hata raporları", "logları aç",
            "raporlama ekranı", "hata logları", "sistem günlükleri", "hata raporuna git"
        } },
        { "Settings", new[] { 
            "ayarlar ekranı", "uygulama ayarları", "yapılandırmayı değiştir", "ayarlara git", "ayarları aç", "ayarlar sekmesine geç",
            "tercihlerimi aç", "dil ve tema ayarları", "ayarları düzenle", "seçenekleri göster"
        } },
        { "Chat", new[] { 
            "sohbet robotu", "mesajlaşma ekranı", "ai ile sohbet", "sohbeti aç", "sohbet sekmesine geç", "chat ekranına git",
            "asistanla sohbet", "sohbete başla", "chat aç", "mesajlaşmaya başla"
        } }
    };

    public IntentParserService(IAIService aiService)
    {
        _aiService = aiService;
    }

    public async Task<string> ParseIntentAsync(string transcribedText)
    {
        if (string.IsNullOrWhiteSpace(transcribedText))
            return "Unknown";

        // 1. Adım: Yerel NLU Vektör Uzayı Sınıflandırıcısı ile sınıflandır
        var localResult = ClassifyLocal(transcribedText);
        if (localResult.Score >= 0.50)
        {
            _logger.Information("Sesli komut yerel NLU ile yüksek güvenle sınıflandırıldı: '{Intent}' (Skor: {Score})", localResult.Intent, localResult.Score);
            return localResult.Intent;
        }

        // 2. Adım: Skor düşükse bulut tabanlı AI servisinden yardım iste
        try
        {
            var prompt = $@"
Lütfen aşağıdaki sesli komutu analiz et ve kullanıcının niyetini (intent) belirle.
Kullanabileceğin niyetler şunlardır:
- 'Sync' (kullanıcı hesaplarını, dosyalarını, maillerini veya takvimini senkronize etmek, güncellemek, eşitlemek veya yenilemek istiyorsa).
- 'Summarize' (kullanıcı bir metni, e-postayı veya dosyayı özetletmek, özet çıkarmak istiyorsa).
- 'Calendar' (kullanıcı takvimini, toplantılarını, etkinliklerini görmek veya açmak istiyorsa).
- 'Dashboard' (kullanıcı genel bakış panelini, ana ekranını, dashboard'u görmek istiyorsa).
- 'Accounts' (kullanıcı bağlı bulut hesaplarını yönetmek veya yeni hesap eklemek istiyorsa).
- 'AI' (kullanıcı yapay zeka asistan genel bakış sekmesine gitmek istiyorsa).
- 'Explorer' (kullanıcı dosya gezginini veya sanal sürücüdeki dosyaları görmek istiyorsa).
- 'Logs' (kullanıcı sistem loglarını veya hata raporlarını açmak istiyorsa).
- 'Settings' (kullanıcı ayarlar veya yapılandırma sekmesine gitmek istiyorsa).
- 'Chat' (kullanıcı yapay zeka asistanıyla sohbet ekranını açmak istiyorsa).
- 'Unknown' (niyet yukarıdakilerden hiçbirine uymuyorsa).

Çıktı olarak SADECE niyet adını döndür (Örn: 'Sync', 'Summarize', 'Calendar', 'Dashboard', 'Accounts', 'AI', 'Explorer', 'Logs', 'Settings', 'Chat' veya 'Unknown'). Başka hiçbir metin veya açıklama ekleme.

Kullanıcı komutu: ""{transcribedText}""";

            var response = await _aiService.GetResponseAsync(prompt, "hybrid");
            var parsed = response?.Trim() ?? string.Empty;

            if (parsed == "Sync" || parsed == "Summarize" || parsed == "Calendar" || parsed == "Dashboard" ||
                parsed == "Accounts" || parsed == "AI" || parsed == "Explorer" || parsed == "Logs" ||
                parsed == "Settings" || parsed == "Chat" || parsed == "Unknown")
            {
                return parsed;
            }
        }
        catch (Exception ex)
        {
            _logger.Warning(ex, "Failed to parse intent using AI service. Falling back to offline NLU.");
        }

        // 3. Adım: AI servisi başarısızsa veya bilinmiyorsa, yerel NLU'ya güvenilir bir skorla (>0.30) veya keyword fallback ile geri dön
        if (localResult.Score >= 0.30)
        {
            return localResult.Intent;
        }

        return GetFallbackIntent(transcribedText);
    }

    private (string Intent, double Score) ClassifyLocal(string transcribedText)
    {
        var queryTokens = Tokenize(transcribedText);
        if (queryTokens.Count == 0) return ("Unknown", 0.0);

        string bestIntent = "Unknown";
        double maxScore = 0.0;

        foreach (var pair in SamplePhrases)
        {
            var intent = pair.Key;
            var phrases = pair.Value;

            foreach (var phrase in phrases)
            {
                var sampleTokens = Tokenize(phrase);
                var score = ComputeCosineSimilarity(queryTokens, sampleTokens);
                if (score > maxScore)
                {
                    maxScore = score;
                    bestIntent = intent;
                }
            }
        }

        return (bestIntent, maxScore);
    }

    private static readonly HashSet<string> StopWords = new(StringComparer.OrdinalIgnoreCase)
    {
        "lütfen", "mi", "mısın", "misin", "mu", "musun", "mü", "müsün", "ve", "veya", "ile", "de", "da", "ki", "en", "daha",
        "aç", "göster", "git", "geç", "başlat", "yap", "et", "açın", "gösterin", "gidin", "geçin", "başlatın", "yapın", "edin",
        "tıkla", "tıklayın", "açık", "kapalı"
    };

    private static string StemTurkish(string word)
    {
        if (word.Length <= 3) return word;

        string[] suffixes = { 
            "larımızı", "lerimizi", "larını", "lerini", "ların", "lerin", "ları", "leri", "lar", "ler", 
            "ımızı", "imizi", "umuzu", "ümüzü", "ımız", "imiz", "umuz", "ümüz",
            "ın", "in", "un", "ün", "ı", "i", "u", "ü",
            "a", "e", "da", "de", "ta", "te", "dan", "den", "tan", "ten"
        };

        foreach (var suffix in suffixes)
        {
            if (word.EndsWith(suffix) && word.Length - suffix.Length >= 3)
            {
                return word[..^suffix.Length];
            }
        }

        return word;
    }

    private static List<string> Tokenize(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return [];
        
        var clean = text.ToLowerInvariant();
        var chars = clean.ToCharArray();
        for (int i = 0; i < chars.Length; i++)
        {
            if (char.IsPunctuation(chars[i])) chars[i] = ' ';
        }
        clean = new string(chars);

        var words = clean.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return words
            .Where(w => !StopWords.Contains(w))
            .Select(StemTurkish)
            .ToList();
    }

    private static double ComputeCosineSimilarity(List<string> queryTokens, List<string> sampleTokens)
    {
        if (queryTokens.Count == 0 || sampleTokens.Count == 0) return 0.0;

        var vocab = queryTokens.Union(sampleTokens).ToList();
        
        var queryFreq = new int[vocab.Count];
        var sampleFreq = new int[vocab.Count];

        for (int i = 0; i < vocab.Count; i++)
        {
            var word = vocab[i];
            queryFreq[i] = queryTokens.Count(t => t == word);
            sampleFreq[i] = sampleTokens.Count(t => t == word);
        }

        double dotProduct = 0.0;
        double queryMagSq = 0.0;
        double sampleMagSq = 0.0;

        for (int i = 0; i < vocab.Count; i++)
        {
            dotProduct += queryFreq[i] * sampleFreq[i];
            queryMagSq += queryFreq[i] * queryFreq[i];
            sampleMagSq += sampleFreq[i] * sampleFreq[i];
        }

        if (queryMagSq == 0.0 || sampleMagSq == 0.0) return 0.0;

        return dotProduct / (Math.Sqrt(queryMagSq) * Math.Sqrt(sampleMagSq));
    }

    private string GetFallbackIntent(string transcribedText)
    {
        var text = transcribedText.ToLowerInvariant();

        if (text.Contains("senkronize") || text.Contains("eşitle") || text.Contains("sync") || text.Contains("güncelle"))
            return "Sync";
        
        if (text.Contains("özetle") || text.Contains("özet çıkar") || text.Contains("özetini") || text.Contains("analiz et"))
            return "Summarize";

        if (text.Contains("takvim") || text.Contains("etkinlik") || text.Contains("toplantı") || text.Contains("ajanda") || text.Contains("randevu"))
            return "Calendar";

        if (text.Contains("genel bakış") || text.Contains("dashboard") || text.Contains("ana ekran") || text.Contains("giriş paneli"))
            return "Dashboard";

        if (text.Contains("hesap") || text.Contains("bağlantı"))
            return "Accounts";

        if (text.Contains("asistan") || text.Contains("yapay zeka") || text.Contains("robot") || text.Contains("bot"))
            return "AI";

        if (text.Contains("dosya") || text.Contains("klasör") || text.Contains("gezgin") || text.Contains("explorer") || text.Contains("sanal sürücü") || text.Contains("drive"))
            return "Explorer";

        if (text.Contains("log") || text.Contains("günlük") || text.Contains("hata") || text.Contains("rapor"))
            return "Logs";

        if (text.Contains("ayar") || text.Contains("yapılandırma") || text.Contains("tercih") || text.Contains("seçenek"))
            return "Settings";

        if (text.Contains("sohbet") || text.Contains("chat") || text.Contains("konuş") || text.Contains("mesaj"))
            return "Chat";

        return "Unknown";
    }
}