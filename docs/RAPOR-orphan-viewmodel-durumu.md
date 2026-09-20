# Rapor: Yetim (Orphan) ViewModel Durumu ve Analizi

Yazan: Antigravity (Uygulayıcı — okuma/rapor rolü)  
Tarih: 2026-09-20  
Kapsam: FAZ 2 — Erişilemeyen özellikleri bağla veya kapat (`multisych/docs/YOL-HARITASI.md`)  
Denetlenen Dosyalar: `MultiSych.Desktop/ViewModels/` altındaki 5 yetim ViewModel ve servis katmanı implementasyonları.

---

## Genel Özet Tablosu

| ViewModel | Durum | Servis Bağlantısı | Servis Çalışıyor mu? | Nihai Öneri |
|---|---|---|---|---|
| **AIOverviewViewModel** | **BİTMİŞ** | Gerçek (`IWindowService`, `MultiSychConfig`) | Evet (`WindowService.ShowAIChat`, `AIService`) | Nav'a bağlansın (veya kenar çubuğuyla birleştirilsin) |
| **CalendarViewModel** | **YARIM** | Yetersiz (Yalnızca yerel `LocalCacheDbContext` DB cache) | Kısmi (`CloudCalendarService` var ama VM kullanmıyor) | Ertelensin (CRUD ve Sync eksik) |
| **DocumentAnalyzerViewModel** | **BİTMİŞ** | Gerçek (`IAIService`, `IWindowService`) | Evet (`AIService`, `WindowService.OpenFileDialogAsync`) | Nav'a bağlansın (`DocumentAnalyzerView.cs` yazılarak) |
| **DocumentsViewModel** | **BİTMİŞ** | Gerçek (`IStorageService`, `IAIService`, `IWindowService`, `ICalendarService`, vb.) | Evet (`CloudStorageService`, `AIService`, vb.) | Nav'a bağlansın (`DocumentsView.cs` yazılarak) |
| **ErrorReportViewModel** | **YARIM** | Yok (0 servis bağımlılığı, parametresiz ctor) | Hayır (Sahte URL: `yourusername`, log göstermez) | Ertelensin (İçi baştan yazılmalı) |

---

## AIOverviewViewModel — Durum: BİTMİŞ

### Kanıt (dosya:satır alıntılarıyla)
1. **ViewModel içi komutlar ve servis bağlantısı:**
   - `MultiSych.Desktop/ViewModels/AIOverviewViewModel.cs:16-24`:
     ```csharp
     public AIOverviewViewModel(IWindowService windowService, MultiSychConfig config)
     {
         _windowService = windowService;
         _config = config;

         ProviderButtons = new ObservableCollection<string> { "Copilot", "Gemini", "Yandex" };
         OpenChatCommand = new RelayCommand(provider => _windowService.ShowAIChat(provider?.ToString()?.ToLowerInvariant() ?? "copilot"));
         LoadApiKeyStatus();
     }
     ```
   - ViewModel'in `OpenChatCommand` komutu doğrudan `IWindowService.ShowAIChat` metoduna bağlanmıştır (Satır 22).
   - `LoadApiKeyStatus()` metodu (`MultiSych.Desktop/ViewModels/AIOverviewViewModel.cs:35-45`), `_config.AI` altındaki `CopilotApiKey`, `GeminiApiKey` ve `YandexAiApiKey` değerlerini gerçek konfigürasyondan okuyup durum mesajını (`StatusMessage`) üretmektedir. Hiçbir yerinde mock veri veya TODO bulunmamaktadır.

