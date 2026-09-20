## İŞ PAKETİ — 4 nav düğmesine gerçek View bağlanacak (AI, Analyzer, Documents, Logs)

**Yazan:** Claude Code · 2026-09-20 · **Uygulayan:** Codex
**Referans:** `docs/YOL-HARITASI.md` FAZ 2 · `docs/KURALLAR.md` ·
`~/Projelerim/KURALLAR.md` · `.kapilar.conf` kapısı: `erisim`
**Karar:** `docs/KARARLAR.md` K5 · **Araştırma kanıtı:**
`docs/RAPOR-orphan-viewmodel-durumu.md` (Antigravity, spot-check ile
doğrulandı)

### Neden

Sidebar'da "AI", "Analyzer", "Mail", "Documents", "Logs" düğmeleri
zaten görünüyor ve tıklanabiliyor (`MainWindowViewModel.cs:279-284`,
`UpdateCurrentPage()` satır 254-268 doğru ViewModel'i atıyor). Ama
`MainWindow.cs`'in görüntüleme anahtarında (`UpdateActiveView()`)
bunlar için `else if` dalı yok — kullanıcı tıklayınca gerçek View
yerine `"🚧 Bu ekran yakında (<TypeName>)"` yazan placeholder görüyor.
"Mail" zaten `EmailViewModel` için çalışıyor (bu iş paketinin konusu
değil); eksik olanlar `AIOverviewViewModel`, `DocumentAnalyzerViewModel`,
`DocumentsViewModel`, `ErrorReportViewModel`.

### Bulgu

- `docs/RAPOR-orphan-viewmodel-durumu.md`: `AIOverviewViewModel` ve
  `DocumentAnalyzerViewModel` ve `DocumentsViewModel` BİTMİŞ — gerçek
  servislere bağlı, çalışıyor. `ErrorReportViewModel` YARIM (GitHub
  gönderim URL'i yer tutucu `yourusername`) ama mevcut iki alanı
  (`IssueTitle`, `IssueDescription`, `SubmitGitHubIssueCommand`) zaten
  fonksiyonel — View bunları göstermek için yeterli.
- `MultiSych.Desktop/Views/DashboardView.cs` — kanonik View deseni
  (bkz. o dosya): `Gtk.Box`'tan türeyen sınıf, nullable `DataContext`
  property'si set edilince `InitializeBindings()` çağırıyor.
- `MultiSych.Desktop/Views/MainWindow.cs` `UpdateActiveView()` —
  her View için: alan (`_dashboardView` gibi) → yoksa oluştur +
  `_contentStack.AddNamed(view, "İsim")` → `DataContext` set et →
  `_contentStack.SetVisibleChildName("İsim")`.

### Sözleşme (değiştirilmeyecek)

`MainWindowViewModel`'deki `AIPage`, `DocumentAnalyzerPage`,
`DocumentsPage`, `ErrorReportPage` property adları ve
`UpdateCurrentPage()`'deki section string'leri (`"AI"`, `"Analyzer"`,
`"Documents"`, `"Logs"`) SABİT — bunlara dokunma, yalnız `MainWindow.cs`
tarafında tüket.

### İstenen (numaralı, somut)

1. **`Views/AIOverviewView.cs`** (yeni) — `AIOverviewViewModel.cs`'i
   oku, hangi property/command'ları var ise (muhtemelen sağlayıcı
   seçimi + sohbet başlatma) `DashboardView.cs` deseninde bir
   `Gtk.Box` View'ı yaz.
2. **`Views/DocumentAnalyzerView.cs`** (yeni) — aynı desen,
   `DocumentAnalyzerViewModel.cs`'i oku (dosya seçme/analiz sonucu
   göstermeye yarayan alanlar).
3. **`Views/DocumentsView.cs`** (yeni) — aynı desen,
   `DocumentsViewModel.cs`'i oku (1253 satır, kapsamlı; en azından
   dosya listesi + temel aksiyonları göster, VM'in HER özelliğini
   birebir UI'a dökmek zorunlu değil, ama var olan komutlardan hiçbiri
   erişilemez kalmasın).
4. **`Views/ErrorReportView.cs`** (yeni) — `IssueTitle` (Entry),
   `IssueDescription` (TextView/multi-line), `SubmitGitHubIssueCommand`'a
   bağlı bir buton. GitHub URL'inin yer tutucu olması bu View'ın işi
   DEĞİL — dokunma, sadece mevcut VM yüzeyini göster.
5. **`MainWindow.cs` `UpdateActiveView()`'e 4 yeni `else if` dalı ekle**
   (`ShowPlaceholder` çağrısından ÖNCE, mevcut dallarla aynı desende):
   `AIOverviewViewModel` → `"AI"`, `DocumentAnalyzerViewModel` →
   `"Analyzer"`, `DocumentsViewModel` → `"Documents"`,
   `ErrorReportViewModel` → `"Logs"` (stack adları `UpdateCurrentPage()`
   section string'leriyle AYNI olmalı, karışıklık olmasın).

### Fail-loud kuralları

- Bir ViewModel'in gerçek bir command'ı/property'si varsa ve yeni View
  onu hiç göstermiyorsa bu "eksik" sayılır — en azından erişilebilir
  olmalı (gerekirse basit bir liste/etiket olarak).

### Kabul ölçütü

```
bash tools/erisim-testi.sh
ERISIM-TESTI kayitli=15 erisilemez=1 — kapı yeşil YOK, DİKKAT
```
**Not:** `erisim-testi.sh` şu an "erisilemez=0" ise yeşil sayıyor;
`CalendarViewModel` bilerek ertelendiği için bu iş paketinden sonra
`erisilemez=1` kalacak ve kapı KIRMIZI görünecek — bu BEKLENEN bir
durum, hata değil. Claude Code bu iş paketini kabul ettikten sonra
`tools/erisim-testi.sh`'in `ISTISNALAR` listesine `CalendarViewModel`'i
ekleyip kapıyı yeniden yeşile çekecek (bu adım SENİN işin değil,
iş paketine dokunma).

Ayrıca:
```
dotnet build   → 0 Warning(s) 0 Error(s)
dotnet test    → mevcut 78 test hâlâ geçmeli
```

### Sınırlar

- `CalendarViewModel`'e, `tools/erisim-testi.sh`'e, `MainWindowViewModel.cs`'e
  DOKUNMA.
- Yalnız Linux'ta derlenip test edilebiliyor.
- Yeni View'lar mevcut `DashboardView.cs`/`SettingsView.cs` gibi
  dosyaların kod stiline (isimlendirme, margin/spacing değerleri)
  uysun — tutarsız bir üslup 13 bölüme yayılıp örnek kirlenmesi riski
  taşır (`~/Projelerim/KURALLAR.md` §4).

---

## Düzeltme günlüğü (varsa)

- **2026-09-20 · Codex:** `AIOverviewView`, `DocumentAnalyzerView`,
  `DocumentsView` ve `ErrorReportView` GTK4/GirCore ile eklendi. Dört
  ViewModel'in mevcut property/command yüzeyleri erişilebilir kontrollere
  bağlandı; `MainWindow.UpdateActiveView()` içine `AI`, `Analyzer`,
  `Documents` ve `Logs` stack dalları eklendi. `CalendarViewModel`,
  `MainWindowViewModel.cs` ve `tools/erisim-testi.sh` değiştirilmedi.
