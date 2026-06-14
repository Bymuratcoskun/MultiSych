using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Linq;
using System.Threading.Tasks;
using MultiSych.Services.Configuration;
using MultiSych.Services.Interfaces;
using MultiSych.Services.Models;

namespace MultiSych.Services.Implementations;

public class AIService : IAIService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly MultiSychConfig _config;

    public AIService(IHttpClientFactory httpClientFactory, MultiSychConfig config)
    {
        _httpClientFactory = httpClientFactory;
        _config = config;
    }

    public async Task<string> GetResponseAsync(string prompt, string provider = "hybrid")
    {
        return await SendMessageAsync(new List<ChatHistoryMessage> { new() { Role = "user", Content = prompt } }, provider);
    }

    public async Task<string> GetResponseAsync(string prompt, string provider, AccountCredentials? credentials = null)
    {
        return await SendMessageAsync(new List<ChatHistoryMessage> { new() { Role = "user", Content = prompt } }, provider);
    }

    public async Task<string> SendMessageAsync(string message, List<string> conversationHistory, string provider = "hybrid")
    {
        var history = new List<ChatHistoryMessage>();
        for (var i = 0; i < conversationHistory.Count; i += 2)
        {
            history.Add(new ChatHistoryMessage { Role = "user", Content = conversationHistory[i] });
            if (i + 1 < conversationHistory.Count)
            {
                history.Add(new ChatHistoryMessage { Role = "model", Content = conversationHistory[i + 1] });
            }
        }
        history.Add(new ChatHistoryMessage { Role = "user", Content = message });
        return await SendMessageAsync(history, provider);
    }

    public async Task<string> AnalyzeEmailAsync(EmailMessage email, string provider = "hybrid")
    {
        if (email == null) throw new ArgumentNullException(nameof(email));

        var prompt = $@"
Lütfen aşağıdaki e-posta içeriğini detaylı bir şekilde analiz et. 
Bana şu başlıklar altında profesyonel bir özet ve analiz sun:
1. **Kısa Özet:** E-postanın ana fikri (1-2 cümle).
2. **Önem Derecesi:** (Düşük, Orta, Yüksek) ve kısa gerekçesi.
3. **Aksiyon Öğeleri:** Yapılması gereken işlemler veya cevaplanması gereken sorular var mı? (Madde imleri ile listele, yoksa 'Yok' yaz).
4. **Duygu/Ton:** E-postanın genel dili (Örn: Resmi, acil, samimi, şikayet vb.).

E-posta Konusu: {email.Subject}
Gönderen: {email.From}
Tarih: {email.ReceivedDate}

E-posta İçeriği:
{email.Body}";

        return await GetResponseAsync(prompt, provider);
    }

    public async Task<List<CalendarEvent>> GenerateCalendarSuggestionsAsync(List<EmailMessage> emails, string provider = "hybrid")
    {
        if (emails == null || !emails.Any()) return new List<CalendarEvent>();

        var promptBuilder = new StringBuilder();
        promptBuilder.AppendLine("Lütfen aşağıdaki e-postaları analiz et ve potansiyel takvim etkinliklerini (toplantı, uçuş, randevu vb.) çıkar.");
        promptBuilder.AppendLine("SADECE aşağıdaki formatta geçerli bir JSON dizisi (array) döndür, fazladan hiçbir metin yazma:");
        promptBuilder.AppendLine(@"[
  {
    ""title"": ""Etkinlik Başlığı"",
    ""description"": ""Kısa açıklama"",
    ""location"": ""Yer veya Online"",
    ""startTime"": ""YYYY-MM-DDTHH:mm:ssZ"",
    ""endTime"": ""YYYY-MM-DDTHH:mm:ssZ"",
    ""isAllDay"": false
  }
]");
        promptBuilder.AppendLine("Eğer etkinlik yoksa boş bir dizi [] döndür.\n");

        foreach (var email in emails)
        {
            promptBuilder.AppendLine("--- E-Posta Başlangıcı ---");
            promptBuilder.AppendLine($"Konu: {email.Subject}");
            promptBuilder.AppendLine($"Kimden: {email.From}");
            promptBuilder.AppendLine($"Tarih: {email.ReceivedDate:O}");
            promptBuilder.AppendLine($"İçerik: {email.Body}");
            promptBuilder.AppendLine("--- E-Posta Sonu ---\n");
        }

        var aiResponse = await GetResponseAsync(promptBuilder.ToString(), provider);
        
        var jsonStr = aiResponse.Trim();
        if (jsonStr.StartsWith("```json", StringComparison.OrdinalIgnoreCase)) jsonStr = jsonStr.Substring(7);
        if (jsonStr.StartsWith("```")) jsonStr = jsonStr.Substring(3);
        if (jsonStr.EndsWith("```")) jsonStr = jsonStr.Substring(0, jsonStr.Length - 3);
        jsonStr = jsonStr.Trim();

        if (string.IsNullOrWhiteSpace(jsonStr) || jsonStr == "[]") 
            return new List<CalendarEvent>();

        var foundEvents = new List<CalendarEvent>();
        try
        {
            using var doc = JsonDocument.Parse(jsonStr);
            foreach (var element in doc.RootElement.EnumerateArray())
            {
                var calendarEvent = new CalendarEvent
                {
                    EventId = Guid.NewGuid().ToString(),
                    Title = element.TryGetProperty("title", out var t) ? t.GetString() ?? "Yeni Etkinlik" : "Yeni Etkinlik",
                    Description = element.TryGetProperty("description", out var d) ? d.GetString() : string.Empty,
                    Location = element.TryGetProperty("location", out var l) ? l.GetString() : string.Empty,
                    StartTime = element.TryGetProperty("startTime", out var st) && DateTime.TryParse(st.GetString(), out var sdt) ? sdt : DateTime.UtcNow,
                    EndTime = element.TryGetProperty("endTime", out var et) && DateTime.TryParse(et.GetString(), out var edt) ? edt : DateTime.UtcNow.AddHours(1),
                    IsAllDay = element.TryGetProperty("isAllDay", out var iad) && iad.GetBoolean()
                };
                foundEvents.Add(calendarEvent);
            }
        }
        catch
        {
            // JSON okuma başarısız olursa boş liste döndür (AI beklenen formatta çıktı vermedi demektir)
        }

        return foundEvents;
    }

    public async Task<string> SummarizeDocumentAsync(string content, string provider = "hybrid")
    {
        var prompt = $"Lütfen aşağıdaki metni dikkatlice incele ve en önemli noktalarını vurgulayarak profesyonel bir özet çıkar:\n\n{content}";
        return await GetResponseAsync(prompt, provider);
    }

    public async Task<string> SendMessageAsync(List<ChatHistoryMessage> conversation, string provider)
    {
        var selectedProvider = provider?.ToLowerInvariant() ?? "gemini";
        try
        {
            if (selectedProvider.Contains("gemini")) return await SendToGeminiAsync(conversation);
            if (selectedProvider.Contains("yandex")) return await SendToYandexAsync(conversation);
            
            // Varsayılan / Copilot (OpenAI uyumlu endpointler için)
            return await SendToOpenAIAsync(conversation); 
        }
        catch (Exception ex)
        {
            return $"Yapay Zeka API Hatası ({selectedProvider}): {ex.Message}";
        }
    }

    private async Task<string> SendToGeminiAsync(List<ChatHistoryMessage> conversation)
    {
        var apiKey = _config.AI?.GeminiApiKey;
        if (string.IsNullOrWhiteSpace(apiKey)) return "Gemini API anahtarı ayarlanmamış.";

        using var client = _httpClientFactory.CreateClient();
        var url = $"https://generativelanguage.googleapis.com/v1beta/models/gemini-1.5-pro:generateContent?key={apiKey}";

        var contents = conversation.Select(m => new
        {
            role = m.Role,
            parts = new[] { new { text = m.Content } }
        }).ToList();

        var payload = new
        {
            contents
        };

        var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
        var response = await client.PostAsync(url, content);
        response.EnsureSuccessStatusCode();

        var responseJson = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(responseJson);
        
        return doc.RootElement.GetProperty("candidates")[0].GetProperty("content").GetProperty("parts")[0].GetProperty("text").GetString() ?? "";
    }

    private async Task<string> SendToYandexAsync(List<ChatHistoryMessage> conversation)
    {
        var apiKey = _config.AI?.YandexAiApiKey;
        if (string.IsNullOrWhiteSpace(apiKey)) return "Yandex AI API anahtarı ayarlanmamış.";

        using var client = _httpClientFactory.CreateClient();
        client.DefaultRequestHeaders.Add("Authorization", $"Api-Key {apiKey}");

        var messages = conversation.Select(m => new
        {
            role = m.Role == "model" ? "assistant" : m.Role, // Yandex "model" yerine "assistant" kullanıyor
            text = m.Content
        }).ToList();

        var payload = new {
            modelUri = "gpt://b1g00000000000000000/yandexgpt/latest", // Yandex Klasör ID'nizi ayarlarınızdan dinamik çekebilirsiniz
            completionOptions = new { stream = false, temperature = 0.6, maxTokens = 1000 },
            messages
        };

        var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
        var response = await client.PostAsync("https://llm.api.cloud.yandex.net/foundationModels/v1/completion", content);
        
        if (!response.IsSuccessStatusCode) return $"Yandex HTTP Hatası: {response.StatusCode}";

        var responseJson = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(responseJson);
        return doc.RootElement.GetProperty("result").GetProperty("alternatives")[0].GetProperty("message").GetProperty("text").GetString() ?? "";
    }

    private async Task<string> SendToOpenAIAsync(List<ChatHistoryMessage> conversation)
    {
        var apiKey = _config.AI?.CopilotApiKey;
        if (string.IsNullOrWhiteSpace(apiKey)) return "Copilot/OpenAI API anahtarı ayarlanmamış.";

        using var client = _httpClientFactory.CreateClient();
        client.DefaultRequestHeaders.Add("Authorization", $"Bearer {apiKey}");

        var messages = conversation.Select(m => new
        {
            role = m.Role == "model" ? "assistant" : m.Role,
            content = m.Content
        }).ToList();

        var payload = new
        {
            model = "gpt-4", // Not: İhtiyaca göre MultiSychConfig modeline eklenip dinamik alınabilir.
            messages = messages
        };

        var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
        var response = await client.PostAsync("https://api.openai.com/v1/chat/completions", content);
        
        if (!response.IsSuccessStatusCode) return $"OpenAI HTTP Hatası: {response.StatusCode}";

        var responseJson = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(responseJson);
        return doc.RootElement.GetProperty("choices")[0].GetProperty("message").GetProperty("content").GetString() ?? "";
    }
}
