# MultiSych Mimarisi

## Genel Mimari

MultiSych, **3 katmanlı mimariye** (3-Tier Architecture) uyar:

```
┌─────────────────────────────────────────────────────────────┐
│                 Presentation Layer (UI)                      │
│              MultiSych.Desktop (Avalonia UI)                │
│          ViewModels, Views, Services (IWindowService)       │
└────────────────────────┬────────────────────────────────────┘
                         │
┌────────────────────────▼────────────────────────────────────┐
│                  Business Logic Layer                         │
│                 MultiSych.Services                           │
│  (Google, Microsoft, Yandex APIs, Sync, AI Integration)    │
└────────────────────────┬────────────────────────────────────┘
                         │
┌────────────────────────▼────────────────────────────────────┐
│                   Data Access Layer                          │
│              Entity Framework Core + SQLite                 │
│                  (LocalCacheDbContext)                       │
└─────────────────────────────────────────────────────────────┘
```

---

## Katman Detayları

### 1. Presentation Layer (UI)

**Proje:** `MultiSych.Desktop/`

#### Sorumluluğu
- Kullanıcı arayüzü gösterimi (XAML)
- Kullanıcı etkileşimlerini yönetme
- ViewModels aracılığı ile veri bağlama

#### Bileşenler

| Bileşen | Amaç | Teknoloji |
|---------|------|-----------|
| `Views/` | XAML UI tanımları | Avalonia |
| `ViewModels/` | Veri bağlama & komutlar | ReactiveUI |
| `Services/` | Masaüstü spesifik işler | Custom |

#### ViewModels Listesi

```
MainWindowViewModel          → Ana pencere durumu yönetimi
DashboardViewModel          → Dashboard özeti
AccountsViewModel           → Hesap listesi
AddAccountViewModel         → Yeni hesap ekleme
SyncViewModel              → Senkronizasyon durumu
FileExplorerViewModel      → Dosya tarayıcı
AIOverviewViewModel        → AI özeti & özet
DocumentAnalyzerViewModel  → Döküman analizi
SettingsViewModel          → Uygulama ayarları
ChatViewModel              → Sohbet arayüzü
AIChatViewModel            → AI sohbet
CalendarViewModel          → Takvim gösterimi
ErrorReportViewModel       → Hata raporlama
```

#### Veri Akışı (MVVM)

```
User Interaction
       │
       ▼
View (XAML)
       │
       ├─ Binding ─► ViewModel
       │
       ▼
Command Execution
       │
       ▼
ICommand.Execute() → Service Call
       │
       ▼
ObservableCollection Update
       │
       ▼
View Refresh
```

---

### 2. Business Logic Layer

**Proje:** `MultiSych.Services/`

#### Sorumluluğu
- İş mantığı uygulaması
- Bulut servisleri entegrasyonu (Google, Microsoft, Yandex)
- Veri senkronizasyonu
- AI servisleri
- Güvenlik & Şifreleme

#### Alt Katmanlar

##### A. OAuth2 & Authentication (`Interfaces/IOAuthService.cs`)

```
┌─────────────────────────────────────────────┐
│         IOAuthService (Interface)           │
├─────────────────────────────────────────────┤
│ + GetAuthorizationUrl(state)                │
│ + GetTokenAsync(authorizationCode)          │
│ + RefreshTokenAsync(currentToken)           │
│ + GetUserInfoAsync(token)                   │
│ + RevokeTokenAsync(token)                   │
└────────────┬──────────────┬─────────────┬──┘
             │              │             │
      ┌──────▼──┐    ┌──────▼──┐   ┌─────▼──────┐
      │ Google  │    │Microsoft│   │  Yandex    │
      │ OAuth   │    │  OAuth  │   │   OAuth    │
      └─────────┘    └─────────┘   └────────────┘
```

**Implements:**
- `GoogleAuthenticationService.cs`
- `MicrosoftAuthenticationService.cs`
- `YandexAuthenticationService.cs`

##### B. Cloud Services (`Interfaces/ICloudService.cs`)

Her bulut sağlayıcı kendi servisi uygular:

```
Google Services              Microsoft Services          Yandex Services
├─ Gmail                     ├─ Outlook                  ├─ Yandex Mail
├─ Google Calendar           ├─ Calendar                 ├─ Yandex Calendar
├─ Google Drive              ├─ OneDrive                 ├─ Yandex Disk
└─ Contacts                  └─ Contacts                 └─ Contacts
```