2. **Bağlı olduğu servisin gerçek çalışırlığı:**
   - `MultiSych.Desktop/Services/WindowService.cs:31-43`:
     ```csharp
     public void ShowAIChat(string provider)
     {
         GLib.Functions.IdleAdd(0, () =>
         {
             if (MainWindow.Instance != null)
             {
                 var vm = new AIChatViewModel(provider, serviceProvider);
                 var dialog = new AIChatWindow(MainWindow.Instance, vm);
                 dialog.Present();
             }
             return false;
         });
     }
     ```
   - Servis metodu boş veya yer tutucu değildir; `AIChatViewModel` ve `AIChatWindow` penceresini GTK thread'inde (`IdleAdd`) oluşturup ekranda göstermektedir.
   - `MultiSych.Desktop/Views/AIChatWindow.cs:8-118`: Pencere GTK4/Adwaita bileşenleriyle (`TextView`, `Entry`, `Button`, `ScrolledWindow`) eksiksiz inşa edilmiştir.
   - `MultiSych.Services/Implementations/AIService.cs:145-198`: `IAIService` arka planda Gemini, OpenAI/Copilot ve Yandex GPT uç noktalarına gerçek HTTP REST çağrıları yapmaktadır.

3. **Mevcut UI / Nav durumu:**
   - `MultiSych.Desktop/Program.cs:322`: DI konteynerine `services.AddTransient<AIOverviewViewModel>();` ile eklenmiştir.
   - `MultiSych.Desktop/ViewModels/MainWindowViewModel.cs:58, 92, 259`: `AIPage` özelliği üzerinden DI'dan çözülmekte ve `"AI"` navigasyon id'sine yönlendirilmektedir.
   - `MultiSych.Desktop/Views/MainWindow.cs:114`: Kenar çubuğunda `AddNavigationRow("AI", "🧠 AI Genel Bakış");` satırı mevcuttur.
   - Eksiklik: `MultiSych.Desktop/Views/MainWindow.cs:248-325` içinde `currentVm is AIOverviewViewModel` kontrolü ve ona ait bir `AIOverviewView.cs` widget'ı yoktur; seçildiğinde `ShowPlaceholder` fallback'ine düşmektedir.

### Öneri (nav'a bağlansın / ertelensin, sebebiyle)
- **Öneri: Nav'a bağlansın (veya kenar çubuğuyla entegre edilsin).**
- **Sebebi:** ViewModel mantığı, durum denetimi ve açtığı diyaloglar tamamen çalışmaktadır. Ancak `MainWindow.cs:136-148` satırları arasında sol kenar çubuğunda doğrudan "🤖 Microsoft Copilot", "✨ Google Gemini", "🧠 Yandex AI" modal açma butonları zaten mevcuttur. `AIOverviewView.cs` yazılarak kullanıcıya hangi yapay zeka anahtarlarının tanımlı olduğunu gösteren bir kart/genel bakış sayfası olarak `MainWindow.cs` switch'ine bağlanması uygundur.

---

## CalendarViewModel — Durum: YARIM

### Kanıt (dosya:satır alıntılarıyla)
1. **ViewModel içi komutlar ve servis bağlantısı:**
   - `MultiSych.Desktop/ViewModels/CalendarViewModel.cs:24-29`:
     ```csharp
     public CalendarViewModel(IServiceScopeFactory scopeFactory)
     {
         _scopeFactory = scopeFactory;
         ClearFilterCommand = new RelayCommand(_ => SelectedDate = null, _ => SelectedDate.HasValue);
         Task.Run(LoadEventsAsync);
     }
     ```
   - ViewModel'de yalnızca tek bir komut vardır: `ClearFilterCommand` (filtre temizleme).
   - ViewModel, projede mevcut olan `ICalendarService` arayüzünü **hiç enjekte etmemiştir**. Yalnızca `LocalCacheDbContext` üzerinden yerel veritabanındaki `CachedEvents` tablosunu sorgulamaktadır (`CalendarViewModel.cs:70-73`):
     ```csharp
     using var scope = _scopeFactory.CreateScope();
     var dbContext = scope.ServiceProvider.GetRequiredService<LocalCacheDbContext>();
     var events = await dbContext.CachedEvents.OrderByDescending(e => e.StartTime).ToListAsync();
     ```
   - ViewModel'de yeni etkinlik oluşturma (`CreateEvent`), etkinlik düzenleme (`UpdateEvent`), etkinlik silme (`DeleteEvent`) veya buluttan takvim verilerini eşitleme/yenileme (`SyncEvents`) komutlarının **hiçbiri yoktur**.

