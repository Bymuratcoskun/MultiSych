using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Text.Json;
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