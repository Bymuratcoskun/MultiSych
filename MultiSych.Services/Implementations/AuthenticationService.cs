using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Identity.Client;
using MultiSych.Services.Interfaces;
using MultiSych.Services.Models;
using MultiSych.Services.Configuration;
using System.Linq;
using Serilog;

namespace MultiSych.Services.Implementations;

public class AuthenticationService : IAuthenticationService
{
    private readonly MultiSychConfig _config;

    public AuthenticationService(MultiSychConfig config)
    {
        _config = config;
    }
    public async Task<AccountCredentials> AuthenticateGoogleAsync(string clientId, string clientSecret, string redirectUrl)
    {
        if (string.IsNullOrWhiteSpace(clientId) || string.IsNullOrWhiteSpace(clientSecret))
            throw new ArgumentException("Google ClientId and ClientSecret must be provided in settings.");

        if (!redirectUrl.EndsWith("/")) redirectUrl += "/";

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
                      $"prompt=consent";

        var codeTask = ListenForCallbackAsync(redirectUrl);
        OpenBrowser(authUrl);

        var code = await codeTask;
        if (string.IsNullOrWhiteSpace(code))
            throw new OperationCanceledException("Google authentication did not return an authorization code.");

        Log.Information("Received authorization code. Exchanging for tokens...");

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
        tokenResponse.EnsureSuccessStatusCode();

        var tokenContent = await tokenResponse.Content.ReadAsStringAsync();
        using var tokenDocument = JsonDocument.Parse(tokenContent);
        var accessToken = tokenDocument.RootElement.GetProperty("access_token").GetString();
        var refreshToken = tokenDocument.RootElement.TryGetProperty("refresh_token", out var rt) ? rt.GetString() : string.Empty;
        var expiresIn = tokenDocument.RootElement.GetProperty("expires_in").GetInt32();

        Log.Information("Tokens acquired successfully. Fetching user profile...");

        httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        var userInfoResponse = await httpClient.GetAsync("https://www.googleapis.com/oauth2/v2/userinfo");
        userInfoResponse.EnsureSuccessStatusCode();
        
        var userInfoContent = await userInfoResponse.Content.ReadAsStringAsync();
        using var userInfoData = JsonDocument.Parse(userInfoContent);
        var email = userInfoData.RootElement.GetProperty("email").GetString();

        return new AccountCredentials
        {
            Provider = "Google",
            Email = email ?? "unknown@google.com",
            AccessToken = accessToken ?? string.Empty,
            RefreshToken = refreshToken ?? string.Empty,
            ExpiresAt = DateTime.UtcNow.AddSeconds(expiresIn),
            Scopes = "email profile calendar drive"
        };
    }

    public async Task<AccountCredentials> AuthenticateMicrosoftAsync(string clientId, string clientSecret, string redirectUrl, string? tenantId)
    {
        var app = PublicClientApplicationBuilder.Create(clientId)
            .WithAuthority(AzureCloudInstance.AzurePublic, tenantId ?? "common")
            .WithRedirectUri(redirectUrl)
            .Build();

        // Gerekli izin kapsamları
        string[] scopes = { "User.Read", "Mail.ReadWrite", "Calendars.ReadWrite", "Files.ReadWrite.All", "offline_access" };

        AuthenticationResult result;
        try
        {
            // MSAL, sistem tarayıcısını açıp süreci kendisi yönetecek
            result = await app.AcquireTokenInteractive(scopes).ExecuteAsync();
        }
        catch (MsalClientException ex) when (ex.ErrorCode == "authentication_canceled")
        {
            throw new OperationCanceledException("Microsoft authentication was canceled by the user.", ex);
        }

        // Kullanıcı bilgilerini (e-posta) almak için Microsoft Graph API'ye istek atıyoruz
        using var httpClient = new HttpClient();
        httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", result.AccessToken);
        var userResponse = await httpClient.GetAsync("https://graph.microsoft.com/v1.0/me");
        userResponse.EnsureSuccessStatusCode();
        
        var userJson = await userResponse.Content.ReadAsStringAsync();
        using var userDocument = JsonDocument.Parse(userJson);
        var email = userDocument.RootElement.GetProperty("userPrincipalName").GetString();

        return new AccountCredentials
        {
            Provider = "Microsoft",
            Email = email ?? "unknown@microsoft",
            AccessToken = result.AccessToken,
            // MSAL, refresh token'ı kendi token cache'inde güvenli bir şekilde yönetir.
            // Veritabanında direkt olarak saklamaya gerek yoktur, cache'in kendisi kalıcı hale getirilebilir.
            RefreshToken = "msal_managed", 
            ExpiresAt = result.ExpiresOn.UtcDateTime,
            Scopes = string.Join(" ", result.Scopes)
        };
    }

