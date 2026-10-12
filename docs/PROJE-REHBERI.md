# MultiSych — Proje Rehberi

Yazan: Claude Code (baş mühendis) · 2026-10-12
Okuyacak: operatör (Murat Coşkun) + bu depoda çalışacak her ajan

Bu belge, projenin **ne olduğunu, bugüne kadar ne yapıldığını, ne
kaldığını, hangi sıkıntıların yaşandığını ve ileride nelerle
karşılaşılabileceğini** tek bir yerde toplar. Kaynak: `docs/KARARLAR.md`
(K1-K26, her satırı ölçümle yazılmış karar günlüğü), `docs/YOL-HARITASI.md`
(güncel açık işler), `docs/KURALLAR.md` (ekip çalışma kuralları) ve bu
oturumların kendisi. Bir iddia gördüğünde kaynağını merak edersen
parantez içindeki K-numarasına `docs/KARARLAR.md`'den bakabilirsin —
orada kanıtıyla (komut çıktısı, dosya/satır) birlikte duruyor.

---

## 1. Proje nedir

MultiSych, Google/Microsoft/Yandex bulut hesaplarını (e-posta, takvim,
dosya depolama) tek bir masaüstü uygulamasından yönetmeyi hedefleyen,
Linux öncelikli (GTK4 + libadwaita) bir .NET 10 uygulaması. İddia
edilen temel özellikler (README'den):

- Üç sağlayıcının e-posta/takvim/dosyalarını tek arayüzden yönetme
- Bulut depolama alanlarını **gerçek bir sanal sürücü** gibi mount etme
- Çevrimdışı çalışma (SQLite + SQLCipher şifreli yerel önbellek)
- Yapay zeka asistanları (Copilot, Gemini, Yandex AI) ile sohbet/özetleme
- Çevrimdışı ses tanıma (Whisper.net) — mikrofon verisi hiç buluta gitmiyor

**Teknoloji yığını:** C# 13, .NET 10, GTK4/libadwaita (GirCore
bağlamaları), Entity Framework Core 10 + SQLite (SQLCipher ile
şifreli), MSAL.NET (Microsoft kimlik doğrulama), Google.Apis.* paket
ailesi, MailKit, NAudio, Whisper.net, LTRData.FuseDotNet (gerçek FUSE
mount). MVVM deseni, Dependency Injection, arka plan servisleri
(`BackgroundService`).

**Önemli — README'deki bazı iddialar henüz doğrulanmadı:** AI sohbet
ekranı, belge analizörü, "Analyze Emails for Events" özelliği gibi
maddeler README'de anlatılıyor ama bu rehberi yazarken hiçbiri canlı
test edilmedi (bkz. §5 "İleride karşılaşılabilecek sorunlar").

---

## 2. Mimari genel bakış

```
MultiSych.Desktop/     — GTK4 arayüzü (Views, ViewModels), Program.cs (DI kurulumu + CLI)
MultiSych.Services/    — İş mantığı: OAuth, bulut API entegrasyonları, veritabanı, FUSE mount
MultiSych.Tests/       — xUnit testleri (şu an 81 test, hepsi gerçek davranış test ediyor)
```

**Katmanlar arası akış (özet):**

- `Program.cs` hem GUI başlatıcı hem CLI komut işleyicisi (`auth-google`,
  `sync-all`, `mount <id>`, `list-accounts` vb.) — ikisi de aynı DI
  container'ı kullanıyor.
- `AuthenticationService` üç sağlayıcı için OAuth2 akışlarını yönetiyor
  (Google/Yandex klasik authorization-code + refresh_token; Microsoft
  MSAL.NET ile).
- `CloudStorageService`/`CloudEmailService`/`CloudCalendarService`
  sağlayıcıya özel API çağrılarını yapıp sonucu yerel SQLite önbelleğine
  (`CloudFiles`, `CachedEmails`, `CachedEvents` tabloları) yazıyor.
