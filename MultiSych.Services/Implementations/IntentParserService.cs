using System.Threading.Tasks;
using MultiSych.Services.Interfaces;

namespace MultiSych.Services.Implementations;

public class IntentParserService : IIntentParserService
{
    public Task<string> ParseIntentAsync(string transcribedText)
    {
        if (string.IsNullOrWhiteSpace(transcribedText))
            return Task.FromResult("Unknown");

        // Metni küçük harfe çevirerek kelime analizi yapıyoruz
        var text = transcribedText.ToLowerInvariant();

        if (text.Contains("senkronize et") || text.Contains("eşitle") || text.Contains("sync"))
        {
            return Task.FromResult("Sync");
        }
        
        if (text.Contains("maili özetle") || text.Contains("özet çıkar") || text.Contains("özetle"))
        {
            return Task.FromResult("Summarize");
        }

        if (text.Contains("takvimi aç") || text.Contains("etkinlikler"))
        {
            return Task.FromResult("Calendar");
        }

        return Task.FromResult("Unknown");
    }
}