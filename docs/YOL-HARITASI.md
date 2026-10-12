# YOL HARİTASI — MultiSych

Yazan: Claude Code (baş mühendis) · İlk yazım 2026-09-20, **2026-10-12'de
baştan ölçülerek güncellendi** (önceki hâli üç hafta eskiydi, FAZ 0-5
tamamlanmış görünüyordu ama belge hâlâ "FAZ 1 sırada" diyordu).
Okuyacak: operatör (Murat Coşkun) + bu depoda çalışacak her ajan
(Claude Code, Codex — Antigravity 2026-10-07'den beri görev almıyor,
bkz. `~/.claude/CLAUDE.md` §8)

Bu belge **ölçümle** yazıldı, tahminle değil. Ekip kuralları için
`docs/KURALLAR.md`, kapı tanımları için `.kapilar.conf` + `tools/kapilar`
bak, kararların tam gerekçesi için `docs/KARARLAR.md` (K1-K24).

---

## 0. ŞU AN NEREDEYİZ — 2026-10-12 denetimi, ölçülmüş

### Kapı durumu (bugün, `tools/kapilar` ile ölçüldü)

```
KAPI                DURUM
dotnet-sdk          ✅ yeşil
derleme             ✅ yeşil
test                ✅ yeşil (81/81)
guvenlik-baslangic  ✅ yeşil
bildirim            ✅ yeşil
erisim              ✅ yeşil
eksiklik            ✅ yeşil
lokalizasyon        ✅ yeşil
✅ 8 kapının 8'i YEŞİL
```

**Bugün bulunup düzeltilen kapı hatası:** `derleme` ve `test` kapıları
İngilizce desen arıyordu (`Build succeeded`, `Passed!`) ama sistem
locale'i Türkçe çıktı üretiyordu (`Oluşturma başarılı oldu.`,
`Başarılı!`) — gerçek build/test BAŞARILIYDI ama kapı bunu
göremiyordu. `.kapilar.conf`'taki iki satıra `DOTNET_CLI_UI_LANGUAGE=en`
eklenerek locale-bağımsız hale getirildi (bkz. `docs/KARARLAR.md` K24).
Negatif kontrol: düzeltmeden önce ölçülüp kırmızı olduğu görüldü,
düzeltmeden sonra tekrar ölçülüp yeşile döndüğü doğrulandı.

### FAZ 0-5 TAMAMLANDI (bu belgenin önceki hâlinin tüm maddeleri)

Aşağıdaki fazlar, bu belgenin 2026-09-20 tarihli ilk sürümünde "kırmızı/
sırada" olarak listelenmişti. Bugünkü denetimde hepsinin fiilen
tamamlanmış olduğu `docs/KARARLAR.md`'deki karar numaralarıyla
doğrulandı — belge güncellenmemişti, iş yapılmıştı:

| Faz | Konu | Kanıt (KARARLAR.md) |
|---|---|---|
| FAZ 0 | .NET 10 SDK + paket hizalama | K2-K3 |
| FAZ 1 | Güvenlik başlangıç kapısı (parola/2FA) | K4, `guvenlik-baslangic` yeşil |
| FAZ 2 | 5 erişilemeyen ViewModel bağlandı/kapatıldı | K5, `erisim` yeşil |
| FAZ 3 | Bildirimler gerçek çalışıyor | K10, `bildirim` yeşil |
| FAZ 4 | Çoklu dil desteği (JSON + `Loc.Get`) | K11, `lokalizasyon` yeşil |
| FAZ 5 | `SearchFilesAsync` eksik implementasyon kapatıldı | K15, `eksiklik` yeşil |

### FAZ 6 — Küçük artıklar (bugün ilerletildi)

1. ✅ `MainWindow.axaml.cs` (0 bayt, izlenmeyen) — **bugün silindi**,
   build/test/kapılar yeşil kaldığı doğrulandı.
