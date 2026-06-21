using System;
using System.Threading.Tasks;
using MultiSych.Services.Interfaces;
using Serilog;

namespace MultiSych.Services.Implementations;

public class IntentParserService : IIntentParserService
{
    private readonly IAIService _aiService;
    private readonly ILogger _logger = Log.ForContext<IntentParserService>();

    public IntentParserService(IAIService aiService)
    {
        _aiService = aiService;
    }

    public async Task<string> ParseIntentAsync(string transcribedText)
    {
        if (string.IsNullOrWhiteSpace(transcribedText))
            return "Unknown";

        var fallbackIntent = GetFallbackIntent(transcribedText);

        try
        {
            var prompt = $@"
Lütfen aşağıdaki sesli komutu analiz et ve kullanıcının niyetini (intent) belirle.
Kullanabileceğin niyetler şunlardır:
- 'Sync' (kullanıcı hesaplarını, dosyalarını, maillerini veya takvimini senkronize etmek, güncellemek, eşitlemek veya yenilemek istiyorsa).
- 'Summarize' (kullanıcı bir metni, e-postayı veya dosyayı özetletmek, özet çıkarmak istiyorsa).
- 'Calendar' (kullanıcı takvimini, toplantılarını, etkinliklerini görmek veya açmak istiyorsa).
- 'Unknown' (niyet yukarıdakilerden hiçbirine uymuyorsa).

Çıktı olarak SADECE niyet adını döndür (Örn: 'Sync', 'Summarize', 'Calendar' veya 'Unknown'). Başka hiçbir metin veya açıklama ekleme.

Kullanıcı komutu: ""{transcribedText}""";

            var response = await _aiService.GetResponseAsync(prompt, "hybrid");
            var parsed = response?.Trim() ?? string.Empty;

            if (parsed == "Sync" || parsed == "Summarize" || parsed == "Calendar" || parsed == "Unknown")
            {
                return parsed;
            }
        }
        catch (Exception ex)
        {
            _logger.Warning(ex, "Failed to parse intent using AI service. Falling back to keyword matching.");
        }

        return fallbackIntent;
    }

    private string GetFallbackIntent(string transcribedText)
    {
        var text = transcribedText.ToLowerInvariant();

        if (text.Contains("senkronize et") || text.Contains("eşitle") || text.Contains("sync") || text.Contains("güncelle"))
        {
            return "Sync";
        }
        
        if (text.Contains("maili özetle") || text.Contains("özet çıkar") || text.Contains("özetle") || text.Contains("özet"))
        {
            return "Summarize";
        }

        if (text.Contains("takvimi aç") || text.Contains("etkinlikler") || text.Contains("takvim"))
        {
            return "Calendar";
        }

        return "Unknown";
    }
}