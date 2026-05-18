using MultiSych.Services.Models;

namespace MultiSych.Services.Interfaces
{
    public interface IAccountStore
    {
        Task<List<AccountCredentials>> GetAccountsAsync();
        Task SaveAccountAsync(AccountCredentials credentials);
        Task<AccountCredentials?> GetAccountByIdAsync(string accountId);
        Task DeleteAccountAsync(string accountId);
    }
}
