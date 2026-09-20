# Kod Sağlığı Denetimi: Sahte Başarı ve Sessiz Hata Taraması Raporu

**Tarih:** 2026-09-20  
**Hazırlayan:** Antigravity (Uygulayıcı)  
**Kapsam:** `MultiSych.Services/Implementations/*.cs` ve `MultiSych.Desktop/ViewModels/*.cs`  
**Referans:** `IS-PAKETI-sahte-basari-taramasi.md` · `~/Projelerim/KURALLAR.md` §2 · `docs/KARARLAR.md`  
**Hariç Tutulan:** `MultiSych.Tests/` (kural gereği taranmadı)

---

## Kategori 1: Sessiz Yutulan Hata (Boş / Loglanmayan `catch` Blokları)

### 1.1 İndirme Hatasında 0 Bayt Dosya Oluşturulup Yerel Düzenleyici Açılması
**Dosya:Satır:** `MultiSych.Desktop/ViewModels/DocumentsViewModel.cs:539-545`  
**Kanıt:**
```csharp
                        catch (Exception ex)
                        {
                            _logger.Error(ex, "Failed to download file for local editing, creating empty file instead.");
                            try { await File.WriteAllBytesAsync(localFilePath, Array.Empty<byte>()); } catch { }
                            LaunchLocalEditor(localFilePath);
                        }
```
**Kategori:** 1  
**Neden şüpheli:** Buluttan dosya indirme başarısız olduğunda kullanıcıya hata bildirilmemekte, diske 0 baytlık boş dosya yazılıp düzenleyici açılmaktadır; kullanıcı bu boş dosyayı kaydedip kapatırsa veya otomatik senkronizasyon devreye girerse buluttaki gerçek dosyanın üzerine 0 bayt yazılarak veri kaybı yaşanır. Ayrıca içteki `catch { }` bloğu da hatayı sessizce yutmaktadır.

---

### 1.2 Dosya Şifreleme ve Kopyalama Hatasının Sessizce Yutulması
**Dosya:Satır:** `MultiSych.Desktop/ViewModels/FileExplorerViewModel.cs:199-204`  
**Kanıt:**
```csharp
                    else
                    {
                        System.IO.File.Copy(path, targetPath, true);
                    }
                } 
                catch { /* Önemsiz önbellekleme hatalarını yoksay */ }
            }

            appStatusService.PostUpdate("Yükleme tamamlandı. Liste güncelleniyor...", true);
```
**Kategori:** 1  
**Neden şüpheli:** Dosyanın şifrelenmesi (`EncryptBytes`) veya yerel hedef yola kopyalanması sırasında oluşabilecek I/O veya şifreleme hataları loglanmadan yutulmakta ve kullanıcıya hemen ardından "Yükleme tamamlandı" bildirimi verilmektedir.

---

### 1.3 Linux Sürücü Bağlamada Boş Dosya Oluşturma Hatasının Yutulması
**Dosya:Satır:** `MultiSych.Services/Implementations/PlatformMountProvider.cs:174-178`  
**Kanıt:**
```csharp
                            // Create empty placeholder file if it doesn't exist
                            if (!File.Exists(localPath))
                            {
                                try { await File.WriteAllBytesAsync(localPath, Array.Empty<byte>()); } catch { }
                            }
```
**Kategori:** 1  
**Neden şüpheli:** Linux mount simülasyonunda yerel dizine 0 baytlık dosya oluşturulamazsa hata loglanmadan geçilmekte ve eksik dosyalarla işlem başarılı sayılmaktadır.

---

### 1.4 Linux Sürücü Senkronizasyonunda Boş Dosya Oluşturma Hatasının Yutulması
**Dosya:Satır:** `MultiSych.Services/Implementations/PlatformMountProvider.cs:321-325`  
**Kanıt:**
```csharp
                        if (!File.Exists(localPath))
                        {
                            try { await File.WriteAllBytesAsync(localPath, Array.Empty<byte>()); } catch { }
                        }
```
**Kategori:** 1  
**Neden şüpheli:** Sanal sürücü arka plan yenilemesinde dosya oluşturma hatası sessizce yok sayılmaktadır.

