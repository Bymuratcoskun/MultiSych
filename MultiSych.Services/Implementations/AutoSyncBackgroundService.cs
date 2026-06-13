using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;
using MultiSych.Services.Interfaces;
using MultiSych.Services.Configuration;
using MultiSych.Desktop.Services;
using Microsoft.EntityFrameworkCore;
using MultiSych.Services.Data;

namespace MultiSych.Services.Implementations;

public class AutoSyncBackgroundService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger _logger;
    private readonly ISyncSignalService _syncSignalService;
    private readonly RuntimeSyncSettings _runtimeSyncSettings;
    private readonly IAppStatusService _appStatusService;
    private readonly HashSet<string> _notifiedEventIds = new();
    private readonly HashSet<string> _notifiedEmailIds = new();

    public AutoSyncBackgroundService(
        IServiceScopeFactory scopeFactory, 
        ISyncSignalService syncSignalService, 
        RuntimeSyncSettings runtimeSyncSettings,
        IAppStatusService appStatusService)
    {
        _scopeFactory = scopeFactory;
        _logger = Log.ForContext<AutoSyncBackgroundService>();
        _syncSignalService = syncSignalService;
        _runtimeSyncSettings = runtimeSyncSettings;
        _appStatusService = appStatusService;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("AutoSync Background Service is starting.");

        // Etkinlik hatırlatıcılarını kontrol eden bağımsız paralel döngü
        _ = Task.Run(async () => await CheckRemindersAsync(stoppingToken), stoppingToken);

        // Uygulama kapanmadığı sürece döngü devam eder
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                CancellationToken waitToken = stoppingToken;
                IDisposable? linkedCts = null;

                if (_runtimeSyncSettings.AutoSyncEnabled)
                {
                    var currentInterval = TimeSpan.FromMinutes(_runtimeSyncSettings.SyncIntervalMinutes);
                    var cts = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
                    cts.CancelAfter(currentInterval);
                    waitToken = cts.Token;
                    linkedCts = cts; // Dispose etmek için referansı sakla
                }

                try
                {
                    // Manuel bir sinyal (Channel üzerinden) gelmesini bekliyoruz.
                    await _syncSignalService.WaitAsync(waitToken);
                    if (!waitToken.IsCancellationRequested)
                        _logger.Information("Manual sync signal received.");
                }
                catch (OperationCanceledException)
                {
                    // Sinyal gelmedi, zaman aşımı doldu veya uygulama kapatılıyor. Bu beklenen bir durum.
                }
                finally { linkedCts?.Dispose(); }
                
                // Eğer uygulama tamamen kapatılmıyorsa (zaman dolduğu için veya manuel tıklandığı için buradaysak)
                if (!stoppingToken.IsCancellationRequested)
                {
                    await PerformSyncAsync(stoppingToken);
                }
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Unexpected error in background sync loop.");
            }
        }

        _logger.Information("AutoSync Background Service is stopping.");
    }

    private async Task PerformSyncAsync(CancellationToken cancellationToken)
    {
        _logger.Information("Starting automated sync cycle...");
        _appStatusService.PostUpdate("Senkronizasyon başlıyor...", isSyncing: true);

        try
        {
            // Arka plan servisleri Singleton olduğu için Scoped servisleri yeni bir Scope içinde çağırıyoruz
            using var scope = _scopeFactory.CreateScope();
            
            var accountStore = scope.ServiceProvider.GetRequiredService<IAccountStore>();
            var emailService = scope.ServiceProvider.GetRequiredService<IEmailService>();
            var calendarService = scope.ServiceProvider.GetRequiredService<ICalendarService>();
            var storageService = scope.ServiceProvider.GetRequiredService<IStorageService>();
            var hybridAiService = scope.ServiceProvider.GetRequiredService<IHybridAIService>();

            var accounts = await accountStore.GetAccountsAsync();
            
            if (accounts.Count == 0)
            {
                _logger.Information("No connected accounts found for auto-sync.");
                return;
            }

            foreach (var account in accounts)
            {
                if (cancellationToken.IsCancellationRequested)
                    break;
                
                _logger.Information("Auto-syncing account: {Provider} - {Email}", account.Provider, account.Email);
                
                await emailService.SyncEmailsAsync(account);
                
                // E-posta senkronizasyonu sonrası AI analizi
                await hybridAiService.AnalyzeUnprocessedEmailsAsync(account.AccountId);

                if (cancellationToken.IsCancellationRequested) break;
                await calendarService.SyncEventsAsync(account);
                
                if (cancellationToken.IsCancellationRequested) break;
                await storageService.SyncStorageAsync(account);
            }

            _logger.Information("Automated sync cycle completed successfully.");
            _appStatusService.PostUpdate("Senkronizasyon tamamlandı.", isSyncing: false);

            var windowService = scope.ServiceProvider.GetService<IWindowService>();
            if (windowService != null)
            {
                Avalonia.Threading.Dispatcher.UIThread.Post(() => 
                {
                    windowService.ShowNotification("Senkronizasyon", "Tüm hesaplar başarıyla güncellendi.", NotificationSound.Success);
                });
            }
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "An error occurred during the automated sync cycle.");
            _appStatusService.PostUpdate($"Senkronizasyon hatası: {ex.Message}", isSyncing: false);
        }
    }

    private async Task CheckRemindersAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var dbContext = scope.ServiceProvider.GetRequiredService<LocalCacheDbContext>();
                var windowService = scope.ServiceProvider.GetService<IWindowService>();

                if (windowService != null)
                {
                    var now = DateTime.UtcNow;
                    var upcomingLimit = now.AddMinutes(15);
                    
                    var upcomingEvents = await dbContext.CachedEvents
                        .Where(e => e.StartTime > now && e.StartTime <= upcomingLimit)
                        .ToListAsync(stoppingToken);

                    foreach (var ev in upcomingEvents)
                    {
                        if (_notifiedEventIds.Add(ev.EventId)) // Add metodu, koleksiyonda yoksa ekler ve true döner
                        {
                            var localTime = ev.StartTime.ToLocalTime();
                            Avalonia.Threading.Dispatcher.UIThread.Post(() => 
                            {
                                windowService.ShowNotification("⏰ Yaklaşan Etkinlik", $"{ev.Title}\nZaman: {localTime:HH:mm}", NotificationSound.Event);
                            });
                        }
                    }
                    
                    // Bellek sızıntısını önlemek için, süresi geçmiş etkinlik ID'lerini HashSet'ten temizliyoruz
                    _notifiedEventIds.RemoveWhere(id => !upcomingEvents.Any(e => e.EventId == id));

                    // --- Yeni E-Posta Bildirim Kontrolü ---
                    var recentEmailLimit = now.AddDays(-1); // Yalnızca son 24 saat içindeki okunmamışları dikkate al
                    var unreadEmails = await dbContext.CachedEmails
                        .Where(e => !e.IsRead && e.ReceivedAt > recentEmailLimit)
                        .ToListAsync(stoppingToken);

                    foreach (var email in unreadEmails)
                    {
                        // Eşsiz bir ID oluşturarak aynı mailin tekrar bildirilmesini engelliyoruz
                        var uniqueId = $"{email.AccountId}_{email.ReceivedAt.Ticks}";
                        if (_notifiedEmailIds.Add(uniqueId))
                        {
                            Avalonia.Threading.Dispatcher.UIThread.Post(() => 
                            {
                                windowService.ShowNotification("📧 Yeni E-Posta", email.Subject ?? "Konusuz İleti", NotificationSound.Email);
                            });
                        }
                    }
                    
                    _notifiedEmailIds.RemoveWhere(id => !unreadEmails.Any(e => $"{e.AccountId}_{e.ReceivedAt.Ticks}" == id));
                }
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error checking event reminders.");
            }

            await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken); // Hatırlatıcıları her 1 dakikada bir kontrol et
        }
    }
}