- `AutoSyncBackgroundService` periyodik olarak (kod varsayılanı 15
  dakika — `MultiSychConfig`/`RuntimeSyncSettings`; ama `.env.example`
  `SYNC_INTERVAL_MINUTES=30` yazıyor, küçük bir tutarsızlık, hangisinin
  gerçekte etkili olduğu ayrıca ölçülmedi — pil düşükken uzuyor) tüm
  hesapları senkronize ediyor.
- `PlatformMountProvider` + `CloudMirrorFsOperations` gerçek bir FUSE
  dosya sistemi kurup bulut dosyalarını yerel bir klasör gibi sunuyor.

**Dikkat edilmesi gereken mimari kararlar (gelecekte "neden böyle"
diye sorulacaksa):**

- Dil değişimi (TR/EN) **anlık değil**, uygulama yeniden başladığında
  etkili oluyor — GTK View'ları metinlerini yalnız ilk inşa anında
  okuyor, canlı yeniden bağlama altyapısı yok (K11).
- `IVirtualDriveService` ve `IAccountStore` **Singleton**, diğer çoğu
  servis **Transient/Scoped** — mount durumu gibi uygulama ömrü boyunca
  paylaşılması gereken state için bilinçli seçim (K22).
- Windows desteği **tamamen kaldırıldı** (K18/K20) — proje artık yalnız
  Linux + macOS hedefliyor. README'nin Windows kurulum bölümü artık
  **yanlış/güncel değil**.

---

## 3. Bugüne kadar yapılanlar (kronolojik özet)

### Dönem 1 — GTK4 geçişinin tamamlanması (2026-09-20, FAZ 0-5)
- .NET 10 SDK kurulumu + tüm paketlerin hizalanması (K2-K3)
- Başlangıç güvenlik kapısı (parola/2FA) gerçekten çalışır hale
  getirildi (K4)
- 5 "yetim" ViewModel'den 4'ü arayüze bağlandı, biri (Calendar) o an
  ertelendi (K5)
- Veritabanı migration yarış durumu düzeltildi (K6)
- View'larda biriken event handler hatası düzeltildi — aynı sayfaya
  tekrar gidince tekrarlanan API çağrıları/mount denemeleri (K7)
- Yanlış "arka planda çalışacak" mesajı düzeltildi (K8)
- `dotnet test`'in masaüstünde gerçek hata penceresi açtırdığı bulundu
  ve düzeltildi — testler artık yan etkisiz (K9)
- Bildirimler gerçek çalışır hale getirildi (K10)
- Çoklu dil desteği (JSON tabanlı, `Loc.Get()`) sıfırdan kuruldu (K11)

### Dönem 2 — "Sahte başarı" denetimi (2026-09-20)
Antigravity 29 bulguluk bir denetim raporu çıkardı, Claude Code
hepsini triyaj edip kapattı (K12-K17): yanlış dosyaya yedek geri
yükleme, yanlış klasör temizleme, sessizce 0 bayt dosya yazma, yanıltıcı
"dışa aktarma parolası" alanı (gerçek işlevi yoktu) ve daha fazlası.

### Dönem 3 — Windows kaldırma + Calendar tamamlama (2026-09-21)
- Windows desteği tamamen kaldırıldı — 6 dosyada derleme-zamanı,
  12 dosyada çalışma-zamanı Windows dalı temizlendi (K18, K20)
- CalendarViewModel tamamlandı ve arayüze bağlandı (K19)

### Dönem 4 — Gerçek OAuth bağlantıları (2026-09-27)
- Google, Microsoft, Yandex hesapları gerçek kimlik bilgileriyle
  uçtan uca bağlandı (K21)
- Microsoft'ta tenant null hatası bulunup düzeltildi (K21)
- Hesap durumu (Transient→Singleton) ve çift kayıtlı hesap hatası
  bulundu (K22)
