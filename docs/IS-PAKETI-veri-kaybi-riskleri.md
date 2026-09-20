## İŞ PAKETİ — 3 veri kaybı riski (ACİL)

**Yazan:** Claude Code · 2026-09-20 · **Uygulayan:** Codex
**Referans:** `docs/RAPOR-sahte-basari-taramasi.md` (Antigravity) ·
`~/Projelerim/KURALLAR.md` §2

### Neden

Antigravity'nin kod sağlığı taramasında bulunan 3 en kritik bulgu,
Claude Code tarafından kod satırlarına bakılarak BAĞIMSIZ doğrulandı —
üçü de gerçek, hepsi "sahte başarı" ailesinden (bir şey oldu deniyor
ama olmuyor ya da yanlış şeye oluyor).

### Bulgu 1 — "Yedekten Geri Yükle" yanlış dosyaya yazıyor

`SettingsViewModel.cs:329` (`RestoreDatabase`):
```csharp
var dbPath = Path.Combine(..., "MultiSych", "Database", "localcache.db");
```
Uygulamanın GERÇEKTEN kullandığı veritabanı yolu `Program.cs:281`'de:
```csharp
var databasePath = config.Database?.DatabasePath ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "MultiSych", "multisych.db");
```
Tamamen farklı bir dosya. Kullanıcı "Geri Yükle"ye basıyor,
`"[BAŞARILI] ... yeniden başlatın"` görüyor, yeniden başlatıyor —
**hiçbir şey değişmiyor.**

**Düzeltme:** `RestoreDatabase`'de `dbPath` hesaplamasını
`Program.cs:281`'deki AYNI mantıkla değiştir (`_config.Database?.DatabasePath`
zaten `SettingsViewModel`'de `_config` alanı üzerinden erişilebilir).

### Bulgu 2 — "Önbelleği Temizle" yanlış klasörü siliyor

`SettingsViewModel.cs:275` (`ClearCacheAsync`):
```csharp
var drivesPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "MultiSych", "Drives");
```
Ama GERÇEK indirilen dosyalar HER YERDE (`VirtualDriveService.cs:48`,
`AutoSyncBackgroundService.cs:177`, `PlatformMountProvider.cs:38`,
`DocumentsViewModel.cs:516`) şurada:
```csharp
Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "MultiSych_Drives", accountId)
```
Sonuç: DB'deki `CloudFiles` indeksi tamamen siliniyor (gerçek etki),
ama diskteki gerçek dosyalar silinmiyor (yanlış klasör) — hem disk
temizlenmiyor hem uygulama dosyaların varlığından habersiz kalıyor
(yetim dosyalar).

**Düzeltme:** `drivesPath` hesaplamasını yukarıdaki 4 dosyadaki AYNI
mantıkla değiştir: `Environment.SpecialFolder.UserProfile` +
`"MultiSych_Drives"` (accountId olmadan, kök klasör — tüm hesapların
klasörlerini kapsayacak şekilde).

### Bulgu 3 — İndirme başarısız olursa sessizce 0 bayt dosya açılıyor

`DocumentsViewModel.cs:539-543` (`EditLocally`'nin iç `catch` bloğu):
```csharp
catch (Exception ex)
{
    _logger.Error(ex, "Failed to download file for local editing, creating empty file instead.");
    try { await File.WriteAllBytesAsync(localFilePath, Array.Empty<byte>()); } catch { }
    LaunchLocalEditor(localFilePath);
}
```
İndirme hata verirse (ağ/yetki sorunu) boş bir dosya oluşturup
düzenleyicide açıyor. Kullanıcı bunu gerçek belgesi sanıp
düzenleyip kaydederse, senkronizasyon bu BOŞ içeriği buluttaki gerçek
dosyanın üzerine yazabilir — kalıcı veri kaybı.

**Düzeltme:** Bu catch bloğunda boş dosya YAZMA, editörü AÇMA. Bunun
yerine kullanıcıya AÇIKÇA hata bildir:
```csharp
catch (Exception ex)
{
    _logger.Error(ex, "Failed to download file for local editing.");
    _appStatusService.PostUpdate($"{file.FileName} indirilemedi, düzenleyici açılmadı: {ex.Message}", isSyncing: false);
}
```
(Fail-loud: sessizce devam etme, ama çökme de — kullanıcıya NEDEN
söyle.)

### Sözleşme (değiştirilmeyecek)

- `RestoreDatabase`/`ClearCacheAsync`/`EditLocally`'nin dışa dönük
  davranışı (hangi buton hangi metodu çağırıyor) DEĞİŞMEYECEK, yalnız
  İÇ mantıkları (yol hesaplama, hata yolu) düzeltiliyor.
- `LiveLogs` mesaj formatı (`[BAŞARILI]`/`[HATA]` öneki) korunacak,
  tutarlılık için.

### Kabul ölçütü

```
dotnet build   → 0 Warning(s) 0 Error(s)
dotnet test    → mevcut 78 test hâlâ geçmeli
```

**Manuel doğrulama (Claude Code + operatör bizzat yapacak):** Gerçek
uygulamada yedekle → geri yükle döngüsünü deneyip veritabanının
GERÇEKTEN değiştiğini doğrulamak; önbellek temizlemeyi deneyip doğru
klasörün silindiğini doğrulamak.

### Sınırlar

- `dotnet build` 0 uyarı 0 hata kalacak.
- Bu iş paketi yalnız 3 bulguyu kapsar — `docs/RAPOR-sahte-basari-taramasi.md`'deki
  DİĞER 26 bulguya DOKUNMA, onlar ayrı ayrı triyaj edilecek.
- Yalnız Linux'ta derlenip test edilebiliyor.

---

## Düzeltme günlüğü (varsa)

- **2026-09-20 — Codex:** `RestoreDatabase` etkin veritabanıyla aynı
  `_config.Database?.DatabasePath`/varsayılan yol mantığına geçirildi;
  `ClearCacheAsync` fiziksel önbelleğin gerçek `~/MultiSych_Drives`
  kökünü hedefleyecek şekilde düzeltildi; `EditLocally` indirme hatasında
  boş dosya oluşturup editör açmak yerine hatayı loglayıp kullanıcıya
  açık durum mesajı verecek hale getirildi. Doğrulama: `dotnet build`
  0 uyarı/0 hata; `dotnet test` 78/78 geçti.