---

### 1.5 E-posta Okundu (Seen) Bayrağı Sorgu Hatasının Yutulması
**Dosya:Satır:** `MultiSych.Services/Implementations/EmailService.cs:173-178`  
**Kanıt:**
```csharp
            bool isRead = false;
            try
            {
                var summaries = await client.Inbox.FetchAsync(new[] { uids.First() }, MessageSummaryItems.Flags);
                isRead = summaries?.FirstOrDefault()?.Flags?.HasFlag(MessageFlags.Seen) ?? false;
            }
            catch { }
```
**Kategori:** 1  
**Neden şüpheli:** IMAP sunucusundan mesaj bayrakları alınırken oluşacak ağ veya oturum hatası loglanmamakta, e-posta her zaman okunmamış (`isRead = false`) varsayılmaktadır.

---

### 1.6 Ses Sentezi İptalinde Hatanın Sessizce Yutulması
**Dosya:Satır:** `MultiSych.Services/Implementations/WhisperSpeechService.cs:254-256`  
**Kanıt:**
```csharp
                try { _synthesizer?.SpeakAsyncCancelAll(); }
                catch { }
```
**Kategori:** 1  
**Neden şüpheli:** Windows platformunda konuşma sentezleyicisi durdurulurken oluşabilecek alt sistem hataları hiçbir kayıt bırakılmadan yutulmaktadır.

---

### 1.7 Ses Kaydı Geçici Dosyası Silinirken Hatanın Sessizce Yutulması
**Dosya:Satır:** `MultiSych.Services/Implementations/WhisperSpeechService.cs:355-362`  
**Kanıt:**
```csharp
                var partialCopy = _realTimeTempFilePath + ".partial";
                if (File.Exists(partialCopy))
                {
                    File.Delete(partialCopy);
                }
            }
            catch { }
```
**Kategori:** 1  
**Neden şüpheli:** Geçici ses dosyası temizlenirken karşılaşılan dosya kilidi veya erişim reddi hataları loglanmamaktadır.

---

### 1.8 Dokan Yerel Önbellek Dosyası Silinirken Hatanın Yutulması
**Dosya:Satır:** `MultiSych.Services/Implementations/CloudVirtualFileSystem.cs:560-562`  
**Kanıt:**
```csharp
        {
            try { File.Delete(localCachePath); } catch { }
        }
```
**Kategori:** 1  
**Neden şüpheli:** Yerel önbellekteki dosya silinirken hata oluşursa sessizce geçilmektedir.

---

### 1.9 [Düşük Öncelikli / Sinyal Döndüren] Linux ve macOS Pil Durumu Hatalarının Yutulması
**Dosya:Satır:** `MultiSych.Services/Implementations/PowerStatusHelper.cs:57, 78, 115, 141`  
**Kanıt:**
```csharp
// Satır 57 (Linux IsOnBattery)
catch { }
return false;

// Satır 78 (macOS IsOnBattery)
catch { }
return false;

// Satır 115 (Linux GetBatteryPercent)
catch { }
return 100;

// Satır 141 (macOS GetBatteryPercent)
catch { }
return 100;
```
**Kategori:** 1  
**Neden şüpheli:** `/sys/class/power_supply` veya `pmset` okunamadığında hata loglanmadan varsayılan değer dönülmektedir (en azından bir sinyal dönüldüğü için düşük önceliklidir).

---

### 1.10 [Düşük Öncelikli / Sinyal Döndüren] TOTP Doğrulama Hatasının Yutulması
**Dosya:Satır:** `MultiSych.Desktop/ViewModels/AuthViewModel.cs:100-101`  
**Kanıt:**
```csharp
        }
        catch { }
        return false;
```
**Kategori:** 1  
**Neden şüpheli:** Base32 kod çözümü veya HMAC üretiminde hata olursa log basılmadan doğrudan `false` dönülmektedir.

