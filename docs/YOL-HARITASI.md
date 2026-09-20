# YOL HARİTASI — MultiSych GTK4 geçişini tamamlama

Yazan: Claude Code (baş mühendis) · 2026-09-20
Okuyacak: operatör (Murat Coşkun) + bu depoda çalışacak her ajan
(Claude Code, Codex, Antigravity)

Bu belge **ölçümle** yazıldı, tahminle değil. Her maddenin altında onu
doğrulayan komut var. Ekip kuralları için `docs/KURALLAR.md`, kapı
tanımları için `.kapilar.conf` + `tools/kapilar` bak — üçü birlikte
okunur, biri diğerini geçersiz kılmaz.

---

## 0. ŞU AN NEREDEYİZ — 2026-09-20 denetimi, ölçülmüş

### Bağlam

Proje Avalonia/XAML'den GTK4 + libadwaita'ya (GirCore) taşınıyor.
`ARCHITECTURE.md` diff'i bunu doğruluyor. Geçiş **kısmi** — altyapı
(DI, servis katmanı, veri katmanı) sağlam, sunum katmanında geride
kalmış parçalar var.

### Kapı durumu (2026-09-20 akşamı, FAZ 0 sonrası — `tools/kapilar` ile ölçüldü)

```
KAPI                DURUM
dotnet-sdk          ✅ yeşil
derleme             ✅ yeşil
test                ✅ yeşil
guvenlik-baslangic  🔴 desen tutmadı: 'kapı yeşil'
bildirim            🔴 desen tutmadı: 'kapı yeşil'
erisim              🔴 desen tutmadı: 'kapı yeşil'
eksiklik            ✅ yeşil
⛔ 7 kapının 3'ü KIRMIZI
```

FAZ 0 tamamlandı: .NET 10 SDK kuruldu, birinci-taraf paketler 9.0.0'dan
10.0.12'ye hizalandı, `global.json` + katkıcı SDK kontrolü eklendi
(ayrıntı: `docs/KARARLAR.md` K2-K3). Kalan 3 kırmızı FAZ 1-3'ün konusu.

Beş kırmızının dördü **negatif kontrolle doğrulanmış**, gerçek: kapı
yazılmadan önce eldeki bozukluk elle bulundu, kapı yazıldı, kırmızı
çıktığı görüldü (bu belgedeki sayılar `tools/kapilar` çıktısının
birebir kopyası). `eksiklik` kapısı bir **tavan** kapısıdır (mevcut 1
eksik implementasyonun üstüne çıkılmasın diye), yeşil olması "sorun
yok" değil "en azından kötüleşmedi" demek.

### Bulgular (kanıtlı, dosya + satır)

| # | Bulgu | Kanıt |
|---|---|---|
| 1 | Proje `net10.0` hedefliyor, makinede yalnız .NET 8 SDK kurulu | `dotnet build` → `NETSDK1045` |
| 2 | Başlangıç parola/2FA kontrolü GTK açılışına hiç bağlı değil | `Program.cs:148-149` kendi yorumuyla itiraf ediyor; `tools/guvenlik-baslangic-testi.sh` |
| 3 | `LoginViewModel`/`SetupViewModel`/`AuthViewModel` DI'da yok, hiçbir View'dan referans edilmiyor | `grep -rl` boş sonuç |
| 4 | Bildirimler no-op — asıl gönderim satırı yorumda | `WindowService.cs:111-112`; `tools/bildirim-testi.sh` |
| 5 | 5 ViewModel DI'da kayıtlı ama `MainWindow.cs` sayfa switch'inde yok: `AIOverviewViewModel`, `CalendarViewModel`, `DocumentAnalyzerViewModel`, `DocumentsViewModel`, `ErrorReportViewModel` | `tools/erisim-testi.sh` |
| 6 | `docs/smoke-checklist.md` "Document Analyzer" ekranını manuel test adımı sayıyor — ama madde 5 yüzünden o ekrana ulaşan bir nav yok | çapraz okuma, doğrudan çelişki |
| 7 | Çoklu dil desteği (`en-US.axaml`/`tr-TR.axaml`) silinmiş, yerine hiçbir i18n mekanizması konmamış; metinler C#'a Türkçe gömülü | `git log`'da silinen dosyalar + `grep` ile View'larda sabit string |
| 8 | `CloudStorageService.SearchFilesAsync` provider ayrımı olmadan doğrudan `NotImplementedException` | `CloudStorageService.cs:1152`; `tools/eksiklik-testi.sh` baseline=1 |
| 9 | `MainWindow.axaml.cs` — 0 byte, izlenmeyen, artık dosya | `ls -la` |
| 10 | `GirCore1007` uyarısı bilinçli bastırılmış, GirCore 0.8.0'ın kararlı subclass deseni bekleniyor | `.csproj` içi yorum, kendi kendine belgeli |

