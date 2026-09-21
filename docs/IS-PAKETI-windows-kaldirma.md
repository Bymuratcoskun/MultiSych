## İŞ PAKETİ — Windows desteğini tamamen kaldır

**Yazan:** Claude Code · 2026-09-21 · **Uygulayan:** Codex
**Referans:** `docs/KARARLAR.md` K18 · `docs/KURALLAR.md`

### Neden

Operatör kararı: Windows tarafı uzun süre devre dışı kalacak, mümkünse
tamamen kaldırılsın. Bu makine yalnız Linux'ta test edilebiliyor,
Windows kodu hiç ölçülemiyor — dürüstçe kaldırılıyor.

### Sözleşme (değiştirilmeyecek)

**macOS dallarına DOKUNMA.** Yalnız `OSPlatform.Windows` ile ilgili
kod kaldırılacak; `OSPlatform.OSX`/`OSPlatform.Linux` mantığı ve genel
akış AYNEN kalacak.

### İstenen — A) Tamamen sil (3 dosya)

1. `MultiSych.Services/Implementations/CloudVirtualFileSystem.cs` —
   dosyanın TAMAMI `#if WINDOWS` içinde (1007 satır, DokanNet sanal
   sürücü). Dosyayı komple sil.
2. `MultiSych.Tests/CloudVirtualFileSystemTests.cs` — yukarıdakini
   test ediyor, tamamı `#if WINDOWS` içinde. Komple sil.
3. `MultiSych.Tests/BackgroundSyncOptimizationTests.cs` — tamamı
   `#if WINDOWS` içinde (281 satır), `CloudVirtualFileSystem`
   kullanıyor. Komple sil.

### İstenen — B) Yalnız `#if WINDOWS` bloklarını çıkar (3 dosya)

4. **`MultiSych.Services/Implementations/PlatformMountProvider.cs`:**
   - `#if WINDOWS ... #endif` içindeki `using DokanNet;` satırını sil.
   - `MountAsync`/`UnmountAsync` metodlarındaki
     `#if WINDOWS ... if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) return await MountWindowsAsync(...); #endif`
     bloklarını sil — geriye yalnız `return await MountLinuxAsync(...)`/
     `return await UnmountLinuxAsync(...)` kalsın.
   - `MountWindowsAsync`/`UnmountWindowsAsync` private metodlarını
     (tamamı `#if WINDOWS` içinde) sil.
5. **`MultiSych.Desktop/Program.cs`:**
   - `#if WINDOWS using Squirrel; #endif` satırını sil.
   - `Main` metodu başındaki `#if WINDOWS SquirrelAwareApp.HandleEvents(...) #endif` bloğunu sil.
   - `OnAppInstall`/`OnAppUpdate`/`OnAppUninstall` private metodlarını
     (tamamı `#if WINDOWS` içinde) sil.
6. **`MultiSych.Services/Implementations/WhisperSpeechService.cs`:**
   - Üç ayrı `#if WINDOWS ... #endif` bloğu var (satır ~43, ~196, ~251
     civarı, System.Speech ile TTS). Her birini sil, çevresindeki
     `if/else if` zincirinin Linux/macOS dalları (`espeak`, `say` vb.)
     AYNEN kalsın — yalnız Windows dalı gidiyor.

### İstenen — C) Çalışma zamanı `OSPlatform.Windows` dallarını kaldır (12 dosya)

Her dosyada AYNI desen: `if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) { ... } else { <linux/macOS mantığı> }`
şeklindeki bloklarda Windows dalı silinip `else` içeriği tek yol
olacak (unutma: `else if (OSX)` varsa O KALACAK, yalnız Windows
dalı gidiyor). Dosyalar:

- `MultiSych.Desktop/ViewModels/ErrorReportViewModel.cs`
- `MultiSych.Desktop/ViewModels/DocumentsViewModel.cs`
- `MultiSych.Services/Configuration/ServiceCollectionExtensions.cs`
  (satır ~61: `if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) { services.AddSingleton<IUpdateService, LinuxUpdateService>(); }`
  → koşulsuz hale getir, `LinuxUpdateService` her zaman kayıtlı olsun)
- `MultiSych.Services/Implementations/ErrorReportService.cs`
- `MultiSych.Services/Implementations/AuthenticationService.cs`
- `MultiSych.Services/Implementations/SecureStorageService.cs`
  (dikkat: `#pragma warning disable/restore CA1416` satırları da
  Windows'a özgü DPAPI kullanımıyla ilgili, Windows dalıyla BİRLİKTE
  kaldırılacak; OSX/Linux dalları kalıyor)
- `MultiSych.Services/Implementations/AutoSyncBackgroundService.cs`
  (satır ~172: `if (!IsOSPlatform(Windows)) { ... }` → koşulsuz hale
  getir, içerik her zaman çalışsın)