---

## Kategori 2: Yer Tutucu / Sahte Değerler (Placeholders & Stubs)

### 2.1 Hata Bildiriminde Hardcoded "yourusername" GitHub URL'si
**Dosya:Satır:** `MultiSych.Desktop/ViewModels/ErrorReportViewModel.cs:36`  
**Kanıt:**
```csharp
var url = $"https://github.com/yourusername/MultiSych/issues/new?title={Uri.EscapeDataString(IssueTitle)}&body={Uri.EscapeDataString(body)}";
```
**Kategori:** 2  
**Neden şüpheli:** Kullanıcı hata bildirmek istediğinde projenin gerçek GitHub reposu yerine taslak halindeki `yourusername` adresine yönlendirilmektedir.

---

### 2.2 Dokan Sanal Sürücüsünde Sabit Disk Alanı (1 GB Toplam, 512 MB Boş)
**Dosya:Satır:** `MultiSych.Services/Implementations/CloudVirtualFileSystem.cs:891-896`  
**Kanıt:**
```csharp
    public NtStatus GetDiskFreeSpace(out long freeBytesAvailable, out long totalNumberOfBytes, out long totalNumberOfFreeBytes, IDokanFileInfo info)
    {
        freeBytesAvailable = 512 * 1024 * 1024;
        totalNumberOfBytes = 1024 * 1024 * 1024;
        totalNumberOfFreeBytes = 512 * 1024 * 1024;
        return DokanResult.Success;
    }
```
**Kategori:** 2  
**Neden şüpheli:** Kullanıcının gerçek bulut kotası ve yerel disk kapasitesi göz ardı edilerek Windows dosya yöneticisine her zaman sabit 1 GB toplam, 512 MB boş alan bildirilmektedir.

---

### 2.3 Yandex Kişi Senkronizasyonunda "not yet implemented" Yer Tutucusu
**Dosya:Satır:** `MultiSych.Services/Implementations/CloudYandexService.cs:295-298`  
**Kanıt:**
```csharp
    public async Task<int> SyncContactsAsync()
    {
        try
        {
            // Placeholder for future contact sync
            _logger.LogInformation("Yandex contacts sync not yet implemented");
            return 0;
        }
```
**Kategori:** 2  
**Neden şüpheli:** Adı senkronizasyon olan metot geleceğe yönelik bir yer tutucudan ibarettir ve hiçbir işlem yapmadan log düşüp 0 dönmektedir.

---

### 2.4 Kod Tabanında Yer Alan 0 Baytlık Ölü/Yetim Dosyalar
**Dosya:Satır:**
- `MultiSych.Services/Implementations/AccountStore.cs` (0 bayt)
- `MultiSych.Services/Implementations/SyncedIconOverlayHandler.cs` (0 bayt)
- `MultiSych.Desktop/MainWindow.axaml.cs` (0 bayt)  
**Kanıt:** Dosyalar disk üzerinde mevcuttur ancak boyutları 0 bayttır. Gerçek `IAccountStore` uygulaması `AccountStoreService.cs` içindedir; `SyncedIconOverlayHandler` ve Avalonia'dan kalma `MainWindow.axaml.cs` ise tamamen boştur.  
**Kategori:** 2  
**Neden şüpheli:** Proje ağacında var gibi görünen ama içi tamamen boş olan yetim dosyalardır.

---

