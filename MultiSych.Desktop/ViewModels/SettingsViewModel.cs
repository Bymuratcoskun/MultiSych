using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Collections.ObjectModel;
using System.Windows.Input;
using Microsoft.EntityFrameworkCore;
using Avalonia.Threading;
using Microsoft.Extensions.DependencyInjection;
using MultiSych.Services.Data;
using MultiSych.Services.Configuration;
using Serilog;
using MultiSych.Services.Interfaces;

namespace MultiSych.Desktop.ViewModels;

public class SettingsViewModel : ViewModelBase
{
    private readonly MultiSychConfig _config;
    private readonly RuntimeSyncSettings _runtimeSyncSettings;
    private readonly IConfigurationService _configurationService;
    private readonly IUserSettingsService _userSettingsService;
    private readonly IServiceScopeFactory _scopeFactory;
    private DispatcherTimer? _logTimer;
    private string _liveLogs = "Loading logs...";
    private string _selectedLanguage;
    private string _selectedTheme;
    private bool _isLogPaused;
    private bool _startMinimized;
    private bool _enableNotificationSounds;

    public SettingsViewModel(MultiSychConfig config, RuntimeSyncSettings runtimeSyncSettings, IServiceScopeFactory scopeFactory, IConfigurationService configurationService, IUserSettingsService userSettingsService)
    {
        _config = config;
        _runtimeSyncSettings = runtimeSyncSettings;
        _configurationService = configurationService;
        _userSettingsService = userSettingsService;
        _scopeFactory = scopeFactory;
        AvailableLanguages = ["English", "Türkçe"];
        AvailableThemes = ["Modern", "Retro", "Sade"];
        SaveCommand = new RelayCommand(async _ => await SaveSettingsAsync());
        
        ToggleLogPauseCommand = new RelayCommand(_ => IsLogPaused = !IsLogPaused);
        ClearLogCommand = new RelayCommand(_ => LiveLogs = "Loglar temizlendi.");
        BackupDatabaseCommand = new RelayCommand(async _ => await BackupDatabaseAsync());
        RestoreDatabaseCommand = new RelayCommand(_ => RestoreDatabase());
        ClearCacheCommand = new RelayCommand(async _ => await ClearCacheAsync());

        _selectedLanguage = _userSettingsService.Settings.Language;
        _selectedTheme = _userSettingsService.Settings.Theme;
        _startMinimized = _userSettingsService.Settings.StartMinimized;
        _enableNotificationSounds = _userSettingsService.Settings.EnableNotificationSounds;
        StartLogWatcher();
    }

    public ObservableCollection<string> AvailableLanguages { get; }
    public ObservableCollection<string> AvailableThemes { get; }

    public int SyncIntervalMinutes
    {
        get => _runtimeSyncSettings.SyncIntervalMinutes;
        set
        {
            _runtimeSyncSettings.SyncIntervalMinutes = value;
            OnPropertyChanged();
        }
    }

    public bool AutoSyncEnabled
    {
        get => _runtimeSyncSettings.AutoSyncEnabled;
        set
        {
            _runtimeSyncSettings.AutoSyncEnabled = value;
            OnPropertyChanged();
        }
    }

    public bool StartMinimized
    {
        get => _startMinimized;
        set
        {
            _startMinimized = value;
            OnPropertyChanged();
        }
    }

    public bool EnableNotificationSounds
    {
        get => _enableNotificationSounds;
        set
        {
            _enableNotificationSounds = value;
            OnPropertyChanged();
        }
    }

    public string CopilotApiKey
    {
        get => _config.AI?.CopilotApiKey ?? string.Empty;
        set
        {
            if (_config.AI != null)
            {
                _config.AI.CopilotApiKey = value;
                OnPropertyChanged();
            }
        }
    }