- **Gerçek FUSE mount** araştırılıp kanıtlandı ve uygulandı — önceden
  yalnız sembolik bağlantı + boş yer tutucu dosyalardı (K22-K23)
- Kritik bir `fork()` donma hatası bulunup düzeltildi (K23)
- Google için otomatik token yenileme bağlandı ve doğrulandı (K23)

### Dönem 5 — Yol haritası güncellemesi + FAZ 8/9 (2026-10-12)
- Yol haritası üç haftalık gecikmeyle güncellendi, `derleme`/`test`
  kapılarındaki locale hatası düzeltildi (K24)
- Mount edilen dosyaların gerçek (0 bayt olmayan) içeriğini talep
  üzerine indirme mantığı eklendi (K25) — **doğrulama bekliyor**
- Microsoft için kalıcı MSAL token önbelleği + Gmail OAuth kapsamı
  eklendi (K26) — **doğrulama bekliyor**

---

## 4. Şu an yapılacaklar (açık işler, öncelik sırasıyla)

Güncel ve ayrıntılı hali her zaman `docs/YOL-HARITASI.md`'de. Özet:

### 4.1 — ACİL: K25/K26'nın canlı doğrulanması (operatör gerekiyor)
İki büyük değişiklik (gerçek dosya indirme, Microsoft token yenileme +
Gmail scope) **kod seviyesinde tamamlandı ve derleniyor ama hiçbiri
canlı test edilmedi.** Tarayıcıda interaktif OAuth onayı gerektiriyor,
bu yüzden yalnız operatör yapabilir. Adımlar:
1. `dotnet run --project MultiSych.Desktop/MultiSych.Desktop.csproj -- auth-google`
   (yeni gmail scope'u içerecek, tarayıcı açılacak)
2. `... -- auth-microsoft` (yeniden bağlan, MSAL önbelleği bu sefer
   diske kalıcı olarak kaydedilecek)
3. `... -- sync-all` — Microsoft için "Token refreshed" mi yoksa hâlâ
   "FAILED" mi görünüyor, gerçek sonucu gör
4. `... -- mount <accountId>` ile gerçek bir dosyaya eriş (örn.
   `~/MultiSych_Drives/<rastgele-klasör>/RiskRaporu.pdf`), dosyanın
   artık gerçekten indiğini (0 bayt değil) doğrula

### 4.2 — FAZ 10: AI asistan entegrasyonu (hiç başlamadı)
Sol menüdeki Copilot/Gemini/Yandex AI butonlarının gerçekte ne
yaptığı bu denetimde hiç ölçülmedi. İlk adım: butona tıklanınca ne
oluyor, hata mı veriyor sessiz mi kalıyor — önce bunu gözle/logla ölç,
sonra API anahtarı akışını tasarla.

### 4.3 — FAZ 7 (eski numaralandırmayla): Paketleme hiç denenmedi
`build-deb-package.sh`, `Dockerfile`, `PKGBUILD` dosyaları var ama
hiçbirinin gerçek bir paket üretip çalıştırdığı bu depoda kayıtlı
değil. Düşük öncelikli (günlük kullanım `dotnet run` ile engelsiz
çalışıyor) ama bir gün başka bir makineye/kullanıcıya dağıtılacaksa
mutlaka denenmeli.

### 4.4 — Küçük, düşük öncelikli artıklar
- `docs/smoke-checklist.md` güncellenmedi, eski FAZ numaralarına göre.
- `GirCore1007` derleyici uyarısı hâlâ bastırılıyor (paket 0.8.0'da
  sabit) — her büyük iş paketinden önce yeni sürüm notlarına bakılması
  öneriliyor, henüz bakılmadı.

---

## 5. Yaşanan sıkıntılar — somut örnekler ve çıkarılan dersler

Bu bölüm, "neden bu kadar dikkatli davranıyoruz" sorusunun cevabı.
Her madde gerçekten yaşandı, her biri bir dersle sonuçlandı.

| # | Ne oldu | Kök sebep | Ders |
|---|---|---|---|
| 1 | `dotnet test` koşarken masaüstünde 3 kez "klasör yok" hata penceresi çıktı | Bir test, mock'lanmış arayüzün İÇİNDE mock'lanmamış `Process.Start("xdg-open",...)` çağırıyordu | Arayüzü mock'lamak, o arayüzü kullanan kodun İÇİNDEKİ başka yan etkileri mock'lamak değildir |
| 2 | Aynı sayfaya birden fazla gidince API çağrıları/mount denemeleri katlanarak tekrarlanıyordu | `DataContext` setter'ı her atamada event aboneliği ekliyordu, hiç temizlemiyordu | View'larda idempotent binding şart |
| 3 | "Arka planda çalışmaya devam edecek" diyen bir diyalog, aslında pencere kapanınca HER ŞEYİ durduruyordu | Metin iddia ediyordu, kod yapmıyordu | İddia ile gerçek örtüşmeli — bu projenin en pahalı hata kalıbı |
| 4 | Microsoft OAuth, tarayıcı açılmadan `ArgumentNullException: tenant` ile çöküyordu | Ortam değişkeni boşsa `string.Empty` dönüyordu, `?? "common"` yalnız `null`'ı yakalıyordu | Boş string ile null aynı şey değil, ikisini de kontrol et |
| 5 | Hesaplar sayfası "Bağlı Değil" diyordu ama mount aslında başarılıydı | `IVirtualDriveService` Transient kayıtlıydı, her çözümlemede ayrı bellek durumu oluyordu | Uygulama ömrü boyunca paylaşılması gereken state Singleton olmalı |
| 6 | Aynı Google hesabı veritabanında iki kez kayıtlı çıktı | **Kesin tetikleyici bulunamadı** — açık kaldı | Her bulgu tam açıklanamayabilir, dürüstçe "bilinmiyor" denmeli |
| 7 | Mount gerçekti ama içindeki her dosya 0 bayttı | `PlatformMountProvider` yalnız sembolik bağlantı + yer tutucu dosya oluşturuyordu, gerçek FUSE değildi | "Mount" kelimesi kullanmak, gerçekten mount etmek değildir — README'nin ana vaadi 2+ ay hiç gerçekleşmemiş |
| 8 | "Mount Drive"a basınca **uygulama tamamen kilitlendi** | `fuse_main` varsayılan olarak süreci native `fork()` ile ikiye ayırıyor, çok iş parçacıklı bir GTK/.NET uygulamasında bu güvenli değil | Üçüncü parti bir C kütüphanesinin "varsayılan davranışı" dokümantasyonda yazsa bile .NET'in thread modeliyle çarpışabilir — `-f` (foreground) bayrağı şart |
| 9 | `tools/kapilar` build/test'i kırmızı gösteriyordu ama gerçek build/test başarılıydı | Kapı İngilizce metin arıyordu, sistem artık Türkçe çıktı üretiyordu | Kapılar da hata yapabilir — "kırmızı" her zaman "ürün bozuk" demek değil, önce kapının kendisini şüphelen |
| 10 | Google token'ı birdenbire "expired or revoked" oldu | Google, doğrulanmamış ("Testing" modundaki) OAuth uygulamalarının refresh token'larını **7 gün sonra otomatik iptal ediyor** | Harici servislerin kendi politikaları bizim kontrolümüz dışında kısıtlama getirebilir |

---

## 6. İleride karşılaşılabilecek potansiyel sorunlar (riskler)

Bunlar henüz gerçekleşmedi ama mimari/mevcut durumdan **öngörülebilir**
riskler — bir gün karşına çıkarsa "aa bu neden oluyor" diye şaşırmaman
için burada:

### 6.1 — Google OAuth "Testing" modu yedi günde bir token'ı öldürür
Google Cloud Console'daki OAuth istemcisi hâlâ "Testing" (doğrulanmamış
uygulama) modunda olduğu sürece, her kullanıcının refresh token'ı 7 gün
sonra otomatik iptal oluyor. **Çözüm yolları:** (a) Google'a
uygulamayı doğrulatmak (zaman alır, kullanım amacına göre inceleme
gerektirir), (b) şimdilik 7 günde bir yeniden bağlanmayı kabul etmek,
(c) OAuth consent screen'de "Testing" kullanıcı listesine ekli
hesapların bu kısıtlamadan muaf olup olmadığını ayrıca kontrol etmek
(henüz ölçülmedi).

