using System;
using System.Collections.Generic;

namespace MultiSych.Services.Models
{
    /// <summary>Aranabilir yerel cache kaynağının türü.</summary>
    public enum SearchSourceType
    {
        Email,
        File,
        CalendarEvent
    }

    /// <summary>Aramanın hangi cache kaynaklarını kapsayacağını belirler (bit bayrağı).</summary>
    [Flags]
    public enum SearchScope
    {
        None = 0,
        Emails = 1,
        Files = 2,
        Events = 4,
        All = Emails | Files | Events
    }

    /// <summary>Birleşik aramadan dönen tek bir sonuç (e-posta, dosya veya takvim etkinliği).</summary>
    public record SearchResult(
        SearchSourceType Type,
        string Id,
        string Title,
        string Snippet,
        DateTime? Date,
        string AccountId,
        string Provider,
        double Score);

    /// <summary>RAG cevabı: AI'nin ürettiği yanıt + dayandığı kaynaklar.</summary>
    public record UnifiedAnswer(string Answer, IReadOnlyList<SearchResult> Sources);
}
