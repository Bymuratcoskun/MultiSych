## İŞ PAKETİ — "Sahte başarı" taraması Kademe 4 + 5 (temizlik + loglama)

**Yazan:** Claude Code · 2026-09-20 · **Uygulayan:** Codex
**Referans:** `docs/RAPOR-sahte-basari-taramasi.md` (bulgu 1.3-1.10,
2.2, 2.4, 2.5, 2.6, 3.1-3.4) · `docs/KARARLAR.md` K13, K16

### Neden

Kademe 1-3 (en kritik 7 bulgu) kapandı. Bu iş paketi kalan düşük/orta
riskli 18 bulguyu kapatıyor — çoğu mekanik (sessiz `catch` bloklarına
log ekleme, ölü dosya/property temizliği).

### Kademe 4 — Sessiz `catch` bloklarına log ekle (8 bulgu)

Her biri için AYNI desen: `catch { }` → `catch (Exception ex) { <sınıfın
kullandığı logger>.Warning(ex, "<açıklayıcı Türkçe mesaj>"); }`. Sınıfın
zaten kullandığı logger değişkenini kullan (`_logger`, `Serilog.Log`, ne
varsa) — yeni bir logger icat etme. Davranışı DEĞİŞTİRME (hâlâ hatayı
yutup devam et), yalnız GÖRÜNÜR kıl.

1. `PlatformMountProvider.cs:174-178` — Linux mount boş dosya oluşturma.
2. `PlatformMountProvider.cs:321-325` — Linux senkronizasyon boş dosya oluşturma.
3. `EmailService.cs:173-178` — IMAP "okundu" bayrağı sorgusu.
4. `WhisperSpeechService.cs:254-256` — konuşma sentezi iptali (yalnız Windows kod yolu).
5. `WhisperSpeechService.cs:355-362` — geçici ses dosyası silme.
6. `CloudVirtualFileSystem.cs:560-562` — Dokan yerel önbellek silme (yalnız `#if WINDOWS`).
7. `PowerStatusHelper.cs:57,78,115,141` — pil durumu okuma (4 ayrı catch, hepsine ekle).
8. `AuthViewModel.cs:100-101` — TOTP kod çözme.

### Kademe 5a — Ölü dosyaları sil (sıfır risk, doğrulandı)

Üçü de grep ile referanssız olduğu doğrulandı, güvenle silinebilir:
- `MultiSych.Services/Implementations/AccountStore.cs` (0 bayt)
- `MultiSych.Services/Implementations/SyncedIconOverlayHandler.cs` (0 bayt)
- `MultiSych.Services/Implementations/CloudFuseFileSystem.cs` (yalnız kendi içinde geçiyor, hiç kullanılmıyor — Linux/macOS FUSE desteği zaten devre dışı bırakılmış boş bir kabuk)

`MultiSych.Desktop/MainWindow.axaml.cs` (kök dizindeki, 0 bayt) —
BUNA DOKUNMA, izlenmiyor zaten (git'te yok), silme kararı gerekmiyor.

### Kademe 5b — Yanıltıcı "dışa aktarma parolası" alanını kaldır

`DocumentAnalyzerView.cs:91-102` kullanıcıya gerçek bir parola giriş
kutusu gösteriyor (`Gtk.PasswordEntry`), `DocumentAnalyzerViewModel.ExportPassword`'e
yazıyor — AMA hiçbir "Dışa Aktar" komutu YOK, hiçbir yerde bu parola
okunmuyor. Kullanıcı parola girip koruma sağladığını sanıyor, hiçbir
şey olmuyor. Bu, kullanıcıyı doğrudan yanıltan görünür bir arayüz
öğesi — sahte başarının en açık hâli.

**Düzeltme:** Gerçek bir dışa aktarma özelliği YOKSA (yok), alanı
KALDIR — yarım bir özelliği göstermek, göstermemekten kötü:
- `DocumentAnalyzerView.cs`'ten `exportRow`/`exportLabel`/`_exportPassword`
  bloğunu (satır ~91-103) sil.
- `DocumentAnalyzerViewModel.cs`'ten `ExportPassword` property'sini ve
  `_exportPassword` alanını sil.
- `tr.json`/`en.json`'dan `document_analyzer.export_password_label`
  anahtarını sil (kullanılmayacak).

### Kademe 5c — Dokan sahte-başarı API'lerine dürüst yorum ekle (davranış DEĞİŞMEYECEK)

`CloudVirtualFileSystem.cs`'teki şu metodlar gerçek bir işlem yapmadan
`DokanResult.Success` dönüyor: `CloseFile` (565), `FlushFileBuffers` (567),
`SetFileSecurity` (959-962), `LockFile`/`UnlockFile` (864, 982-985).
Bunlar Windows-only (`#if WINDOWS`), bu makinede test edilemiyor, ve
sanal/bulut destekli bir sürücü için kilit/ACL kavramlarının anlamlı
bir karşılığı yok — davranışı DEĞİŞTİRME. Yalnız her birine KISA bir
yorum ekle, örn:
```csharp
// Sanal/bulut destekli sürücüde gerçek dosya kilidi/ACL kavramı yok —
// bilinçli olarak her zaman başarı dönülüyor, davranış değişikliği DEĞİL.
```
`GetDiskFreeSpace` (891-896, sabit 1GB/512MB) için de aynı: kısa yorum
ekle (`// Yer tutucu sabit değer — gerçek kota/disk alanı hesaplanmıyor`),
DEĞER'i değiştirme.

### Sözleşme (değiştirilmeyecek)

- Kademe 4'teki HİÇBİR `catch` bloğunun DAVRANIŞI değişmeyecek —
  yalnız loglama eklenecek.
- Kademe 5c'de HİÇBİR dönüş değeri değişmeyecek — yalnız yorum eklenecek.
- Kademe 5b DIŞINDA hiçbir View/ViewModel arayüzü değişmeyecek.

### Kabul ölçütü

```
dotnet build   → 0 Warning(s) 0 Error(s)
dotnet test    → mevcut 81 test hâlâ geçmeli
bash tools/lokalizasyon-testi.sh → yeşil kalmalı (bir anahtar azalacak: 104 → 103)
```

### Sınırlar

- `dotnet build` 0 uyarı 0 hata kalacak.
- `MultiSych.Desktop/MainWindow.axaml.cs`'e (kök dizindeki) DOKUNMA.
- Yalnız Linux'ta derlenip test edilebiliyor — `#if WINDOWS` bloklarına
  yaptığın değişiklikler (yorum ekleme) derleme ile doğrulanamaz, dikkatli ol.

---

## Düzeltme günlüğü (varsa)

- **2026-09-20 — Codex:** Kademe 4'teki 8 bulguya ait 11 sessiz
  `catch` bloğuna, mevcut logger düzeni korunarak yalnız uyarı logları
  eklendi. Üç referanssız ölü dosya silindi. Yanıltıcı dışa aktarma
  parolası alanı View, ViewModel ve TR/EN sözlüklerinden kaldırıldı.
  Windows-only Dokan no-op/başarı dönüşlerine ve sabit disk alanı
  değerlerine davranışı değiştirmeyen açıklayıcı yorumlar eklendi.
  Kök `MultiSych.Desktop/MainWindow.axaml.cs` dosyasına dokunulmadı.
  Doğrulama: `dotnet build` 0 uyarı/0 hata; `dotnet test` 81/81;
  `bash tools/lokalizasyon-testi.sh` anahtar=103, sabit_metin=0, kapı
  yeşil.
