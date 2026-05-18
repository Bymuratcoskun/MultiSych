using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using MultiSych.Services.Interfaces;

namespace MultiSych.Services.Implementations;

public class AutoSyncBackgroundService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<AutoSyncBackgroundService> _logger;
    private readonly ISyncSignalService _syncSignalService;
    
    // Otomatik senkronizasyon aralığı (örneğin 15 dakika)
    // Bu değeri ileride appsettings.json veya MultiSychConfig üzerinden alabilirsin.
    private readonly TimeSpan _syncInterval = TimeSpan.FromMinutes(15);

    public AutoSyncBackgroundService(IServiceScopeFactory scopeFactory, ILogger<AutoSyncBackgroundService> logger, ISyncSignalService syncSignalService)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
        _syncSignalService = syncSignalService;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("AutoSync Background Service is starting.");

        // Uygulama kapanmadığı sürece döngü devam eder
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                // Hem uygulamanın kapanma token'ını hem de zaman aşımını (15 dk) içeren bağlantılı bir token oluşturuyoruz
                using var cts = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
                cts.CancelAfter(_syncInterval);

                try
                {
                    // Manuel bir sinyal (Channel üzerinden) gelmesini bekliyoruz.
                    await _syncSignalService.WaitAsync(cts.Token);
                    _logger.LogInformation("Manual sync signal received.");
                }
                catch (OperationCanceledException)
                {
                    // Sinyal gelmedi, 15 dakikalık süre doldu veya uygulama kapatılıyor.
                }

                // Eğer uygulama tamamen kapatılmıyorsa (zaman dolduğu için veya manuel tıklandığı için buradaysak)
                if (!stoppingToken.IsCancellationRequested)
                {
                    await PerformSyncAsync(stoppingToken);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error in background sync loop.");
            }
        }

        _logger.LogInformation("AutoSync Background Service is stopping.");
    }

    private async Task PerformSyncAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Starting automated sync cycle...");

        try
        {
            // Arka plan servisleri Singleton olduğu için Scoped servisleri yeni bir Scope içinde çağırıyoruz
            using var scope = _scopeFactory.CreateScope();
            
            var accountStore = scope.ServiceProvider.GetRequiredService<IAccountStore>();
            var emailService = scope.ServiceProvider.GetRequiredService<IEmailService>();
            var calendarService = scope.ServiceProvider.GetRequiredService<ICalendarService>();
            var storageService = scope.ServiceProvider.GetRequiredService<IStorageService>();

            var accounts = await accountStore.GetAccountsAsync();
            
            if (accounts.Count == 0)
            {
                _logger.LogInformation("No connected accounts found for auto-sync.");
                return;
            }

            foreach (var account in accounts)
            {
                if (cancellationToken.IsCancellationRequested)
                    break;

                _logger.LogInformation("Auto-syncing account: {Provider} - {Email}", account.Provider, account.Email);
                
                await emailService.SyncEmailsAsync(account);
                await calendarService.SyncEventsAsync(account);
                await storageService.SyncStorageAsync(account);
            }

            _logger.LogInformation("Automated sync cycle completed successfully.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An error occurred during the automated sync cycle.");
        }
    }
}