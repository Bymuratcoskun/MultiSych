using System.Threading.Tasks;

namespace MultiSych.Services.Interfaces
{
    public interface ISecureStorageService
    {
        Task SaveSecretAsync(string key, string value);
        Task<string?> GetSecretAsync(string key);
        Task DeleteSecretAsync(string key);
    }
}