### 6.2 — MSAL'in Linux anahtarlık (keyring) bağımlılığı kırılgan
Microsoft'un kalıcı token önbelleği (K26), Secret Service (D-Bus
`org.freedesktop.secrets`) çalışıyorsa şifreli saklıyor. Bu makinede
KDE'nin `ksecretd`'i çalıştığı için sorun yok, ama **farklı bir Linux
masaüstü ortamında (örn. minimal bir pencere yöneticisi, headless
sunucu) Secret Service hiç bulunmayabilir** — MSAL o durumda kendiliğinden
düz dosyaya düşüyor ve yalnız log'a uyarı yazıyor, kullanıcıya hiçbir
şey söylemiyor. İleride "token'lar neden şifresiz duruyor" sorusu
çıkarsa kök sebep bu olabilir.

### 6.3 — FUSE mount büyük dosyalarda performans/bellek sorunu yaşayabilir
Şu anki lazy-download mantığı (K25) bir dosya açıldığında **tüm
içeriği tek seferde** indirip yerel diske yazıyor (`CopyToAsync`,
akışlı ama tam dosya bekliyor). Birkaç GB'lık bir video/arşiv dosyası
açılırsa: (a) kullanıcı dosya gerçekten erişilebilir olana kadar uzun
süre bekler, (b) FUSE `Open()` çağrısı bu süre boyunca bloklanır —
ölçülmedi, ama büyük dosyalarla gerçek kullanımda fark edilecektir.
Gerçek çözüm (parçalı/stream okuma, `Read()`'de ihtiyaç duyulan
baytları anlık indirme) ayrı, daha büyük bir mühendislik işi.

