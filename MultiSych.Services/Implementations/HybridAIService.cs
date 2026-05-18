using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using MultiSych.Services.Configuration;
using MultiSych.Services.Interfaces;
using MultiSych.Services.Models;
using Serilog;

namespace MultiSych.Services.Implementations
{
    public class HybridAIService : IAIService
    {
        private readonly ILogger _logger;
        private readonly HttpClient _httpClient;
        private readonly AISettings _aiSettings;

        public HybridAIService(MultiSychConfig config, HttpClient httpClient)
        {
            _logger = Log.ForContext<HybridAIService>();
            _httpClient = httpClient;
            _aiSettings = config.AI ?? new AISettings();
        }

        public async Task<string> GetResponseAsync(string prompt, string provider = "hybrid")
        {
            try
            {
                _logger.Information("Getting AI response from provider: {Provider}", provider);

                if (provider == "hybrid")
                {
                    var providers = new Func<Task<string>>[]
                    {
                        () => GetResponseFromCopilotAsync(prompt),
                        () => GetResponseFromGeminiAsync(prompt),
                        () => GetResponseFromYandexAsync(prompt)
                    };

                    foreach (var fallbackProvider in providers)
                    {
                        try
                        {
                            return await fallbackProvider();
                        }
                        catch (Exception ex)
                        {
                            _logger.Warning(ex, "Provider failed in hybrid mode, falling back to the next provider.");
                        }
                    }
                    
                    throw new Exception("All AI providers failed in hybrid mode.");
                }

                return provider switch
                {
                    "copilot" => await GetResponseFromCopilotAsync(prompt),
                    "gemini" => await GetResponseFromGeminiAsync(prompt),
                    "yandex" => await GetResponseFromYandexAsync(prompt),
                    _ => throw new ArgumentException($"Unknown provider: {provider}")
                };
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error getting AI response");
                throw;
            }
        }

        public async Task<string> GetResponseAsync(string prompt, string provider, AccountCredentials? credentials = null)
        {
            return await GetResponseAsync(prompt, provider);
        }

        public async Task<string> SendMessageAsync(string message, List<string> conversationHistory, string provider = "hybrid")
        {
            try
            {
                _logger.Information("Sending message with conversation context from provider: {Provider}", provider);
                
                var fullContext = string.Join("\n", conversationHistory) + "\n" + message;
                return await GetResponseAsync(fullContext, provider);
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error sending message");
                throw;
            }
        }

        public async Task<string> AnalyzeEmailAsync(EmailMessage email, string provider = "hybrid")
        {
            try
            {
                _logger.Information("Analyzing email with AI from provider: {Provider}", provider);
                
                var prompt = $@"
Please analyze this email and provide a summary:
From: {email.From}
Subject: {email.Subject}
Body: {email.Body}

Provide:
1. Summary of content
2. Key action items (if any)
3. Sentiment analysis
4. Suggested response (if needed)
";
                return await GetResponseAsync(prompt, provider);
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error analyzing email");
                throw;
            }
        }

        public async Task<List<CalendarEvent>> GenerateCalendarSuggestionsAsync(List<EmailMessage> emails, string provider = "hybrid")
        {
            try
            {
                _logger.Information("Generating calendar suggestions from emails");
                
                var emailSummary = string.Join("\n", emails.Select(e => $"From: {e.From}\nSubject: {e.Subject}"));
                
                var prompt = $@"
Based on the following emails, suggest calendar events that should be created.
Emails:
{emailSummary}

You must respond ONLY with a valid JSON array of objects. Do not include markdown formatting like ```json.
Each object must have the following properties:
- title (string): Event title
- startTime (string): Suggested start date and time in ISO 8601 format (e.g. 2024-12-31T10:00:00Z)
- endTime (string): Suggested end date and time in ISO 8601 format
- description (string): Brief description or context for the event
";
                var response = await GetResponseAsync(prompt, provider);
                
                // Clean up potential markdown formatting from the response
                var jsonText = response.Trim();
                if (jsonText.StartsWith("```json", StringComparison.OrdinalIgnoreCase))
                    jsonText = jsonText.Substring(7);
                else if (jsonText.StartsWith("```", StringComparison.OrdinalIgnoreCase))
                    jsonText = jsonText.Substring(3);
                    
                if (jsonText.EndsWith("```", StringComparison.OrdinalIgnoreCase))
                    jsonText = jsonText.Substring(0, jsonText.Length - 3);
                    
                jsonText = jsonText.Trim();

                var suggestions = new List<CalendarEvent>();
                using var document = JsonDocument.Parse(jsonText);
                
                foreach (var item in document.RootElement.EnumerateArray())
                {
                    suggestions.Add(new CalendarEvent
                    {
                        EventId = Guid.NewGuid().ToString(),
                        Title = item.TryGetProperty("title", out var title) ? title.GetString() ?? "AI Suggested Event" : "AI Suggested Event",
                        Description = item.TryGetProperty("description", out var desc) ? desc.GetString() ?? string.Empty : string.Empty,
                        StartTime = item.TryGetProperty("startTime", out var start) && DateTime.TryParse(start.GetString(), out var startDt) ? startDt.ToUniversalTime() : DateTime.UtcNow.AddDays(1),
                        EndTime = item.TryGetProperty("endTime", out var end) && DateTime.TryParse(end.GetString(), out var endDt) ? endDt.ToUniversalTime() : DateTime.UtcNow.AddDays(1).AddHours(1),
                        Provider = "AI-Generated",
                        AccountId = "Local",
                        CreatedAt = DateTime.UtcNow,
                        UpdatedAt = DateTime.UtcNow
                    });
                }
                
                _logger.Information("Generated {Count} calendar suggestions", suggestions.Count);
                return suggestions;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error generating calendar suggestions");
                return new List<CalendarEvent>();
            }
        }

