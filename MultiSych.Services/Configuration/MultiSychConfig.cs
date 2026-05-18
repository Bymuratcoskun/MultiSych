namespace MultiSych.Services.Configuration
{
    public class MultiSychConfig
    {
        public GoogleSettings? Google { get; set; }
        public MicrosoftSettings? Microsoft { get; set; }
        public YandexSettings? Yandex { get; set; }
        public AISettings? AI { get; set; }
        public SecuritySettings? Security { get; set; }
        public DatabaseSettings? Database { get; set; }
        public SyncSettings? Sync { get; set; }
    }

    public class GoogleSettings
    {
        public string? ClientId { get; set; }
        public string? ClientSecret { get; set; }
        public string RedirectUrl { get; set; } = "http://localhost:8080/oauth/callback";
    }

    public class MicrosoftSettings
    {
        public string? ClientId { get; set; }
        public string? ClientSecret { get; set; }
        public string? TenantId { get; set; }
        public string RedirectUrl { get; set; } = "http://localhost:8080/oauth/callback";
    }

    public class YandexSettings
    {
        public string? ClientId { get; set; }
        public string? ClientSecret { get; set; }
        public string RedirectUrl { get; set; } = "http://localhost:8080/oauth/callback";
    }

    public class AISettings
    {
        public string? CopilotApiKey { get; set; }
        public string? GeminiApiKey { get; set; }
        public string? YandexAiApiKey { get; set; }
        public string DefaultProvider { get; set; } = "hybrid";
        public int RequestTimeoutSeconds { get; set; } = 30;
    }

    public class SecuritySettings
    {
        public bool UseLocalOnly { get; set; } = true;
        public bool RequireStartupPassword { get; set; } = false;
        public bool EnableTwoFactorAuth { get; set; } = false;
        public string? TwoFactorSecret { get; set; }
        public bool EncryptStorage { get; set; } = true;
        public string ReportFolder { get; set; } = "reports";
    }

    public class DatabaseSettings
    {
        public string? ConnectionString { get; set; }
        public string? DatabasePath { get; set; }
    }

    public class SyncSettings
    {
        public int SyncIntervalMinutes { get; set; } = 30;
        public bool AutoSyncEnabled { get; set; } = true;
        public bool SyncEmailsEnabled { get; set; } = true;
        public bool SyncCalendarEnabled { get; set; } = true;
        public bool SyncStorageEnabled { get; set; } = true;
    }
}