### 2.5 Devre Dışı Bırakılmış İçi Boş FUSE Sınıfı
**Dosya:Satır:** `MultiSych.Services/Implementations/CloudFuseFileSystem.cs:10-25`  
**Kanıt:**
```csharp
/// <summary>
/// Linux/macOS FUSE desteği kütüphane uyumsuzluğu nedeniyle bu derleme için devre dışı bırakıldı.
/// </summary>
public class CloudFuseFileSystem
{
    private readonly string _accountId;
    private readonly IStorageService _storageService;
    private readonly IDbContextFactory<LocalCacheDbContext> _dbContextFactory;
    private readonly ILogger _logger = Log.ForContext<CloudFuseFileSystem>();

    public CloudFuseFileSystem(string mountPoint, string accountId, IStorageService storageService, IDbContextFactory<LocalCacheDbContext> dbContextFactory)
    {
        _accountId = accountId;
        _storageService = storageService;
        _dbContextFactory = dbContextFactory;
    }
}
```
**Kategori:** 2  
**Neden şüpheli:** FUSE dosya sistemi sağladığı izlenimi veren sınıfın içinde hiçbir dosya sistemi operasyonu bulunmamakta, yalnızca boş bir yapıcı metot yer almaktadır.

---

### 2.6 DocumentAnalyzerViewModel İçinde Unutulmuş Ölü Property (`ExportPassword`)
**Dosya:Satır:** `MultiSych.Desktop/ViewModels/DocumentAnalyzerViewModel.cs:25, 72-76`  
**Kanıt:**
```csharp
    private string _exportPassword = string.Empty;
...
    public string ExportPassword
    {
        get => _exportPassword;
        set => SetProperty(ref _exportPassword, value);
    }
```
**Kategori:** 2  
**Neden şüpheli:** `DocumentAnalyzerViewModel` içinde tanımlı `ExportPassword` hiçbir komut, servis veya arayüz mantığı tarafından okunmamakta ve kullanılmamaktadır; sahte/ölü bir yer tutucudur.

---

## Kategori 3: Hiçbir Şey Yapmadan `return` Eden Metodlar

### 3.1 Dokan Dosya Kapatma Metodunun Gövdesinin Boş Olması
**Dosya:Satır:** `MultiSych.Services/Implementations/CloudVirtualFileSystem.cs:565`  
**Kanıt:**
```csharp
    public void CloseFile(string fileName, IDokanFileInfo info) { }
```
**Kategori:** 3  
**Neden şüpheli:** Dosya tanıtıcılarının kapatılmasını yönetmesi gereken metot hiçbir işlem yapmadan boş sonlanmaktadır.

---

### 3.2 Dokan Buffer Boşaltmanın (Flush) İşlemsiz Başarı Dönmesi
**Dosya:Satır:** `MultiSych.Services/Implementations/CloudVirtualFileSystem.cs:567`  
**Kanıt:**
```csharp
    public NtStatus FlushFileBuffers(string fileName, IDokanFileInfo info) => DokanResult.Success;
```
**Kategori:** 3  
**Neden şüpheli:** Dosya tampon belleği fiziksel olarak diske veya buluta aktarılmadığı halde işletim sistemine doğrudan başarı raporlanmaktadır.

---

### 3.3 Dokan Dosya Güvenliği Ayarlamasının (SetFileSecurity) İşlemsiz Başarı Dönmesi
**Dosya:Satır:** `MultiSych.Services/Implementations/CloudVirtualFileSystem.cs:959-962`  
**Kanıt:**
```csharp
    public NtStatus SetFileSecurity(string fileName, FileSystemSecurity security, AccessControlSections sections, IDokanFileInfo info)
    {
        return DokanResult.Success;
    }
```
**Kategori:** 3  
**Neden şüpheli:** Güvenlik ve izin ayarları (ACL) sanal sürücüde hiçbir şekilde uygulanmadığı halde başarı dönülmektedir.

---