        public async Task<string> SummarizeDocumentAsync(string content, string provider = "hybrid")
        {
            try
            {
                _logger.Information("Summarizing document using provider: {Provider}", provider);
                
                var prompt = $@"
Please provide a concise summary of the following document:

{content}

Summary should be:
- Clear and concise
- Highlight key points
- Maximum 5 bullets
";
                return await GetResponseAsync(prompt, provider);
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error summarizing document");
                throw;
            }
        }

        private async Task<string> GetResponseFromCopilotAsync(string prompt)
        {
            if (string.IsNullOrWhiteSpace(_aiSettings.CopilotApiKey))
            {
                _logger.Warning("Copilot API key not configured. Returning placeholder response.");
                await Task.Delay(100);
                return $"[Copilot placeholder] No API key configured. Prompt received: {TruncatePrompt(prompt)}";
            }

            _logger.Information("Sending request to Copilot (OpenAI compatible) API.");
            var requestUrl = "https://api.openai.com/v1/chat/completions";
            var payload = new
            {
                model = "gpt-4o",
                messages = new[] { new { role = "user", content = prompt } }
            };

            using var request = new HttpRequestMessage(HttpMethod.Post, requestUrl);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _aiSettings.CopilotApiKey);
            request.Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");

            var response = await _httpClient.SendAsync(request);
            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync();
                _logger.Error("Copilot API error: {Error}", error);
                throw new Exception($"Copilot API error: {response.StatusCode}");
            }

            var responseBody = await response.Content.ReadAsStringAsync();
            using var document = JsonDocument.Parse(responseBody);
            return document.RootElement.GetProperty("choices")[0].GetProperty("message").GetProperty("content").GetString() ?? string.Empty;
        }

        private async Task<string> GetResponseFromGeminiAsync(string prompt)
        {
            if (string.IsNullOrWhiteSpace(_aiSettings.GeminiApiKey))
            {
                _logger.Warning("Gemini API key not configured. Returning placeholder response.");
                await Task.Delay(100);
                return $"[Gemini placeholder] No API key configured. Prompt received: {TruncatePrompt(prompt)}";
            }

            _logger.Information("Sending request to Google Gemini API.");
            var requestUrl = $"https://generativelanguage.googleapis.com/v1beta/models/gemini-1.5-flash:generateContent?key={_aiSettings.GeminiApiKey}";
            var payload = new
            {
                contents = new[]
                {
                    new { parts = new[] { new { text = prompt } } }
                }
            };

            var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
            var response = await _httpClient.PostAsync(requestUrl, content);

            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync();
                _logger.Error("Gemini API error: {Error}", error);
                throw new Exception($"Gemini API error: {response.StatusCode}");
            }

            var responseBody = await response.Content.ReadAsStringAsync();
            using var document = JsonDocument.Parse(responseBody);
            return document.RootElement.GetProperty("candidates")[0].GetProperty("content").GetProperty("parts")[0].GetProperty("text").GetString() ?? string.Empty;
        }

        private async Task<string> GetResponseFromYandexAsync(string prompt)
        {
            if (string.IsNullOrWhiteSpace(_aiSettings.YandexAiApiKey))
            {
                _logger.Warning("Yandex AI API key not configured. Returning placeholder response.");
                await Task.Delay(100);
                return $"[Yandex AI placeholder] No API key configured. Prompt received: {TruncatePrompt(prompt)}";
            }

            _logger.Information("Sending request to Yandex AI API.");
            var requestUrl = "https://llm.api.cloud.yandex.net/foundationModels/v1/completion";
            
            // YandexGPT expects the modelUri to include your folderId. 
            // We parse it assuming format "FolderID:ApiKey"
            var keyParts = _aiSettings.YandexAiApiKey.Split(':', 2);
            var folderId = keyParts.Length == 2 ? keyParts[0] : "YOUR_FOLDER_ID";
            var actualApiKey = keyParts.Length == 2 ? keyParts[1] : _aiSettings.YandexAiApiKey;

            var payload = new
            {
                modelUri = $"gpt://{folderId}/yandexgpt-lite",
                completionOptions = new { stream = false, temperature = 0.6, maxTokens = 1000 },
                messages = new[] { new { role = "user", text = prompt } }
            };

            using var request = new HttpRequestMessage(HttpMethod.Post, requestUrl);
            request.Headers.Authorization = new AuthenticationHeaderValue("Api-Key", actualApiKey);
            request.Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");

            var response = await _httpClient.SendAsync(request);
            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync();
                _logger.Error("Yandex AI error: {Error}", error);
                throw new Exception($"Yandex AI API error: {response.StatusCode}");
            }

            var responseBody = await response.Content.ReadAsStringAsync();
            using var document = JsonDocument.Parse(responseBody);
            return document.RootElement.GetProperty("result").GetProperty("alternatives")[0].GetProperty("message").GetProperty("text").GetString() ?? string.Empty;
        }

        private string TruncatePrompt(string prompt, int maxLength = 120)
        {
            if (string.IsNullOrWhiteSpace(prompt))
                return string.Empty;

            return prompt.Length <= maxLength ? prompt : prompt[..maxLength] + "...";
        }
    }
}
