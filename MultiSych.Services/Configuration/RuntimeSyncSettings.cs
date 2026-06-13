namespace MultiSych.Services.Configuration;

/// <summary>
/// Uygulama ayarlarının yeniden başlatmaya gerek kalmadan anlık olarak
/// arka plan servislerine yansıtılmasını sağlayan anlık durum (in-memory) sınıfı.
/// </summary>
public class RuntimeSyncSettings
{
    public int SyncIntervalMinutes { get; set; } = 15;
    public bool AutoSyncEnabled { get; set; } = true;
}