---

## 1. KİM NE YAPAR

| İş türü | Kim | Neden |
|---|---|---|
| Mimari karar, iş paketi, kapı yazımı/bakımı, kabul-red | **Claude Code** | Baş mühendis, kaldıraç burada |
| Nav bağlama, boilerplate GTK View kodu, i18n altyapı taşıması | **Codex** | Hacim işi, mekanik |
| Çapraz tutarlılık taraması (DI ↔ View, sabit string envanteri) | **Antigravity** | Okuma hacmi — ama headless güvenilmezliği nedeniyle küçük, dosya-bazlı paketler hâlinde (`docs/KURALLAR.md` §6) |
| Sudo gerektiren adımlar, commit/push, güvenlik kodu incelemesi | **Claude Code** (operatör onayıyla) | Geri alınabilirlik/yetki sınırı |
| Gerçek kullanım testi (parola ekranı, bildirim, nav) | **Operatör** | Tek yetkili — "çalışıyor" iddiasının son sınavı |

---

## FAZ 0 — Derleme engelini kaldır 🔴 EN KRİTİK, HER ŞEYİ BLOKE EDİYOR

**Sorun:** `.NET 10 SDK` kurulu değil, hiçbir kapı, hiçbir test
koşamıyor.

**Adım:**
1. `sudo dnf install dotnet-sdk-10.0` (10.0.111, Fedora `updates`
   deposunda hazır, 73.7 MB) — operatör onayıyla, **Claude Code
   çalıştırır**.
2. `dotnet --list-sdks` ile 10.x'in göründüğünü doğrula.
3. `dotnet build MultiSych.slnx` dene — `.slnx` formatının bu SDK
   sürümünde desteklendiğini doğrula (bu bir varsayım, ölçülmedi;
   desteklenmiyorsa `dotnet build` her `.csproj`'a ayrı ayrı veya
   `.sln` dönüştürmeyle çalıştırılır).
4. `scripts/verify.sh` uçtan uca koştur (restore→build→test→publish).

**Kabul ölçütü:** `tools/kapilar` çıktısında `derleme` ve `test`
kapıları yeşil.

**Kim:** Claude Code (sudo + doğrulama), operatör onayı gerekli.

---

## FAZ 1 — Güvenlik regresyonunu kapat 🔴 KRİTİK (sessiz güvenlik açığı)

**Sorun:** `RequireStartupPassword` / `EnableTwoFactorAuth` ayarları
okunuyor, hiç uygulanmıyor. Kullanıcı bu ayarları açık tutsa bile
korumasız.

**Adım:**
1. **Karar gerekiyor (Claude Code + operatör):** GTK4'te bir modal
   parola/2FA penceresi mi yazılacak, yoksa `Adw.PasswordEntryRow`
   kullanan basit bir `Adw.ApplicationWindow` mı? Ölçüt: mevcut
   `SecurityHelper` API'siyle uyumlu olmalı (zaten parola doğrulama
   mantığı `MultiSych.Services/Security` içinde var, yalnız arayüz
   eksik).
2. Karar `docs/KARARLAR.md`'e yazılır (K numarası alır).
3. `Program.cs` `OnActivate` içine, `MainWindow` açılmadan ÖNCE, bir
   güvenlik kapısı eklenir: `RequireStartupPassword` açıksa parola
   doğrulanmadan `mainWindow.Present()` çağrılmaz.
