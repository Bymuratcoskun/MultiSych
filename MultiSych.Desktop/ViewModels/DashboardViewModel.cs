using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Threading;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MultiSych.Desktop.Services;
using MultiSych.Services.Data;
using MultiSych.Services.Interfaces;

namespace MultiSych.Desktop.ViewModels;

public class AccountStatusItem
{
    public string Provider { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
}

public class EmailSyncTrendPoint
{
    public string DayName { get; set; } = string.Empty;
    public int Count { get; set; }
    public double BarHeight { get; set; }
}

public class DashboardViewModel : ViewModelBase, IDisposable
{
    private readonly IAppStatusService _appStatusService;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IDisposable _statusSubscription;

    private string _statusMessage = "Uygulama başlatılıyor...";
    private bool _isSyncing;
    private int _totalAccounts;
    private int _totalEmails;
    private int _totalEvents;
    private int _totalFiles;
    private string _dailyAiSummary = "Yapay zeka günün özetini hazırlıyor...";
    private string _cacheSize = "Hesaplanıyor...";

    public ObservableCollection<string> RecentLogs { get; } = [];
    public ObservableCollection<AccountStatusItem> AccountStatuses { get; } = [];
    public ObservableCollection<EmailSyncTrendPoint> SyncTrendData { get; } = [];

    public DashboardViewModel(IAppStatusService appStatusService, IServiceScopeFactory scopeFactory)
    {
        _appStatusService = appStatusService;
        _scopeFactory = scopeFactory;

        _statusSubscription = _appStatusService.StatusChanged.Subscribe(OnStatusChanged);
        
        // Başlangıçta veritabanındaki mevcut sayıları yükle
        Task.Run(LoadInitialCounts);
        Task.Run(LoadAiSummary);
        Task.Run(LoadSyncTrendData);
    }

    public string StatusMessage { get => _statusMessage; set => SetProperty(ref _statusMessage, value); }
    public bool IsSyncing { get => _isSyncing; set => SetProperty(ref _isSyncing, value); }
    public int TotalAccounts { get => _totalAccounts; set => SetProperty(ref _totalAccounts, value); }
    public int TotalEmails { get => _totalEmails; set => SetProperty(ref _totalEmails, value); }
    public int TotalEvents { get => _totalEvents; set => SetProperty(ref _totalEvents, value); }
    public int TotalFiles { get => _totalFiles; set => SetProperty(ref _totalFiles, value); }
    public string DailyAiSummary { get => _dailyAiSummary; set => SetProperty(ref _dailyAiSummary, value); }
    public string CacheSize { get => _cacheSize; set => SetProperty(ref _cacheSize, value); }

    private void OnStatusChanged(StatusUpdate update)
    {
        Dispatcher.UIThread.Post(() =>
        {
            StatusMessage = update.Message;
            IsSyncing = update.IsSyncing;
            if (update.TotalAccounts.HasValue) TotalAccounts = update.TotalAccounts.Value;
            if (update.TotalEmails.HasValue) 
            {
                TotalEmails = update.TotalEmails.Value;
            }
            if (update.TotalEvents.HasValue) TotalEvents = update.TotalEvents.Value;
            if (update.TotalFiles.HasValue) TotalFiles = update.TotalFiles.Value;

            if (!string.IsNullOrWhiteSpace(update.Message))
            {
                RecentLogs.Insert(0, $"[{DateTime.Now:HH:mm:ss}] {update.Message}");
                if (RecentLogs.Count > 5) RecentLogs.RemoveAt(5); // Sadece son 5 log kaydını tut
            }
        });
    }

    private async Task LoadInitialCounts()
    {
        using var scope = _scopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<LocalCacheDbContext>();
        var accountStore = scope.ServiceProvider.GetRequiredService<IAccountStore>();

        var accounts = await dbContext.Accounts.CountAsync();
        var emails = await dbContext.CachedEmails.CountAsync();
        var events = await dbContext.CachedEvents.CountAsync();
        var files = await dbContext.CloudFiles.CountAsync();
        
        _appStatusService.PostDatabaseCounts(accounts, emails, events, files);

        var allAccounts = await accountStore.GetAccountsAsync();
        Dispatcher.UIThread.Post(() => 
        {
            AccountStatuses.Clear();
            foreach (var acc in allAccounts)
            {
                AccountStatuses.Add(new AccountStatusItem
                {
                    Provider = acc.Provider ?? string.Empty,
                    Email = acc.Email ?? string.Empty,
                    Status = acc.ExpiresAt > DateTime.UtcNow ? "🟢 Bağlı" : "🔴 Süresi Doldu"
                });
            }
        });
    }

    private async Task LoadAiSummary()
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var aiService = scope.ServiceProvider.GetRequiredService<IHybridAIService>();
            var summary = await aiService.GenerateDailySummaryAsync();
            Dispatcher.UIThread.Post(() => DailyAiSummary = summary);
        }
        catch
        {
            Dispatcher.UIThread.Post(() => DailyAiSummary = "Yapay zeka özeti şu an kullanılamıyor.");
        }
    }

    private async Task LoadSyncTrendData()
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<LocalCacheDbContext>();

            var today = DateTime.UtcNow.Date;
            var last7Days = Enumerable.Range(0, 7)
                .Select(i => today.AddDays(-i))
                .Reverse()
                .ToList();

            var trendPoints = new System.Collections.Generic.List<EmailSyncTrendPoint>();

            var startDate = today.AddDays(-6);
            var emailsInPeriod = await dbContext.CachedEmails
                .Where(e => e.ReceivedAt >= startDate)
                .ToListAsync();

            var grouped = emailsInPeriod
                .GroupBy(e => e.ReceivedAt.Date)
                .ToDictionary(g => g.Key, g => g.Count());

            foreach (var date in last7Days)
            {
                grouped.TryGetValue(date, out var count);
                trendPoints.Add(new EmailSyncTrendPoint
                {
                    DayName = date.ToString("ddd"),
                    Count = count
                });
            }

            var maxCount = trendPoints.Max(p => p.Count);

            foreach (var point in trendPoints)
            {
                point.BarHeight = maxCount > 0 ? ((double)point.Count / maxCount * 130) + 10 : 10;
            }

            var cacheSizeStr = GetCacheSizeString();

            Dispatcher.UIThread.Post(() =>
            {
                SyncTrendData.Clear();
                foreach (var point in trendPoints)
                {
                    SyncTrendData.Add(point);
                }
                CacheSize = cacheSizeStr;
            });
        }
        catch (Exception ex)
        {
            Serilog.Log.Warning(ex, "Failed to load email sync trend or cache size");
        }
    }

    private string GetCacheSizeString()
    {
        try
        {
            var cacheFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "MultiSych", "Cache");
            if (!Directory.Exists(cacheFolder))
                return "0 KB";

            var dirInfo = new DirectoryInfo(cacheFolder);
            long totalSize = dirInfo.EnumerateFiles("*", SearchOption.AllDirectories).Sum(file => file.Length);

            if (totalSize >= 1024 * 1024 * 1024)
                return $"{totalSize / (1024.0 * 1024.0 * 1024.0):F1} GB";
            if (totalSize >= 1024 * 1024)
                return $"{totalSize / (1024.0 * 1024.0):F1} MB";
            if (totalSize >= 1024)
                return $"{totalSize / 1024.0:F1} KB";
            return $"{totalSize} B";
        }
        catch
        {
            return "0 KB";
        }
    }

    public void Dispose()
    {
        _statusSubscription.Dispose();
    }
}