### 3.4 Dokan Dosya Kilitleme ve Kilit Açmanın İşlemsiz Başarı Dönmesi
**Dosya:Satır:** `MultiSych.Services/Implementations/CloudVirtualFileSystem.cs:864 & 982-985`  
**Kanıt:**
```csharp
    public NtStatus UnlockFile(string fileName, long offset, long length, IDokanFileInfo info) => DokanResult.Success;
...
    public NtStatus LockFile(string fileName, long offset, long length, IDokanFileInfo info)
    {
        return DokanResult.Success;
    }
```
**Kategori:** 3  
**Neden şüpheli:** Dosya kilitleme ve kilit çözme talepleri arka planda herhangi bir lock tablosu veya eşzamanlılık kontrolü işletilmeden başarılı sayılmaktadır.

---

### 3.5 Desteklenmeyen Takvim Sağlayıcılarında Sessiz Boş Liste Dönülmesi (Fail-Loud İhlali)
**Dosya:Satır:** `MultiSych.Services/Implementations/CloudCalendarService.cs:37-45`  
**Kanıt:**
```csharp
            if (credentials.Provider == "Google")
                return await GetGoogleEventsAsync(credentials, startDate, endDate);
            if (credentials.Provider == "Microsoft")
                return await GetMicrosoftEventsAsync(credentials, startDate, endDate);
            if (credentials.Provider == "Yandex")
                return await GetYandexEventsAsync(credentials, startDate, endDate);

            return new List<CalendarEvent>();
```
**Kategori:** 3  
**Neden şüpheli:** Aynı sınıftaki diğer tüm metodlar (`GetEventAsync`, `CreateEventAsync`, `DeleteEventAsync`) desteklenmeyen sağlayıcılar için `throw new NotSupportedException(...)` fırlatarak fail-loud ilkesine uyarken, `GetEventsAsync` sessizce boş liste dönerek sağlayıcının takviminde etkinlik yokmuş gibi davranmaktadır.

---

### 3.6 CloudStorageService İçinde Dosya Arama Metodunun NotImplementedException Fırlatması
**Dosya:Satır:** `MultiSych.Services/Implementations/CloudStorageService.cs:1152`  
**Kanıt:**
```csharp
        public Task<List<CloudFile>> SearchFilesAsync(AccountCredentials credentials, string query) => throw new NotImplementedException();
```
**Kategori:** 3  
**Neden şüpheli:** `IStorageService` arayüzünün bir parçası olan dosya arama fonksiyonu servis seviyesinde henüz uygulanmamıştır.

---

## Kategori 4: UI Metninde İddia ile Kodun Uyuşmazlığı

### 4.1 Veritabanı Geri Yüklemede Yanlış Dosya Yoluna Kopyalayıp "Başarılı" Mesajı Verilmesi
**Dosya:Satır:** `MultiSych.Desktop/ViewModels/SettingsViewModel.cs:329-343`  
**Kanıt:**
```csharp
            var dbPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "MultiSych", "Database", "localcache.db");
            var backupFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "MultiSych_Backups");
...
            var latestBackup = Directory.GetFiles(backupFolder, "*.db").OrderByDescending(f => f).FirstOrDefault();
            if (latestBackup != null)
            {
                File.Copy(latestBackup, dbPath, true);
                LiveLogs = $"[BAŞARILI] Veritabanı geri yüklendi:\n{latestBackup}\nÖNEMLİ: Değişikliklerin etkili olması için uygulamayı yeniden başlatın!";
            }
```
**Kategori:** 4  
**Neden şüpheli:** Uygulama `Program.cs:281` satırında aktif veritabanı olarak `MultiSych/multisych.db` yolunu kullanmaktadır; ancak geri yükleme fonksiyonu yedeği hiç kullanılmayan `MultiSych/Database/localcache.db` dosyasına kopyalamakta ve kullanıcıya "Veritabanı geri yüklendi... yeniden başlatın" demektedir. Kullanıcı uygulamayı yeniden başlattığında eski `multisych.db` okunur, geri yükleme fiilen hiçbir işe yaramamış olur.

---

