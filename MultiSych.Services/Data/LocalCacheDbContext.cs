using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using MultiSych.Services.Models;

namespace MultiSych.Services.Data;

public class LocalCacheDbContext : DbContext
{
    public LocalCacheDbContext(DbContextOptions<LocalCacheDbContext> options) : base(options)
    {
    }

    public DbSet<CloudFile> CachedFiles { get; set; }
    public DbSet<EmailMessage> CachedEmails { get; set; }
    public DbSet<CalendarEvent> CachedEvents { get; set; }
    public DbSet<AccountCredentialEntity> AccountCredentials { get; set; }
    public DbSet<AppSecretEntity> AppSecrets { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<CloudFile>().HasKey(x => new { x.AccountId, x.FileId });
        // EF Core'un kafasını karıştıran karmaşık Metadata özelliğini veritabanına yazmaktan vazgeçiyoruz (Ignore)
        modelBuilder.Entity<CloudFile>().Ignore(x => x.Metadata);
        modelBuilder.Entity<CalendarEvent>().HasKey(x => new { x.AccountId, x.EventId });
        
        modelBuilder.Entity<EmailMessage>().HasKey(x => new { x.AccountId, x.MessageId });
        // E-posta eklerini (EmailAttachment) veritabanı tablosu yapmasını engelliyoruz
        modelBuilder.Ignore<EmailAttachment>();
        modelBuilder.Entity<EmailMessage>()
            .Property(e => e.To)
            .HasConversion(
                v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
                v => JsonSerializer.Deserialize<List<string>>(v, (JsonSerializerOptions?)null) ?? new List<string>())
            .Metadata.SetValueComparer(new ValueComparer<List<string>>(
                (c1, c2) => c1 != null && c2 != null && c1.SequenceEqual(c2),
                c => c.Aggregate(0, (a, v) => HashCode.Combine(a, v.GetHashCode())),
                c => c.ToList()));
    }
}