    public string GeminiApiKey
    {
        get => _config.AI?.GeminiApiKey ?? string.Empty;
        set
        {
            if (_config.AI != null)
            {
                _config.AI.GeminiApiKey = value;
                OnPropertyChanged();
            }
        }
    }

    public string YandexApiKey
    {
        get => _config.AI?.YandexAiApiKey ?? string.Empty;
        set
        {
            if (_config.AI != null)
            {
                _config.AI.YandexAiApiKey = value;
                OnPropertyChanged();
            }
        }
    }

    public string SelectedLanguage
    {
        get => _selectedLanguage;
        set
        {
            if (SetProperty(ref _selectedLanguage, value))
            {
                App.ApplyLanguage(value == "Türkçe" ? "tr-TR" : "en-US");
            }
        }
    }

    public string SelectedTheme
    {
        get => _selectedTheme;
        set
        {
            if (SetProperty(ref _selectedTheme, value))
            {
                App.ApplyTheme(value);
            }
        }
    }

    public ICommand SaveCommand { get; }

    public string LiveLogs
    {
        get => _liveLogs;
        set => SetProperty(ref _liveLogs, value);
    }

    public bool IsLogPaused
    {
        get => _isLogPaused;
        set { if (SetProperty(ref _isLogPaused, value)) OnPropertyChanged(nameof(PauseButtonText)); }
    }

    public string PauseButtonText => IsLogPaused ? "▶️ Devam Et" : "⏸️ Duraklat";

    public ICommand ToggleLogPauseCommand { get; }
    public ICommand ClearLogCommand { get; }
    public ICommand BackupDatabaseCommand { get; }
    public ICommand RestoreDatabaseCommand { get; }
    public ICommand ClearCacheCommand { get; }

    private async Task SaveSettingsAsync()
    {
        Log.Information("Saving settings to .env file. SyncInterval: {Interval}, AutoSync: {AutoSync}", SyncIntervalMinutes, AutoSyncEnabled);

        try
        {
            var settingsToSave = new Dictionary<string, string>
            {
                { "MULTISYCH_SYNC_INTERVAL", SyncIntervalMinutes.ToString() },
                { "MULTISYCH_AUTO_SYNC", AutoSyncEnabled.ToString() },
                { "COPILOT_API_KEY", CopilotApiKey },
                { "GEMINI_API_KEY", GeminiApiKey },
            { "YANDEX_API_KEY", YandexApiKey },
            };

            // Çevresel ve API ayarlarını .env'ye kaydet
            await _configurationService.SaveSettingsAsync(settingsToSave);
            
            // Arayüz ve kullanıcı ayarlarını JSON'a kaydet
            _userSettingsService.Settings.Language = SelectedLanguage;
            _userSettingsService.Settings.Theme = SelectedTheme;
            _userSettingsService.Settings.StartMinimized = StartMinimized;
            _userSettingsService.Settings.EnableNotificationSounds = EnableNotificationSounds;
            await _userSettingsService.SaveAsync();
            
            Log.Information("Settings successfully saved via ConfigurationService.");
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to save settings to .env file");
        }
    }

    private async Task ClearCacheAsync()
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<LocalCacheDbContext>();
            
            // Sanal Sürücü kayıtlarını veritabanından temizle
            dbContext.CloudFiles.RemoveRange(dbContext.CloudFiles);
            await dbContext.SaveChangesAsync();

