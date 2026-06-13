# MultiSych - Evrensel Senkronizasyon ve Yapay Zeka Asistanı 🚀

MultiSych, Google, Microsoft ve Yandex bulut hesaplarınızı tek bir merkezden yönetmenizi sağlayan, yapay zeka (AI) destekli, %100 açık kaynaklı ve uçtan uca şifreli, çapraz platform (Windows/Linux/macOS) bir masaüstü asistanıdır.

Arayüzünden arka plan görevlerine, yerel ses işlemeden bulut senkronizasyonuna kadar .NET 8 teknolojisinin sınırlarını zorlayan MultiSych ile veri yönetimini yepyeni bir seviyeye taşıyın!

---

## 🌟 Benzersiz Özellikler

### 1. Bulut Ekosistemi Entegrasyonu
Tüm bulut dosyalarınızı, e-postalarınızı ve takvim etkinliklerinizi tek bir çatı altında toplayın:
- **Google:** Gmail, Google Takvim, Google Drive
- **Microsoft:** Outlook, Office 365, OneDrive
- **Yandex:** Yandex Mail, Yandex Takvim, Yandex Disk
- **Sanal Sürücü (Virtual Drive):** Bulut depolama alanlarınızı işletim sisteminize yerel bir disk gibi bağlayın (Mount).

### 2. Çevrimdışı Çalışma ve Şifreli Yerel Hafıza (Cache)
İnternet bağlantınız kopsa bile verilerinize erişin.
- Tüm e-postalarınız, takvimleriniz ve dosya metadatalarınız arka planda otomatik olarak yerel bir **SQLite** veritabanına kaydedilir.
- **Üst Düzey Güvenlik:** Veritabanınız, `SQLCipher` ve `AES-256` şifrelemesi kullanılarak tamamen kilitlenir. Bilgisayarınız çalınsa dahi verileriniz ve Token'larınız güvende kalır.

### 3. Yapay Zeka Asistanları (AI Hybrid)
En güçlü yapay zeka modelleri MultiSych'in merkezinde!
- **Desteklenen Modeller:** Microsoft Copilot, Google Gemini, Yandex AI.
- **Belge & E-Posta Analizi:** Karmaşık ve uzun e-postalarınızı tek tuşla özetletin.
- **Akıllı Takvim:** Gelen e-postaları okuyup yapay zekanın "Bu mail bir toplantı bildirimi, takvime ekleyeyim mi?" demesinin keyfini çıkarın.

### 4. İnternetsiz Ses İşleme (Whisper AI)
Gizliliğinize önem veriyoruz! Sesli komut veya dikte özelliği kullanmak istediğinizde ses kaydınız hiçbir bulut sunucusuna gönderilmez. `Whisper.net` entegrasyonu sayesinde konuşmalarınız **tamamen çevrimdışı (offline)** olarak bilgisayarınızın içinde metne dönüştürülür.

### 5. Modern ve Akıcı Arayüz (Avalonia UI)
- **Çapraz Platform:** Avalonia UI altyapısı ile Windows, Linux ve macOS üzerinde aynı şık arayüz deneyimi.
- **Temalar:** Sade (Light), Modern (Dark) ve Retro temaları arasında çalışma zamanında kesintisiz geçiş.

---

## 🛠️ Kurulum Rehberi

MultiSych, **.NET 8.0 SDK** gerektirir. Sisteminizde yüklü değilse, öncelikle Microsoft'un resmi sayfasından işletim sisteminize uygun SDK'yı indirin.

### 🐧 Linux (Ubuntu, Arch, Manjaro) Kurulumu

Avalonia arayüzünün Linux üzerinde kusursuz çizilebilmesi için sisteminizde bazı temel grafik kütüphanelerinin olması gerekir.

1. **Gerekli Kütüphaneleri Yükleyin:**
   ```bash
   # Ubuntu/Debian tabanlı sistemler için:
   sudo apt update
   sudo apt install -y libx11-dev libxext-dev libxcomposite-dev libxcursor-dev libxdamage-dev libxrandr-dev libxtst-dev

   # Arch/Manjaro tabanlı sistemler için:
   sudo pacman -S gtk3 libxss nss atk at-spi2-core cairo gdk-pixbuf2 glib2 pango libx11 libxcomposite libxcursor libxdamage libxext libxfixes libxi libxrandr libxrender libxtst
   ```

2. **Projeyi Klonlayın ve Derleyin:**
   ```bash
   git clone https://github.com/yourusername/MultiSych.git
   cd MultiSych
   dotnet restore MultiSych.slnx
   dotnet build MultiSych.slnx
   ```

3. **Veritabanını Oluşturun (Migrations):**
   ```bash
   dotnet tool install --global dotnet-ef
   dotnet ef database update --project MultiSych.Services/MultiSych.Services.csproj --startup-project MultiSych.Desktop/MultiSych.Desktop.csproj
   ```

### 🪟 Windows Kurulumu
Windows kullanıcıları için ekstra bir kütüphane kurulumuna gerek yoktur. Terminalinizi veya PowerShell'i açıp şu komutları sırasıyla çalıştırın:

```bash
git clone https://github.com/yourusername/MultiSych.git
cd MultiSych
dotnet restore MultiSych.slnx
dotnet build MultiSych.slnx
dotnet ef database update --project MultiSych.Services/MultiSych.Services.csproj --startup-project MultiSych.Desktop/MultiSych.Desktop.csproj
```

---

## 🚀 Kullanım Rehberi

### Uygulamayı Başlatma
Projeyi derledikten sonra, masaüstü arayüzünü çalıştırmak için şu komutu kullanın:
```bash
dotnet run --project MultiSych.Desktop/MultiSych.Desktop.csproj
```

### 1. Bulut Hesaplarını Bağlama (OAuth2)
MultiSych, hesapları bağlamak için güvenli bir CLI (Komut Satırı) altyapısı kullanır. Uygulama **kapalıyken** terminalinize bağlanmak istediğiniz hesaba göre şu komutlardan birini yazın:

- **Google Hesabı:** `dotnet run --project MultiSych.Desktop/MultiSych.Desktop.csproj auth-google`
- **Microsoft Hesabı:** `dotnet run --project MultiSych.Desktop/MultiSych.Desktop.csproj auth-microsoft`
- **Yandex Hesabı:** `dotnet run --project MultiSych.Desktop/MultiSych.Desktop.csproj auth-yandex`

Tarayıcınız açılacak, güvenli giriş yaptıktan sonra onay verdiğinizde Token'larınız sistemin şifreli veritabanına kaydedilecektir.

### 2. AI Anahtarlarını Ayarlama (API Keys)
Uygulamayı normal bir şekilde başlatın (`dotnet run`).
1. Sol menüden **Settings (Ayarlar ⚙️)** sekmesine tıklayın.
2. **AI Provider Settings** kısmına sahip olduğunuz Copilot, Gemini veya Yandex AI API anahtarlarını yapıştırın.
3. **Save API Keys** butonuna tıklayarak işlemi kaydedin.

### 3. Arayüz Bölümleri ve Görevleri

*   **Dashboard (Genel Bakış):** Uygulamanın merkezidir. Bağlı hesaplarınızın, takılı sanal sürücülerin ve arka plan senkronizasyonlarının sağlıklı bir şekilde çalışıp çalışmadığını gösterir.
*   **Accounts (Hesaplar):** Sisteme CLI ile eklediğiniz tüm bulut hesaplarını burada listelenmiş olarak görürsünüz. Hesaplara tıklayarak **Mount (Sanal Sürücüye Bağla)** seçeneğiyle Google Drive veya OneDrive'ı yerel bir disk gibi (Örn: `Z:`) bilgisayarınıza bağlayabilirsiniz.
*   **Sync (Senkronizasyon):** Dünyanın en iyi özelliklerinden biri burada gizlidir! Arka planda 15 dakikada bir çalışan sistemi beklemeden tüm e-posta ve dosyalarınızı manuel olarak senkronize edebilirsiniz. Ek olarak **"Analyze Emails for Events"** butonuna basarak, gelen maillerinizi yapay zekaya okutabilir ve size toplantı takvimi önermesini sağlayabilirsiniz.
*   **AI (Yapay Zeka):** API key'ini kaydettiğiniz Gemini veya Copilot'a tıklayarak harika sohbet ekranına geçiş yapın. İsterseniz yazarak, isterseniz **Mikrofon 🎤** butonuna basıp konuşarak sohbet edebilirsiniz (Whisper.net ile sesiniz çevrimdışı analiz edilir).
*   **Analyzer (Belge Okuyucu):** Uzun makaleleri veya e-postaları kopyalayıp buraya yapıştırın. İstediğiniz yapay zekayı seçip anında özetini çıkartın.

---

## 🛡️ Gizlilik ve Güvenlik Bildirimi

MultiSych geliştirilirken "Kullanıcı Gizliliği" en ön planda tutulmuştur:
1. Uygulama kaynak kodları tamamen açıktır.
2. Toplanan e-postalarınız, dosya içerikleriniz veya takvimleriniz asla geliştirici sunucularına veya üçüncü şahıs takip uygulamalarına aktarılmaz. Sadece yetki verdiğiniz Google/Microsoft sunucuları ile sizin bilgisayarınız arasında dolaşır.
3. İnternete kapalı ses (Whisper) modülü sayesinde mikrofon verileriniz buluta taşınmaz, makinenizde kalır.

---

## 👩‍💻 Geliştirici ve Katkıda Bulunma

MultiSych, sıfırdan .NET 8 standartlarına, MVVM tasarım desenine, Dependency Injection ve Clean Code prensiplerine tamamen sadık kalınarak, hiçbir detayı atlanmadan geliştirilmiş kusursuz bir mimaridir.

Projeyi Fork edebilir, yeni özellikler için Pull Request gönderebilir ve gelişim sürecinin bir parçası olabilirsiniz!

**Geliştirme Ortamı:** C# 12, .NET 8, Avalonia UI, Entity Framework Core, SQLite (SQLCipher), MailKit, NAudio, Whisper.net.
