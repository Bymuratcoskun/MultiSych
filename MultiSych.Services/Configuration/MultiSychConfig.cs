namespace MultiSych.Services.Configuration;

public class MultiSychConfig
{
    public ProviderSettings? Google { get; set; }
    public ProviderSettings? Microsoft { get; set; }
    public ProviderSettings? Yandex { get; set; }
    public AISettings? AI { get; set; }
    public SecuritySettings? Security { get; set; }
    public SyncSettings? Sync { get; set; }
    public DatabaseSettings? Database { get; set; }
}

public class ProviderSettings
{
    public string? ClientId { get; set; }
    public string? ClientSecret { get; set; }
    public string? RedirectUrl { get; set; }
    public string? TenantId { get; set; } // Sadece Microsoft için
}

public class AISettings
{
    public string? CopilotApiKey { get; set; }
    public string? GeminiApiKey { get; set; }
    public string? YandexAiApiKey { get; set; }
    public string? DefaultProvider { get; set; } = "hybrid";
}

public class SecuritySettings
{
    public bool UseLocalOnly { get; set; }
    public bool EncryptStorage { get; set; }
    public bool RequireStartupPassword { get; set; }
    public bool EnableTwoFactorAuth { get; set; }
    public string? TwoFactorSecret { get; set; }
    public string? ReportFolder { get; set; }
}

public class SyncSettings
{
    public bool AutoSyncEnabled { get; set; } = true;
    public int SyncIntervalMinutes { get; set; } = 15;
    public bool SyncEmailsEnabled { get; set; } = true;
    public bool SyncCalendarEnabled { get; set; } = true;
    public bool SyncStorageEnabled { get; set; } = true;
}

public class DatabaseSettings
{
    public string? DatabasePath { get; set; }
}