2. **Bağlı olduğu servisin gerçek çalışırlığı:**
   - Projede `MultiSych.Services/Implementations/CloudCalendarService.cs:21-147` mevcuttur ve Google/Microsoft/Yandex takvimleri için CRUD ve Senkronizasyon (`SyncEventsAsync`) işlemlerini tam olarak desteklemektedir (`ServiceCollectionExtensions.cs:43` ile Scoped kayıtlıdır).
   - Ancak `CalendarViewModel` bu servisi kullanmadığı için, yerel önbellek harici olarak (örn. CLI `sync-all` komutu `Program.cs:504`) tetiklenmedikçe takvimde hiçbir veri listelenemez ve kullanıcı arayüz üzerinden senkronizasyon tetikleyemez.

3. **Mevcut UI / Nav durumu:**
   - `MultiSych.Desktop/Program.cs:324`: `services.AddTransient<CalendarViewModel>();` ile DI'a eklenmiştir.
   - `MultiSych.Desktop/ViewModels/MainWindowViewModel.cs:55-66, 89-100, 254-268`: `MainWindowViewModel` içinde `CalendarViewModel` için **hiçbir property veya sayfa eşleşmesi tanımlanmamıştır**.
   - `MultiSych.Desktop/Views/MainWindow.cs:107-118`: Menüde "Calendar" için bir navigasyon öğesi **hiç bulunmamaktadır**.

### Öneri (nav'a bağlansın / ertelensin, sebebiyle)
- **Öneri: Ertelensin.**
- **Sebebi:** Bu özellik yalnızca nav'a bağlanarak çalışır hale gelemez. ViewModel henüz bir takvim yönetimi için gereken temel yeteneklere (etkinlik ekleme/silme/düzenleme, buluttan yenileme) sahip değildir; sadece yerel cache'i okuyan yarım bir prototiptir. `MainWindowViewModel` ve `MainWindow.cs`'de bile kaydı yoktur. FAZ 2 kapsamında `Program.cs`'deki DI kaydı kaldırılmalı veya `tools/erisim-testi.sh` içindeki `ISTISNALAR` listesine eklenerek `docs/KARARLAR.md` dosyasına ertelenme gerekçesi kaydedilmelidir.

---

## DocumentAnalyzerViewModel — Durum: BİTMİŞ

### Kanıt (dosya:satır alıntılarıyla)
1. **ViewModel içi komutlar ve servis bağlantısı:**
   - `MultiSych.Desktop/ViewModels/DocumentAnalyzerViewModel.cs:27-35`:
     ```csharp
     public DocumentAnalyzerViewModel(IAIService aiService, IWindowService windowService)
     {
         _aiService = aiService;
         _windowService = windowService;
         AvailableProviders = new ObservableCollection<string> { "hybrid", "copilot", "gemini", "yandex" };
         SummarizeCommand = new RelayCommand(async _ => await SummarizeAsync(), _ => !string.IsNullOrWhiteSpace(DocumentContent) && !IsAnalyzing);
         AnalyzeEmailCommand = new RelayCommand(async _ => await AnalyzeEmailAsync(), _ => !string.IsNullOrWhiteSpace(EmailBody) && !IsAnalyzing);
         LoadFileCommand = new RelayCommand(async _ => await LoadFileAsync());
     }
     ```
   - ViewModel'in üç ana komutu (`SummarizeCommand`, `AnalyzeEmailCommand`, `LoadFileCommand`) gerçek ve somut servislere bağlanmıştır.

