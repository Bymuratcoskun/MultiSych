using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using MultiSych.Services.Configuration;
using MultiSych.Services.Interfaces;
using Serilog;

namespace MultiSych.Services.Implementations
{
    public class BackgroundSyncService : BackgroundService
    {
        private readonly ILogger _logger;
        private readonly IAccountStore _accountStore;
        private readonly IEmailService _emailService;
        private readonly ICalendarService _calendarService;
        private readonly IStorageService _storageService;
        private readonly SyncSettings _syncSettings;

        public BackgroundSyncService(
            IAccountStore accountStore,
            IEmailService emailService,
            ICalendarService calendarService,
            IStorageService storageService,
            MultiSychConfig config)
        {
            _logger = Log.ForContext<BackgroundSyncService>();
            _accountStore = accountStore;
            _emailService = emailService;
            _calendarService = calendarService;
            _storageService = storageService;
            _syncSettings = config.Sync ?? new SyncSettings();
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            if (!_syncSettings.AutoSyncEnabled)
            {
                _logger.Information("Background sync disabled in configuration.");
                return;
            }

            _logger.Information("Background sync service started. Interval: {IntervalMinutes} minutes", _syncSettings.SyncIntervalMinutes);

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await RunSyncCycleAsync(stoppingToken);
                }
                catch (TaskCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger.Error(ex, "Background sync cycle failed");
                }

                await Task.Delay(TimeSpan.FromMinutes(Math.Max(1, _syncSettings.SyncIntervalMinutes)), stoppingToken);
            }

            _logger.Information("Background sync service stopped.");
        }

        private async Task RunSyncCycleAsync(CancellationToken cancellationToken)
        {
            _logger.Information("Starting background sync cycle...");

            var accounts = await _accountStore.GetAccountsAsync();
            _logger.Information("Background sync found {AccountCount} accounts", accounts.Count);

            if (!_syncSettings.SyncEmailsEnabled && !_syncSettings.SyncCalendarEnabled && !_syncSettings.SyncStorageEnabled)
            {
                _logger.Information("All sync categories are disabled; skipping sync cycle.");
                return;
            }

            foreach (var account in accounts)
            {
                if (cancellationToken.IsCancellationRequested)
                    break;

                if (_syncSettings.SyncEmailsEnabled)
                {
                    await SafeSyncAsync(() => _emailService.SyncEmailsAsync(account), account.Provider, "email");
                }

                if (_syncSettings.SyncCalendarEnabled)
                {
                    await SafeSyncAsync(() => _calendarService.SyncEventsAsync(account), account.Provider, "calendar");
                }

                if (_syncSettings.SyncStorageEnabled)
                {
                    await SafeSyncAsync(() => _storageService.SyncStorageAsync(account), account.Provider, "storage");
                }
            }

            _logger.Information("Background sync cycle completed successfully.");
        }

        private async Task SafeSyncAsync(Func<Task> syncOperation, string? provider, string category)
        {
            try
            {
                await syncOperation();
                _logger.Information("{Category} sync completed for provider {Provider}.", category, provider ?? "unknown");
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "{Category} sync failed for provider {Provider}.", category, provider ?? "unknown");
            }
        }
    }
}