### 6.4 — Çift hesap kaydı tekrarlanabilir (kök sebep bilinmiyor)
K22'de bir Google hesabının veritabanında iki kez (iki farklı
`AccountId` ile) kayıtlı çıktığı görüldü, kesin tetikleyici kod yolu
bulunamadı. Eğer bu tekrarlarsa (özellikle çoklu süreç/pencere açıkken
mount+senkron aynı anda çalışırsa) arayüzde aynı hesap yine iki kez
görünebilir — şimdilik "Remove" ile elle temizleniyor, kalıcı bir
dedup mantığı (örn. e-posta+sağlayıcı bazlı upsert) henüz yazılmadı.

### 6.5 — GirCore 0.8.0'ın derleyici uyarı bastırması kalıcılaşabilir
`GirCore1007` uyarısı, kütüphanenin kararlı bir native-tip-subclass
deseni yayınlayana kadar bastırılıyor (bilinçli, ama süresiz bir
bekleme). Proje büyüdükçe bu bastırmanın ARKASINA gerçek, farklı bir
uyarı gizlenebilir — periyodik kontrol edilmezse fark edilmez.

### 6.6 — Paketleme hiç denenmediği için ilk gerçek deneme sürpriz çıkarabilir
`PKGBUILD`/`build-deb-package.sh`/`Dockerfile` yazılı ama uçtan uca
hiç çalıştırılmadı. Self-contained yayın (runtime gömülü) + FUSE'un
native kütüphane bağımlılığı (`libfuse3.so`) + SQLCipher'ın native
derlemesi bir arada paketlenirken beklenmedik bir eksik-bağımlılık
sorunu çıkarabilir — ilk paketleme denemesi bu yüzden ayrılmış bir
zaman gerektirir, "5 dakikalık iş" sanılmamalı.

