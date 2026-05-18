using MultiSych.Services.Models;

namespace MultiSych.Services.Interfaces
{
    public interface IAIService
    {
        /// <summary>
        /// Get AI response from hybrid AI providers (Copilot, Gemini, Yandex AI)
        /// </summary>
        Task<string> GetResponseAsync(string prompt, string provider = "hybrid");
        
        /// <summary>
        /// Get response from specific provider: "copilot", "gemini", "yandex"
        /// </summary>
        Task<string> GetResponseAsync(string prompt, string provider, AccountCredentials? credentials = null);
        
        /// <summary>
        /// Send message with context (multi-turn conversation)
        /// </summary>
        Task<string> SendMessageAsync(string message, List<string> conversationHistory, string provider = "hybrid");
        
        /// <summary>
        /// Analyze email content with AI
        /// </summary>
        Task<string> AnalyzeEmailAsync(EmailMessage email, string provider = "hybrid");
        
        /// <summary>
        /// Generate calendar suggestions based on emails
        /// </summary>
        Task<List<CalendarEvent>> GenerateCalendarSuggestionsAsync(List<EmailMessage> emails, string provider = "hybrid");
        
        /// <summary>
        /// Summarize document content
        /// </summary>
        Task<string> SummarizeDocumentAsync(string content, string provider = "hybrid");
    }
}
