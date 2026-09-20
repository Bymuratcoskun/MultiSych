using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using MultiSych.Services.Data;
using MultiSych.Services.Interfaces;
using MultiSych.Services.Models;
using Serilog;

namespace MultiSych.Services.Implementations
{
    /// <summary>
    /// Yerel cache üzerinde anahtar-kelime araması ve RAG AI cevabı üretir.
    /// Gömme (embedding) altyapısı gerektirmeden, token bazlı LIKE eşleşmesi + alaka
    /// puanlaması ile retrieval yapar; bulunan kayıtları bağlam olarak AI'ye verir.
    /// </summary>
    public class UnifiedSearchService : IUnifiedSearchService
    {
        private readonly IDbContextFactory<LocalCacheDbContext> _dbContextFactory;
        private readonly IAIService _aiService;
        private readonly ILogger _logger = Log.ForContext<UnifiedSearchService>();

        // Puanlamada gürültü yapan çok yaygın kelimeler (TR + EN).
        private static readonly HashSet<string> StopWords = new(StringComparer.OrdinalIgnoreCase)
        {
            "ve", "ile", "bir", "bu", "şu", "için", "de", "da", "mi", "mu", "mı", "ne",
            "en", "gibi", "ama", "veya", "the", "a", "an", "of", "to", "in", "is", "on",
            "and", "or", "for", "with", "at", "by"
        };

        // Alan ağırlıkları: başlık > özet/konum > gövde.
        private const double TitleWeight = 3.0;
        private const double SnippetWeight = 2.0;
        private const double BodyWeight = 1.0;

        // Token başına aday getirme üst sınırı (bellek koruması).
        private const int CandidatesPerToken = 200;

        public UnifiedSearchService(IDbContextFactory<LocalCacheDbContext> dbContextFactory, IAIService aiService)
        {
            _dbContextFactory = dbContextFactory;
            _aiService = aiService;
        }

        public async Task<IReadOnlyList<SearchResult>> SearchAsync(
            string query,
            SearchScope scope = SearchScope.All,
            int maxResults = 20,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(query) || scope == SearchScope.None)
                return Array.Empty<SearchResult>();

            var tokens = Tokenize(query);
            if (tokens.Count == 0)
                return Array.Empty<SearchResult>();

            await using var db = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

            // (Type, Id) -> birikimli sonuç. Aynı kayıt birden çok token'la eşleşince puanı toplanır.
            var accumulator = new Dictionary<string, ScoredResult>();

            if (scope.HasFlag(SearchScope.Emails))
                await ScoreEmailsAsync(db, tokens, accumulator, cancellationToken);

            if (scope.HasFlag(SearchScope.Files))
                await ScoreFilesAsync(db, tokens, accumulator, cancellationToken);

            if (scope.HasFlag(SearchScope.Events))
                await ScoreEventsAsync(db, tokens, accumulator, cancellationToken);

            return accumulator.Values
                .OrderByDescending(r => r.Result.Score)
                .ThenByDescending(r => r.Result.Date ?? DateTime.MinValue)
                .Take(maxResults)
                .Select(r => r.Result)
                .ToList();
        }

