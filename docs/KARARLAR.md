# MultiSych — KARARLAR

Bu dosya projenin bağlayıcı kararlarını tutar. **En yeni altta**
(append-only). Bir karar değişirse eskisi SİLİNMEZ, üstü çizilir ve
yenisi altına yazılır.

Sorumlu: Claude Code (baş mühendis) · Uygulayıcılar: Codex, Antigravity
Operatör: Murat Coşkun

---

## K1 — ÇOK AJANLI ÇALIŞMA MODELİ · 2026-09-20

**Karar:** Bu projede Claude Code baş mühendis, Codex ve Antigravity
onun emrinde uygulayıcı. İş her zaman iş paketi olarak paylaştırılır
(bkz. `docs/IS-PAKETI-SABLONU.md`), kabul/red kararını yalnız Claude
Code verir ve her kabul kapıların bizzat Claude Code tarafından
koşturulmasına dayanır.

**Gerekçe:** Operatörün talebi. Ayrıca genel kural
(`~/.claude/CLAUDE.md` §2, §4) zaten "alt ajanın raporuna güvenme,
kendin koştur" diyor — bu proje o kuralı organizasyonel olarak
somutlaştırıyor.

**Sonucu:** Her prompt'ta `docs/YOL-HARITASI.md`, `docs/KURALLAR.md`,
`.kapilar.conf` referans verilir (bkz. `docs/KURALLAR.md` §1).

---

## K2 — .NET 10 SDK KURULUMU ONAYLANDI · 2026-09-20

**Karar:** `sudo dnf install dotnet-sdk-10.0` (10.0.111, Fedora
`updates` deposu) çalıştırılacak.

**Gerekçe (ölçüldü):** Proje `net10.0` hedefliyor, makinede yalnız
.NET 8 SDK kuruluydu (`dotnet build` → `NETSDK1045`). Paket resmi
Fedora deposunda hazır bekliyor, 73.7 MB indirme. `~/.dotnet` altında
15 Eylül tarihli yarım kalmış bir manuel kurulum girişimi vardı
(yalnız sentinel dosyaları, `sdk/` klasörü yok) — o kullanılmadı,
dnf paketi tercih edildi çünkü sistem paket yöneticisiyle tutarlı ve
güncellemesi otomatik.

**Sonucu:** `docs/YOL-HARITASI.md` FAZ 0'ın tek maddesi. Operatör
onayı bu konuşmada verildi.

**✅ UYGULANDI — 2026-09-20.** `sudo dnf install -y dotnet-sdk-10.0`
çalıştırıldı (8 bağımlılık, 120 MiB indirme). Kanıt:
```
dotnet --list-sdks
8.0.130 [/usr/lib64/dotnet/sdk]
10.0.111 [/usr/lib64/dotnet/sdk]

dotnet build MultiSych.slnx
Build succeeded. 0 Warning(s) 0 Error(s)
```
Ayrı bir varsayım da doğrulandı: `.slnx` formatı .NET 10 SDK'da
desteklendi, dönüştürmeye gerek kalmadı. `tools/kapilar`: 6 kapıdan
`derleme` ve `test` yeşile döndü, kalan 3 (`guvenlik-baslangic`,
`bildirim`, `erisim`) FAZ 1-3'ün konusu, beklenen kırmızı.

---

## K3 — TAM .NET 10 HİZALAMASI (paket sürümleri + PKGBUILD) · 2026-09-20

**Karar:** Proje yalnız `net10.0` HEDEFLEMEKLE kalmayacak, birinci-taraf
(Microsoft.*) paketler de .NET 10 sürümüne hizalanacak; kullanılmayan
Windows-odaklı bağımlılıklar kaldırılacak; kaynaktan derleyen katkıcılar
için SDK eksikliği açıkça söylenecek.