2. **Bağlı olduğu servisin gerçek çalışırlığı:**
   - `LoadFileCommand` (`DocumentAnalyzerViewModel.cs:121-137`): `_windowService.OpenFileDialogAsync` metodunu çağırır.
     - `MultiSych.Desktop/Services/WindowService.cs:67-81`: Metot `Gtk.FileDialog` kullanarak yerel dosya sisteminden seçim yapar ve seçilen dosya içeriği `File.ReadAllTextAsync` ile `DocumentContent` property'sine doldurulur.
   - `SummarizeCommand` (`DocumentAnalyzerViewModel.cs:140-155`): `_aiService.SummarizeDocumentAsync` metodunu çağırır.
     - `MultiSych.Services/Implementations/AIService.cs:139-143`: Metot metni alıp profesyonel özetleme promptu hazırlayarak `GetResponseAsync`'e iletir.
   - `AnalyzeEmailCommand` (`DocumentAnalyzerViewModel.cs:157-178`): `_aiService.AnalyzeEmailAsync` metodunu çağırır.
     - `MultiSych.Services/Implementations/AIService.cs:50-70`: Gönderen, konu ve gövdeyi ayrıştırarak özet, önem derecesi, aksiyon öğeleri ve ton analizi üreten gerçek AI servisini çalıştırır.
   - Her iki metodun bağlandığı `AIService.GetResponseAsync` (`AIService.cs:145-198`), yapılandırılmış AI anahtarlarıyla (Gemini, Copilot, Yandex) gerçek API'lerle konuşmaktadır.

3. **Mevcut UI / Nav durumu:**
   - `MultiSych.Desktop/Program.cs:323`: DI konteynerinde `services.AddTransient<DocumentAnalyzerViewModel>();` olarak kayıtlıdır.
   - `MultiSych.Desktop/ViewModels/MainWindowViewModel.cs:59, 93, 260`: `DocumentAnalyzerPage` olarak DI'dan alınır, `"Analyzer"` rotasına ve `NavigationItems` koleksiyonuna (Satır 280) eklenmiştir.
   - `MultiSych.Desktop/Views/MainWindow.cs:113`: Sol menüde `AddNavigationRow("Analyzer", "🔍 Belge Analizi");` navigasyon satırı mevcuttur.
   - `docs/smoke-checklist.md:37`: Smoke test dokümanında "Document Analyzer: Belge özeti ve e-posta analiz butonlarını dene" maddesi yer almaktadır.
   - Eksik olan tek şey: `MultiSych.Desktop/Views/MainWindow.cs:248-325` içinde `currentVm is DocumentAnalyzerViewModel` dalının olmaması ve GTK `DocumentAnalyzerView.cs` dosyasının bulunmamasıdır.

### Öneri (nav'a bağlansın / ertelensin, sebebiyle)
- **Öneri: Nav'a bağlansın.**
- **Sebebi:** ViewModel ve servis katmanı %100 bitmiş ve işlevseldir. Smoke test kontrol listesinde dahi kullanıcı testi olarak tanımlıdır. Codex'e iş paketi verilerek GTK tabanlı `DocumentAnalyzerView.cs` yazılmalı (dosya yükleme, metin alanı, sağlayıcı seçici ve analiz sonuç paneli) ve `MainWindow.cs` sayfa switch'ine bağlanmalıdır.

---

## DocumentsViewModel — Durum: BİTMİŞ