4. Ölü kod kararı: `LoginViewModel`/`SetupViewModel`/`AuthViewModel`
   ya bu yeni akışa bağlanır ya da "bu üçü kullanılmıyor, silinecek"
   diye `docs/KARARLAR.md`'e yazılıp kaldırılır — belirsiz hâlde
   bırakılmaz.

**Kabul ölçütü:** `tools/guvenlik-baslangic-testi.sh` yeşil **ve**
operatör gerçek uygulamada parolayı deneyerek doğrular (statik kapı
runtime davranışını garanti etmez, bu bilerek böyle — bkz. script içi
yorum).

**Kim:** Claude Code (karar + `Program.cs` bağlama), Codex (Login
penceresi View kodu, iş paketiyle).

---

## FAZ 2 — Erişilemeyen özellikleri bağla veya kapat

**Sorun:** 5 ViewModel (`AIOverviewViewModel`, `CalendarViewModel`,
`DocumentAnalyzerViewModel`, `DocumentsViewModel`,
`ErrorReportViewModel`) DI'da var, arayüzde yok.

**Adım (her biri için ayrı ayrı karar):**
1. Her ViewModel için: "bu özellik bitmiş mi, yalnız nav mı eksik" ya
   da "özelliğin kendisi de yarım mı" diye bak (ViewModel içeriğini
   oku).
2. Nav mı eksikse: Codex'e iş paketi — `MainWindow.cs` sayfa
   switch'ine bağlanacak View yazılır (mevcut `DashboardView.cs` gibi
   örneklerden desen alınır).
3. Özellik kendisi yarımsa: kapsam dışına al, DI kaydını kaldır,
   `docs/KARARLAR.md`'e "şu an ertelendi, sebep şu" diye yaz,
   `tools/erisim-testi.sh` içindeki `ISTISNALAR` listesine ekle.
4. `docs/smoke-checklist.md`'i güncel duruma göre düzelt (Document
   Analyzer adımı, karar 2'ye göre kaldırılır ya da nav eklenene kadar
   "bilinen eksik" notu düşülür).

**Kabul ölçütü:** `tools/erisim-testi.sh` yeşil (bağlanan + bilinçli
ertelenen toplamı = kayıtlı VM sayısı, hiçbiri "unutulmuş" durumda
kalmaz).

**Kim:** Claude Code (karar), Codex (View + nav kodu), Antigravity
(her ViewModel'in mevcut içeriğini okuyup "bitmiş mi yarım mı" ön
raporu — küçük, dosya-bazlı paket).

---

## FAZ 3 — Bildirimleri gerçek çalışır hale getir

**Sorun:** `ShowNotification` hiçbir şey yapmıyor.

**Adım:**
1. `app.SendNotification(null, notification)` çağrısının neden
   yorumda bırakıldığını araştır (GirCore API uyumsuzluğu mu, bilinçli
   mi — commit geçmişine bak).
2. Çalışan bir bildirim çağrısı yaz, gerekirse `Gio.Notification`
   API'sinin GirCore 0.8.0'daki doğru kullanım şeklini bul.
3. En az bir gerçek tetikleyiciyle (örn. senkronizasyon tamamlandığında)
   uçtan uca dene — masaüstünde bildirim gerçekten görünmeli.

**Kabul ölçütü:** `tools/bildirim-testi.sh` yeşil **ve** operatör
masaüstünde bildirimi gerçekten görür.

**Kim:** Codex (uygulama), Claude Code (kapı doğrulama).

---

## FAZ 4 — Çoklu dil desteğini geri getir

**Sorun:** `en-US.axaml`/`tr-TR.axaml` silindi, yerine hiçbir i18n
mekanizması konmadı; UI metinleri C# dosyalarına Türkçe gömülü.

