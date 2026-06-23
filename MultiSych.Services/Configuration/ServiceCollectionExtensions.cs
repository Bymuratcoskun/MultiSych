using System;
using System.IO;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using MultiSych.Services.Implementations;
using MultiSych.Services.Interfaces;
using MultiSych.Services.Data;
using Serilog;

namespace MultiSych.Services.Configuration;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddMultiSychConfiguration(this IServiceCollection services, IConfiguration configuration)
    {
        var config = new MultiSychConfig();
        configuration.Bind(config); // appsettings.json içerisindeki değerleri MultiSychConfig nesnesine doldurur
        
        // Uygulamanın her yerinden yapılandırmalara erişebilmek için Singleton olarak kaydediyoruz
        services.AddSingleton(config);
        
        return services;
    }

    public static IServiceCollection AddMultiSychBackgroundServices(this IServiceCollection services)
    {
        // HttpClient sınıflarının (Socket Exhaustion) havuzunu güvenle yönetmek için IHttpClientFactory kaydı
        services.AddHttpClient();

        // UI ve Arka plan servisinin haberleşmesini sağlayan sinyal kanalı
        services.AddSingleton<ISyncSignalService, SyncSignalService>();

        // Arka planda sürekli çalışacak olan otomatik senkronizasyon servisi
        services.AddHostedService<AutoSyncBackgroundService>();

        // Veri kalıcılığı ve Bulut Depolama servisleri
        services.AddScoped<IAuthenticationService, AuthenticationService>();
        services.AddScoped<IAccountStore, AccountStoreService>();
        services.AddScoped<IEmailService, CloudEmailService>();
        services.AddScoped<ICalendarService, CloudCalendarService>();
        services.AddScoped<IStorageService, CloudStorageService>();
        services.AddScoped<IHybridAIService, HybridAIService>(); // Bu satır zaten vardı, şimdi implementasyonu ekledik.
        services.AddScoped<IIntentParserService, IntentParserService>();

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
            options.UseSqlite($"Data Source={dbPath};Password={dbPassword};")
                   .LogTo(msg => Log.Information(msg), Microsoft.Extensions.Logging.LogLevel.Information)
                   .EnableSensitiveDataLogging()
                   // Trim işlemi sırasında EF Core'un yansıma uyarılarını yönetmek için
                   .ConfigureWarnings(warnings => warnings.Ignore(CoreEventId.ManyServiceProvidersCreatedWarning)));

        return services;
    }
}