##### C. Sync Engine (`ISyncService`)

```
Sync Flow:
1. GetChanges() → Hangi veriler değişti?
2. TransformData() → Veriler uyumlu mu?
3. UploadChanges() → Buluta yükle
4. DownloadChanges() → Buluttan indir
5. MergeConflicts() → Çakışmaları çöz
6. UpdateLocalCache() → Yerel cache'i güncelle
```

##### D. AI Services (`IAIService`, `ISpeechService`)

```
AI Pipeline:
Input (Text/Audio)
       │
       ├─ Preprocessing
       ├─ Model Selection
       ├─ Inference
       ├─ Post-processing
       └─ Output (Summary/Response)
```

##### E. Storage & Encryption

```
Encryption Flow:
Plain Data
     │
     ├─ SQLCipher
     ├─ AES-256
     └─ SQLite Database
          │
          └─ Encrypted File
```

#### Services Dependency Graph

```
IHttpClientFactory (System)
         │
         └─ IOAuthService Implementations
                    │
                    ├─ CloudYandexService
                    ├─ GoogleCloudService
                    └─ MicrosoftCloudService
                    
IConfigurationService
         │
         └─ Multiple Services

LocalCacheDbContext (EF Core)
         │
         ├─ ISyncService
         ├─ IStorageService
         └─ IEmailService
```

---

### 3. Data Access Layer

**Proje:** `MultiSych.Services/` → `Data/`

#### Entity Framework Core

**DbContext:** `LocalCacheDbContext`

```csharp
DbSets:
├─ Accounts (User bulut hesapları)
├─ CachedEmails (E-posta cache'i)
├─ CloudFiles (Dosya meta verileri)
├─ CalendarEvents (Takvim etkinlikleri)
├─ Contacts (İletişim bilgileri)
└─ SyncStatus (Senkronizasyon durumu)
```

#### Database Schema

```
┌─────────────────────────────────────────┐
│            Accounts Table               │
├─────────────────────────────────────────┤
│ Id (PK)                                 │
│ Email                                   │
│ Provider (Google/Microsoft/Yandex)      │
│ AccessToken (Encrypted)                 │
│ RefreshToken (Encrypted)                │
│ ExpiresAt                               │
│ IsActive                                │
│ CreatedAt, UpdatedAt                    │
└─────────────────────────────────────────┘
```

#### Migrations

```
dotnet ef migrations list
────────────────────────
1. 20260621164556_InitialCreate
2. 20260621164612_AddIdToBaseEntity
```

---

## Dependency Injection Container

**Konfigürasyon:** `MultiSych.Desktop/Program.cs`

```csharp
// Lifetime Policies
AddSingleton  → Uygulamanın ömrü boyunca (1 instance)
AddScoped     → Http request başına (1 instance)
AddTransient  → Her istek (yeni instance her seferinde)
```

### Geçerli Registrations

```
Singleton:
├─ IConfigurationService
├─ IUserSettingsService
├─ ISecureStorageService
├─ IAppStatusService
├─ IAccountStore
├─ IAudioRecordingService (Whisper)
└─ IErrorReporter

Scoped:
├─ LocalCacheDbContext
└─ IStorageService

Transient:
├─ All ViewModels (12 adet)
├─ IOAuthService (YandexAuthenticationService)
├─ CloudYandexService
├─ IPlatformMountProvider
├─ IVirtualDriveService
├─ ISpeechService (Whisper)
└─ IAIService
```

---

## Veri Akışı Senaryoları

### Senaryo 1: E-posta Senkronizasyonu

```
[Sync Triggered]
     │
     ▼
[SyncViewModel.SyncCommand]
     │
     ▼
[ISyncService.SyncAllAsync()]
     │
     ├─▶ [For each account]
     │   ├─ Get OAuth Token
     │   ├─ Call Gmail API
     │   ├─ Download Emails
     │   ├─ Transform to EmailEntity
     │   └─ Save to SQLite
     │
     └─▶ [Update UI]
         └─ SyncViewModel.Messages
```

### Senaryo 2: Dosya Tarama

```
[User navigates to File Explorer]
     │
     ▼
[FileExplorerViewModel.LoadFilesCommand]
     │
     ├─ Determine Provider
     ├─ Get Auth Token
     ├─ Call Cloud API
     ├─ Cache Results
     └─ Bind to UI
```

