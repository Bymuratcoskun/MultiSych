using Microsoft.EntityFrameworkCore;
using MultiSych.Services.Models;

namespace MultiSych.Services.Data;

public class LocalCacheDbContext : DbContext
{
    public LocalCacheDbContext(DbContextOptions<LocalCacheDbContext> options) : base(options)
    {
    }

    public DbSet<AccountCredentialEntity> Accounts { get; set; } = null!;
    public DbSet<CloudFileEntity> CloudFiles { get; set; } = null!;
    public DbSet<EmailMessageEntity> CachedEmails { get; set; } = null!;
    public DbSet<CalendarEventEntity> CachedEvents { get; set; } = null!;
    public DbSet<AppSecretEntity> AppSecrets { get; set; } = null!;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
    }
}
