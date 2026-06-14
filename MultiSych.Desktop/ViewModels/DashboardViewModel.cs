using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Threading;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MultiSych.Desktop.Services;
using MultiSych.Services.Data;
using MultiSych.Services.Interfaces;
using OxyPlot;
using OxyPlot.Axes;
using OxyPlot.Series;

namespace MultiSych.Desktop.ViewModels;

public class AccountStatusItem
{
    public string Provider { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
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
    private PlotModel _trendModel = null!;
    private string _dailyAiSummary = "Yapay zeka günün özetini hazırlıyor...";

    public ObservableCollection<string> RecentLogs { get; } = [];
    public ObservableCollection<AccountStatusItem> AccountStatuses { get; } = [];

    public DashboardViewModel(IAppStatusService appStatusService, IServiceScopeFactory scopeFactory)
    {
        _appStatusService = appStatusService;
        _scopeFactory = scopeFactory;

        _statusSubscription = _appStatusService.StatusChanged.Subscribe(OnStatusChanged);
        
        InitializeChart();
        
        // Başlangıçta veritabanındaki mevcut sayıları yükle
        Task.Run(LoadInitialCounts);
        Task.Run(LoadAiSummary);
    }

    public string StatusMessage { get => _statusMessage; set => SetProperty(ref _statusMessage, value); }
    public bool IsSyncing { get => _isSyncing; set => SetProperty(ref _isSyncing, value); }
    public int TotalAccounts { get => _totalAccounts; set => SetProperty(ref _totalAccounts, value); }
    public int TotalEmails { get => _totalEmails; set => SetProperty(ref _totalEmails, value); }
    public int TotalEvents { get => _totalEvents; set => SetProperty(ref _totalEvents, value); }
    public int TotalFiles { get => _totalFiles; set => SetProperty(ref _totalFiles, value); }
    public PlotModel TrendModel { get => _trendModel; set => SetProperty(ref _trendModel, value); }
    public string DailyAiSummary { get => _dailyAiSummary; set => SetProperty(ref _dailyAiSummary, value); }

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
                UpdateChart(TotalEmails);
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

    private void InitializeChart()
    {
        TrendModel = new PlotModel 
        { 
            TextColor = OxyColors.LightGray, 
            PlotAreaBorderColor = OxyColors.Transparent
        };
        
        TrendModel.Axes.Add(new DateTimeAxis 
        { 
            Position = AxisPosition.Bottom, StringFormat = "HH:mm", 
            TextColor = OxyColors.LightGray, TicklineColor = OxyColors.Gray,
            AxislineColor = OxyColors.Gray
        });
        
        TrendModel.Axes.Add(new LinearAxis 
        { 
            Position = AxisPosition.Left, MinimumPadding = 0.1, MaximumPadding = 0.1,
            TextColor = OxyColors.LightGray, TicklineColor = OxyColors.Gray,
            AxislineColor = OxyColors.Gray
        });
        
        TrendModel.Series.Add(new LineSeries 
        { 
            Title = "E-Posta Sayısı", Color = OxyColor.Parse("#0078D7"), 
            MarkerType = MarkerType.Circle, MarkerSize = 4, 
            MarkerFill = OxyColor.Parse("#0078D7"), MarkerStroke = OxyColors.White 
        });
    }

    private void UpdateChart(int emailCount)
    {
        if (TrendModel.Series[0] is LineSeries series)
        {
            series.Points.Add(new DataPoint(DateTimeAxis.ToDouble(DateTime.Now), emailCount));
            if (series.Points.Count > 20) series.Points.RemoveAt(0); // Son 20 noktayı tutarak kaydır
            TrendModel.InvalidatePlot(true);
        }
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

    public void Dispose()
    {
        _statusSubscription.Dispose();
    }
}
