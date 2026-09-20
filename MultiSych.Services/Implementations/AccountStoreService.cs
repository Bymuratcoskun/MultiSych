using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using MultiSych.Services.Data;
using MultiSych.Services.Interfaces;
using MultiSych.Services.Models;
using Serilog;
using AccountCredentialEntity = MultiSych.Services.Data.AccountCredentialEntity;

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
            InitializeDatabase(context);
        }

        /// <summary>
        /// Veritabanı şemasını güvenli şekilde başlatır. Migrations klasörü mevcut;
        /// EnsureCreated migration'ları atlar ve model değiştiğinde "no such table"
        /// hatalarına yol açar. Ancak eski sürümler DB'yi EnsureCreated ile oluşturmuştu,
        /// dolayısıyla tablolar var ama __EFMigrationsHistory tablosu yok — böyle bir
        /// DB'de doğrudan Migrate() "table already exists" hatası verir. Bu metod, eski
        /// EnsureCreated veritabanlarını tespit edip mevcut şemayı migration geçmişine
        /// "baseline" olarak işaretler, ardından bekleyen migration'ları uygular.
        /// </summary>
        private void InitializeDatabase(LocalCacheDbContext context)
        {
            var db = context.Database;
            try
            {
                var applied = db.GetAppliedMigrations().ToList();
                var pending = db.GetPendingMigrations().ToList();

                // Migration geçmişi boş ama zaten tablolar varsa: eski EnsureCreated DB'si.
                if (applied.Count == 0 && pending.Count > 0 && LegacySchemaExists(db))
                {
                    _logger.Warning("Eski EnsureCreated veritabanı tespit edildi. Mevcut şema migration geçmişine baseline olarak işaretleniyor.");
                    db.ExecuteSqlRaw(@"CREATE TABLE IF NOT EXISTS ""__EFMigrationsHistory"" (""MigrationId"" TEXT NOT NULL CONSTRAINT ""PK___EFMigrationsHistory"" PRIMARY KEY, ""ProductVersion"" TEXT NOT NULL);");
                    foreach (var migrationId in pending)
                    {
                        db.ExecuteSqlRaw(@"INSERT OR IGNORE INTO ""__EFMigrationsHistory"" (""MigrationId"", ""ProductVersion"") VALUES ({0}, {1});", migrationId, "9.0.0");
                    }
                }

                // Taze DB → tüm migration'ları uygular; baseline'lanmış eski DB → yalnızca
                // gelecekteki yeni migration'ları uygular.
                db.Migrate();
            }
            catch (System.Exception ex)
            {
                _logger.Error(ex, "Veritabanı başlatılamadı (migration). Uygulama şema olmadan hatalı çalışabilir.");
                throw;
            }
        }

        /// <summary>Ana tablonun (Accounts) zaten var olup olmadığını kontrol eder.</summary>
        private static bool LegacySchemaExists(Microsoft.EntityFrameworkCore.Infrastructure.DatabaseFacade db)
        {
            try
            {
                db.ExecuteSqlRaw(@"SELECT 1 FROM ""Accounts"" LIMIT 1;");
                return true;
            }
            catch
            {
                return false;
            }
        }

        public async Task<List<AccountCredentials>> GetAccountsAsync()
        {
            try
            {
                await using var context = await _dbContextFactory.CreateDbContextAsync();
                var entities = await context.Accounts
                    .AsNoTracking()
                    .ToListAsync();

                return entities.Select(ToModel).ToList();
            }
            catch (System.Exception ex)
            {
                _logger.Error(ex, "Failed to read accounts from local cache database");
                return new List<AccountCredentials>();
            }
        }

        public async Task<AccountCredentials?> GetAccountAsync(string accountId)
        {
            await using var context = await _dbContextFactory.CreateDbContextAsync();
            var entity = await context.Accounts
                .AsNoTracking()
                .FirstOrDefaultAsync(a => a.AccountId == accountId);

            return entity is null ? null : ToModel(entity);
        }

        public Task<AccountCredentials?> GetAccountByIdAsync(string id) => GetAccountAsync(id);

        public async Task SaveAccountAsync(AccountCredentials credentials)
        {
            try
            {
                await using var context = await _dbContextFactory.CreateDbContextAsync();
                var existing = await context.Accounts.FirstOrDefaultAsync(a => a.AccountId == credentials.AccountId);
                if (existing == null)
                {
                    await context.Accounts.AddAsync(ToEntity(credentials));
                }
                else
                {
                    existing.Email = credentials.Email;
                    existing.Provider = credentials.Provider;
                    existing.AccessToken = credentials.AccessToken;
                    existing.RefreshToken = credentials.RefreshToken;
                    existing.ExpiresAt = credentials.ExpiresAt;
                    // CreatedAt BaseEntity üzerinden set edildi, sadece UpdatedAt güncellenir
                    existing.UpdatedAt = System.DateTime.UtcNow;
                    existing.AdditionalProperties = credentials.AdditionalProperties ?? new();
                }

                await context.SaveChangesAsync();
                _logger.Information("Saved account {AccountId} to local cache database", credentials.AccountId);
            }
            catch (System.Exception ex)
            {
                _logger.Error(ex, "Failed to save account {AccountId} to local cache database", credentials.AccountId);
            }
        }

        public async Task DeleteAccountAsync(string accountId)
        {
            try
            {
                await using var context = await _dbContextFactory.CreateDbContextAsync();
                var existing = await context.Accounts.FirstOrDefaultAsync(a => a.AccountId == accountId);
                if (existing == null)
                    return;

                context.Accounts.Remove(existing);
                await context.SaveChangesAsync();
                _logger.Information("Deleted account {AccountId} from local cache database", accountId);
            }
            catch (System.Exception ex)
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
                UpdatedAt = System.DateTime.UtcNow,
                AdditionalProperties = credentials.AdditionalProperties ?? new()
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
