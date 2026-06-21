using System.Threading.Tasks;
using MultiSych.Services.Models;

namespace MultiSych.Services.Interfaces;

/// <summary>
/// OAuth2 Authentication Service Interface
/// Supports multiple cloud providers (Google, Microsoft, Yandex)
/// </summary>
public interface IOAuthService
{
    /// <summary>
    /// Get the provider name
    /// </summary>
    string ProviderName { get; }

    /// <summary>
    /// Get authorization URL for user to authenticate
    /// </summary>
    string GetAuthorizationUrl(string state = "");

    /// <summary>
    /// Exchange authorization code for access token
    /// </summary>
    Task<OAuthToken> GetTokenAsync(string authorizationCode);

    /// <summary>
    /// Refresh access token using refresh token
    /// </summary>
    Task<OAuthToken> RefreshTokenAsync(OAuthToken currentToken);

    /// <summary>
    /// Get user information using access token
    /// </summary>
    Task<OAuthUserInfo> GetUserInfoAsync(OAuthToken token);

    /// <summary>
    /// Revoke/invalidate the access token
    /// </summary>
    Task RevokeTokenAsync(OAuthToken token);
}
