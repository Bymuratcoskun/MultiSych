using System;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using MultiSych.Services.Data;
using MultiSych.Services.Interfaces;
using Serilog;

namespace MultiSych.Services.Implementations
{
    public class SecureStorageService : ISecureStorageService
    {
        private readonly IDbContextFactory<LocalCacheDbContext> _dbContextFactory;
        private readonly ILogger _logger;

        public SecureStorageService(IDbContextFactory<LocalCacheDbContext> dbContextFactory)
        {
            _dbContextFactory = dbContextFactory;
            _logger = Log.ForContext<SecureStorageService>();
        }

        public async Task SaveSecretAsync(string key, string value)
        {
            try
            {
                await using var context = await _dbContextFactory.CreateDbContextAsync();
                var existing = await context.Set<AppSecretEntity>().FirstOrDefaultAsync(s => s.Key == key);
                if (existing == null)
                {
                    await context.Set<AppSecretEntity>().AddAsync(new AppSecretEntity
                    {
                        Key = key,
                        Value = value,
                        UpdatedAt = DateTime.UtcNow
                    });
                }
                else
                {
                    existing.Value = value;
                    existing.UpdatedAt = DateTime.UtcNow;
                }
                await context.SaveChangesAsync();
                _logger.Information("Saved secure secret for key: {Key}", key);
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to save secure secret for key: {Key}", key);
                throw;
            }
        }

        public async Task<string?> GetSecretAsync(string key)
        {
            try
            {
                await using var context = await _dbContextFactory.CreateDbContextAsync();
                var entity = await context.Set<AppSecretEntity>().AsNoTracking().FirstOrDefaultAsync(s => s.Key == key);
                return entity?.Value;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to retrieve secure secret for key: {Key}", key);
                return null;
            }
        }

        public async Task DeleteSecretAsync(string key)
        {
            try
            {
                await using var context = await _dbContextFactory.CreateDbContextAsync();
                var entity = await context.Set<AppSecretEntity>().FirstOrDefaultAsync(s => s.Key == key);
                if (entity != null)
                {
                    context.Set<AppSecretEntity>().Remove(entity);
                    await context.SaveChangesAsync();
                    _logger.Information("Deleted secure secret for key: {Key}", key);
                }
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to delete secure secret for key: {Key}", key);
            }
        }
    }
}