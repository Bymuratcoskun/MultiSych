using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using MultiSych.Services.Interfaces;
using MultiSych.Services.Models;
using MultiSych.Services.Configuration;
using Serilog;

namespace MultiSych.Services.Implementations;

public class GoogleAuthenticationService : IAuthenticationService
{
    private readonly ILogger _logger = Log.ForContext<GoogleAuthenticationService>();
    private readonly MultiSychConfig _config;

    public GoogleAuthenticationService(MultiSychConfig config)
    {
        _config = config;
    }

    public async Task<AccountCredentials> AuthenticateGoogleAsync(string clientId, string clientSecret, string redirectUrl)
    {
        if (string.IsNullOrWhiteSpace(clientId) || string.IsNullOrWhiteSpace(clientSecret))
            throw new ArgumentException("Google ClientId and ClientSecret must be provided in settings.");

        if (!redirectUrl.EndsWith("/")) redirectUrl += "/";

        // PKCE Güvenlik Anahtarlarını Üret
        string codeVerifier = GenerateCodeVerifier();
        string codeChallenge = GenerateCodeChallenge(codeVerifier);

        var authUrl = $"https://accounts.google.com/o/oauth2/v2/auth?" +
                      $"client_id={clientId}&" +
                      $"redirect_uri={Uri.EscapeDataString(redirectUrl)}&" +
                      $"response_type=code&" +
                      $"scope={Uri.EscapeDataString("email profile https://www.googleapis.com/auth/calendar https://www.googleapis.com/auth/drive")}&" +
                      $"code_challenge={codeChallenge}&" +
                      $"code_challenge_method=S256&" +
                      $"access_type=offline&" +
                      $"prompt=consent"; // Her zaman Refresh Token vermesi için consent şarttır.

        // Loopback Dinleyici Başlat
        using var listener = new HttpListener();
        listener.Prefixes.Add(redirectUrl);
        listener.Start();
        _logger.Information("Listening for Google OAuth callback on {RedirectUrl}", redirectUrl);

        // Tarayıcıyı Aç
        OpenBrowser(authUrl);

        // Tarayıcıdan gelecek yanıtı bekle
        var context = await listener.GetContextAsync();
        var request = context.Request;
        var response = context.Response;

        string? code = request.QueryString.Get("code");
        string? error = request.QueryString.Get("error");

        // Tarayıcıya mesaj dön
        string responseString = string.IsNullOrEmpty(error) 
            ? "<html><body style='font-family:sans-serif; text-align:center; margin-top:50px;'><h2>Authentication successful!</h2><p>You can close this tab and return to MultiSych.</p></body></html>"
            : $"<html><body style='font-family:sans-serif; text-align:center; margin-top:50px; color:red;'><h2>Authentication failed!</h2><p>Error: {error}</p></body></html>";

        var buffer = Encoding.UTF8.GetBytes(responseString);
        response.ContentLength64 = buffer.Length;
        await response.OutputStream.WriteAsync(buffer, 0, buffer.Length);
        response.OutputStream.Close();
        listener.Stop();

        if (!string.IsNullOrEmpty(error) || string.IsNullOrEmpty(code))
            throw new Exception($"Google OAuth error: {error ?? "No authorization code returned."}");

        _logger.Information("Received authorization code. Exchanging for tokens...");

        // Code'u al ve Token ile takas et
        using var httpClient = new HttpClient();
        var tokenRequest = new Dictionary<string, string>
        {
            { "client_id", clientId },
            { "client_secret", clientSecret },
            { "code", code },
            { "code_verifier", codeVerifier },
            { "redirect_uri", redirectUrl },
            { "grant_type", "authorization_code" }
        };

        var tokenResponse = await httpClient.PostAsync("https://oauth2.googleapis.com/token", new FormUrlEncodedContent(tokenRequest));
        var tokenContent = await tokenResponse.Content.ReadAsStringAsync();

        if (!tokenResponse.IsSuccessStatusCode)
        {
            _logger.Error("Failed to get tokens: {Response}", tokenContent);
            throw new Exception("Failed to exchange authorization code for tokens.");
        }

        var tokenData = JsonSerializer.Deserialize<JsonElement>(tokenContent);
        string accessToken = tokenData.GetProperty("access_token").GetString() ?? "";
        string refreshToken = tokenData.TryGetProperty("refresh_token", out var rt) ? rt.GetString() ?? "" : "";
        int expiresIn = tokenData.GetProperty("expires_in").GetInt32();

        _logger.Information("Tokens acquired successfully. Fetching user profile...");

        // Hangi e-posta adresiyle giriş yapıldığını bul
        httpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);
        var userInfoResponse = await httpClient.GetAsync("https://www.googleapis.com/oauth2/v2/userinfo");
        