### Senaryo 3: AI Özet

```
[User clicks Summarize Button]
     │
     ▼
[DocumentAnalyzerViewModel.SummarizeCommand]
     │
     ├─ Extract Text
     ├─ Call IAIService
     ├─ Get Summary
     ├─ Format Result
     └─ Show in UI
```

---

## Güvenlik Mimarisi

```
┌─────────────────────────────────────────────┐
│        Security Perimeter                    │
├─────────────────────────────────────────────┤
│                                              │
│  ┌──────────────────────────────────────┐  │
│  │  Application Memory                   │  │
│  │  (Token in RAM - potential risk)      │  │
│  └──────────────────────────────────────┘  │
│                   │                         │
│                   ▼                         │
│  ┌──────────────────────────────────────┐  │
│  │  Encrypted Storage (SQLCipher)       │  │
│  │  ├─ AccessToken (AES-256)            │  │
│  │  ├─ RefreshToken (AES-256)           │  │
│  │  ├─ Passwords (PBKDF2)               │  │
│  │  └─ Sensitive Data                   │  │
│  └──────────────────────────────────────┘  │
│                   │                         │
│                   ▼                         │
│  ┌──────────────────────────────────────┐  │
│  │  File System Permissions             │  │
│  │  (User-only access to db files)      │  │
│  └──────────────────────────────────────┘  │
│                                              │
└─────────────────────────────────────────────┘
```

---

## Performance Optimizations

### 1. Caching Strategy

```
L1 Cache:    Application Memory (In-Process)
                   │
                   ▼
L2 Cache:    SQLite Local Database
                   │
                   ▼
L3 Source:   Cloud APIs (Google, Microsoft, Yandex)
```

### 2. Async/Await Pattern

```
All I/O operations are asynchronous:
├─ Cloud API calls
├─ Database operations  
├─ File I/O
└─ Network operations
```

### 3. Pagination & Lazy Loading

```
FileExplorer:
├─ Load 50 items initially
├─ Load more on scroll
└─ Async download in background
```

---

## Testing Architecture

### Test Pyramid

```
        /\
       /  \  (2-3)  Integration Tests
      /────\
     /      \  (5-10)  Unit Tests
    /────────\
   /          \ (1)  E2E Tests (Manual)
```

### Unit Test Structure

```
MultiSych.Tests/
├─ ConfigurationServiceTests.cs
├─ OAuthServiceTests.cs (planned)
├─ CloudServiceTests.cs (planned)
└─ Mocks/
   └─ MockOAuthTokenProvider.cs
```

---

## Extension Points

### 1. Yeni Cloud Provider Ekleme

```csharp
// 1. Implement IOAuthService
public class NewProviderAuthService : IOAuthService { }

// 2. Implement ICloudService
public class CloudNewProviderService : ICloudService { }

// 3. Register in DI
services.AddTransient<IOAuthService, NewProviderAuthService>();
services.AddTransient<CloudNewProviderService>();

// 4. Add ViewModel
services.AddTransient<NewProviderViewModel>();
```

### 2. Yeni AI Model Ekleme

```csharp
public class CustomAIService : IAIService
{
    public async Task<string> SummarizeAsync(string text) { }
    public async Task<string> AnalyzeAsync(string content) { }
}
```

### 3. Yeni Sync Source Ekleme

```csharp
public class CustomSyncProvider : ISyncProvider
{
    public async Task<List<IEntity>> GetChangesAsync() { }
    public async Task ApplyChangesAsync(List<IEntity> entities) { }
}
```

---

## Deployment Architecture

```
┌──────────────────────────────┐
│   Windows Installer          │
│   (Squirrel.Windows)         │
└──────────────────────────────┘
           │
           ▼
┌──────────────────────────────┐
│   Desktop Application        │
│   MultiSych.Desktop.exe      │
└──────────────────────────────┘
           │
           ├─▶ Services DLL
           ├─▶ Config (.env)
           ├─▶ LocalDB
           └─▶ Encryption Keys
```

---

## Future Architecture Enhancements

1. **Microservices** - Backend API ayrılması
2. **Event Bus** - Services arası haberleşme
3. **Plugin System** - Üçüncü parti eklentileri
4. **Offline Sync Queue** - Çevrimdışı değişiklik alma
5. **Database Replication** - Multi-device sync

---

**Son Güncelleme:** 21 Haziran 2026