            // İndirilen dosyaların bulunduğu fiziksel klasörü boşalt
            var drivesPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "MultiSych", "Drives");
            if (Directory.Exists(drivesPath))
            {
                Directory.Delete(drivesPath, true);
                Directory.CreateDirectory(drivesPath);
            }

            LiveLogs = $"[BAŞARILI] Bulut sürücü önbelleği ve indirilen dosyalar tamamen temizlendi. Toplam açılan alan diskte güncellendi.";
        }
        catch (Exception ex)
        {
            LiveLogs = $"[HATA] Önbellek temizlenirken hata oluştu: {ex.Message}";
        }
    }

    private async Task BackupDatabaseAsync()
    {
        try
        {
            var backupFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "MultiSych_Backups");
            if (!Directory.Exists(backupFolder)) Directory.CreateDirectory(backupFolder);
            
            var backupPath = Path.Combine(backupFolder, $"localcache_backup_{DateTime.Now:yyyyMMdd_HHmmss}.db");
            
            using var scope = _scopeFactory.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<LocalCacheDbContext>();
            
            // Güvenli SQLite Backup API (Lock hatalarını ve veri bozulmasını önler)
            var sourceConnection = dbContext.Database.GetDbConnection();
            bool wasClosed = sourceConnection.State == System.Data.ConnectionState.Closed;
            if (wasClosed) await sourceConnection.OpenAsync();
            
            using var destinationConnection = new Microsoft.Data.Sqlite.SqliteConnection($"Data Source={backupPath}");
            await destinationConnection.OpenAsync();
            
            if (sourceConnection is Microsoft.Data.Sqlite.SqliteConnection sqliteSource)
            {
                sqliteSource.BackupDatabase(destinationConnection);
            }
            
            if (wasClosed) await sourceConnection.CloseAsync();
            
            LiveLogs = $"[BAŞARILI] Veritabanı yedeği alındı:\n{backupPath}";
        }
        catch (Exception ex)
        {
            LiveLogs = $"[HATA] Yedekleme başarısız: {ex.Message}";
        }
    }

    private void RestoreDatabase()
    {
        try
        {
            var dbPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "MultiSych", "Database", "localcache.db");
            var backupFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "MultiSych_Backups");
            
            if (!Directory.Exists(backupFolder))
            {
                LiveLogs = "[HATA] Yedek klasörü (Belgelerim/MultiSych_Backups) bulunamadı.";
                return;
            }
            
            var latestBackup = Directory.GetFiles(backupFolder, "*.db").OrderByDescending(f => f).FirstOrDefault();
            if (latestBackup != null)
            {
                File.Copy(latestBackup, dbPath, true);
                LiveLogs = $"[BAŞARILI] Veritabanı geri yüklendi:\n{latestBackup}\nÖNEMLİ: Değişikliklerin etkili olması için uygulamayı yeniden başlatın!";
            }
            else
            {
                LiveLogs = "[HATA] Geri yüklenecek herhangi bir .db dosyası bulunamadı.";
            }
        }
        catch (Exception ex)
        {
            LiveLogs = $"[HATA] Geri yükleme başarısız: {ex.Message}\nNot: Arka planda senkronizasyon çalışıyorsa dosya kilitli olabilir.";
        }
    }

    private void StartLogWatcher()
    {
        _logTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
        _logTimer.Tick += async (s, e) => await UpdateLogsAsync();
        _logTimer.Start();
    }

    private async Task UpdateLogsAsync()
    {
        if (IsLogPaused) return;

        try
        {
            var logsFolder = Path.Combine(Directory.GetCurrentDirectory(), "logs");
            if (Directory.Exists(logsFolder))
            {
                var latestLog = Directory.GetFiles(logsFolder, "multisych-*.txt").OrderByDescending(f => f).FirstOrDefault();
                if (latestLog != null)
                {
                    using var stream = new FileStream(latestLog, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                    
                    // Dosya büyükse doğrudan son 10.000 bayta atla, tüm dosyayı belleğe yükleme
                    if (stream.Length > 10000)
                    {
                        stream.Seek(-10000, SeekOrigin.End);
                    }
                    
                    using var reader = new StreamReader(stream);
                    var content = await reader.ReadToEndAsync();
                    LiveLogs = string.IsNullOrWhiteSpace(content) ? "Log file is currently empty." : content;
                }
            }
        }
        catch (Exception ex)
        {
            LiveLogs = $"Error reading logs: {ex.Message}";
        }
    }
}
