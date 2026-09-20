## İŞ PAKETİ — View'larda biriken event handler'ları durdur (ACİL)

**Yazan:** Claude Code · 2026-09-20 · **Uygulayan:** Codex
**Referans:** `docs/KURALLAR.md` · `~/Projelerim/KURALLAR.md`
**Karar:** `docs/KARARLAR.md` K7

### Neden

Operatör gerçek kullanımda `~/MultiSych_Drives/acc_1 dosyası veya
klasörü yok` hata penceresinin **3 kez aynı anda** çıktığını bildirdi.
Kök sebep: `AccountsView.cs:16-23`'teki `DataContext` setter, değer her
atandığında koşulsuz `InitializeBindings()` çağırıyor ve orada
`_viewModel.Accounts.CollectionChanged` aboneliği ekleniyor —
eskisi hiç kaldırılmadan. `MainWindow.cs` View örneğini önbellekten
kullanıyor ama `DataContext`'i her navigasyonda yeniden atıyor, yani
sayfaya N kez gidilince N abonelik birikiyor; bir sonraki koleksiyon
değişimi yan etkili kodu (mount, xdg-open, muhtemelen mesaj gönderme)
N kez tetikliyor.

### Bulgu

Aynı desen (`grep -n "InitializeBindings" MultiSych.Desktop/Views/*.cs`)
şu 8 dosyada var: `AccountsView.cs`, `ChatView.cs`, `DashboardView.cs`,
`EmailView.cs`, `FileExplorerView.cs`, `MainWindow.cs`, `SettingsView.cs`,
`SyncView.cs`. `ChatView`/`EmailView` özellikle riskli — orada aynı
desen tekrarlanan mesaj gönderimine yol açabilir.

### Sözleşme (değiştirilmeyecek)

`InitializeBindings()` metodlarının İÇERİĞİNE dokunma (ne event'e
abone oluyorlarsa aynen kalsın). Yalnız setter'a bir koruma ekleniyor.

### İstenen (numaralı, somut)

Yukarıdaki 8 dosyanın HER birinde, `DataContext` setter'ının EN BAŞINA
şu koruma eklenecek (mevcut iki farklı yazım biçimi var, ikisine de
uyarlanacak):

```csharp
set
{
    if (ReferenceEquals(_viewModel, value)) return;
    _viewModel = value;
    if (_viewModel != null) InitializeBindings();
}
```

`AccountsView.cs`'in tam hâli örnek (satır 13-24):
```csharp
public AccountsViewModel? DataContext
{
    get => _viewModel;
    set
    {
        if (ReferenceEquals(_viewModel, value)) return;
        _viewModel = value;
        if (_viewModel != null)
        {
            InitializeBindings();
        }
    }
}
```

`MainWindow.cs`'teki setter da AYNI korumayı alacak (satır ~36-41),
tutarlılık için — orada risk düşük ama desen tutarlı kalmalı.

Her dosyanın kendi `_viewModel` alan adını ve mevcut null-check
biçimini (`if (_viewModel != null) InitializeBindings();` tek satır ya
da blok) koru, yalnız başa `ReferenceEquals` koruması ekle.

### Fail-loud kuralları

Bu iş paketi bir "sessizce düzelt" değil — eğer bir View'da
`DataContext` setter'ı yukarıdaki iki kalıptan FARKLI bir biçimdeyse
(örn. property değil de ayrı bir `Bind()` metodu varsa), o dosyayı
atlama, iş paketine not düş, Claude Code'a sor.

### Kabul ölçütü

```
dotnet build   → 0 Warning(s) 0 Error(s)
dotnet test    → mevcut 78 test hâlâ geçmeli
```

**Manuel doğrulama (Claude Code + operatör bizzat yapacak):**
Gerçek uygulamada Accounts sayfasına 3 kez gidip gelip sonra "Sürücüyü
Bağla"ya bir kez tıklayarak hata penceresinin artık yalnız 1 kez (ya da
hiç, klasör zaten varsa) çıktığını doğrulamak.

### Sınırlar

- `dotnet build` 0 uyarı 0 hata kalacak.
- `InitializeBindings()` gövdelerine dokunma, yalnız setter'a koruma
  ekle.
- Yalnız Linux'ta derlenip test edilebiliyor.

---

## Düzeltme günlüğü (varsa)

- **2026-09-20 · Codex:** Listelenen 8 View'ın `DataContext`
  setter'ına, aynı ViewModel yeniden atandığında `InitializeBindings()`
  çağrısını atlayan `ReferenceEquals(_viewModel, value)` koruması
  eklendi. Mevcut null-check biçimleri ve `InitializeBindings()`
  gövdeleri değiştirilmedi.