2. `docs/smoke-checklist.md` — henüz güncellenmedi, hâlâ açık.
3. `GirCore1007` bastırması — **hâlâ dokunulmadı** (bilinçli, roadmap'in
   orijinal kararı). Paket sürümü hâlâ `0.8.0`
   (`MultiSych.Desktop.csproj`). Yeni kararlı desen yayınlanıp
   yayınlanmadığı her büyük iş paketinden önce bir kez kontrol edilsin.

### FAZ 7 — Genel regresyon ve paketleme (HÂLÂ AÇIK, hiç denenmedi)

`build-deb-package.sh`, `Dockerfile`, `PKGBUILD` dosyaları var ama
hiçbirinin uçtan uca gerçek bir paket üretip çalıştırdığı bu depoda
kayıtlı değil. K3'te yalnız `PKGBUILD`'in self-contained/runtime
bağımlılık çelişkisi düzeltildi, gerçek bir paketleme denemesi
yapılmadı. **Bu projenin en eski açık işi** — ama kullanıcı şu an
`dotnet run` ile çalışıyor, günlük kullanımı engellemiyor, bu yüzden
düşük öncelikli.

---

## 1. YENİ KAPSAM — OAuth + Gerçek FUSE Mount (K18-K23, Eylül sonu-Ekim)

Bu belgenin ilk sürümü yalnızca GTK4 geçişinin tamamlanmasını
kapsıyordu. O tamamlandıktan sonra proje çok daha büyük bir iş
yaptı — gerçek bulut hesap bağlantıları ve gerçek (simüle değil)
sanal sürücü desteği. Özet (ayrıntı `docs/KARARLAR.md` K18-K23):

- Google, Microsoft, Yandex OAuth uçtan uca bağlı ve doğrulanmış.
- Windows desteği tamamen kaldırıldı (K18/K20) — proje yalnız Linux
  + macOS hedefliyor.
- Gerçek FUSE mount (LTRData.FuseDotNet, kernel seviyesinde,
  `/proc/mounts` ile kanıtlı) — önceki sembolik bağlantı simülasyonunun
  yerini aldı. Kritik bir `fork()` donma hatası bulunup `-f` bayrağıyla
  düzeltildi.
- Google için otomatik token yenileme çalışıyor (gerçek hesapla
  doğrulandı).

### Bu kapsamdan kalan açık işler (FAZ 8-10, aşağıda)

---

## FAZ 8 — Mount edilen dosyaların GERÇEK içeriği 🔴 GERÇEK HATA BULUNDU

**Sorun:** FUSE mount gerçek (kernel seviyesinde) ama içindeki dosyalar
hâlâ 0 baytlık yer tutucuydu.

**✅ Uygulandı (2026-10-12, K25):** `CloudMirrorFsOperations.Open`
dosya açılmadan önce, yerel yer tutucu DB'deki gerçek boyuttan küçükse
`IStorageService.DownloadFileAsync` ile gerçek içeriği indirip yerel
dosyanın üzerine yazıyor. `GetAttr` de artık indirmeden önce bile
gerçek boyutu bildiriyor (dosya yöneticisinde "0 B" görünmesin diye).
Test için `mount`/`unmount <accountId>` CLI komutları eklendi.

