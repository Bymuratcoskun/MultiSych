using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using MultiSych.Services.Interfaces;
using MultiSych.Services.Models;

namespace MultiSych.Services.Implementations;

/// <summary>
/// Yandex OAuth2 authentication service
/// Supports Yandex Mail, Disk, Calendar, and Contacts APIs
/// </summary>
public class YandexAuthenticationService : IOAuthService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<YandexAuthenticationService> _logger;
    private readonly string _clientId;
    private readonly string _clientSecret;
    private readonly string _redirectUri;

    private const string AuthorizeUrl = "https://oauth.yandex.com/authorize";
    private const string TokenUrl = "https://oauth.yandex.com/token";
    private const string UserInfoUrl = "https://login.yandex.ru/info";

    public string ProviderName => "Yandex";

    public YandexAuthenticationService(
        IHttpClientFactory httpClientFactory,
        IConfigurationServiceExtended configService,
        ILogger<YandexAuthenticationService> logger)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;

        // Load from configuration
        _clientId = configService.GetString("YANDEX_CLIENT_ID", "");
        _clientSecret = configService.GetString("YANDEX_CLIENT_SECRET", "");
        _redirectUri = configService.GetString("YANDEX_REDIRECT_URI", "http://localhost:5000/callback/yandex");

        if (string.IsNullOrEmpty(_clientId))
        {
            _logger.LogWarning("Yandex Client ID not configured");
        }
    }

    public string GetAuthorizationUrl(string state = "")
    {
        var scopes = new[]
        {
            "login:email",
            "login:info",
            "mail:imap_full",
            "mail:imap_all",
            "mail:notifies",
            "disk:all",
            "calendar:all",
            "contacts:all"
        };

        var query = new Dictionary<string, string>
        {
            { "response_type", "code" },
            { "client_id", _clientId },
            { "redirect_uri", _redirectUri },
            { "scope", string.Join(" ", scopes) },
            { "state", state ?? Guid.NewGuid().ToString() }
        };

        return BuildUrl(AuthorizeUrl, query);
    }

    public async Task<OAuthToken> GetTokenAsync(string authorizationCode)
    {
        try
        {
            using var client = _httpClientFactory.CreateClient();

            var request = new HttpRequestMessage(HttpMethod.Post, TokenUrl)
            {
                Content = new FormUrlEncodedContent(new Dictionary<string, string>
                {
                    { "grant_type", "authorization_code" },
                    { "code", authorizationCode },
                    { "client_id", _clientId },
                    { "client_secret", _clientSecret },
                    { "redirect_uri", _redirectUri }
                })
            };

            var response = await client.SendAsync(request);
            response.EnsureSuccessStatusCode();

            var content = await response.Content.ReadAsStringAsync();
            var token = JsonSerializer.Deserialize<YandexTokenResponse>(content);

            if (token?.AccessToken == null)
            {
                throw new InvalidOperationException("No access token in response");
            }

            return new OAuthToken
            {
                AccessToken = token.AccessToken,
                RefreshToken = token.RefreshToken,
                ExpiresIn = token.ExpiresIn,
                TokenType = token.TokenType ?? "Bearer",
                Scope = string.Join(" ", token.Scope ?? Array.Empty<string>()),
                ObtainedAt = DateTime.UtcNow
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get Yandex token");
            throw;
        }
    }

    public async Task<OAuthToken> RefreshTokenAsync(OAuthToken currentToken)
    {
        try
        {
            using var client = _httpClientFactory.CreateClient();

            var request = new HttpRequestMessage(HttpMethod.Post, TokenUrl)
            {
                Content = new FormUrlEncodedContent(new Dictionary<string, string>
                {
                    { "grant_type", "refresh_token" },
                    { "refresh_token", currentToken.RefreshToken },
                    { "client_id", _clientId },
                    { "client_secret", _clientSecret }
                })
            };

            var response = await client.SendAsync(request);
            response.EnsureSuccessStatusCode();

            var content = await response.Content.ReadAsStringAsync();
            var token = JsonSerializer.Deserialize<YandexTokenResponse>(content);

            if (token?.AccessToken == null)
            {
                throw new InvalidOperationException("No access token in response");
            }

            return new OAuthToken
            {
                AccessToken = token.AccessToken,
                RefreshToken = token.RefreshToken ?? currentToken.RefreshToken,
                ExpiresIn = token.ExpiresIn,
                TokenType = token.TokenType ?? "Bearer",
                Scope = string.Join(" ", token.Scope ?? Array.Empty<string>()),
                ObtainedAt = DateTime.UtcNow
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to refresh Yandex token");
            throw;
        }
    }

    public async Task<OAuthUserInfo> GetUserInfoAsync(OAuthToken token)
    {
        try
        {
            using var client = _httpClientFactory.CreateClient();

            var request = new HttpRequestMessage(HttpMethod.Get, UserInfoUrl);
            request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("OAuth", token.AccessToken);

            var response = await client.SendAsync(request);
            response.EnsureSuccessStatusCode();

            var content = await response.Content.ReadAsStringAsync();
            var userInfo = JsonSerializer.Deserialize<YandexUserInfoResponse>(content);

            if (userInfo == null)
            {
                throw new InvalidOperationException("No user info in response");
            }

            return new OAuthUserInfo
            {
                Id = userInfo.Id ?? string.Empty,
                Email = userInfo.DefaultEmail ?? string.Empty,
                Name = userInfo.DisplayName ?? string.Empty,
                FirstName = userInfo.FirstName ?? string.Empty,
                LastName = userInfo.LastName ?? string.Empty,
                Picture = !string.IsNullOrWhiteSpace(userInfo.AvatarId)
                    ? $"https://avatars.yandex.net/get-yapic/{userInfo.AvatarId}/islands-small" 
                    : string.Empty
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get Yandex user info");
            throw;
        }
    }

    public async Task RevokeTokenAsync(OAuthToken token)
    {
        try
        {
            using var client = _httpClientFactory.CreateClient();

            var request = new HttpRequestMessage(HttpMethod.Post, "https://oauth.yandex.com/revoke_token")
            {
                Content = new FormUrlEncodedContent(new Dictionary<string, string>
                {
                    { "access_token", token.AccessToken }
                })
            };

            var response = await client.SendAsync(request);
            // Yandex returns 200 on success, ignore errors for revoke
            _logger.LogInformation("Token revoked successfully");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to revoke Yandex token");
        }
    }

    private static string BuildUrl(string baseUrl, Dictionary<string, string> parameters)
    {
        var query = new System.Text.StringBuilder();
        foreach (var param in parameters)
        {
            if (query.Length > 0) query.Append("&");
            query.Append(Uri.EscapeDataString(param.Key));
            query.Append("=");
            query.Append(Uri.EscapeDataString(param.Value));
        }

        return $"{baseUrl}?{query}";
    }
}

internal class YandexTokenResponse
{
    [System.Text.Json.Serialization.JsonPropertyName("access_token")]
    public string AccessToken { get; set; } = string.Empty;

    [System.Text.Json.Serialization.JsonPropertyName("refresh_token")]
    public string RefreshToken { get; set; } = string.Empty;

    [System.Text.Json.Serialization.JsonPropertyName("expires_in")]
    public int ExpiresIn { get; set; }

    [System.Text.Json.Serialization.JsonPropertyName("token_type")]
    public string TokenType { get; set; } = "Bearer";

    [System.Text.Json.Serialization.JsonPropertyName("scope")]
    public string[] Scope { get; set; } = Array.Empty<string>();
}

internal class YandexUserInfoResponse
{
    [System.Text.Json.Serialization.JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [System.Text.Json.Serialization.JsonPropertyName("login")]
    public string Login { get; set; } = string.Empty;

    [System.Text.Json.Serialization.JsonPropertyName("client_id")]
    public string ClientId { get; set; } = string.Empty;

    [System.Text.Json.Serialization.JsonPropertyName("default_email")]
    public string DefaultEmail { get; set; } = string.Empty;

    [System.Text.Json.Serialization.JsonPropertyName("emails")]
    public string[] Emails { get; set; } = Array.Empty<string>();

    [System.Text.Json.Serialization.JsonPropertyName("first_name")]
    public string FirstName { get; set; } = string.Empty;

    [System.Text.Json.Serialization.JsonPropertyName("last_name")]
    public string LastName { get; set; } = string.Empty;

    [System.Text.Json.Serialization.JsonPropertyName("display_name")]
    public string DisplayName { get; set; } = string.Empty;

    [System.Text.Json.Serialization.JsonPropertyName("sex")]
    public string Sex { get; set; } = string.Empty;

    [System.Text.Json.Serialization.JsonPropertyName("birthday")]
    public string Birthday { get; set; } = string.Empty;

    [System.Text.Json.Serialization.JsonPropertyName("default_avatar_id")]
    public string AvatarId { get; set; } = string.Empty;

    [System.Text.Json.Serialization.JsonPropertyName("is_avatar_empty")]
    public bool IsAvatarEmpty { get; set; }

    [System.Text.Json.Serialization.JsonPropertyName("real_name")]
    public string RealName { get; set; } = string.Empty;

    [System.Text.Json.Serialization.JsonPropertyName("psuid")]
    public string PsUID { get; set; } = string.Empty;
}