### 4.2 "Logları Temizle" Butonunun Dosyayı Temizlemeyip Sadece UI Metnini Geçici Değiştirmesi
**Dosya:Satır:** `MultiSych.Desktop/ViewModels/SettingsViewModel.cs:60, 357-364`  
**Kanıt:**
```csharp
// SettingsViewModel.cs:60
ClearLogCommand = new RelayCommand(_ => LiveLogs = "Loglar temizlendi.");

// SettingsViewModel.cs:357-364
_logTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
_logTimer.Tick += async (s, e) => await UpdateLogsAsync();
```
**Kategori:** 4  
**Neden şüpheli:** Kullanıcı "Logları Temizle" butonuna bastığında log dosyası silinmez veya içi boşaltılmaz; yalnızca arayüzdeki `LiveLogs` değişkenine "Loglar temizlendi." metni atanır ve 2 saniye sonra periyodik `_logTimer` diskteki log dosyasını tekrar okuyarak eski içeriği ekrana geri basar.

---

### 4.3 Önbellek Temizlemede Yanlış Klasörü Silip "İndirilen Dosyalar Temizlendi" Mesajı Verilmesi
**Dosya:Satır:** `MultiSych.Desktop/ViewModels/SettingsViewModel.cs:271-282`  
**Kanıt:**
```csharp
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
```
**Kategori:** 4  
**Neden şüpheli:** Uygulamada indirilen dosyalar gerçekte `~/MultiSych_Drives/` klasöründedir (`DocumentsViewModel.cs:516`, `AutoSyncBackgroundService.cs:177`); kod ise boş bağlama dizinleri içeren `~/.config/MultiSych/Drives` yolunu silmekte, indirilen dosyalara hiç dokunmamaktadır. Buna karşın veritabanındaki `CloudFiles` kayıtları silindiği için diskte yetim/indeksiz dosyalar kalırken kullanıcıya "indirilen dosyalar tamamen temizlendi" mesajı verilmektedir.

---

### 4.4 Kenar Çubuğundaki "📋 Loglar" Menü Öğesinin GitHub Hata Formuna Açılması
**Dosya:Satır:** `MultiSych.Desktop/Views/MainWindow.cs:122` ve `MultiSych.Desktop/ViewModels/MainWindowViewModel.cs:262`  
**Kanıt:**
```csharp
// MainWindow.cs:122
AddNavigationRow("Logs", "📋 Loglar");

// MainWindowViewModel.cs:262
"Logs" => ErrorReportPage,
```
**Kategori:** 4  
**Neden şüpheli:** Kullanıcı menüde "📋 Loglar" sekmesine tıkladığında sistem loglarını inceleyeceğini beklerken `ErrorReportPage` (GitHub Issue başlık/açıklama gönderme formu) ile karşılaşmaktadır. Canlı loglar ise Ayarlar ekranı altındadır.

---

### 4.5 Linux'ta "Sanal Montaj" Adı Altında Kullanıcıya 0 Baytlık Dosyalar Sunulması
**Dosya:Satır:** `MultiSych.Services/Implementations/PlatformMountProvider.cs:139, 174-176`  
**Kanıt:**
```csharp
_logger.Information("Starting simulated Unix mount on {MountPoint} linking to {TargetPath}", mountPoint, targetPath);
...
// Create empty placeholder file if it doesn't exist
if (!File.Exists(localPath))
{
    try { await File.WriteAllBytesAsync(localPath, Array.Empty<byte>()); } catch { }
}
```
**Kategori:** 4  
**Neden şüpheli:** Kullanıcı arayüzünde "Sürücü başarıyla bağlandı" denilip dosya yöneticisi (`xdg-open`) açılmakta; ancak klasördeki tüm dosyalar 0 baytlık içi boş dosyalardan ibaret olmaktadır. Kullanıcı bir dosyaya tıkladığında boş bir belge ile karşılaşmaktadır.

---