**Gerekçe (ölçüldü):** `dotnet list package --outdated` şunu gösterdi:
proje `net10.0` derlerken `Microsoft.EntityFrameworkCore`,
`Microsoft.Extensions.Hosting`, `Microsoft.Extensions.DependencyInjection`,
`Microsoft.Extensions.Http`, `Microsoft.EntityFrameworkCore.Design`,
`System.IO.Packaging` hâlâ **9.0.0**'daydı; `Microsoft.Extensions.Caching.Memory`
ise zaten **10.0.9**'a çekilmişti — yani proje içinde bile tutarsız iki
farklı "hangi .NET sürümüne göre yazıldı" katmanı bir aradaydı. Operatör
bunu bekliyordu: proje .NET 8 ile başladı, 9 ve 10'a göre kısmen elden
geçirildi.

**Ek bulgu (paketleme çelişkisi):** `PKGBUILD` hem
`--self-contained true --runtime linux-x64` ile yayınlıyor (çalışma
zamanı pakete gömülü) hem de `depends=('dotnet-runtime-10.0' ...)` ile
sistemden runtime istiyor — self-contained'da bu gereksiz. Operatöre
soruldu: **self-contained kalsın, gereksiz bağımlılık kaldırılsın**
(paket biraz büyür ama kullanıcı hiçbir şey kurmak zorunda kalmaz).

**Ek bulgu (kapsam dışı kalan senaryo):** Operatör ".NET kurulu değilse
kullanıcıyı indirme sitesine yönlendirsin" istedi. Ölçüldü: hem
`build-deb-package.sh` hem `PKGBUILD` self-contained yayınlıyor —
**kurulu uygulamayı çalıştıran hiçbir son kullanıcı için bu senaryo
teknik olarak tetiklenemez** (runtime pakette gömülü). Yönlendirme kodu
bu yüzden uygulamanın kendi içine (`Program.cs`/`Main`) DEĞİL,
**kaynaktan derleyen katkıcı** akışına yazıldı — orada gerçek ve
tekrarlanmış bir sorun (bu makinede yaşandı).