    public async Task<AccountCredentials> AuthenticateYandexAsync(string clientId, string clientSecret, string redirectUrl)
    {
        // 1. OAuth2 callback'ini dinlemek için yerel bir HTTP sunucusu başlat
        var codeTask = ListenForCallbackAsync(redirectUrl);

        // 2. Yandex yetkilendirme URL'sini oluştur ve sistem tarayıcısında aç
        var authUrl = $"https://oauth.yandex.com/authorize?response_type=code&client_id={clientId}&redirect_uri={redirectUrl}";
        OpenBrowser(authUrl);

        // 3. Kullanıcının giriş yapmasını ve yetkilendirme kodunun gelmesini bekle
        var code = await codeTask;
        if (string.IsNullOrWhiteSpace(code))
            throw new OperationCanceledException("Yandex authentication did not return an authorization code.");

        // 4. Gelen yetkilendirme kodunu Access Token ve Refresh Token ile takas et
        using var httpClient = new HttpClient();
        var tokenRequestContent = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["grant_type"] = "authorization_code",
            ["code"] = code,
            ["client_id"] = clientId,
            ["client_secret"] = clientSecret
        });

        var tokenResponse = await httpClient.PostAsync("https://oauth.yandex.com/token", tokenRequestContent);
        tokenResponse.EnsureSuccessStatusCode();
        
        var tokenJson = await tokenResponse.Content.ReadAsStringAsync();
        using var tokenDocument = JsonDocument.Parse(tokenJson);
        var accessToken = tokenDocument.RootElement.GetProperty("access_token").GetString();
        var refreshToken = tokenDocument.RootElement.GetProperty("refresh_token").GetString();
        var expiresIn = tokenDocument.RootElement.GetProperty("expires_in").GetInt32();

        // 5. Alınan Access Token ile kullanıcı bilgilerini (e-posta) çek
        httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("OAuth", accessToken);
        var userResponse = await httpClient.GetAsync("https://login.yandex.ru/info?format=json");
        userResponse.EnsureSuccessStatusCode();
        
        var userJson = await userResponse.Content.ReadAsStringAsync();
        using var userDocument = JsonDocument.Parse(userJson);
        var email = userDocument.RootElement.GetProperty("default_email").GetString();

        return new AccountCredentials
        {
            Provider = "Yandex",
            Email = email ?? "unknown@yandex",
            AccessToken = accessToken ?? string.Empty,
            RefreshToken = refreshToken ?? string.Empty,
            ExpiresAt = DateTime.UtcNow.AddSeconds(expiresIn),
            Scopes = "login:info mail:read" // Örnek kapsamlar
        };
    }

    public async Task<bool> RefreshTokenAsync(AccountCredentials credentials)
    {
        if (credentials == null) return false;

        if (string.Equals(credentials.Provider, "Google", StringComparison.OrdinalIgnoreCase))
        {
            if (string.IsNullOrWhiteSpace(credentials.RefreshToken))
                return false;

            try
            {
                var clientId = _config.Google?.ClientId;
                var clientSecret = _config.Google?.ClientSecret;

                if (string.IsNullOrWhiteSpace(clientId) || string.IsNullOrWhiteSpace(clientSecret))
                {
                    Log.Warning("Google credentials not configured in settings for token refresh.");
                    return false;
                }

                using var httpClient = new HttpClient();
                var tokenRequest = new Dictionary<string, string>
                {
                    { "client_id", clientId },
                    { "client_secret", clientSecret },
                    { "refresh_token", credentials.RefreshToken },
                    { "grant_type", "refresh_token" }
                };

                var response = await httpClient.PostAsync("https://oauth2.googleapis.com/token", new FormUrlEncodedContent(tokenRequest));
                if (!response.IsSuccessStatusCode)
                {
                    var err = await response.Content.ReadAsStringAsync();
                    Log.Error("Failed to refresh Google token: {Response}", err);
                    return false;
                }

                var tokenContent = await response.Content.ReadAsStringAsync();
                using var tokenDocument = JsonDocument.Parse(tokenContent);
                var accessToken = tokenDocument.RootElement.GetProperty("access_token").GetString();
                var expiresIn = tokenDocument.RootElement.GetProperty("expires_in").GetInt32();

                credentials.AccessToken = accessToken ?? string.Empty;
                credentials.ExpiresAt = DateTime.UtcNow.AddSeconds(expiresIn);
                
                if (tokenDocument.RootElement.TryGetProperty("refresh_token", out var rt))
                {
                    credentials.RefreshToken = rt.GetString() ?? credentials.RefreshToken;
                }

                return true;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to refresh Google token");
                return false;
            }
        }
        else if (string.Equals(credentials.Provider, "Yandex", StringComparison.OrdinalIgnoreCase))
        {
            if (string.IsNullOrWhiteSpace(credentials.RefreshToken))
                return false;

            try
            {
                var clientId = _config.Yandex?.ClientId;
                var clientSecret = _config.Yandex?.ClientSecret;

                if (string.IsNullOrWhiteSpace(clientId) || string.IsNullOrWhiteSpace(clientSecret))
                {
                    Log.Warning("Yandex credentials not configured in settings for token refresh.");
                    return false;
                }

                using var httpClient = new HttpClient();
                var tokenRequest = new Dictionary<string, string>
                {
                    { "grant_type", "refresh_token" },
                    { "refresh_token", credentials.RefreshToken },
                    { "client_id", clientId },
                    { "client_secret", clientSecret }
                };

                var response = await httpClient.PostAsync("https://oauth.yandex.com/token", new FormUrlEncodedContent(tokenRequest));
                if (!response.IsSuccessStatusCode)
                {
                    var err = await response.Content.ReadAsStringAsync();
                    Log.Error("Failed to refresh Yandex token: {Response}", err);
                    return false;
                }

                var tokenContent = await response.Content.ReadAsStringAsync();
                using var tokenDocument = JsonDocument.Parse(tokenContent);
                var accessToken = tokenDocument.RootElement.GetProperty("access_token").GetString();
                var expiresIn = tokenDocument.RootElement.GetProperty("expires_in").GetInt32();

                credentials.AccessToken = accessToken ?? string.Empty;
                credentials.ExpiresAt = DateTime.UtcNow.AddSeconds(expiresIn);

                if (tokenDocument.RootElement.TryGetProperty("refresh_token", out var rt))
                {
                    credentials.RefreshToken = rt.GetString() ?? credentials.RefreshToken;
                }

                return true;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to refresh Yandex token");
                return false;
            }
        }
        else if (string.Equals(credentials.Provider, "Microsoft", StringComparison.OrdinalIgnoreCase))
        {
            try
            {
                var clientId = _config.Microsoft?.ClientId;
                if (string.IsNullOrWhiteSpace(clientId))
                {
                    Log.Warning("Microsoft ClientId not configured in settings for token refresh.");
                    return false;
                }

                var app = PublicClientApplicationBuilder.Create(clientId)
                    .WithAuthority(AzureCloudInstance.AzurePublic, _config.Microsoft?.TenantId ?? "common")
                    .Build();

                var accounts = await app.GetAccountsAsync();
                var account = accounts.FirstOrDefault(a => a.Username.Equals(credentials.Email, StringComparison.OrdinalIgnoreCase));
                if (account != null)
                {
                    string[] scopes = { "User.Read", "Mail.ReadWrite", "Calendars.ReadWrite", "Files.ReadWrite.All" };
                    var result = await app.AcquireTokenSilent(scopes, account).ExecuteAsync();
                    credentials.AccessToken = result.AccessToken;
                    credentials.ExpiresAt = result.ExpiresOn.UtcDateTime;
                    return true;
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to refresh Microsoft token silently");
            }
        }

        return false;
    }

    public async Task RevokeTokenAsync(AccountCredentials credentials)
    {
        if (credentials == null || string.IsNullOrWhiteSpace(credentials.AccessToken)) return;

        using var httpClient = new HttpClient();
        if (string.Equals(credentials.Provider, "Google", StringComparison.OrdinalIgnoreCase))
        {
            try
            {
                var content = new FormUrlEncodedContent(new Dictionary<string, string>
                {
                    { "token", credentials.AccessToken }
                });
                var response = await httpClient.PostAsync("https://oauth2.googleapis.com/revoke", content);
                response.EnsureSuccessStatusCode();
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Failed to revoke Google token");
            }
        }
        else if (string.Equals(credentials.Provider, "Yandex", StringComparison.OrdinalIgnoreCase))
        {
            try
            {
                var content = new FormUrlEncodedContent(new Dictionary<string, string>
                {
                    { "access_token", credentials.AccessToken }
                });
                await httpClient.PostAsync("https://oauth.yandex.com/revoke_token", content);
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Failed to revoke Yandex token");
            }
        }
    }

    public bool IsTokenExpired(AccountCredentials credentials)
    {
        if (credentials == null) return true;
        return credentials.ExpiresAt <= DateTime.UtcNow.AddMinutes(1);
    }

    private async Task<string?> ListenForCallbackAsync(string redirectUrl)
    {
        using var listener = new HttpListener();
        listener.Prefixes.Add(redirectUrl);
        listener.Start();

        try
        {
            var context = await listener.GetContextAsync();
            var request = context.Request;
            var code = request.QueryString.Get("code");

            // Tarayıcıya işlemin başarılı olduğuna dair bir mesaj gönder
            var responseString = "<html><body><h1>Authentication successful!</h1><p>You can now close this browser tab.</p></body></html>";
            var buffer = Encoding.UTF8.GetBytes(responseString);
            var response = context.Response;
            response.ContentLength64 = buffer.Length;
            var output = response.OutputStream;
            await output.WriteAsync(buffer, 0, buffer.Length);
            output.Close();

            return code;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error in local OAuth listener.");
            return null;
        }
        finally
        {
            listener.Stop();
        }
    }

    private void OpenBrowser(string url)
    {
        try
        {
            Process.Start(url);
        }
        catch
        {
            // Farklı işletim sistemleri için fallback mekanizmaları
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                url = url.Replace("&", "^&");
                Process.Start(new ProcessStartInfo("cmd", $"/c start {url}") { CreateNoWindow = true });
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                Process.Start("xdg-open", url);
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                Process.Start("open", url);
            }
            else
            {
                throw;
            }
        }
    }

    private string GenerateCodeVerifier()
    {
        var bytes = new byte[32];
        System.Security.Cryptography.RandomNumberGenerator.Fill(bytes);
        return Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    }

    private string GenerateCodeChallenge(string verifier)
    {
        var hash = System.Security.Cryptography.SHA256.HashData(Encoding.UTF8.GetBytes(verifier));
        return Convert.ToBase64String(hash).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    }
}
