using System;

namespace MultiSych.Services.Models;

/// <summary>
/// OAuth2 Token Model
/// </summary>
public class OAuthToken
{
    /// <summary>
    /// Access token for API calls
    /// </summary>
    public string AccessToken { get; set; } = string.Empty;

    /// <summary>
    /// Refresh token for getting new access tokens
    /// </summary>
    public string RefreshToken { get; set; } = string.Empty;

    /// <summary>
    /// Lifetime of access token in seconds
    /// </summary>
    public int ExpiresIn { get; set; }

    /// <summary>
    /// Type of token (usually "Bearer")
    /// </summary>
    public string TokenType { get; set; } = "Bearer";

    /// <summary>
    /// Comma-separated list of granted scopes
    /// </summary>
    public string Scope { get; set; } = string.Empty;

    /// <summary>
    /// When this token was obtained
    /// </summary>
    public DateTime ObtainedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// When this token expires
    /// </summary>
    public DateTime ExpiresAt => ObtainedAt.AddSeconds(ExpiresIn);

    /// <summary>
    /// Check if token is expired
    /// </summary>
    public bool IsExpired => DateTime.UtcNow >= ExpiresAt.AddSeconds(-60); // 1 minute buffer

    /// <summary>
    /// Check if token can be refreshed
    /// </summary>
    public bool CanRefresh => !string.IsNullOrEmpty(RefreshToken);
}

/// <summary>
/// OAuth2 User Information
/// </summary>
public class OAuthUserInfo
{
    /// <summary>
    /// Unique user identifier from provider
    /// </summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// User's email address
    /// </summary>
    public string Email { get; set; } = string.Empty;

    /// <summary>
    /// Full name
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// First name
    /// </summary>
    public string FirstName { get; set; } = string.Empty;

    /// <summary>
    /// Last name
    /// </summary>
    public string LastName { get; set; } = string.Empty;

    /// <summary>
    /// Profile picture URL
    /// </summary>
    public string Picture { get; set; } = string.Empty;

    /// <summary>
    /// User locale
    /// </summary>
    public string Locale { get; set; } = string.Empty;

    /// <summary>
    /// Email verified flag
    /// </summary>
    public bool EmailVerified { get; set; }
}
