## İŞ PAKETİ — "Sahte başarı" taraması Kademe 1 (4 bulgu)

**Yazan:** Claude Code · 2026-09-20 · **Uygulayan:** Codex
**Referans:** `docs/RAPOR-sahte-basari-taramasi.md` (Antigravity, bulgu
2.1, 4.2, 4.4, 1.2) · `~/Projelerim/KURALLAR.md` §2

### Neden

Antigravity'nin 29 bulguluk taramasından, kullanıcıyı doğrudan yanlış
bilgilendiren/yanıltan 4 tanesi (Kademe 1, operatör onayıyla) şimdi
düzeltiliyor. Kalan 22 bulgu bu iş paketinin KAPSAMI DIŞI.

### Bulgu 2.1 — Hata bildirimi yer tutucu GitHub URL'sine gidiyor

`ErrorReportViewModel.cs:36`:
```csharp
var url = $"https://github.com/yourusername/MultiSych/issues/new?...";
```
Gerçek repo (bu makinedeki git remote'tan doğrulandı):
`https://github.com/Bymuratcoskun/multisych`

**Düzeltme:** `yourusername/MultiSych` → `Bymuratcoskun/multisych`.
**Ek:** `PKGBUILD`'de de AYNI yer tutucu var (`url="https://github.com/yourusername/MultiSych"`),
onu da düzelt.

### Bulgu 4.2 — "Logları Temizle" hiçbir şey silmiyor

`SettingsViewModel.cs:60`:
```csharp
ClearLogCommand = new RelayCommand(_ => LiveLogs = "Loglar temizlendi.");
```
Log dosyası hiç dokunulmuyor; 2 saniye sonra `_logTimer` diskten eski
içeriği tekrar okuyup ekrana basıyor — kullanıcı "temizledim" sanıyor,
loglar geri geliyor.

**Düzeltme:** Gerçekten dosyayı kısalt, sonucu DÜRÜSTÇE bildir:
```csharp
ClearLogCommand = new RelayCommand(_ => ClearLog());
...
private void ClearLog()
{
    try
    {
        var logsFolder = Path.Combine(Directory.GetCurrentDirectory(), "logs");
        var latestLog = Directory.Exists(logsFolder)
            ? Directory.GetFiles(logsFolder, "multisych-*.txt").OrderByDescending(f => f).FirstOrDefault()
            : null;
        if (latestLog != null)
        {
            using var fs = new FileStream(latestLog, FileMode.Truncate, FileAccess.Write, FileShare.ReadWrite);
        }
        LiveLogs = "Loglar temizlendi.";
    }
    catch (Exception ex)
    {
        LiveLogs = $"[HATA] Loglar temizlenemedi: {ex.Message}";
    }
}
```
`ClearLogCommand`'ın mevcut tanımlandığı yerdeki `RelayCommand` satırını
yeni `ClearLog()` metoduna yönlendir, metodu sınıfın uygun bir yerine
(örn. `RestoreDatabase`'in yanına) ekle.

### Bulgu 4.4 — "📋 Loglar" menüsü GitHub issue formu açıyor

`MainWindow.cs`'teki nav etiketi (`Loc.Get("nav.logs")` → `"📋 Loglar"`)
kullanıcıyı `ErrorReportPage`'e (GitHub issue başlık/açıklama formu)
götürüyor; gerçek canlı loglar Ayarlar ekranında. İsimlendirme yanlış
beklenti yaratıyor.

**Düzeltme:** Sayfanın kendisini DEĞİŞTİRME (nav yapısı K5'te karara
bağlandı, yeniden açma) — yalnız ETİKETİ gerçeğe uydur.
`tr.json`/`en.json`'da:
```
"nav.logs": "🐞 Hata Bildir"   (tr)
"nav.logs": "🐞 Report Issue"  (en)
```
`error_report.title` zaten "Hata Bildirimi / GitHub Issue Aç" diyor —
tutarlı olacak.

### Bulgu 1.2 — Dosya önbellekleme hatası sessizce yutuluyor

`FileExplorerViewModel.cs:203`:
```csharp
catch { /* Önemsiz önbellekleme hatalarını yoksay */ }
```
Not: Bu blok bulut YÜKLEMESİNDEN SONRA çalışan bir yerel-çevrimdışı-
önbellek adımı (asıl bulut yüklemesi bu bloktan önce zaten tamamlanmış
oluyor) — yani veri kaybı riski YOK, ama hata tamamen sessiz, log bile
yok.

**Düzeltme:** Sessizce yutma, en azından logla:
```csharp
catch (Exception ex)
{
    Serilog.Log.Warning(ex, "Yerel çevrimdışı önbellek kopyası başarısız oldu (bulut yüklemesi etkilenmedi): {Path}", targetPath);
}
```

### Sözleşme (değiştirilmeyecek)

- `ErrorReportViewModel`/`SettingsViewModel`/`FileExplorerViewModel`'in
  dışa dönük komut isimleri ve imzaları DEĞİŞMEYECEK.
- Nav yapısı ("Logs" → `ErrorReportPage` eşlemesi) DEĞİŞMEYECEK — yalnız
  görünen METİN düzeltiliyor (K5'i yeniden açma).

### Kabul ölçütü

```
dotnet build   → 0 Warning(s) 0 Error(s)
dotnet test    → mevcut 78 test hâlâ geçmeli
bash tools/lokalizasyon-testi.sh → hâlâ yeşil (nav.logs değeri değişti, anahtar sayısı aynı kalmalı)
```

### Sınırlar

- `dotnet build` 0 uyarı 0 hata kalacak.
- Bu iş paketi yalnız 4 bulguyu kapsar — raporun geri kalanına
  (Kademe 2-5) DOKUNMA.
- Yalnız Linux'ta derlenip test edilebiliyor.

---

## Düzeltme günlüğü (varsa)

- **2026-09-20 — Codex:** Hata bildirimi ve `PKGBUILD` GitHub URL'leri
  gerçek `Bymuratcoskun/multisych` deposuna yönlendirildi; log temizleme
  komutu son log dosyasını gerçekten kısaltacak ve hatayı dürüstçe
  bildirecek hale getirildi; `nav.logs` anahtarının yalnız TR/EN değerleri
  hata bildirimi işlevine uygun biçimde değiştirildi; yerel çevrimdışı
  önbellek kopyalama hatası sessizce yutulmak yerine uyarı olarak loglanır
  hale getirildi. Doğrulama: `dotnet build` 0 uyarı/0 hata; `dotnet test`
  78/78; `bash tools/lokalizasyon-testi.sh` anahtar=104, sabit_metin=0,
  kapı yeşil.