**Uygulanan değişiklikler:**
1. `MultiSych.Services.csproj`, `MultiSych.Desktop.csproj`: yukarıdaki
   6 paket → `10.0.12` (nuget.org'da doğrulanan en güncel 10.x).
2. `System.Drawing.Common` (8.0.0) kaldırıldı — projede **sıfır**
   kullanım yeri vardı (`grep` ile doğrulandı), Linux-öncelikli bir
   projede Windows-odaklı ölü bağımlılıktı.
3. `global.json` eklendi: SDK `10.0.111`, `rollForward: latestMinor`.
   Bilinçli olarak `latestMajor` DEĞİL — .NET 11 hiç test edilmedi,
   sessizce ona atlamak "ölçmeden iddia etmeme" kuralını çiğnerdi.
4. `PKGBUILD`: `dotnet-runtime-10.0` bağımlılığı kaldırıldı, yerine
   neden kaldırıldığını açıklayan yorum kondu.
5. `scripts/check-dotnet-sdk.sh` (yeni): kaynaktan derleyen katkıcı
   için `.NET 10+` SDK kontrolü; eksikse `dotnet.microsoft.com/download`
   adresine `xdg-open` ile yönlendirmeyi dener, olmazsa URL'i basar.
   Negatif kontrolle sınandı (eşiği 99'a çekip kırmızı çıktığı
   görüldü). `scripts/verify.sh`'in ilk adımı yapıldı, `.kapilar.conf`'a
   `dotnet-sdk` kapısı olarak eklendi.

**Kanıt:** temiz `dotnet restore` + `tools/kapilar` → `dotnet-sdk`,
`derleme`, `test`, `eksiklik` yeşil (kalan 3 kırmızı FAZ 1-3'ün konusu,
bu kararla ilgisiz).

---

## K4 — GÜVENLİK KONTROLÜ İÇİN ARAYÜZ KARARI · 2026-09-20

**Karar:** Sıfırdan yazmıyoruz. Mevcut `SecurityHelper.VerifyStartupSecurity`
konsol I/O'sundan ayrıştırılıp saf doğrulama fonksiyonlarına bölünecek
(`ValidatePassword`, `ValidateTwoFactorCode` — Console yok, yalnız
`SecuritySettings` + girilen değeri alıp `bool` döner). `LoginViewModel`
bu saf fonksiyonları çağıracak şekilde düzeltilecek (2FA adımı eklenecek,
"saklı parola boşsa her parolayı kabul et" hatası kaldırılacak). Yeni bir
`Views/SecurityGateWindow.cs` (mevcut `AddAccountWindow.cs` deseninde)
`Program.cs`'in `OnActivate`'inde `MainWindow.Present()`'tan ÖNCE
gösterilecek.

**Gerekçe:** `VerifyStartupSecurity` konsol tabanlı (`Console.ReadLine`)
— GTK açılışında konsol genelde yok, bu yüzden Program.cs yorum bırakıp
atlamıştı. Mantığı iki kez yazmak (CLI + GUI ayrı ayrı parola
karşılaştırsın) ileride sapabilir; tek doğrulama fonksiyonu, iki çağıran
(CLI ve GUI) daha güvenli.

**Kapsam dışı (bilinçli):** `SetupViewModel`/`AuthViewModel` bu kararın
KONUSU DEĞİL — onlar hesap/OAuth ilk kurulum akışına ait, başlangıç
parola kapısıyla karıştırılmayacak. Onların kaderi FAZ 2'de, K5'te
ayrıca karara bağlanacak.

**Uygulayan:** Codex, iş paketiyle (`docs/IS-PAKETI-guvenlik-kapisi.md`).
Kabul: Claude Code, `tools/guvenlik-baslangic-testi.sh` + gerçek
uygulamada elle parola deneyerek.

---

## K5 — 5 ORPHAN VIEWMODEL İÇİN KARAR · 2026-09-20

**Karar:** `AIOverviewViewModel`, `DocumentAnalyzerViewModel`,
`DocumentsViewModel`, `ErrorReportViewModel` için View yazılıp
`MainWindow.cs`'e bağlanacak. `CalendarViewModel` ERTELENİYOR.

**Gerekçe (Antigravity raporu + Claude Code doğrulaması):**
`docs/RAPOR-orphan-viewmodel-durumu.md` — Antigravity'nin bulguları iki
noktada bizzat spot-check edildi ve doğrulandı: `ErrorReportViewModel`
gerçekten 0 servis bağımlılığıyla parametresiz constructor'a sahip
(`ErrorReportViewModel.cs:13`), `CalendarViewModel` gerçekten
`ICalendarService`'e hiç referans vermiyor (`grep` sıfır sonuç).

**Ek bulgu (Claude Code, Antigravity'nin görmediği, ilk yazışta abartılı
aktarıldı — düzeltildi):** `MainWindowViewModel.cs:279-284` sidebar'da
**"AI", "Analyzer", "Mail", "Documents", "Logs" düğmeleri zaten
görünüyor ve tıklanabiliyor** — `UpdateCurrentPage()` (satır 254-268)
da bu ViewModel'leri doğru sayfa olarak atıyor. `MainWindow.cs`'te
gerçek View'ı olmayan sayfalar için bir `ShowPlaceholder` yolu var
(`"🚧 Bu ekran yakında (<TypeName>)"` yazan bir kutu) — yani kullanıcı
tıklayınca ekran DONMUYOR, ama üretime çıkmış bir uygulamada dört
navigasyon düğmesi "yakında" yazıyor. Bu "ölü kod" değil, "üründe
görünür yarım özellik" — `erisim-testi.sh`'in yakaladığı asıl risk
tam bu. `CalendarViewModel` için nav düğmesi hiç yok, o tutarlı
biçimde tam erişilemez durumda kaldığı için ertelenmesi güvenli.

**Kapsam:**
- `AIOverviewView.cs`, `DocumentAnalyzerView.cs`, `DocumentsView.cs`,
  `ErrorReportView.cs` yazılacak (mevcut `DashboardView.cs` deseninde).
- `MainWindow.cs`'in sayfa switch'ine 4 yeni `else if` dalı eklenecek.
- `ErrorReportViewModel`'in GitHub gönderim URL'i hâlâ yer tutucu
  (`yourusername`) — bu View'ın kendisini engellemiyor, AYRI bir iş
  (kapsam dışı, bu kararın konusu değil).
- `CalendarViewModel` DI kaydı ve `tools/erisim-testi.sh` çıktısı
  DEĞİŞMİYOR — o hâlâ kırmızı görünecek, bilerek.

**Uygulayan:** Codex, iş paketiyle (`docs/IS-PAKETI-faz2-nav-baglama.md`).

**✅ KABUL EDİLDİ — 2026-09-20.** 4 View eklendi, `MainWindow.cs`
switch'ine bağlandı, sınırlara uyuldu (doğrulandı). Claude Code
`tools/erisim-testi.sh`'e `CalendarViewModel` istisnasını ekledi.
Kanıt: `tools/kapilar` → 7 kapıdan 6'sı yeşil, yalnız `bildirim`
(FAZ 3, henüz yapılmadı) kırmızı.

---

## K6 — DB MIGRATION YARIŞ DURUMU DÜZELTİLDİ · 2026-09-20

**Bulgu:** Uygulama gerçek çalıştırmada `SqliteException: no such table:
CachedEvents` ile başlıyordu. Kök sebep: veritabanı migration'ı yalnızca
`AccountStoreService` (Singleton) ilk kez DI'dan çözümlendiğinde,
constructor'ının yan etkisi olarak çalışıyordu
(`AccountStoreService.cs:24-25`). `AutoSyncBackgroundService`
`host.StartAsync()` ile hemen DB'ye erişmeye başladığı için, migration
henüz çalışmadan tabloya erişmeye çalışıyordu — deterministik olmayan
bir sıralama yarışı.

**Ek bulgu:** Ayrıca gerçek model (`DocumentChatMessageEntity` dahil
yeni alanlar) son migration'dan (`AddOfflineSyncQueue`, 2026-06-23)
beri hiç migration'a dökülmemişti (`dotnet ef migrations
has-pending-model-changes` → "Changes have been made to the model
since the last migration").

**Uygulanan düzeltme:**
1. `dotnet ef migrations add AddDocumentChatMessages` — yalnız ekleyici
   (yeni sütunlar + yeni tablo), `Down()` doğru tersine çeviriyor,
   mevcut veriye dokunmuyor. İçeriği elle incelendi.
2. `Program.cs`: `ServiceProvider.GetRequiredService<IAccountStore>();`
   çağrısı `host.StartAsync()`'ten ÖNCEye taşındı — migration artık
   arka plan servisleri başlamadan deterministik olarak tamamlanıyor.

**Kanıt:** Gerçek uygulama koşumu, migration günlüğü "No migrations
were applied. The database is already up to date." diyor, `CachedEvents`
hatası bir daha çıkmadı. `dotnet build`/`dotnet test` 0 hata, 78/78 geçti.

---

## K7 — VIEW'LARDA BİRİKEN EVENT HANDLER'LAR (sistemik) · 2026-09-20

**Bulgu:** Operatör gerçek kullanımda `~/MultiSych_Drives/acc_1 dosyası
veya klasörü yok` hata penceresinin **3 kez aynı anda** çıktığını
bildirdi. Kök sebep izlendi: her View'ın `DataContext` setter'ı, değer
her atandığında koşulsuz `InitializeBindings()` çağırıyor
(`AccountsView.cs:16-23` örnek). `MainWindow.cs`'in `UpdateActiveView()`'i
View örneğini ÖNBELLEKTEN kullanıyor ama `DataContext`'i HER navigasyonda
yeniden atıyor — yani aynı ViewModel referansı View'a tekrar tekrar
veriliyor ve her seferinde `InitializeBindings()` yeni bir
`Accounts.CollectionChanged` aboneliği ekliyor, eskisini hiç
kaldırmadan. Sayfaya N kez gidilirse, bir sonraki koleksiyon değişimi
`PopulateAccounts()`'u N kez tetikliyor — bu da zincirleme olarak
mount/xdg-open gibi yan etkili çağrıların tekrarlanmasına yol açıyor.

**Kapsam — aynı desen 8 dosyada bulundu (grep ile doğrulandı):**
`AccountsView.cs`, `ChatView.cs`, `DashboardView.cs`, `EmailView.cs`,
`FileExplorerView.cs`, `MainWindow.cs`, `SettingsView.cs`, `SyncView.cs`.

**Karar:** Her dosyada `DataContext` setter'ına
`if (ReferenceEquals(_viewModel, value)) return;` koruması eklenecek
(değer değişmediyse `InitializeBindings()` tekrar çağrılmaz). Bu minimal
ve güvenli çözüm: bu uygulamada ViewModel'ler `MainWindowViewModel`
tarafından oturum boyunca TEK SEFER oluşturulup önbelleğe alınıyor, yani
"aynı View'a FARKLI bir ViewModel atanması" senaryosu şu an hiç
gerçekleşmiyor — tam abonelikten-çıkma (unsubscribe) mekanizması bugün
gereksiz karmaşıklık olur. Mimari ileride değişip View'lara farklı
ViewModel örnekleri atanmaya başlarsa, o zaman tam unsubscribe eklenir
— bugün olmayan bir senaryo için önden karmaşıklık eklenmiyor.

**Ciddiyet notu:** Bu yalnız görsel bir tekrar değil — `ChatView`/
`EmailView` gibi yan etkili (mesaj gönderme, senkronizasyon) sayfalarda
aynı desen **tekrarlanan API çağrısı / tekrarlanan mesaj gönderimi**
riski taşıyor. FAZ 2'den ÖNCE, acil ele alınacak.

**Uygulayan:** Codex, iş paketiyle (`docs/IS-PAKETI-view-rebind-fix.md`).

---

## K8 — YANLIŞ "ARKA PLANDA ÇALIŞACAK" MESAJI DÜZELTİLDİ · 2026-09-20

**Bulgu:** `MainWindow.cs:378` çıkış onayı diyaloğu *"MultiSych arka
planda çalışmaya devam edecektir. Kapatmak istediğinize emin misiniz?"*
diyordu. Gerçekte "Evet"e basınca yalnızca `this.Close()` çağrılıyor
(satır 388) — hiçbir tray simgesi, hiçbir arka plan tutma mekanizması
(`Gio.Application.Hold()` vb.) yok. Pencere kapanınca `Program.cs`'teki
`app.Run(args)` döner ve hemen ardından `host.StopAsync()` ÇALIŞIR —
`AutoSyncBackgroundService` dahil her şey durur. Operatör bunu gerçek
kullanımda yakaladı: "Evet" dedi, görev yöneticisinde uygulamayla ilgili
hiçbir süreç kalmadı.

**Bu, projenin en pahalı hata kalıbı: iddia ile gerçek örtüşmüyor**
(bkz. `~/Projelerim/KURALLAR.md` §2, `CALISMA-KURALLARI.md` §2).

**Karar (acil, küçük düzeltme — Claude Code doğrudan yaptı):** Yanlış
metin kaldırıldı, diyalog artık gerçekte olanı söylüyor ("MultiSych
kapatılacak, senkronizasyon durdurulacak. Emin misiniz?"). **Gerçek
arka plan modu (tray simgesi + `Gio.Application.Hold()` ile pencere
kapanınca da senkronizasyonun sürmesi) AYRI, daha büyük bir özellik —
bu kararın kapsamı DEĞİL.** İstenirse `docs/YOL-HARITASI.md`'ye ayrı
bir faz olarak eklenir; tasarım kararı gerektirir (tray simgesi GTK'da
nasıl gösterilecek, GNOME'un AppIndicator uzantısı gerektirip
gerektirmediği ölçülmeli).

**Kanıt:** `dotnet build` 0 hata, `dotnet test` 78/78.

---

## K9 — `dotnet test`'İN GERÇEK MASAÜSTÜ YAN ETKİSİ (KÖK SEBEP: `~/MultiSych_Drives/acc_1` UYARISI) · 2026-09-20

**Bulgu — kesin, tesadüfe yer bırakmadan doğrulandı:** Operatörün defalarca
bildirdiği "`~/MultiSych_Drives/acc_1 dosyası veya klasörü yok`" hata
penceresi, GTK uygulamasından DEĞİL, **Claude Code'un bu oturumda
tekrar tekrar çalıştırdığı `dotnet test` komutundan** geliyordu.
Operatör bunu kanıtladı: "burada ne yapıyorsan aynı şekilde 3 hata
mesajı fırlattı" diyerek build/test komutunu benimle aynı anda
ilişkilendirdi.

**Kök sebep:** `VirtualDriveServiceTests.cs`'te `IPlatformMountProvider`
doğru şekilde mock'lanmıştı (`MountAsync` → `ReturnsAsync(true)`,
gerçek dosya sistemi işlemi yok). Ama `VirtualDriveService.MountDriveAsync`
mock `true` döndükten SONRA doğrudan **mock'lanmamış**
`Process.Start("xdg-open", targetFolder)` çağırıyordu (eski
`VirtualDriveService.cs:60-66`). Üç test (`MountDriveAsync_AlreadyMounted...`,
`UnmountDriveAsync_Success...`, `Dispose_ShouldUnmountAllActiveDrives`)
bu koda "acc_1" hesabıyla ulaşıyor — üçü de gerçekten var olmayan
`~/MultiSych_Drives/acc_1`'i açmaya çalışıp masaüstünde 3 gerçek hata
penceresi açtırıyordu. Bu, ders niteliğinde bir "yanlış mock sınırı"
örneği: arayüz doğru mock'lanmıştı ama gerçek yan etkili çağrı yanlış
sınıfta duruyordu.

**Karar:** `IPlatformMountProvider`'a `RevealInFileManager(string path)`
eklendi; gerçek `Process.Start` mantığı `VirtualDriveService`'ten
`PlatformMountProvider`'a taşındı. Artık testler `IPlatformMountProvider`'ı
mock'ladığında bu çağrı da otomatik olarak no-op oluyor — ekstra bir
mock ayarı gerekmiyor, sınır artık doğru yerde.

**Kanıt:** `dotnet build` 0 uyarı 0 hata, `dotnet test` 78/78 geçti,
**ve** operatör düzeltmeden sonraki koşumlarda hiçbir hata penceresi
çıkmadığını doğruladı ("hata mesajını bildirdikten sonra yaptığın
işlemler hatayı tetiklemedi", 2026-09-20). **KAPANDI.**

**Ders (genel kurallara eklenecek):** Bir arayüzü mock'larken, o
arayüzü kullanan sınıfın İÇİNDE arayüzün DIŞINDA başka bir yan etkili
çağrı (Process.Start, dosya I/O, ağ) olup olmadığı kontrol edilmeli —
"arayüz mock'landı" ile "yan etki mock'landı" aynı şey değil.

---

## K10 — LOKALİZASYON MEKANİZMASI — BEKLİYOR

**Durum:** Karar henüz verilmedi. FAZ 4'te JSON tabanlı kaynak sözlüğü
mü yoksa `gettext` mi kullanılacağı burada kayda geçirilecek.