**🔴 YENİ BULGU (2026-10-12, dış inceleme + kod doğrulaması — bkz.
§1.5 altında F02):** `DownloadFromGoogleDriveAsync`, native Google
Docs/Sheets/Slides (`application/vnd.google-apps.*`) dosyalarını
`Files.Get().DownloadAsync()` ile indirmeye çalışıyor — Google Drive
API bu formatları bu yolla HİÇ vermiyor, `Files.Export()` gerektiriyor.
Gerçek test hesabında doğrulandı: Google'daki 6 dosyadan 3'ü tam bu
formatta — bu üç dosya mount üzerinden açılırsa sessizce boş kalır
(`try/catch` hatayı yutup placeholder'ı koruyor). **FAZ 8'in
"tamamlandı" sayılabilmesi için önce bu düzeltilmeli.**

**⚠️ Ek, düşük öncelikli bulgu (D04, dış inceleme):** `DownloadFileAsync`
(üç sağlayıcı da) dosyayı indirirken önce TAMAMEN bir `MemoryStream`'e
okuyor — rehberde "akışlı" denmişti, bu YANLIŞTI. Büyük bir dosya
(ör. birkaç GB video) tüm boyutuyla bellekte tutulur. Düzeltme (parçalı/
byte-range indirme) ayrı, daha büyük bir iş — şimdilik yalnız
belgelendirilecek, bilinen bir sınır olarak.

**⚠️ DOĞRULAMA EKSİK (K25'in asıl kapsamı için hâlâ geçerli):** PDF/düz
metin dosyalarıyla gerçek bir indirmeyle uçtan uca KANITLANAMADI — tek
dolu hesabın (Google) token'ı ölü çıktı, operatör o an yeniden
bağlanamadı. Ayrıntı ve sonraki adım: `docs/KARARLAR.md` K25.

**Kim:** Claude Code (F02 düzeltmesi + uygulama), **operatör** (makinenin
başında Google'ı yeniden bağlayıp canlı doğrulama — tek yapabilen o).

---

## FAZ 9 — Token yenileme ve OAuth kapsam eksikleri 🟡 UYGULANDI, DOĞRULAMA BEKLİYOR

**Sorun 1 — Microsoft token yenileme başarısız:** Her çağrı ayrı,
bellek-içi bir MSAL nesnesi üretiyordu, `AcquireTokenSilent` önceki
girişi bulamıyordu (gerçek `sync-all` koşumunda "Token refresh FAILED"
olarak ölçüldü).

**✅ Uygulandı (2026-10-12, K26):** Microsoft'un resmi
`Microsoft.Identity.Client.Extensions.Msal` paketiyle diske kalıcı,
paylaşılan bir MSAL önbelleği bağlandı (`BuildMicrosoftAppAsync`,
her iki çağrı yerinde ortak). Bu makinede Secret Service (KDE ksecretd)
çalıştığı doğrulandı — önbellek şifreli saklanacak.

**Sorun 2 — Google Gmail `403 Insufficient Permission`:** OAuth scope
dizesi gmail'i hiç istemiyordu; `CloudEmailService` ise kalıcı silme
dahil tam Gmail erişimi kullanıyor.

**✅ Uygulandı (2026-10-12, K26):** Scope dizesine
`https://mail.google.com/` (tam erişim, kalıcı silme dahil her
operasyonu kapsıyor) eklendi.

**⚠️ DOĞRULAMA EKSİK (ikisi de):** Hiçbiri gerçek interaktif girişle
canlı test edilmedi — operatörün tarayıcıda onay vermesi gerekiyor.
Ayrıntı ve sonraki adım (tek oturumda hem Google hem Microsoft'u
yeniden bağlama sırası): `docs/KARARLAR.md` K26.

**🟡 Düzeltme (D02, dış inceleme + araştırma — 2026-10-12):** K26'da
ve `docs/PROJE-REHBERI.md`'de "Secret Service yoksa MSAL kendisi düz
dosyaya düşüp uyarı loglar" denmişti — bu muhtemelen YANLIŞ. Araştırma:
`WithUnprotectedFile()` açıkça çağrılmadıysa, `MsalCacheHelper.CreateAsync`
Secret Service yokken muhtemelen **exception fırlatıyor**, sessizce düz
dosyaya düşmüyor. Kendi kodumuzda `WithUnprotectedFile()` HİÇ
çağrılmadı — ve çağrı zaten `try/catch` içinde, yani gerçek davranış
muhtemelen "bellek-içi önbelleğe geri dön" (K23'ün orijinal sorunu
sürer, ama hiç sır diske yazılmaz). Belgelenen davranıştan daha güvenli
ama farklı — **KARARLAR.md K26 ve Rehber'in ilgili cümlesi düzeltilmeli,
gerçek davranış Secret Service'siz bir ortamda bizzat ölçülerek
kesinleştirilmeli** (bu makinede Secret Service var, test edilemedi).

**Kim:** Claude Code (uygulandı + D02 düzeltmesi), **operatör** (canlı
doğrulama).

---

## FAZ 10 — AI asistan entegrasyonu (hiç başlamadı)

**Sorun:** Sol menüde "Microsoft Copilot", "Google Gemini", "Yandex AI"
butonları görünüyor ama hiçbiri için API anahtarı ayarlanmadı —
tıklanınca ne olduğu bu denetimde kontrol edilmedi.

**Adım:** Önce gerçek davranışı ölç (buton tıklanınca ne oluyor — hata
mı veriyor, sessiz mi kalıyor), sonra API anahtarı akışını (muhtemelen
`--set-ai-key` CLI komutuna benzer) tasarla.

**Kim:** Belirlenmedi — kapsam henüz netleşmedi, önce ölçüm gerekiyor.

---

## 1.5 — DIŞ İNCELEME (Perplexity, operatör talebiyle, 2026-10-12)

Operatör, `docs/PROJE-REHBERI.md`'yi dış bir modele (Perplexity)
inceletip bulguları getirdi. Claude Code üç en somut iddiayı (F02, D04,
D02) kodda bizzat doğruladı — ikisi (F02, D04) gerçek çıktı, biri (D02)
kendi belgelediğimiz bir yanlışı ortaya çıkardı. Kalan maddeler kod
doğrulaması gerektirmeyen, kabul edilebilir mühendislik/süreç önerileri
— aşağıda FAZ 11-13 olarak kabul edildi. Tam rapor operatörün elinde
(bu depoya eklenmedi, sır içermiyor ama üçüncü parti bir belge).

**Zaten FAZ 8-9'a işlenen (yukarıda):** F02 (Google Docs export hatası,
🔴 gerçek hata), D04 (bellek sınırsız indirme, bilinen sınır), D02
(MSAL fallback belge hatası).

### FAZ 11 — Güvenlik ve veri bütünlüğü sertleştirmesi (dış inceleme G01-G05)

Öncelik sırası dış incelemenin kendi P0 sınıflandırmasıyla aynı —
makul bulundu, değiştirilmedi:

1. **G01 — Token/anahtar yaşam döngüsü belgelenmeli:** SQLCipher
   anahtarının kaynağı, kilitleme/çıkış davranışı ve silme politikası
   şu an hiçbir yerde açık değil.
2. **G02 — Yarım indirme atomik olmalı:** FAZ 8'in indirme mantığı şu
   an dosyayı doğrudan hedef yola yazıyor; kesinti olursa yarım dosya
   "tam" gibi görünebilir. Düzeltme: geçici dosyaya yaz, boyut/hash
   doğrula, aynı dosya sisteminde atomik rename yap.
3. **G03 — Mount yazma/silme sözleşmesi netleşmeli:** Şu an yazma
   davranışı (truncate/rename/delete/uzak değişiklik) belgeli değil.
   İlk güvenilir sürümde salt-okunur mod açıkça ilan edilebilir.
4. **G04 — Cache yol izolasyonu test edilmeli:** `CloudMirrorFsOperations`
   yerel dosya sistemi sınırında çalışıyor; symlink kaçışı, ad
   çakışması gibi senaryolar hiç test edilmedi.
5. **G05 — Yedek/geri yükleme doğruluğu:** K12'de düzeltilen eski
   hata (yanlış dosyaya yedek) bir daha olmasın diye format/sürüm/sır
   dahil olma durumu açıkça belgelenmeli, restore öncesi doğrulama
   eklenmeli.

**Kim:** Claude Code (uygulama), operatör (kabul).

### FAZ 12 — Eşzamanlılık, kimlik ve senkronizasyon sertleştirmesi (E01-E05)

1. **E01 — Hesap tekilleştirme veritabanında garanti edilmeli:** K22'de
   bulunan "aynı hesap iki kez kayıtlı" sorununun kök sebebi hâlâ
   bilinmiyor. Öneri: sağlayıcı + sabit kimlik (e-posta, Microsoft'ta
   tenant/home-account ayrımına dikkat) üzerinde UNIQUE kısıt +
   transaction içinde upsert.
2. **E02 — Scope/DbContext ömürleri denetlenmeli:** Singleton servisler
   (K22) ile Transient/Scoped servisler bir arada kullanılıyor,
   bağımlılık grafiği hiç çıkarılmadı.
3. **E03 — Token yenileme ve sync tekrarları koordine edilmeli:**
   GUI'nin periyodik senkronu ile CLI'nin `sync-all`'ı aynı anda aynı
   hesabı yenilemeye çalışırsa ne olur, hiç test edilmedi.
4. **E04 — Delta sync/silme/çakışma sözleşmesi eksik:** Sayfalama,
   uzak silme ve çevrimdışı değişiklik kuyruğu davranışı belgeli değil.
5. **E05 — Yapılandırma tek kaynaktan gelmeli:** Zaten bilinen bir
   bulgu (kod varsayılanı 15 dakika, `.env.example` 30 yazıyor —
   `docs/PROJE-REHBERI.md` §2'de not edilmişti), burada resmi bir iş
   maddesi olarak teyit edildi.

**Kim:** Claude Code (uygulama), operatör (kabul).

### FAZ 13 — Test/dağıtım/kullanıcı deneyimi iyileştirmeleri (T01-T05, F01, F03-F04)

Düşük-orta öncelikli, FAZ 11-12'den sonra ele alınacak:

- Native bağımlılıkların (libfuse3, SQLCipher) hiç paketlenip
  denenmediği (zaten FAZ 7'de biliniyordu, burada teyit edildi).
- Büyük dosyalar için parçalı/byte-range indirme (F01, D04'ün gerçek
  çözümü).
- "81 test" sayısının tek başına kalite ölçütü sayılmaması — sağlayıcı
  API'leri, FUSE davranışı ve token kalıcılığı için ayrı, gerçek
  entegrasyon testleri gerekiyor.
- Çevrimdışı çalışmanın kapsamının (yalnız metadata mı, dosya içeriği
  de mi) kullanıcıya açıkça anlatılması.

**Kim:** Belirlenmedi, FAZ 11-12 bitmeden sıraya alınmayacak.

**Operatörün P2 önceliği onaylandı:** AI entegrasyonu (FAZ 10), FAZ
11-13'ten SONRA ele alınacak — dış incelemenin de önerdiği sıralama bu.

---

## 2. SIRADAKİ TEK ADIM

**✅ Bugün (2026-10-12):** Yol haritası gerçek duruma göre yeniden
yazıldı, `derleme`/`test` kapılarındaki locale hatası düzeltildi (K24),
ölü `MainWindow.axaml.cs` silindi. FAZ 8 (gerçek dosya indirme, K25) ve
FAZ 9 (Microsoft MSAL kalıcı önbellek + Gmail scope, K26) uygulandı.
Operatörün istediği dış inceleme (Perplexity) işlendi — F02 (Google
Docs export hatası) ve D04 (bellek sınırsız indirme) kodda doğrulanan
gerçek bulgular, D02 kendi belgelediğimiz bir yanlışı ortaya çıkardı;
kalan kabul edilen maddeler FAZ 11-13 olarak eklendi (§1.5).

**Şu an başlanan: FAZ 8'in F02 düzeltmesi** — Google Docs/Sheets/Slides
için `Files.Export()` yolu eklenecek (Claude Code, kod değişikliği,
operatör etkileşimi gerektirmiyor).

**Operatör makinenin başına geçince, TEK oturumda (K25/K26'nın asıl
doğrulaması):**
1. Google'ı yeniden bağla (yeni gmail scope + taze refresh token).
2. Microsoft'u yeniden bağla, `sync-all` koştur — sessiz yenileme
   gerçekten çalışıyor mu gör.
3. `mount <accountId>` ile hem bir PDF'e hem bir Google Doc'a eriş
   (F02 düzeltmesi de dahil test edilsin), içeriğin indiğini doğrula.
4. Hepsi başarılıysa K25/K26 "tamamlandı" işaretlenir, sıradaki FAZ 11
   (güvenlik/veri bütünlüğü sertleştirmesi) ele alınır.

