
using MultiSych.Services.Models;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace MultiSych.Services.Interfaces
{
    public interface IAccountStore
    {
        Task<List<AccountCredentials>> GetAccountsAsync();
        Task SaveAccountAsync(AccountCredentials credentials);
        Task<AccountCredentials?> GetAccountByIdAsync(string accountId);
        Task<AccountCredentials?> GetAccountAsync(string accountId);
        Task DeleteAccountAsync(string accountId);
    }
}
