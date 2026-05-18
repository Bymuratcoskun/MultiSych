using System.Linq;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using MultiSych.Services.Data;
using MultiSych.Services.Interfaces;
using MultiSych.Services.Models;
using Serilog;

namespace MultiSych.Services.Implementations
{
    public class AccountStoreService : IAccountStore
    {
        private readonly ILogger _logger;
        private readonly IDbContextFactory<LocalCacheDbContext> _dbContextFactory;

        public AccountStoreService(IDbContextFactory<LocalCacheDbContext> dbContextFactory)
        {
            _logger = Log.ForContext<AccountStoreService>();
            _dbContextFactory = dbContextFactory;

            using var context = _dbContextFactory.CreateDbContext();
            context.Database.EnsureCreated();
        }

        public async Task<List<AccountCredentials>> GetAccountsAsync()
        {
            try
            {
                await using var context = await _dbContextFactory.CreateDbContextAsync();
                var entities = await context.AccountCredentials
                    .AsNoTracking()
                    .ToListAsync();

                return entities.Select(ToModel).ToList();
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to read accounts from local cache database");
                return new List<AccountCredentials>();
            }
        }

        public async Task<AccountCredentials?> GetAccountByIdAsync(string accountId)
        {
            await using var context = await _dbContextFactory.CreateDbContextAsync();
            var entity = await context.AccountCredentials
                .AsNoTracking()
                .FirstOrDefaultAsync(a => a.AccountId == accountId);

            return entity is null ? null : ToModel(entity);
        }

        public async Task SaveAccountAsync(AccountCredentials credentials)
        {
            try
            {
                await using var context = await _dbContextFactory.CreateDbContextAsync();
                var existing = await context.AccountCredentials.FirstOrDefaultAsync(a => a.AccountId == credentials.AccountId);
                if (existing == null)
                {
                    await context.AccountCredentials.AddAsync(ToEntity(credentials));
                }
                else
                {
                    existing.Email = credentials.Email;
                    existing.Provider = credentials.Provider;
                    existing.AccessToken = credentials.AccessToken;
                    existing.RefreshToken = credentials.RefreshToken;
                    existing.ExpiresAt = credentials.ExpiresAt;
                    existing.CreatedAt = credentials.CreatedAt;
                    existing.AdditionalProperties = credentials.AdditionalProperties;
                }

                await context.SaveChangesAsync();
                _logger.Information("Saved account {AccountId} to local cache database", credentials.AccountId);
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to save account {AccountId} to local cache database", credentials.AccountId);
            }
        }

        public async Task DeleteAccountAsync(string accountId)
        {
            try
            {
                await using var context = await _dbContextFactory.CreateDbContextAsync();
                var existing = await context.AccountCredentials.FirstOrDefaultAsync(a => a.AccountId == accountId);
                if (existing == null)
                    return;

                context.AccountCredentials.Remove(existing);
                await context.SaveChangesAsync();
                _logger.Information("Deleted account {AccountId} from local cache database", accountId);
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to delete account {AccountId} from local cache database", accountId);
            }
        }

        private static AccountCredentialEntity ToEntity(AccountCredentials credentials)
        {
            return new AccountCredentialEntity
            {
                AccountId = credentials.AccountId ?? string.Empty,
                Email = credentials.Email,
                Provider = credentials.Provider,
                AccessToken = credentials.AccessToken,
                RefreshToken = credentials.RefreshToken,
                ExpiresAt = credentials.ExpiresAt,
                CreatedAt = credentials.CreatedAt,
                AdditionalProperties = credentials.AdditionalProperties
            };
        }

        private static AccountCredentials ToModel(AccountCredentialEntity entity)
        {
            return new AccountCredentials
            {
                AccountId = entity.AccountId,
                Email = entity.Email,
                Provider = entity.Provider,
                AccessToken = entity.AccessToken,
                RefreshToken = entity.RefreshToken,
                ExpiresAt = entity.ExpiresAt,
                CreatedAt = entity.CreatedAt,
                AdditionalProperties = entity.AdditionalProperties
            };
        }
    }
}
