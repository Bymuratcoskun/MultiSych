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

## FAZ 8 — Mount edilen dosyaların GERÇEK içeriği 🔴 EN YÜKSEK ÖNCELİK

**Sorun:** FUSE mount gerçek (kernel seviyesinde) ama içindeki dosyalar
hâlâ 0 baytlık yer tutucu. Kullanıcı sürücüyü açıp dosyaya tıkladığında
boş bir dosya görüyor — bu tam olarak projenin "sahte başarı" dediği
risk kalıbı: ürün çalışıyor GİBİ görünüyor ama asıl işi yapmıyor.

**Neden en öncelikli:** Geçen oturumun en büyük yatırımı (gerçek FUSE
mount) bu olmadan kullanıcıya somut bir fayda sağlamıyor — yalnız boş
klasörler gösteriyor.

**Adım (taslak, uygulamaya geçmeden önce netleştirilecek):**
1. `CloudMirrorFsOperations.Open`'da dosya ilk açıldığında (lazy)
   gerçek içeriği ilgili sağlayıcının API'sinden indirip yerel önbellek
   dosyasına yazma mantığı eklenecek — `CloudStorageService`'te zaten
   var olan indirme desenlerinden (`EditLocally` vb.) yararlanılabilir.
2. Büyük dosyalarda tam indirmeyi beklemek mi (basit, yavaş) yoksa
   parçalı/stream okuma mı (karmaşık, hızlı) — karar gerekiyor, ölçüm
   olmadan seçilmeyecek.
3. Yazma geri senkronu zaten `FileSystemWatcher` ile çalışıyor
   (K22) — bu adım yalnız OKUMA tarafını tamamlıyor.

**Kim:** Claude Code (mimari karar + uygulama, karmaşık/riskli alan).

---

## FAZ 9 — Token yenileme ve OAuth kapsam eksikleri

**Sorun 1 — Microsoft token yenileme başarısız:** MSAL'in
`PublicClientApplicationBuilder.Create()` çağrısı her seferinde
bellek-içi, kalıcı olmayan bir önbellekle taze nesne üretiyor;
`AcquireTokenSilent` önceki interaktif girişi hiç bulamıyor.
**Düzeltme:** MSAL'in `ITokenCache.SetBeforeAccess`/`SetAfterAccess`
API'siyle diske kalıcı bir token önbelleği bağlanacak (iki yerde de —
`AuthenticateMicrosoftAsync` ve `RefreshTokenAsync` AYNI
`PublicClientApplicationBuilder` nesnesini/önbelleğini paylaşmalı).

**Sorun 2 — Google Gmail senkronizasyonu `403 Insufficient
Permission`:** Orijinal OAuth scope dizesi (`AuthenticationService.cs:43`)
yalnız `email profile calendar drive` istiyor, gmail kapsamı hiç
istenmemiş. **Düzeltme:** scope dizesine gmail kapsamı eklenip
kullanıcının Google hesabını YENİDEN bağlaması gerekiyor (mevcut
refresh token'a sonradan kapsam eklenemez).

**Kim:** Claude Code (ikisi de kod + gerçek hesapla doğrulama
gerektiriyor).

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

## 2. SIRADAKİ TEK ADIM

**✅ Bugün (2026-10-12):** Yol haritası gerçek duruma göre yeniden
yazıldı, `derleme`/`test` kapılarındaki locale hatası düzeltildi (K24),
ölü `MainWindow.axaml.cs` silindi. `tools/kapilar` → 8/8 yeşil.

**Sıradaki: FAZ 8 — mount edilen dosyaların gerçek içeriği.** Bu,
geçen oturumun en büyük yatırımını (gerçek FUSE mount) kullanıcıya
gerçekten faydalı hale getiren tek eksik adım.