### Kanıt (dosya:satır alıntılarıyla)
1. **ViewModel içi komutlar ve servis bağlantısı:**
   - `MultiSych.Desktop/ViewModels/DocumentsViewModel.cs:53-1251`:
     - 1253 satırdan oluşan devasa, tam teşekküllü bir ViewModel'dir.
     - DI Enjeksiyonları (Satır 309-327): `IAccountStore`, `IStorageService`, `IAIService`, `IWindowService`, `IAppStatusService`, `IDbContextFactory<LocalCacheDbContext>`, `MultiSychConfig`, `ICalendarService`.
   - Komutlar ve Bağlantılar:
     - `RefreshCommand` (`Satır 329, 450-472`): `_storageService.SyncStorageAsync` ile bulut eşitlemesi yapar ve `LoadDocumentsAsync` ile yerel cache'i tazeler.
     - `DownloadFileCommand` (`Satır 331, 815-843`): `_windowService.SaveFileDialogAsync` ile yol alıp `_storageService.DownloadFileAsync` ile dosyayı diske kaydeder.
     - `DeleteFileCommand` (`Satır 332, 845-884`): `_windowService.ShowConfirmationDialogAsync` ile onay alıp `_storageService.DeleteFileAsync` ile buluttan ve yerel EF Core tablosundan siler.
     - `SummarizeDocumentCommand` (`Satır 333, 959-996`): Belge metnini çıkarıp `_aiService.SummarizeDocumentAsync` ile özetler.
     - `SendChatMessageCommand` (`Satır 335, 1104-1173`): Belge içeriğini alarak `_aiService.GetResponseAsync` ile RAG tabanlı doküman sohbeti yürütür ve mesajları `DocumentChatMessages` veritabanı tablosuna kaydeder.
     - `ConfirmCreateCommand` (`Satır 339, 1182-1250`): Base64 şablonlarından temiz Word (.docx) veya Excel (.xlsx) oluşturup `_storageService.UploadFileAsync` ile buluta yükler.
     - `EditLocallyCommand` (`Satır 340, 510-584`): Dosyayı `~/MultiSych_Drives/` dizinine indirip Linux'ta `xdg-open` ile yerel düzenleyicide açar.
     - `CreateEmailDraftCommand` (`Satır 341, 586-657`): Belgeden AI ile e-posta taslağı üretip `_windowService.ShowNewEmailDialog` penceresine aktarır.
     - `ExtractEventsCommand` & `AddEventSuggestionCommand` (`Satır 342-343, 659-742`): Belgeden takvim etkinliklerini AI ile çıkarır ve `_calendarService.CreateEventAsync` ile takvime ekler.
     - `SaveDocumentTextCommand` (`Satır 345, 774-813`): Düz metin belgelerindeki inline düzenlemeleri diske yazar.

2. **Bağlı olduğu servisin gerçek çalışırlığı:**
   - `MultiSych.Services/Implementations/CloudStorageService.cs`: Google Drive, OneDrive ve Yandex Disk için dosya listeleme, indirme, silme ve yükleme metotları tam ve aktiftir. *(Not: `CloudStorageService.SearchFilesAsync` metodunda bilinen bir `NotImplementedException` vardır; ancak `DocumentsViewModel` arama işlemlerini bulut servisi üzerinden değil, `LocalCacheDbContext.CloudFiles` tablosu üzerinde LINQ sorgusuyla yapmaktadır (`DocumentsViewModel.cs:394-433`). Dolayısıyla bu eksiklikten kesinlikle etkilenmez).*
   - `MultiSych.Services/Implementations/AIService.cs`: Multimodal metin çıkarma, özetleme ve sohbet metotlarının tamamı aktiftir.
   - `MultiSych.Desktop/Services/WindowService.cs`: Kaydetme diyalogu, onay penceresi ve e-posta diyalogları aktiftir.

3. **Mevcut UI / Nav durumu:**
   - `MultiSych.Desktop/Program.cs:330`: `services.AddTransient<DocumentsViewModel>();` ile DI'a kayıtlıdır.
   - `MultiSych.Desktop/ViewModels/MainWindowViewModel.cs:65, 99, 266, 283`: `DocumentsPage` olarak DI'dan alınır ve `"Documents"` rotasına bağlanmıştır.
   - `MultiSych.Desktop/Views/MainWindow.cs:112`: Kenar çubuğunda `AddNavigationRow("Documents", "📄 Belgeler");` menü öğesi mevcuttur.
   - Eksik olan tek şey: `MultiSych.Desktop/Views/MainWindow.cs:248-325` içinde `currentVm is DocumentsViewModel` kontrolü ve ona ait bir `DocumentsView.cs` bileşeni olmamasıdır.

### Öneri (nav'a bağlansın / ertelensin, sebebiyle)
- **Öneri: Nav'a bağlansın.**
- **Sebebi:** Uygulamanın en gelişmiş, en zengin özellik setine sahip ViewModel'idir. AI destekli belge sohbeti, yerel düzenleme, özetleme, e-posta taslağı ve takvim çıkarma gibi özelliklerin tüm altyapısı eksiksiz hazırdır. Codex'e iş paketi açılarak `DocumentsView.cs` GTK arayüzü yazılmalı ve `MainWindow.cs` içerisindeki `UpdateActiveView` switch'ine bağlanmalıdır.