**Adım:**
1. **Karar gerekiyor:** GTK dünyasının standardı `gettext`/`.po`
   dosyalarıdır ama bu proje zaten JSON/CSV tabanlı veri katmanı
   kullanıyor (`MultiSych.Services/Data`) — tutarlılık için basit bir
   JSON kaynak sözlüğü (`tr.json`/`en.json` + `Loc.Get(key)` yardımcı
   sınıfı) daha az bağımlılık getirebilir. Karar `docs/KARARLAR.md`'e
   yazılır.
2. Mevcut hardcoded string'lerin envanteri çıkarılır (Antigravity —
   dosya-bazlı okuma paketi, `grep` zaten bugünkü denetimde başlangıç
   sayılarını verdi: MainWindow 16, SettingsView 7, SyncView 3, vb.).
3. String'ler kaynak sözlüğe taşınır, View'lar `Loc.Get(...)` çağırır.
4. Yeni kapı yazılır: `tools/lokalizasyon-testi.sh` — View dosyalarında
   iki karakterden uzun, tırnak içi Türkçe/İngilizce sabit metin
   deseni ararsa **kırmızı** olur (negatif kontrolle sınanacak: bu
   kapı yazıldığında bugünkü hardcoded string'ler üstünde çalıştırılıp
   önce kırmızı çıktığı gösterilecek).

**Kabul ölçütü:** yeni kapı yeşil, uygulama hem `tr` hem `en` ile
açılıp gözle doğrulanır.

**Kim:** Claude Code (karar + kapı yazımı), Codex (string taşıma —
hacimli, mekanik), Antigravity (envanter raporu).

---

## FAZ 5 — Eksik implementasyonu kapat

**Sorun:** `CloudStorageService.SearchFilesAsync` koşulsuz
`NotImplementedException`.

**Adım:** Diğer `CloudStorageService` metodlarının provider-switch
desenini örnek alarak gerçek arama implementasyonu yaz (Google
Drive/OneDrive/Yandex Disk API'lerinin arama uç noktaları kullanılır).

**Kabul ölçütü:** `tools/eksiklik-testi.sh` BASELINE'ı 1'den 0'a
düşür, script yorumunu güncelle, kapı hâlâ yeşil.

**Kim:** Codex (iş paketiyle, mevcut provider desenlerine sadık
kalarak), Claude Code (kapı baseline güncellemesi + kabul).

---

## FAZ 6 — Küçük artıkları temizle

1. `MainWindow.axaml.cs` (0 byte, izlenmeyen) — silinir.
2. `docs/smoke-checklist.md` — FAZ 2 kararlarına göre güncellenir.
3. `GirCore1007` bastırması — **şimdilik dokunma.** GirCore
   sürüm notları her büyük iş paketinden önce bir kez kontrol edilir;
   stabil desen yayınlanırsa `docs/KARARLAR.md`'e not düşülüp
   bastırma kaldırılır.

**Kim:** Codex (dosya silme, checklist güncelleme), Claude Code
(GirCore takibi — düşük öncelik, ayrı zamanlama gerektirmez).

---

## FAZ 7 — Genel regresyon ve paketleme

1. `tools/kapilar` uçtan uca yeşil.
2. `scripts/verify.sh` yeşil (restore/build/test/publish).
3. `docs/smoke-checklist.md`'deki manuel adımlar operatör tarafından
   gerçek uygulamada koşulur (özellikle Güvenlik Checks bölümü —
   zaten checklist'te var, FAZ 1'den sonra gerçek anlam kazanacak).
4. `build-deb-package.sh` / `Dockerfile` ile paketleme denenir.

**Kabul ölçütü:** bu belgedeki her fazın kabul ölçütü tek seferde,
aynı commit üstünde, `tools/kapilar` ile yeşil.

---

## 2. SIRADAKİ TEK ADIM

**✅ FAZ 0 TAMAMLANDI (2026-09-20).** `.NET 10 SDK` kuruldu, birinci
taraf paketler hizalandı, kanıt `docs/KARARLAR.md` K2-K3'te.

**Sıradaki: FAZ 1 — güvenlik regresyonunu kapat.** K4 kararı (GTK4'te
parola/2FA akışının nasıl kurulacağı) verilmeden `guvenlik-baslangic`
kapısı yeşile dönemez.
