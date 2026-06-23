# MultiSych'e Katkı Rehberi

MultiSych'e katkı sağlayacağınız için çok teşekkür ederiz! Bu rehber, kodunuzu projeye entegre etme sürecini açıklar.

---

## 📋 Başlamadan Önce

### Gereksinimler

- **.NET 10.0 SDK** veya daha yeni
- **Git** versiyon kontrol sistemi
- **Visual Studio Code** veya **Visual Studio 2022**
- Temel C# bilgisi
- Türkçe kod açıklamaları (tercih)

### Projeyi Hazırlama

```bash
# 1. Repository'yi fork edin (GitHub Web UI)

# 2. Kodunuzu klonlayın
git clone https://github.com/YOUR-USERNAME/MultiSych.git
cd MultiSych

# 3. Upstream ekleyin
git remote add upstream https://github.com/ORIGINAL-OWNER/MultiSych.git

# 4. Bağımlılıkları yükleyin
dotnet restore

# 5. Build test edin
dotnet build
```

---

## 🔄 Geliştirme Akışı

### 1. Issue Seçme

- [Issues](https://github.com/ORIGINAL-OWNER/MultiSych/issues) sayfasında açık task'ları tarayın
- `good first issue` etiketli başlangıç görevleri arayın
- Issue'ye yorum yaparak üzerinde çalıştığınızı belirtin

### 2. Feature Branch Oluşturma

```bash
# En son upstream'i çekin
git fetch upstream
git rebase upstream/main

# Feature branch oluşturun
git checkout -b feature/your-feature-name
# veya
git checkout -b fix/bug-fix-name
```

### 3. Kod Yazma

```bash
# Geliştirme sırasında değişiklikleri izleyin
git status

# Yeni dosya/değişiklikleri stage'leyin
git add MultiSych.Services/Implementations/YourService.cs
git add MultiSych.Tests/YourServiceTests.cs
```

### 4. Test Yazma (ZORUNLU)

```bash
# Her özellik için unit test yazın
cd MultiSych.Tests

# Test dosyası oluşturun
cat > YourFeatureTests.cs << 'EOF'
using Xunit;
using MultiSych.Services.Implementations;

namespace MultiSych.Tests;

public class YourFeatureTests
{
    [Fact]
    public void YourFeature_ShouldWorkCorrectly()
    {
        // Arrange
        var service = new YourService();
        
        // Act
        var result = service.DoSomething();
        
        // Assert
        Assert.NotNull(result);
    }
}
EOF

# Testleri çalıştırın
dotnet test
```

### 5. Commit Yapma

```bash
# İyi bir commit mesajı yazın
git commit -m "feat: Add Yandex OAuth2 integration

- Implement IOAuthService for Yandex
- Add YandexAuthenticationService
- Support mail, disk, calendar scopes
- Add .env configuration template"
```

**Commit İçeriği Kuralları:**
- `feat:` Yeni özellik
- `fix:` Hata düzeltmesi
- `refactor:` Kod iyileştirmesi
- `test:` Test ekleme
- `docs:` Dokümantasyon
- `chore:` Build, dependencies, vb.

### 6. Push & Pull Request

```bash
# Değişikliklerinizi push edin
git push origin feature/your-feature-name

# GitHub'da Pull Request oluşturun
# 1. GitHub.com'da repo sayfanıza gidin
# 2. "Compare & pull request" butonunu tıklayın
# 3. Başlık ve açıklama yazın
# 4. Reviewers atayın
```

### Pull Request Şablonu

```markdown
## Açıklama
Bu PR ne yapar?

Closes #ISSUE_NUMBER

## Değişiklikler
- [ ] Özellik 1
- [ ] Özellik 2
- [ ] Test eklendi

## Test Edildi
- [ ] Yerel ortamda test ettim
- [ ] Tüm testler geçti
- [ ] Döküman güncelledim

## Screenshots (opsiyonel)
UI değişikliği varsa ekleyin
```

---

## 💻 Kod Yazma Kuralları

### C# Stil Rehberi

```csharp
// 1. Naming Conventions
public class UserAuthenticationService { }     // PascalCase
public string userName { get; set; }           // camelCase
private int _maxRetries = 3;                   // _camelCase

// 2. Async/Await
public async Task<OAuthToken> GetTokenAsync(string code)
{
    using var client = _httpClientFactory.CreateClient();
    // İşlem
    return token;
}

// 3. Error Handling
try
{
    // İşlem
}
catch (HttpRequestException ex)
{
    _logger.LogError(ex, "HTTP request failed");
    throw;
}

// 4. Dependency Injection
public YourService(
    IHttpClientFactory httpClientFactory,
    ILogger<YourService> logger,
    IConfigurationService configService)
{
    _httpClientFactory = httpClientFactory;
    _logger = logger;
}

// 5. XML Documentation
/// <summary>
/// Gets OAuth token from Yandex
/// </summary>
/// <param name="authorizationCode">Authorization code from OAuth flow</param>
/// <returns>OAuthToken with access and refresh tokens</returns>
public async Task<OAuthToken> GetTokenAsync(string authorizationCode)
{
}
```

### Dosya Organizasyonu

```
MultiSych.Services/
├─ Interfaces/
│  └─ IYourService.cs                    ← Interface ilk
├─ Implementations/
│  └─ YourService.cs                     ← Implementation
├─ Models/
│  └─ YourDataModel.cs                   ← Veri modelleri
└─ Data/
   └─ Migrations/                        ← EF Core migrations
```

### Türkçe Dokümantasyon

```csharp
/// <summary>
/// Yandex OAuth2 servisini sağlar
/// </summary>
public class YandexAuthenticationService : IOAuthService
{
    /// <summary>
    /// Authorization URL'si oluşturur
    /// </summary>
    /// <param name="state">CSRF koruma için state parametresi</param>
    /// <returns>Yandex OAuth login URL'si</returns>
    public string GetAuthorizationUrl(string state = "")
    {
        // Uygulama
    }
}
```

---

## 🧪 Test Yazma Kuralları

### AAA Pattern (Arrange-Act-Assert)

```csharp
[Fact]
public async Task SaveSettingAsync_WithValidKey_ShouldSaveSuccessfully()
{
    // Arrange - Hazırlık
    var service = new ConfigurationService();
    var key = "TEST_KEY";
    var value = "test_value";

    // Act - Eylem
    await service.SaveSettingAsync(key, value);

    // Assert - Kontrol
    var retrieved = service.GetString(key);
    Assert.Equal(value, retrieved);
}
```

### Test Dosya Adlandırma

```
YourServiceTests.cs             ← Test sınıfı
YourService_MethodName_ShouldBehavior()  ← Test method
```

### Mock Kullanımı (Moq)

```csharp
[Fact]
public async Task GetTokenAsync_CallsHttpClient()
{
    // Arrange
    var mockHttpClientFactory = new Mock<IHttpClientFactory>();
    var mockConfigService = new Mock<IConfigurationService>();
    var mockLogger = new Mock<ILogger<YandexAuthenticationService>>();

    var service = new YandexAuthenticationService(
        mockHttpClientFactory.Object,
        mockConfigService.Object,
        mockLogger.Object);

    // Act & Assert
    // ...
}
```

---

## 📚 Dokümantasyon

### Güncellenmesi Gereken Dosyalar

#### 1. README.md
Yeni özellik varsa:
```markdown
### Yeni Özellik
Açıklaması...

#### Kurulum
```bash
# Adımlar
```

#### 2. ARCHITECTURE.md
Sistem tasarımı değişirse:
```markdown
## Yeni Katman
Açıklaması ve diyagramı...
```

#### 3. Inline Comments
Karmaşık logik varsa:
```csharp
// Yandex API'si her token yenileşinde yeni refresh token dönüyor
// Bu nedenle her yenileme sonrası token'ı güncelliyoruz
var newToken = await _service.RefreshTokenAsync(currentToken);
```

---

## 🔍 Code Review Süreci

### Review İncelemesi Sırasında

1. **Functionality Check**
   - Kod istenen işi yapıyor mu?
   - Hata handling var mı?

2. **Code Quality**
   - Stil kurallarına uyuyor mu?
   - Testler yeterli mi?
   - Tekrarlayan kod (DRY) var mı?

3. **Security**
   - Sensitive data loglanıyor mu?
   - Input validation yapılıyor mu?
   - SQLi/XSS riski var mı?

4. **Performance**
   - Async/await kullanılıyor mu?
   - Veritabanı sorguları optimize mi?
   - Memory leak riski var mı?

### Geri Bildirim Yanıtlama

```bash
# Değişiklikleri yaptıktan sonra
git add .
git commit -m "refactor: Address review comments"
git push origin feature/your-feature

# GitHub'da "Resolve conversation" tıklayın
```

---

## 🚀 Merge Kriteriyonları

PR merge edilebilmesi için:

- ✅ Tüm testler geçmeli
- ✅ Code coverage %60+ olmalı
- ✅ En az 2 reviewer onaylamalı
- ✅ Conflict'ler çözülmeli
- ✅ CI/CD pipelines başarılı olmalı

---

## 🐛 Hata Bildirme

### Issue Başlığı
```
[BUG] Yandex OAuth2'de token refresh başarısız oluyor
[FEATURE] Çoklu dil desteği eklensin
[DOCS] Kurulum rehberi eksik
```

### Issue Açıklaması
```markdown
## Sorunu Açıkla
Kısa açıklama...

## Tekrar Etme Adımları
1. Adım 1
2. Adım 2

## Beklenen Davranış
Bunun olması gerekiyordu...

## Gerçek Davranış
Fakat bu oldu...

## Ortam
- .NET sürümü: 10.0
- OS: Linux Ubuntu
- Browser: Chrome
```

---

## 💬 İletişim

### Sorularınız Varsa

- GitHub Discussions - Genel sorular
- Discord - Gerçek zamanlı sohbet (bağlantı yakında)
- GitHub Issues - Bug bildirim & feature istekleri

### Community Kuralları

- Saygılı olun
- İyi niyetli soruları severiz
- Türkçe veya İngilizce yazabilirsiniz
- SPAM ve reklam yapılmaz

---

## 🎓 Learning Resources

### .NET & C#
- [Microsoft Learn](https://learn.microsoft.com/dotnet)
- [C# Documentation](https://learn.microsoft.com/dotnet/csharp)

### Git
- [Git Handbook](https://guides.github.com/introduction/git-handbook)
- [Interactive Git Learning](https://learngitbranching.js.org)

### Testing
- [xUnit Documentation](https://xunit.net)
- [Moq Documentation](https://github.com/moq/moq4/wiki/Quickstart)

### Avalonia
- [Avalonia Documentation](https://docs.avaloniaui.net)
- [Avalonia Samples](https://github.com/AvaloniaUI/Avalonia)

---

## 📝 Checklist Örneği

PR göndermeden önce:

```
- [ ] Branching doğru yapıldı (upstream/main'den)
- [ ] Test yazıldı ve geçti
- [ ] Kod style'a uygun
- [ ] Commit mesajları açıklayıcı
- [ ] Dokümantasyon güncellendi
- [ ] CHANGELOG güncellenecek
- [ ] PR açıklaması yazıldı
```

---

## 🎉 Kapanış

Katkılarınız bu projeyi daha iyi hale getiriyor! Sorularınız veya önerileriniz varsa lütfen GitHub Issues'de bize ulaşın.

**Keyifli kodlama! 👨‍💻**
