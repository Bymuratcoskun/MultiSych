using Google.Apis.Auth.OAuth2;
using Google.Apis.Util.Store;
using MultiSych.Services.Interfaces;
using MultiSych.Services.Models;
using Serilog;
using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace MultiSych.Services.Implementations
{
    public class GoogleAuthenticationService : IAuthenticationService
    {
        private readonly ILogger _logger;
        private readonly string _tokenStorePath;
        private readonly HttpClient _httpClient;

        public GoogleAuthenticationService(IHttpClientFactory httpClientFactory, string? tokenStorePath = null)
        {
            _logger = Log.ForContext<GoogleAuthenticationService>();
            _tokenStorePath = tokenStorePath ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "MultiSych", "Tokens");
            _httpClient = httpClientFactory.CreateClient();
        }

        public async Task<AccountCredentials> AuthenticateGoogleAsync(string clientId, string clientSecret, string redirectUrl)
        {
            try
            {
                var clientSecrets = new ClientSecrets
                {
                    ClientId = clientId,
                    ClientSecret = clientSecret
                };

                var scopes = new[]
                {
                    "https://www.googleapis.com/auth/gmail.readonly",
                    "https://www.googleapis.com/auth/calendar",
                    "https://www.googleapis.com/auth/drive",
                    "https://www.googleapis.com/auth/userinfo.email",
                    "https://www.googleapis.com/auth/userinfo.profile"
                };

                _logger.Information("Google authentication initiated for clientId: {ClientId}", clientId);

                var dataStore = new FileDataStore(_tokenStorePath, true);
                var userCredential = await GoogleWebAuthorizationBroker.AuthorizeAsync(
                    clientSecrets,
                    scopes,
                    "user",
                    CancellationToken.None,
                    dataStore);

                var email = await GetGoogleUserEmailAsync(userCredential.Token.AccessToken) ?? userCredential.UserId;
                var accountId = GenerateAccountId("Google", email);

                var credentials = new AccountCredentials
                {
                    AccountId = accountId,
                    Email = email,
                    Provider = "Google",
                    AccessToken = userCredential.Token.AccessToken,
                    RefreshToken = userCredential.Token.RefreshToken,
                    ExpiresAt = userCredential.Token.IssuedUtc.AddSeconds(userCredential.Token.ExpiresInSeconds ?? 3600),
                    CreatedAt = DateTime.UtcNow,
                    AdditionalProperties = new Dictionary<string, object>
                    {
                        ["TokenType"] = userCredential.Token.TokenType ?? string.Empty,
                        ["Scopes"] = scopes
                    }
                };

                _logger.Information("Google authentication completed successfully for user: {Email}", email);
                return credentials;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error during Google authentication");
                throw;
            }
        }

        private async Task<string?> GetGoogleUserEmailAsync(string accessToken)
        {
            var request = new HttpRequestMessage(HttpMethod.Get, "https://www.googleapis.com/oauth2/v3/userinfo");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

            var response = await _httpClient.SendAsync(request);
            if (!response.IsSuccessStatusCode)
                return null;

            using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            if (document.RootElement.TryGetProperty("email", out var emailElement) && emailElement.GetString() is { Length: > 0 } email)
                return email;

            return null;
        }

        public async Task<AccountCredentials> AuthenticateMicrosoftAsync(string clientId, string clientSecret, string redirectUrl, string? tenantId = null)
        {
            const string provider = "Microsoft";
            var tenant = string.IsNullOrWhiteSpace(tenantId)
                ? Environment.GetEnvironmentVariable("MICROSOFT_TENANT_ID") ?? "common"
                : tenantId;
            var scopes = new[]
            {
                "openid",
                "offline_access",
                "profile",
                "User.Read",
                "Mail.Read",
                "Calendars.Read",
                "Files.ReadWrite"
            };

            var authorizationUrl = $"https://login.microsoftonline.com/{tenant}/oauth2/v2.0/authorize?client_id={Uri.EscapeDataString(clientId)}&response_type=code&redirect_uri={Uri.EscapeDataString(redirectUrl)}&response_mode=query&scope={Uri.EscapeDataString(string.Join(' ', scopes))}&prompt=select_account";
            var code = await AcquireAuthorizationCodeAsync(authorizationUrl, redirectUrl);

            var tokenJson = await ExchangeOAuthTokenAsync(
                $"https://login.microsoftonline.com/{tenant}/oauth2/v2.0/token",
                new Dictionary<string, string>
                {
                    ["client_id"] = clientId,
                    ["client_secret"] = clientSecret,
                    ["code"] = code,
                    ["redirect_uri"] = redirectUrl,
                    ["grant_type"] = "authorization_code",
                    ["scope"] = string.Join(' ', scopes)
                });

            var accessToken = tokenJson.GetProperty("access_token").GetString() ?? string.Empty;
            var refreshToken = tokenJson.TryGetProperty("refresh_token", out var refreshTokenElement)
                ? refreshTokenElement.GetString()
                : null;
            var expiresIn = tokenJson.TryGetProperty("expires_in", out var expiresInElement)
                ? expiresInElement.GetInt32()
                : 3600;
            var email = await GetMicrosoftUserEmailAsync(accessToken) ?? string.Empty;

            return new AccountCredentials
            {
                AccountId = GenerateAccountId(provider, email),
                Email = email,
                Provider = provider,
                AccessToken = accessToken,
                RefreshToken = refreshToken,
                ExpiresAt = DateTime.UtcNow.AddSeconds(expiresIn),
                CreatedAt = DateTime.UtcNow,
                AdditionalProperties = new Dictionary<string, object>
                {
                    ["TenantId"] = tenant,
                    ["Scopes"] = scopes
                }
            };
        }

        public async Task<AccountCredentials> AuthenticateYandexAsync(string clientId, string clientSecret, string redirectUrl)
        {
            const string provider = "Yandex";
            var scopes = new[]
            {
                "login:email",
                "cloud:disk.read",
                "cloud:disk.write",
                "mail:imap_full",
                "caldav:read",
                "caldav:write"
            };

            var authorizationUrl = $"https://oauth.yandex.com/authorize?response_type=code&client_id={Uri.EscapeDataString(clientId)}&redirect_uri={Uri.EscapeDataString(redirectUrl)}&scope={Uri.EscapeDataString(string.Join(' ', scopes))}";
            var code = await AcquireAuthorizationCodeAsync(authorizationUrl, redirectUrl);

            var tokenJson = await ExchangeOAuthTokenAsync(
                "https://oauth.yandex.com/token",
                new Dictionary<string, string>
                {
                    ["grant_type"] = "authorization_code",
                    ["code"] = code,
                    ["client_id"] = clientId,
                    ["client_secret"] = clientSecret,
                    ["redirect_uri"] = redirectUrl
                });

            var accessToken = tokenJson.GetProperty("access_token").GetString() ?? string.Empty;
            var refreshToken = tokenJson.TryGetProperty("refresh_token", out var refreshTokenElement)
                ? refreshTokenElement.GetString()
                : null;
            var expiresIn = tokenJson.TryGetProperty("expires_in", out var expiresInElement)
                ? expiresInElement.GetInt32()
                : 3600;
            var email = await GetYandexUserEmailAsync(accessToken) ?? string.Empty;

            return new AccountCredentials
            {
                AccountId = GenerateAccountId(provider, email),
                Email = email,
                Provider = provider,
                AccessToken = accessToken,
                RefreshToken = refreshToken,
                ExpiresAt = DateTime.UtcNow.AddSeconds(expiresIn),
                CreatedAt = DateTime.UtcNow,
                AdditionalProperties = new Dictionary<string, object>
                {
                    ["Scopes"] = scopes
                }
            };
        }

        private async Task<string?> GetMicrosoftUserEmailAsync(string accessToken)
        {
            var request = new HttpRequestMessage(HttpMethod.Get, "https://graph.microsoft.com/v1.0/me?$select=mail,userPrincipalName");
            request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);

            var response = await _httpClient.SendAsync(request);
            if (!response.IsSuccessStatusCode)
                return null;

            using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            var root = document.RootElement;
            if (root.TryGetProperty("mail", out var mail) && mail.GetString() is { Length: > 0 } mailValue)
                return mailValue;

            if (root.TryGetProperty("userPrincipalName", out var upn) && upn.GetString() is { Length: > 0 } upnValue)
                return upnValue;

            return null;
        }

        private async Task<string?> GetYandexUserEmailAsync(string accessToken)
        {
            var request = new HttpRequestMessage(HttpMethod.Get, "https://login.yandex.ru/info?format=json");
            request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);

            var response = await _httpClient.SendAsync(request);
            if (!response.IsSuccessStatusCode)
                return null;

            using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            if (document.RootElement.TryGetProperty("default_email", out var defaultEmail) && defaultEmail.GetString() is { Length: > 0 } defaultEmailValue)
                return defaultEmailValue;

            return null;
        }

        private async Task<string> AcquireAuthorizationCodeAsync(string authorizationUrl, string redirectUrl)
        {
            ValidateLocalRedirectUrl(redirectUrl);
            var uri = new Uri(redirectUrl);
            var prefix = $"{uri.Scheme}://{uri.Host}:{uri.Port}/";
            using var listener = new HttpListener();
            listener.Prefixes.Add(prefix);
            listener.Start();

            OpenBrowser(authorizationUrl);

            var context = await listener.GetContextAsync();
            var request = context.Request;
            var response = context.Response;

            var responseHtml = "<html><body><h1>Authentication completed</h1><p>You may now close this window.</p></body></html>";
            var buffer = Encoding.UTF8.GetBytes(responseHtml);
            response.ContentLength64 = buffer.Length;
            response.ContentType = "text/html";
            await response.OutputStream.WriteAsync(buffer);
            response.OutputStream.Close();
            listener.Stop();

            if (!string.IsNullOrEmpty(request.QueryString["error"]))
            {
                var error = request.QueryString["error"];
                var errorDescription = request.QueryString["error_description"];
                throw new InvalidOperationException($"OAuth authorization failed: {error} {errorDescription}");
            }

            var code = request.QueryString["code"];
            if (string.IsNullOrEmpty(code))
                throw new InvalidOperationException("Authorization code was not returned from the OAuth provider.");

            return code;
        }

        private async Task<JsonElement> ExchangeOAuthTokenAsync(string tokenUrl, Dictionary<string, string> parameters)
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, tokenUrl)
            {
                Content = new FormUrlEncodedContent(parameters)
            };

            var response = await _httpClient.SendAsync(request);
            var body = await response.Content.ReadAsStringAsync();
            if (!response.IsSuccessStatusCode)
            {
                _logger.Error("Token exchange failed: {StatusCode} - {Body}", response.StatusCode, body);
                throw new InvalidOperationException($"Token exchange failed: {body}");
            }

            using var document = JsonDocument.Parse(body);
            return document.RootElement.Clone();
        }

        private static string GenerateAccountId(string provider, string email)
        {
            var normalized = (email ?? string.Empty).Trim().ToLowerInvariant();
            return string.IsNullOrEmpty(normalized)
                ? $"{provider}-{Guid.NewGuid():N}"
                : $"{provider}-{normalized}";
        }

        private static void OpenBrowser(string url)
        {
            try
            {
                var processInfo = new ProcessStartInfo
                {
                    FileName = url,
                    UseShellExecute = true
                };

                if (OperatingSystem.IsWindows())
                {
                    processInfo.FileName = url;
                }
                else if (OperatingSystem.IsLinux())
                {
                    processInfo.FileName = "xdg-open";
                    processInfo.ArgumentList.Add(url);
                    processInfo.UseShellExecute = false;
                }
                else if (OperatingSystem.IsMacOS())
                {
                    processInfo.FileName = "open";
                    processInfo.ArgumentList.Add(url);
                    processInfo.UseShellExecute = false;
                }

                Process.Start(processInfo);
            }
            catch (Exception ex)
            {
                Log.ForContext<GoogleAuthenticationService>().Warning(ex, "Unable to open browser automatically. Please open the URL manually: {Url}", url);
                Console.WriteLine($"Please open the following URL in your browser:\n{url}");
            }
        }

        private static void ValidateLocalRedirectUrl(string redirectUrl)
        {
            if (!Uri.TryCreate(redirectUrl, UriKind.Absolute, out var uri))
                throw new InvalidOperationException("Redirect URL must be a valid absolute URI.");

            if (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps)
                throw new InvalidOperationException("Redirect URL must use http or https.");

            if (uri.Host != "localhost" && uri.Host != "127.0.0.1")
                throw new InvalidOperationException("Redirect URL must target localhost or 127.0.0.1 for local OAuth callbacks.");

            if (uri.Port <= 0)
                throw new InvalidOperationException("Redirect URL must specify a port.");
        }

        public async Task RefreshTokenAsync(AccountCredentials credentials)
        {
            if (credentials == null || string.IsNullOrEmpty(credentials.RefreshToken))
                throw new ArgumentException("Invalid credentials or refresh token");

            try
            {
                _logger.Information("Refreshing token for provider: {Provider}", credentials.Provider);
                // Token refresh logic would go here
                credentials.ExpiresAt = DateTime.UtcNow.AddHours(1);
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error refreshing token");
                throw;
            }
        }

        public async Task RevokeTokenAsync(AccountCredentials credentials)
        {
            if (credentials == null)
                throw new ArgumentException("Invalid credentials");

            try
            {
                _logger.Information("Revoking token for provider: {Provider}", credentials.Provider);
                // Token revocation logic would go here
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error revoking token");
                throw;
            }
        }

        public bool IsTokenExpired(AccountCredentials credentials)
        {
            if (credentials == null)
                return true;

            return DateTime.UtcNow >= credentials.ExpiresAt.AddMinutes(-5);
        }
    }

    public class OAuth2Initializer
    {
        public string ClientId { get; set; }
        public string ClientSecret { get; set; }
        public List<string> Scopes { get; set; }

        public OAuth2Initializer(string clientId, string clientSecret)
        {
            ClientId = clientId;
            ClientSecret = clientSecret;
            Scopes = new List<string>();
        }
    }
}
