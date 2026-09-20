## İŞ PAKETİ — Başlangıç güvenlik kapısı (parola + 2FA) GTK4'e bağlanacak

**Yazan:** Claude Code · 2026-09-20 · **Uygulayan:** Codex
**Referans:** `docs/YOL-HARITASI.md` FAZ 1 · `docs/KURALLAR.md` ·
`~/Projelerim/KURALLAR.md` · `.kapilar.conf` kapısı: `guvenlik-baslangic`
**Karar:** `docs/KARARLAR.md` K4

### Neden

`MultiSych.Desktop/Program.cs:148-149` kendi yorumuyla itiraf ediyor:
*"Bu güvenlik kontrolleri şimdilik atlanıyor. İleride Avalonia UI
üzerinden yapılacaktır."* `SecuritySettings.RequireStartupPassword` ve
`EnableTwoFactorAuth` ayarları okunuyor ama GTK açılışında (`OnActivate`)
hiç kontrol edilmiyor — kullanıcı bu ayarları açık tutsa bile hiçbir
engel yok.

### Bulgu

- `MultiSych.Services/Security/SecurityHelper.cs:96-135` —
  `VerifyStartupSecurity(SecuritySettings)` zaten var, mantığı doğru
  ama `Console.ReadLine`/`Console.Write` kullanıyor — GTK açılışına
  bağlanamaz (konsol genelde yok).
- `MultiSych.Desktop/ViewModels/LoginViewModel.cs` zaten var ama iki
  hatası var: (1) 2FA'yı hiç sormuyor, (2) `storedPassword` boşsa
  (yanlış yapılandırma) **her parolayı kabul ediyor** — bu projenin
  yasakladığı sessiz geçiş hatası.
- `LoginViewModel`, `SetupViewModel`, `AuthViewModel` şu an DI'da kayıtlı
  değil, hiçbir View'dan referans edilmiyor. Bu iş paketi yalnız
  `LoginViewModel`'i düzeltip bağlar; `SetupViewModel`/`AuthViewModel`'e
  **dokunma** — onlar K5'te ayrı ele alınacak.

### Sözleşme (değiştirilmeyecek)

`SecuritySettings` sınıfının alan adları (`RequireStartupPassword`,
`EnableTwoFactorAuth`, `TwoFactorSecret`) ve `MULTISYCH_STARTUP_PASSWORD`
ortam değişkeni adı sabit kalacak — CLI (`--set-ai-key`, `setup-security`
komutu) ve `.env` akışı bunlara bağımlı, değiştirilirse onlar kırılır.

### İstenen (numaralı, somut)

1. **`SecurityHelper.cs`'e iki yeni PUBLIC saf fonksiyon ekle** (Console
   I/O YOK, yalnız girdi alıp `bool` dönerler):
   ```csharp
   public static bool ValidatePassword(SecuritySettings security, string enteredPassword)
   public static bool ValidateTwoFactorCode(SecuritySettings security, string enteredCode)
   ```
   `ValidatePassword`: `MULTISYCH_STARTUP_PASSWORD` ortam değişkenini
   okur; boşsa **`false` döner** (sessizce kabul etmez — mevcut
   `VerifyStartupSecurity`'nin 104-108. satırlarındaki "yapılandırılmamış"
   davranışıyla birebir aynı). `ValidateTwoFactorCode`: `TwoFactorSecret`
   boşsa `false`, doluysa mevcut `internal ValidateTotpCode`'u çağırır
   (aynı assembly içinde, erişim sorunu yok).
2. **`VerifyStartupSecurity`'i bu iki fonksiyonu çağıracak şekilde
   yeniden yaz** (CLI davranışı ve mesajları AYNEN korunacak, yalnız
   karşılaştırma mantığı tekilleşecek — kod tekrarı kalmayacak).
3. **`LoginViewModel.cs`'i düzelt:**
   - Constructor artık `SecuritySettings security` ve `Action<bool> callback`
     alsın (mevcut tek-parametre password-only mantığı kaldırılır).
   - `Password` property'sine ek olarak `TwoFactorCode` property'si ekle.
   - `RequiresTwoFactor` (bool, `security.EnableTwoFactorAuth`) — View bu
     alana bakıp 2FA kutusunu gösterip göstermeyeceğine karar verir.
   - `Login()`: `SecurityHelper.ValidatePassword` çağır (RequireStartupPassword
     kapalıysa bu adımı atla, otomatik geç); başarılıysa ve 2FA açıksa
     `SecurityHelper.ValidateTwoFactorCode` çağır; ikisi de geçerse
     `_callback(true)`; herhangi biri başarısızsa `ErrorMessage` set et,
     **hangi adımın** başarısız olduğunu söyle ("Hatalı parola" /
     "Hatalı 2FA kodu"), `_callback` ÇAĞIRMA (kullanıcı tekrar dener).
