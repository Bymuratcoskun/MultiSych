using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MultiSych.Services.Models;

namespace MultiSych.Services.Interfaces
{
    /// <summary>
    /// Şifreli yerel cache (e-postalar, dosyalar, takvim etkinlikleri) üzerinde
    /// birleşik anahtar-kelime araması ve bu sonuçlara dayalı RAG (retrieval-augmented
    /// generation) AI cevabı üretir. "Verilerinle Sohbet" özelliğinin çekirdeğidir.
    /// </summary>
    public interface IUnifiedSearchService
    {
        /// <summary>
        /// Cache içinde anahtar kelime araması yapar ve alaka düzeyine göre sıralı sonuç döner.
        /// </summary>
        Task<IReadOnlyList<SearchResult>> SearchAsync(
            string query,
            SearchScope scope = SearchScope.All,
            int maxResults = 20,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Soruyla ilgili cache kayıtlarını bulur, bunları bağlam olarak AI'ye vererek
        /// kaynak referanslı bir cevap üretir (RAG).
        /// </summary>
        Task<UnifiedAnswer> AskAsync(
            string question,
            string provider = "hybrid",
            CancellationToken cancellationToken = default);
    }
}