### 6.7 — Windows'un tamamen kaldırılmış olması geri dönüşü zorlaştırır
Windows desteği yalnız devre dışı bırakılmadı, **kod tabanından
tamamen silindi** (DokanNet, Clowd.Squirrel, System.Speech paketleri
ve ilgili tüm dallar). İleride "Windows'u geri getirelim" denirse bu,
küçük bir toggle değil, **sıfırdan yeniden yazım** anlamına gelir —
git geçmişinde eski kod duruyor ama mevcut mimariyle (FUSE mount,
GTK4, locale self-re-exec gibi Linux'a özgü çözümler) uyumlu
olmayabilir.

### 6.8 — README'deki AI özellikleri doğrulanmamış durumda
"Akıllı Takvim" (e-postadan toplantı algılama), belge analizörü, AI
sohbet ekranı gibi özellikler README'de var ama bu rehberi yazarken
hiçbiri canlı test edilmedi (FAZ 10 henüz başlamadı). Bunların ne kadarı
gerçekten çalışıyor, ne kadarı yarım/placeholder — bilinmiyor.

---

## 7. Hızlı referans

**Projenin üç ana belgesi (her zaman güncel tutulmalı):**

- `docs/YOL-HARITASI.md` — hangi fazdayız, sıradaki iş ne
- `docs/KARARLAR.md` — her kararın tam gerekçesi ve kanıtı (K1-K26)
- `docs/KURALLAR.md` — ekip çalışma kuralları (kim ne yapar, kabul süreci)

**Sık kullanılan komutlar:**

```bash
# Tüm kapıları tek seferde koştur
bash tools/kapilar

# Uygulamayı GUI modunda çalıştır
dotnet run --project MultiSych.Desktop/MultiSych.Desktop.csproj

# Hesap bağlama (tarayıcı açılır)
dotnet run --project MultiSych.Desktop/MultiSych.Desktop.csproj -- auth-google
dotnet run --project MultiSych.Desktop/MultiSych.Desktop.csproj -- auth-microsoft
dotnet run --project MultiSych.Desktop/MultiSych.Desktop.csproj -- auth-yandex

# Hesapları listele
dotnet run --project MultiSych.Desktop/MultiSych.Desktop.csproj -- list-accounts

# Tüm hesapları senkronize et (token yenileme dahil)
dotnet run --project MultiSych.Desktop/MultiSych.Desktop.csproj -- sync-all

# Bir hesabı FUSE ile mount/unmount et
dotnet run --project MultiSych.Desktop/MultiSych.Desktop.csproj -- mount <accountId>
dotnet run --project MultiSych.Desktop/MultiSych.Desktop.csproj -- unmount <accountId>
```

**Önemli dosya konumları:**

- Veritabanı: `~/.config/MultiSych/multisych.db` (SQLCipher şifreli)
- Gizli bilgiler: `.env` (gitignore'da, asla commit edilmez)
- Mount edilen sürücüler: `~/MultiSych_Drives/<rastgele-id>`
- Microsoft MSAL kalıcı önbelleği: `~/.config/MultiSych/msal_cache.bin`
  (KDE/GNOME anahtarlığı varsa şifreli, yoksa düz dosya — bkz. §6.2)

**Çalışma kuralı (özet, tamamı `docs/KURALLAR.md`'de):** Her kabul,
`tools/kapilar`'ın bizzat koşturulmasına dayanır; alt ajanın "kapı
yeşil" demesine güvenilmez. Her büyük karar `docs/KARARLAR.md`'ye
kanıtıyla birlikte yazılır. "Çalışıyor" iddiası, çalıştığını GÖRMEDEN
atılmaz.