        string email = "unknown@google.com";
        if (userInfoResponse.IsSuccessStatusCode)
        {
            var userInfoContent = await userInfoResponse.Content.ReadAsStringAsync();
            var userInfoData = JsonSerializer.Deserialize<JsonElement>(userInfoContent);
            email = userInfoData.GetProperty("email").GetString() ?? email;
        }

        return new AccountCredentials
        {
            AccountId = Guid.NewGuid().ToString(),
            Email = email,
            Provider = "Google",
            AccessToken = accessToken,
            RefreshToken = refreshToken,
            ExpiresAt = DateTime.UtcNow.AddSeconds(expiresIn),
            CreatedAt = DateTime.UtcNow,
            AdditionalProperties = new Dictionary<string, object>()
        };
    }

    public async Task<AccountCredentials> AuthenticateMicrosoftAsync(string clientId, string clientSecret, string redirectUrl, string? tenantId)
    {
        if (string.IsNullOrWhiteSpace(clientId))
            throw new ArgumentException("Microsoft ClientId must be provided in settings.");

        if (!redirectUrl.EndsWith("/")) redirectUrl += "/";

        string tenant = string.IsNullOrWhiteSpace(tenantId) ? "common" : tenantId;
        string codeVerifier = GenerateCodeVerifier();
        string codeChallenge = GenerateCodeChallenge(codeVerifier);
        
        // Graph API üzerinde kullanılacak yetkiler (E-posta, Takvim, Drive okuma/yazma ve Offline Access)
        string scopes = "offline_access User.Read Mail.ReadWrite Calendars.ReadWrite Files.ReadWrite.All";

        var authUrl = $"https://login.microsoftonline.com/{tenant}/oauth2/v2.0/authorize?" +
                      $"client_id={clientId}&" +
                      $"redirect_uri={Uri.EscapeDataString(redirectUrl)}&" +
                      $"response_type=code&" +
                      $"scope={Uri.EscapeDataString(scopes)}&" +
                      $"code_challenge={codeChallenge}&" +
                      $"code_challenge_method=S256&" +
                      $"prompt=select_account";

        using var listener = new HttpListener();
        listener.Prefixes.Add(redirectUrl);
        listener.Start();
        _logger.Information("Listening for Microsoft OAuth callback on {RedirectUrl}", redirectUrl);

        OpenBrowser(authUrl);

        var context = await listener.GetContextAsync();
        var request = context.Request;
        var response = context.Response;

        string? code = request.QueryString.Get("code");
        string? error = request.QueryString.Get("error");
        string? errorDescription = request.QueryString.Get("error_description");

        string responseString = string.IsNullOrEmpty(error) 
            ? "<html><body style='font-family:sans-serif; text-align:center; margin-top:50px;'><h2>Authentication successful!</h2><p>You can close this tab and return to MultiSych.</p></body></html>"
            : $"<html><body style='font-family:sans-serif; text-align:center; margin-top:50px; color:red;'><h2>Authentication failed!</h2><p>Error: {error}<br/>{errorDescription}</p></body></html>";

        var buffer = Encoding.UTF8.GetBytes(responseString);
        response.ContentLength64 = buffer.Length;
        await response.OutputStream.WriteAsync(buffer, 0, buffer.Length);
        response.OutputStream.Close();
        listener.Stop();

        if (!string.IsNullOrEmpty(error) || string.IsNullOrEmpty(code))
            throw new Exception($"Microsoft OAuth error: {error} - {errorDescription ?? "No authorization code returned."}");

        _logger.Information("Received authorization code. Exchanging for tokens...");

        using var httpClient = new HttpClient();
        var tokenRequest = new Dictionary<string, string>
        {
            { "client_id", clientId },
            { "code", code },
            { "code_verifier", codeVerifier },
            { "redirect_uri", redirectUrl },
            { "grant_type", "authorization_code" }
        };

        // Microsoft "Desktop Application" türü kayıtlarda bazen secret gerektirmez, varsa ekliyoruz.
        if (!string.IsNullOrWhiteSpace(clientSecret))
        {
            tokenRequest.Add("client_secret", clientSecret);
        }

        var tokenResponse = await httpClient.PostAsync($"https://login.microsoftonline.com/{tenant}/oauth2/v2.0/token", new FormUrlEncodedContent(tokenRequest));
        var tokenContent = await tokenResponse.Content.ReadAsStringAsync();

        if (!tokenResponse.IsSuccessStatusCode)
        {
            _logger.Error("Failed to get tokens: {Response}", tokenContent);
            throw new Exception("Failed to exchange authorization code for tokens.");
        }

        var tokenData = JsonSerializer.Deserialize<JsonElement>(tokenContent);
        string accessToken = tokenData.GetProperty("access_token").GetString() ?? "";
        string refreshToken = tokenData.TryGetProperty("refresh_token", out var rt) ? rt.GetString() ?? "" : "";
        int expiresIn = tokenData.GetProperty("expires_in").GetInt32();

        _logger.Information("Tokens acquired successfully. Fetching user profile...");

        httpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);
        var userInfoResponse = await httpClient.GetAsync("https://graph.microsoft.com/v1.0/me");
        
        string email = "unknown@microsoft.com";
        if (userInfoResponse.IsSuccessStatusCode)
        {
            var userInfoContent = await userInfoResponse.Content.ReadAsStringAsync();
            var userInfoData = JsonSerializer.Deserialize<JsonElement>(userInfoContent);
            
            // Bazen bireysel hesaplarda 'userPrincipalName' e-posta iken kurumsallarda 'mail' olur
            if (userInfoData.TryGetProperty("mail", out var mailProp) && mailProp.ValueKind == JsonValueKind.String)
                email = mailProp.GetString() ?? email;
            else if (userInfoData.TryGetProperty("userPrincipalName", out var upnProp) && upnProp.ValueKind == JsonValueKind.String)
                email = upnProp.GetString() ?? email;
        }

        return new AccountCredentials
        {
            AccountId = Guid.NewGuid().ToString(),
            Email = email,
            Provider = "Microsoft",
            AccessToken = accessToken,
            RefreshToken = refreshToken,
            ExpiresAt = DateTime.UtcNow.AddSeconds(expiresIn),
            CreatedAt = DateTime.UtcNow,
            AdditionalProperties = new Dictionary<string, object>()
        };
    }

    public async Task<AccountCredentials> AuthenticateYandexAsync(string clientId, string clientSecret, string redirectUrl)
    {
        if (string.IsNullOrWhiteSpace(clientId) || string.IsNullOrWhiteSpace(clientSecret))
            throw new ArgumentException("Yandex ClientId and ClientSecret must be provided in settings.");

        if (!redirectUrl.EndsWith("/")) redirectUrl += "/";

        var authUrl = $"https://oauth.yandex.com/authorize?" +
                      $"response_type=code&" +
                      $"client_id={clientId}&" +
                      $"redirect_uri={Uri.EscapeDataString(redirectUrl)}&" +
                      $"force_confirm=yes";

        using var listener = new HttpListener();
        listener.Prefixes.Add(redirectUrl);
        listener.Start();
        _logger.Information("Listening for Yandex OAuth callback on {RedirectUrl}", redirectUrl);

        OpenBrowser(authUrl);

        var context = await listener.GetContextAsync();
        var request = context.Request;
        var response = context.Response;

        string? code = request.QueryString.Get("code");
        string? error = request.QueryString.Get("error");
        string? errorDescription = request.QueryString.Get("error_description");

        string responseString = string.IsNullOrEmpty(error) 
            ? "<html><body style='font-family:sans-serif; text-align:center; margin-top:50px;'><h2>Authentication successful!</h2><p>You can close this tab and return to MultiSych.</p></body></html>"
            : $"<html><body style='font-family:sans-serif; text-align:center; margin-top:50px; color:red;'><h2>Authentication failed!</h2><p>Error: {error}<br/>{errorDescription}</p></body></html>";

        var buffer = Encoding.UTF8.GetBytes(responseString);
        response.ContentLength64 = buffer.Length;
        await response.OutputStream.WriteAsync(buffer, 0, buffer.Length);
        response.OutputStream.Close();
        listener.Stop();

        if (!string.IsNullOrEmpty(error) || string.IsNullOrEmpty(code))
            throw new Exception($"Yandex OAuth error: {error} - {errorDescription ?? "No authorization code returned."}");

        _logger.Information("Received authorization code. Exchanging for tokens...");

        using var httpClient = new HttpClient();
        var tokenRequest = new Dictionary<string, string>
        {
            { "grant_type", "authorization_code" },
            { "code", code },
            { "client_id", clientId },
            { "client_secret", clientSecret }
        };

        var tokenResponse = await httpClient.PostAsync("https://oauth.yandex.com/token", new FormUrlEncodedContent(tokenRequest));
        var tokenContent = await tokenResponse.Content.ReadAsStringAsync();

        if (!tokenResponse.IsSuccessStatusCode)
        {
            _logger.Error("Failed to get tokens: {Response}", tokenContent);
            throw new Exception("Failed to exchange authorization code for tokens.");
        }

        var tokenData = JsonSerializer.Deserialize<JsonElement>(tokenContent);
        string accessToken = tokenData.GetProperty("access_token").GetString() ?? "";
        string refreshToken = tokenData.TryGetProperty("refresh_token", out var rt) ? rt.GetString() ?? "" : "";
        int expiresIn = tokenData.GetProperty("expires_in").GetInt32();

        _logger.Information("Tokens acquired successfully. Fetching user profile...");

        // Yandex'te yetkilendirme başlığı "OAuth" ön ekiyle gönderilir.
        httpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("OAuth", accessToken);
        var userInfoResponse = await httpClient.GetAsync("https://login.yandex.ru/info?format=json");
        
        string email = "unknown@yandex.com";
        if (userInfoResponse.IsSuccessStatusCode)
        {
            var userInfoContent = await userInfoResponse.Content.ReadAsStringAsync();
            var userInfoData = JsonSerializer.Deserialize<JsonElement>(userInfoContent);
            
            if (userInfoData.TryGetProperty("default_email", out var mailProp) && mailProp.ValueKind == JsonValueKind.String)
                email = mailProp.GetString() ?? email;
        }

        return new AccountCredentials
        {
            AccountId = Guid.NewGuid().ToString(),
            Email = email,
            Provider = "Yandex",
            AccessToken = accessToken,
            RefreshToken = refreshToken,
            ExpiresAt = DateTime.UtcNow.AddSeconds(expiresIn),
            CreatedAt = DateTime.UtcNow,
            AdditionalProperties = new Dictionary<string, object>()
        };
    }

    private void OpenBrowser(string url)
    {
        try { Process.Start(new ProcessStartInfo { FileName = url, UseShellExecute = true }); }
        catch
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                Process.Start(new ProcessStartInfo("cmd", $"/c start {url.Replace("&", "^&")}") { CreateNoWindow = true });
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux)) Process.Start("xdg-open", url);
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX)) Process.Start("open", url);
        }
    }

    private string GenerateCodeVerifier()
    {
        var bytes = new byte[32];
        RandomNumberGenerator.Fill(bytes);
        return Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    }

    private string GenerateCodeChallenge(string verifier) => 
        Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(verifier))).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    public async Task<bool> RefreshTokenAsync(AccountCredentials credentials)
    {
        if (credentials == null) throw new ArgumentNullException(nameof(credentials));
        
        try
        {
            using var httpClient = new HttpClient();

            if (credentials.Provider == "Google")
            {
                var clientId = _config.Google?.ClientId;
                var clientSecret = _config.Google?.ClientSecret;

                if (string.IsNullOrEmpty(clientId) || string.IsNullOrEmpty(clientSecret)) return false;

                var request = new Dictionary<string, string>
                {
                    { "client_id", clientId },
                    { "client_secret", clientSecret },
                    { "refresh_token", credentials.RefreshToken ?? string.Empty }
                };

                var response = await httpClient.PostAsync("https://oauth2.googleapis.com/token", new FormUrlEncodedContent(request));
                if (response.IsSuccessStatusCode)
                {
                    var tokenData = JsonSerializer.Deserialize<JsonElement>(await response.Content.ReadAsStringAsync());
                    credentials.AccessToken = tokenData.GetProperty("access_token").GetString() ?? credentials.AccessToken;
                    credentials.ExpiresAt = DateTime.UtcNow.AddSeconds(tokenData.GetProperty("expires_in").GetInt32());
                    return true;
                }
            }
            else if (credentials.Provider == "Microsoft")
            {
                var clientId = _config.Microsoft?.ClientId;
                var tenant = _config.Microsoft?.TenantId ?? "common";

                if (string.IsNullOrEmpty(clientId)) return false;

                var request = new Dictionary<string, string>
                {
                    { "client_id", clientId },
                    { "refresh_token", credentials.RefreshToken ?? string.Empty },
                    { "grant_type", "refresh_token" }
                };

                if (!string.IsNullOrEmpty(_config.Microsoft?.ClientSecret)) request.Add("client_secret", _config.Microsoft.ClientSecret);

                var response = await httpClient.PostAsync($"https://login.microsoftonline.com/{tenant}/oauth2/v2.0/token", new FormUrlEncodedContent(request));
                if (response.IsSuccessStatusCode)
                {
                    var tokenData = JsonSerializer.Deserialize<JsonElement>(await response.Content.ReadAsStringAsync());
                    credentials.AccessToken = tokenData.GetProperty("access_token").GetString() ?? credentials.AccessToken;
                    if (tokenData.TryGetProperty("refresh_token", out var rt)) credentials.RefreshToken = rt.GetString() ?? credentials.RefreshToken;
                    credentials.ExpiresAt = DateTime.UtcNow.AddSeconds(tokenData.GetProperty("expires_in").GetInt32());
                    return true;
                }
            }
            else if (credentials.Provider == "Yandex")
            {
                var clientId = _config.Yandex?.ClientId;
                var clientSecret = _config.Yandex?.ClientSecret;

                if (string.IsNullOrEmpty(clientId) || string.IsNullOrEmpty(clientSecret)) return false;

                var request = new Dictionary<string, string>
                {
                    { "grant_type", "refresh_token" },
                    { "refresh_token", credentials.RefreshToken ?? string.Empty },
                    { "client_id", clientId },
                    { "client_secret", clientSecret }
                };

                var response = await httpClient.PostAsync("https://oauth.yandex.com/token", new FormUrlEncodedContent(request));
                if (response.IsSuccessStatusCode)
                {
                    var tokenData = JsonSerializer.Deserialize<JsonElement>(await response.Content.ReadAsStringAsync());
                    credentials.AccessToken = tokenData.GetProperty("access_token").GetString() ?? credentials.AccessToken;
                    if (tokenData.TryGetProperty("refresh_token", out var rt)) credentials.RefreshToken = rt.GetString() ?? credentials.RefreshToken;
                    credentials.ExpiresAt = DateTime.UtcNow.AddSeconds(tokenData.GetProperty("expires_in").GetInt32());
                    return true;
                }
            }
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error refreshing token for {Provider}", credentials.Provider);
        }
        return false;
    }

    public async Task RevokeTokenAsync(AccountCredentials credentials)
    {
        if (credentials == null) throw new ArgumentNullException(nameof(credentials));
        
        try 
        {
            using var httpClient = new HttpClient();
            if (credentials.Provider == "Google")
            {
                await httpClient.PostAsync($"https://oauth2.googleapis.com/revoke?token={credentials.AccessToken}", null);
            }
            _logger.Information("Token revocation requested for {Provider} account: {Email}", credentials.Provider, credentials.Email);
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Failed to revoke token for {Provider}", credentials.Provider);
        }
    }

    public bool IsTokenExpired(AccountCredentials credentials)
    {
        if (credentials == null) throw new ArgumentNullException(nameof(credentials));
        return credentials.ExpiresAt <= DateTime.UtcNow.AddMinutes(5);
    }
}
