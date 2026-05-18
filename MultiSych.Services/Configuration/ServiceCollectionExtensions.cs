using System;
using System.IO;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MultiSych.Services.Implementations;
using MultiSych.Services.Interfaces;
using MultiSych.Services.Data;

namespace MultiSych.Services.Configuration;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddMultiSychBackgroundServices(this IServiceCollection services)
    {
        // HttpClient sınıflarının (Socket Exhaustion) havuzunu güvenle yönetmek için IHttpClientFactory kaydı
        services.AddHttpClient();

        // UI ve Arka plan servisinin haberleşmesini sağlayan sinyal kanalı
        services.AddSingleton<ISyncSignalService, SyncSignalService>();

        // Arka planda sürekli çalışacak olan otomatik senkronizasyon servisi
        services.AddHostedService<AutoSyncBackgroundService>();

        return services;
    }

    public static IServiceCollection AddMultiSychDatabase(this IServiceCollection services, string dbPassword)
    {
        var dbFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "MultiSych", "Database");
        if (!Directory.Exists(dbFolder))
        {
            Directory.CreateDirectory(dbFolder);
        }
        
        var dbPath = Path.Combine(dbFolder, "localcache.db");
        
        // SQLCipher (AES-256) kullanılarak şifrelenmiş SQLite bağlantısı
        services.AddDbContext<LocalCacheDbContext>(options =>
            options.UseSqlite($"Data Source={dbPath};Password={dbPassword};"));

        return services;
    }
}