- `MultiSych.Services/Implementations/GoogleAuthenticationService.cs`
- `MultiSych.Services/Implementations/NAudioRecordingService.cs`
  (dikkat: `StartWindowsRecording`/ilgili Windows-özel yardımcı
  metodlar varsa onları da sil, yalnız `if` dalını değil)
- `MultiSych.Services/Implementations/VirtualDriveService.cs`
- `MultiSych.Tests/AudioRecordingServiceTests.cs`

### İstenen — D) Paket referanslarını kaldır

7. `MultiSych.Services/MultiSych.Services.csproj`: `System.Speech` ve
   `DokanNet` `PackageReference` satırlarını sil.
8. `MultiSych.Desktop/MultiSych.Desktop.csproj`: `Clowd.Squirrel`
   `PackageReference` satırını sil.
9. `MultiSych.Desktop/MultiSych.Desktop.csproj`'daki
   `<DefineConstants>$(DefineConstants);WINDOWS</DefineConstants>`
   `PropertyGroup`'unu (Windows Condition'lı) sil — `WINDOWS` sembolü
   artık hiçbir yerde tanımlanmasın.

### Fail-loud kuralları

Bir dosyada Windows dalını kaldırırken geriye kalan Linux/macOS
mantığının SÖZDİZİMSEL olarak eksiksiz kaldığından emin ol (örn. bir
`if/else if/else` zincirinden ortadaki dal silinirse zincir kopmasın).
Emin olmadığın bir dosya varsa atlama, iş paketine "soru" notu düş.

### Kabul ölçütü

```
dotnet build   → 0 Warning(s) 0 Error(s)
dotnet test    → mevcut 81 testten 3 test dosyası (CloudVirtualFileSystemTests,
                 BackgroundSyncOptimizationTests) silinince test SAYISI
                 azalacak — bu BEKLENEN, hata değil. Kalan testlerin
                 hepsi geçmeli.
bash tools/kapilar → 8/8 yeşil kalmalı
```

### Sınırlar

- `dotnet build` 0 uyarı 0 hata kalacak.
- macOS (`OSPlatform.OSX`) mantığına DOKUNMA.
- `#if WINDOWS` sembolünün TANIMLANDIĞI tek yer (Desktop.csproj'daki
  PropertyGroup) kaldırılınca, artık kalan `#if WINDOWS` bloğu
  OLMAMALI — işin sonunda `grep -rn "#if WINDOWS" --include="*.cs" .`
  boş dönmeli, bunu kendin doğrula.

---

## Düzeltme günlüğü (varsa)

- 2026-09-21 (Codex): `PlatformMountProvider.cs` içindeki Dokan/Windows
  derleme blokları ve Windows mount/unmount yardımcıları kaldırıldı; Linux
  mount/unmount yolu tek yol olarak bırakıldı.
- 2026-09-21 (Codex): C maddesinde listelenen dosyalardaki çalışma zamanı
  Windows dalları kaldırıldı. `NAudioRecordingService` içindeki NAudio alanları,
  `StartWindowsRecording` yardımcısı ve Windows durdurma akışı da silindi;
  Linux/macOS akışları korundu.
- 2026-09-21 (Codex): Windows'a özgü üç dosya tamamen silindi; Program ve
  Whisper içindeki `#if WINDOWS` blokları, Squirrel/Dokan/System.Speech paketleri
  ve üç projedeki `WINDOWS` sabit tanımları kaldırıldı. macOS koşulları ve
  içerikleri korundu; yalnız Windows dalı kalkınca gereken `else if` → `if`
  sözdizimi dönüşümleri yapıldı.
- **Soru/tespit:** İş paketi envanterinde yer almayan iki
  `OperatingSystem.IsWindows()` dalı `PowerStatusHelper.cs` içinde, bir tane de
  `SecurityHelper.cs` içinde bulundu. `PlatformMountProvider.cs` içinde de
  `#if` dışında iki Windows dalı vardı; Services ve Tests proje dosyaları da
  `WINDOWS` sabitini tanımlıyordu. "Windows desteğini tamamen kaldır" üst
  sözleşmesi gereği bunlar ve Win32 P/Invoke kaldırıldı. Baş mühendisin kapsamı
  yalnız numaralı listeyle sınırlama niyeti varsa bu ek temizlikler ayrıca
  gözden geçirilmelidir.
- Doğrulama: yalın `dotnet build`, sandbox'ın MSBuild paralel düğümlerinin
  soket açmasına izin vermemesi nedeniyle tanısız çıkış 1 verdi;
  `dotnet build MultiSych.slnx -m:1` **0 uyarı, 0 hata** ile geçti. Yalın
  `dotnet test`, VSTest TCP dinleyicisini açamadığı için
  `SocketException (13): Permission denied` ile koşumu iptal etti; testlerin
  geçtiği iddia edilmiyor. `bash tools/kapilar` bu iki altyapı engeli yüzünden
  **6/8 yeşil** döndü. `grep -rn '#if WINDOWS' --include='*.cs' .` çıktısı
  boştu (eşleşme yok, grep çıkış kodu 1).