---

## ErrorReportViewModel — Durum: YARIM

### Kanıt (dosya:satır alıntılarıyla)
1. **ViewModel içi komutlar ve servis bağlantısı:**
   - `MultiSych.Desktop/ViewModels/ErrorReportViewModel.cs:8-50`:
     - Sınıf toplamda yalnızca 51 satırdır.
     - Yapıcı metot parametresizdir: `public ErrorReportViewModel()` (Satır 13). DI üzerinden hiçbir servis almaz.
     - Komut: Yalnızca tek bir komut tanımlıdır:
       ```csharp
       SubmitGitHubIssueCommand = new RelayCommand(_ => SubmitToGitHub(), _ => !string.IsNullOrWhiteSpace(IssueTitle));
       ```
       (Satır 15).
   - `SubmitToGitHub()` metodu incelendiğinde (`ErrorReportViewModel.cs:32-49`):
     ```csharp
     var body = $"**Açıklama / Description:**\n{IssueDescription}\n\n**Uygulama Bilgileri:**\nMultiSych Desktop v1.0\nOS: {RuntimeInformation.OSDescription}";
     var url = $"https://github.com/yourusername/MultiSych/issues/new?title={Uri.EscapeDataString(IssueTitle)}&body={Uri.EscapeDataString(body)}";
     ```
     (Satır 35-36).
     - URL içindeki `yourusername` ifadesi **sahte/yer tutucu (placeholder)** olarak sabit bırakılmıştır.
     - Metot yalnızca işletim sisteminin tarayıcısını (`xdg-open` vb.) bu sahte bağlantıyla açmaya çalışır.

2. **Bağlı olduğu servisin gerçek çalışırlığı:**
   - ViewModel hiçbir servise bağlı değildir.
   - Projede `IErrorReporter` arayüzü ve `ErrorReportService` (`MultiSych.Services/Implementations/ErrorReportService.cs:10-120`) mevcuttur. Bu servis raporları şifreli/güvenli izinlerle dosya sistemine (`AppData/MultiSych/Reports/`) yazabilmektedir. Ancak `ErrorReportViewModel` bu servisi hiç kullanmamaktadır.
   - ViewModel içinde log dosyalarını okuma, listeleme veya kullanıcıya sunma yeteneği **kesinlikle bulunmamaktadır**.

3. **Mevcut UI / Nav durumu ve çelişkisi:**
   - `MultiSych.Desktop/Views/MainWindow.cs:116`: Sol menüde bu sayfa kullanıcıya `"📋 Loglar"` ("Logs") etiketiyle sunulmaktadır.
   - `MultiSych.Desktop/ViewModels/MainWindowViewModel.cs:262`: `"Logs" => ErrorReportPage`.
   - Kullanıcı arayüzde "Loglar" sekmesine tıkladığında sistem/hata loglarını incelemeyi beklerken, ViewModel içi boş, sahte GitHub URL'li bir Issue bildirim formundan ibarettir.

### Öneri (nav'a bağlansın / ertelensin, sebebiyle)
- **Öneri: Ertelensin.**
- **Sebebi:** Özellik tamamlanmamış bir taslaktır. Sahte GitHub repo linki barındırmakta, projedeki gerçek `ErrorReportService` ile konuşmamakta ve menüde vadedilen "Loglar" işlevini yerine getirmemektedir. Mevcut haliyle nav'a bağlanması kullanıcıya kırık/anlamsız bir ekran sunar. FAZ 2 kapsamında `tools/erisim-testi.sh` dosyasındaki `ISTISNALAR` listesine eklenmeli, DI kaydı geçici olarak kaldırılmalı ve ileride gerçek bir Log Görüntüleyici / Hata Raporlama ekranı olarak tasarlanmak üzere ertelenmelidir (`docs/KARARLAR.md`'e not düşülmelidir).

