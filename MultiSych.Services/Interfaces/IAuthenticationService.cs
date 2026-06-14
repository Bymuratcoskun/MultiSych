using MultiSych.Services.Models;
using System.Threading.Tasks;

namespace MultiSych.Services.Interfaces
{
    public interface IAuthenticationService
    {
        Task<AccountCredentials> AuthenticateGoogleAsync(string clientId, string clientSecret, string redirectUrl);
        Task<AccountCredentials> AuthenticateMicrosoftAsync(string clientId, string clientSecret, string redirectUrl, string? tenantId = null);
        Task<AccountCredentials> AuthenticateYandexAsync(string clientId, string clientSecret, string redirectUrl);
        Task<bool> RefreshTokenAsync(AccountCredentials credentials);
        Task RevokeTokenAsync(AccountCredentials credentials);
        bool IsTokenExpired(AccountCredentials credentials);
    }
}