### 4.6 Sayfa Yenileme Komutunun Yalnızca Hesaplar Sayfasında Çalışması
**Dosya:Satır:** `MultiSych.Desktop/ViewModels/MainWindowViewModel.cs:355-365`  
**Kanıt:**
```csharp
    private async Task RefreshCurrentPageAsync()
    {
        if (CurrentPageViewModel is AccountsViewModel accountsPage)
        {
            accountsPage.LoadAccountsCommand.Execute(null);
            StatusMessage = "Refreshing accounts page...";
            return;
        }

        StatusMessage = "Refresh is supported on the Accounts page only.";
        await Task.CompletedTask;
    }
```
**Kategori:** 4  
**Neden şüpheli:** Genel bir "Sayfayı Yenile" (Refresh) komutu sunulmasına rağmen, Hesaplar sayfası dışındaki tüm sayfalarda (Panel, Belgeler, Dosyalar, vb.) hiçbir işlem yapılmadan yalnızca durum çubuğuna kısıtlayıcı bir mesaj yazılmaktadır.

---

### 4.7 ❓ EMİN DEĞİLİM / RİSKLİ: Avalonia'dan Kalan `Dispatcher.UIThread` Çağrısı (GTK4 Çalışma Zamanında Olası NRE)
**Dosya:Satır:** `MultiSych.Desktop/ViewModels/CalendarViewModel.cs:7, 75-79`  
**Kanıt:**
```csharp
using Avalonia.Threading;
...
Dispatcher.UIThread.Post(() => 
{ 
    _allEvents = events;
    ApplyFilter();
});
```
**Kategori:** 4  
**Neden şüpheli:** MultiSych.Desktop projesi Avalonia'dan GTK4'e (GirCore) geçmiştir. `Program.cs` içinde Avalonia runtime başlatılmamaktadır (`Gtk.Application.New` çalışmaktadır). `CalendarViewModel` yüklendiğinde ve `Dispatcher.UIThread.Post` çağrıldığında Avalonia UIThread null olabileceğinden ya sessizce çalışmayabilir ya da `NullReferenceException` üretebilir. GTK4 ana iş parçacığı senkronizasyonu `GLib.Functions.IdleAdd` ile yapılmalıdır.

---

## Özet ve İstatistikler

### Toplam Bulgu Sayısı: 23

| Kategori | Açıklama | Bulgu Sayısı |
|:---|:---|:---:|
| **Kategori 1** | Sessiz Yutulan Hatalar (Boş / Loglanmayan `catch` blokları) | 10 |
| **Kategori 2** | Yer Tutucu / Sahte Değerler (Placeholders, Stubs, Ölü Dosyalar) | 6 |
| **Kategori 3** | Hiçbir Şey Yapmadan `return` Eden Metodlar | 6 |
| **Kategori 4** | UI Metninde İddia ile Kodun Uyuşmazlığı | 7 (1'i riskli/kontrol edilmeli) |
| **Toplam** | | **29** (Tekil bulgu başlığı sayısı: 23) |

> [!IMPORTANT]
> **En Kritik Bulgular (Öncelikli Eylem Önerilenler):**
> 1. `DocumentsViewModel.cs:539-545`: İndirilemeyen dosyanın yerine 0 bayt boş dosya yazılıp yerel düzenleyicinin açılması (buluttaki gerçek veriyi sıfırlama riski).
> 2. `SettingsViewModel.cs:329-343`: Veritabanı geri yüklemesinin `multisych.db` yerine `localcache.db` dosyasına kopyalanıp kullanıcıya "Geri yüklendi" denmesi (sahte başarı).
> 3. `SettingsViewModel.cs:271-282`: Önbellek temizliğinde gerçek indirme klasörü yerine boş klasörün silinip veritabanı kayıtlarının temizlenmesi (yetim dosyalar).
> 4. `ErrorReportViewModel.cs:36`: GitHub URL'sinde `yourusername` yer tutucusu.
> 5. `CalendarViewModel.cs:75`: GTK4 altında Avalonia `Dispatcher.UIThread.Post` çağrısı.