4. **Yeni `Views/SecurityGateWindow.cs`** — `AddAccountWindow.cs`
   deseninde (bkz. o dosya, `Gtk.Window`, `SetModal(true)`,
   `Gtk.Box`/`Gtk.Button` düzeni):
   - `Gtk.PasswordEntry` (parola, yalnız `RequireStartupPassword` açıksa
     görünür), 2FA açıksa ayrıca bir `Gtk.Entry` (6 haneli kod).
   - "Giriş" butonu → `LoginViewModel.LoginCommand.Execute(null)`.
   - Hata mesajı için bir `Gtk.Label` (`LoginViewModel.ErrorMessage`'a
     bağlı — `PropertyChanged` dinleyip `label.SetText(...)` yeter, tam
     bir binding altyapısı kurmaya gerek yok, diğer View'ların deseniyle
     tutarlı kalsın).
   - **Pencere kapatma düğmesi (X) güvenlik kapısını atlatmasın:**
     `OnCloseRequest` override edilip `true` döndürülerek native kapatma
     engellensin; yerine küçük bir "Çıkış" butonu konsun, o buton
     `Application.Quit()` çağırsın (kapıyı atlatıp MainWindow'a geçmeden
     temiz çıkış).
5. **`Program.cs` `OnActivate` bloğunu güncelle** (satır ~148-164):
   - "şimdilik atlanıyor" yorumunu SİL.
   - `security.RequireStartupPassword || security.EnableTwoFactorAuth`
     doğruysa: `SecurityGateWindow` göster, `callback(true)` geldiğinde
     mevcut `MainWindow` açma kodunu çalıştır (bir yerel fonksiyona
     çıkarılabilir, örn. `void OpenMainWindow()`); `callback` hiç
     gelmeden pencere kapanırsa (Çıkış butonu) uygulama zaten
     `Application.Quit()` ile kapanmış olacak.
   - İkisi de kapalıysa direkt `MainWindow` aç (mevcut davranış).

### Fail-loud kuralları

- `ValidatePassword`/`ValidateTwoFactorCode` hiçbir zaman "yapılandırma
  eksik" durumunda `true` dönmez — madde 1'de açıkça yazıldı.
- `SecurityGateWindow`'da parola/2FA alanı boş bırakılıp "Giriş"e
  basılırsa bu da bir "hatalı" denemedir, sessizce geçilmez, hata
  mesajı gösterilir.

### Kabul ölçütü

```
bash tools/guvenlik-baslangic-testi.sh
GUVENLIK-BASLANGIC-TESTI kapı yeşil
```
Ayrıca `bash tools/kapilar` çalıştırıldığında `erisim` ve `bildirim`
kapılarının durumu DEĞİŞMEMİŞ olmalı (bu iş paketi onları etkilemez,
hâlâ kırmızı olmaları beklenir, FAZ 2-3'ün konusu).

**Manuel doğrulama (Claude Code kabul öncesi bizzat yapacak):**
`.env`'de `MULTISYCH_STARTUP_PASSWORD` ayarlayıp uygulamayı gerçekten
açıp yanlış/doğru parola deneyerek.

### Sınırlar

- `dotnet build` 0 uyarı 0 hata kalacak.
- `SetupViewModel`, `AuthViewModel`'e dokunma (K5'in konusu).
- CLI `setup-security` komutunun ve `VerifyStartupSecurity`'nin dışa
  dönük mesajları/davranışı DEĞİŞMEYECEK (yalnız iç implementasyonu
  saf fonksiyonlara bölünecek).
- Yalnız Linux'ta derlenip test edilebiliyor; `#if WINDOWS` bloklarına
  dokunmuyorsun zaten, bu iş paketi onları etkilemiyor.

---

## Düzeltme günlüğü (varsa)

- **2026-09-20 · Codex:** Parola ve 2FA doğrulaması saf
  `SecurityHelper` fonksiyonlarına ayrıldı; CLI mesajları korunarak
  `VerifyStartupSecurity` bu fonksiyonlara bağlandı. `LoginViewModel`
  iki aşamalı ve fail-loud hale getirildi. Yeni GTK4
  `SecurityGateWindow`, native kapatmayı engelleyen/temiz çıkış sunan
  akışla `Program.OnActivate` içinde `MainWindow` önüne bağlandı.