        public async Task<UnifiedAnswer> AskAsync(
            string question,
            string provider = "hybrid",
            CancellationToken cancellationToken = default)
        {
            var sources = await SearchAsync(question, SearchScope.All, maxResults: 8, cancellationToken);

            if (sources.Count == 0)
            {
                return new UnifiedAnswer(
                    "Verilerinizde bu soruyla ilgili bir kayıt bulamadım. Farklı anahtar kelimelerle tekrar deneyebilirsiniz.",
                    sources);
            }

            var prompt = BuildRagPrompt(question, sources);

            try
            {
                var answer = await _aiService.GetResponseAsync(prompt, provider);
                return new UnifiedAnswer(answer?.Trim() ?? string.Empty, sources);
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "RAG cevabı üretilirken AI hatası oluştu.");
                return new UnifiedAnswer(
                    "İlgili kayıtları buldum ancak AI cevabı üretilirken bir hata oluştu. Aşağıdaki kaynaklara bakabilirsiniz.",
                    sources);
            }
        }

        private static string BuildRagPrompt(string question, IReadOnlyList<SearchResult> sources)
        {
            var sb = new StringBuilder();
            sb.AppendLine("Aşağıda kullanıcının kişisel verilerinden (e-posta, dosya, takvim) alınmış kaynaklar var.");
            sb.AppendLine("SADECE bu kaynaklara dayanarak soruyu Türkçe yanıtla. Kaynaklarda yanıt yoksa bunu açıkça belirt, uydurma.");
            sb.AppendLine("Yanıtında hangi kaynağı kullandığını [1], [2] gibi numaralarla belirt.");
            sb.AppendLine();
            sb.AppendLine("KAYNAKLAR:");
            for (int i = 0; i < sources.Count; i++)
            {
                var s = sources[i];
                var dateStr = s.Date.HasValue ? s.Date.Value.ToLocalTime().ToString("yyyy-MM-dd HH:mm") : "tarih yok";
                sb.AppendLine($"[{i + 1}] ({s.Type}, {dateStr}) {s.Title}");
                if (!string.IsNullOrWhiteSpace(s.Snippet))
                    sb.AppendLine($"    {Truncate(s.Snippet, 400)}");
            }
            sb.AppendLine();
            sb.AppendLine($"SORU: {question}");
            return sb.ToString();
        }

        private static async Task ScoreEmailsAsync(
            LocalCacheDbContext db, List<string> tokens,
            Dictionary<string, ScoredResult> acc, CancellationToken ct)
        {
            foreach (var token in tokens)
            {
                // EF Core, string.Contains'i SQLite'ta büyük/küçük harf duyarlı instr()'a
                // çevirir; LIKE ise ASCII harfler için harf-duyarsızdır. Token'lar yalnızca
                // alfanümerik olduğundan LIKE joker karakteri (% _) riski yoktur.
                var pattern = "%" + token + "%";
                var matches = await db.CachedEmails
                    .AsNoTracking()
                    .Where(e => EF.Functions.Like(e.Subject, pattern)
                             || EF.Functions.Like(e.From, pattern)
                             || EF.Functions.Like(e.Snippet, pattern)
                             || EF.Functions.Like(e.Body, pattern))
                    .OrderByDescending(e => e.ReceivedAt)
                    .Take(CandidatesPerToken)
                    .ToListAsync(ct);

                foreach (var e in matches)
                {
                    double delta = FieldScore(e.Subject, token, TitleWeight)
                                 + FieldScore(e.From, token, SnippetWeight)
                                 + FieldScore(e.Snippet, token, SnippetWeight)
                                 + FieldScore(e.Body, token, BodyWeight);
                    if (delta <= 0) continue;

                    var key = $"Email:{e.MessageId}";
                    Accumulate(acc, key, delta, () => new SearchResult(
                        SearchSourceType.Email,
                        e.MessageId,
                        string.IsNullOrWhiteSpace(e.Subject) ? "(konu yok)" : e.Subject,
                        BuildEmailSnippet(e),
                        e.ReceivedAt == default ? e.ReceivedDate : e.ReceivedAt,
                        e.AccountId,
                        e.Provider,
                        0));
                }
            }
        }

        private static async Task ScoreFilesAsync(
            LocalCacheDbContext db, List<string> tokens,
            Dictionary<string, ScoredResult> acc, CancellationToken ct)
        {
            foreach (var token in tokens)
            {
                var pattern = "%" + token + "%";
                var matches = await db.CloudFiles
                    .AsNoTracking()
                    .Where(f => !f.IsDirectory && (EF.Functions.Like(f.FileName, pattern) || EF.Functions.Like(f.Path, pattern)))
                    .Take(CandidatesPerToken)
                    .ToListAsync(ct);

                foreach (var f in matches)
                {
                    double delta = FieldScore(f.FileName, token, TitleWeight)
                                 + FieldScore(f.Path, token, BodyWeight);
                    if (delta <= 0) continue;

                    var key = $"File:{f.FileId}";
                    Accumulate(acc, key, delta, () => new SearchResult(
                        SearchSourceType.File,
                        f.FileId,
                        f.FileName,
                        f.Path,
                        f.UpdatedAt,
                        f.AccountId,
                        f.Provider,
                        0));
                }
            }
        }

        private static async Task ScoreEventsAsync(
            LocalCacheDbContext db, List<string> tokens,
            Dictionary<string, ScoredResult> acc, CancellationToken ct)
        {
            foreach (var token in tokens)
            {
                var pattern = "%" + token + "%";
                var matches = await db.CachedEvents
                    .AsNoTracking()
                    .Where(ev => EF.Functions.Like(ev.Title, pattern)
                              || EF.Functions.Like(ev.Description, pattern)
                              || (ev.Location != null && EF.Functions.Like(ev.Location, pattern)))
                    .OrderByDescending(ev => ev.StartTime)
                    .Take(CandidatesPerToken)
                    .ToListAsync(ct);

                foreach (var ev in matches)
                {
                    double delta = FieldScore(ev.Title, token, TitleWeight)
                                 + FieldScore(ev.Location ?? string.Empty, token, SnippetWeight)
                                 + FieldScore(ev.Description, token, BodyWeight);
                    if (delta <= 0) continue;

                    var key = $"Event:{ev.EventId}";
                    Accumulate(acc, key, delta, () => new SearchResult(
                        SearchSourceType.CalendarEvent,
                        ev.EventId,
                        ev.Title,
                        BuildEventSnippet(ev),
                        ev.StartTime,
                        ev.AccountId,
                        ev.Provider,
                        0));
                }
            }
        }

        private static void Accumulate(
            Dictionary<string, ScoredResult> acc, string key, double delta, Func<SearchResult> factory)
        {
            if (acc.TryGetValue(key, out var existing))
            {
                existing.Result = existing.Result with { Score = existing.Result.Score + delta };
            }
            else
            {
                var baseResult = factory();
                acc[key] = new ScoredResult { Result = baseResult with { Score = delta } };
            }
        }

        /// <summary>Bir metinde token'ın (büyük/küçük harf duyarsız) geçiş sayısını ağırlıkla çarpar.</summary>
        private static double FieldScore(string? text, string token, double weight)
        {
            if (string.IsNullOrEmpty(text)) return 0;
            int count = 0, index = 0;
            while ((index = text.IndexOf(token, index, StringComparison.OrdinalIgnoreCase)) >= 0)
            {
                count++;
                index += token.Length;
            }
            return count * weight;
        }

        private static List<string> Tokenize(string query)
        {
            var tokens = new List<string>();
            foreach (var raw in SplitWords(query))
            {
                var t = raw.Trim();
                if (t.Length < 2) continue;
                if (StopWords.Contains(t)) continue;
                tokens.Add(t);
            }
            // Anlamlı token çıkmazsa (ör. tümü stopword) ham sorguyu tek token yap.
            if (tokens.Count == 0)
            {
                var trimmed = query.Trim();
                if (trimmed.Length >= 2) tokens.Add(trimmed);
            }
            return tokens.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        }

        private static IEnumerable<string> SplitWords(string text)
        {
            var current = new StringBuilder();
            foreach (var ch in text)
            {
                if (char.IsLetterOrDigit(ch))
                {
                    current.Append(ch);
                }
                else if (current.Length > 0)
                {
                    yield return current.ToString();
                    current.Clear();
                }
            }
            if (current.Length > 0) yield return current.ToString();
        }

        private static string BuildEmailSnippet(EmailMessageEntity e)
        {
            var snippet = !string.IsNullOrWhiteSpace(e.Snippet) ? e.Snippet : e.Body;
            var from = string.IsNullOrWhiteSpace(e.From) ? string.Empty : $"{e.From}: ";
            return Truncate(from + snippet, 300);
        }

        private static string BuildEventSnippet(CalendarEventEntity ev)
        {
            var parts = new List<string>();
            if (!string.IsNullOrWhiteSpace(ev.Location)) parts.Add($"Konum: {ev.Location}");
            parts.Add($"{ev.StartTime.ToLocalTime():yyyy-MM-dd HH:mm} - {ev.EndTime.ToLocalTime():HH:mm}");
            if (!string.IsNullOrWhiteSpace(ev.Description)) parts.Add(ev.Description);
            return Truncate(string.Join(" | ", parts), 300);
        }

        private static string Truncate(string text, int max)
        {
            if (string.IsNullOrEmpty(text)) return string.Empty;
            text = text.Replace('\n', ' ').Replace('\r', ' ').Trim();
            return text.Length <= max ? text : text.Substring(0, max) + "…";
        }

        /// <summary>Sözlükte referans-tipli birikim için değiştirilebilir sarmalayıcı.</summary>
        private sealed class ScoredResult
        {
            public SearchResult Result { get; set; } = null!;
        }
    }
}
