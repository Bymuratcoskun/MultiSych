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
        
        // AccountId'yi birincil anahtar (Primary Key) olarak ayarlıyoruz.
        modelBuilder.Entity<AccountCredentialEntity>().HasKey(a => a.AccountId);
        
        // CloudFile için AccountId ve FileId'yi birleşik anahtar (Composite Key) yapıyoruz.
        modelBuilder.Entity<CloudFileEntity>().HasKey(c => new { c.AccountId, c.FileId });

        // EmailMessage için AccountId ve MessageId'yi birleşik anahtar yapıyoruz.
        modelBuilder.Entity<EmailMessageEntity>().HasKey(e => new { e.AccountId, e.MessageId });

        // CalendarEvent için AccountId ve EventId'yi birleşik anahtar yapıyoruz.
        modelBuilder.Entity<CalendarEventEntity>().HasKey(c => new { c.AccountId, c.EventId });
    }
}
