## İŞ PAKETİ — Bildirimleri gerçek çalışır hale getir

**Yazan:** Claude Code · 2026-09-20 · **Uygulayan:** Codex
**Referans:** `docs/YOL-HARITASI.md` FAZ 3 · `docs/KURALLAR.md` ·
`~/Projelerim/KURALLAR.md` · `.kapilar.conf` kapısı: `bildirim`

### Neden

`WindowService.ShowNotification` (`MultiSych.Desktop/Services/WindowService.cs:99-121`)
çağrılıyor, log basmıyor, hata da vermiyor — sessizce hiçbir şey
yapmıyor. Asıl gönderim satırı yorumda: `// app.SendNotification(null, notification);`.

### Bulgu (API doğrulandı — reflection ile ölçüldü, tahmin değil)

`GirCore.Gio-2.0` (0.8.0) paketinde gerçek imza:
```
Gio.Application.SendNotification(string id, Gio.Notification notification)
Gio.Notification.New(string title) / .SetBody(string) / .SetPriority(...)
```
`id` **non-null** olmalı — `null` geçmek mevcut yorumdaki hatanın bir
parçasıydı. `MainWindow.Instance?.Application` zaten `Adw.Application`
dönüyor (bkz. mevcut kod, satır 108), bu tip `Gio.Application`'dan türer,
doğrudan `SendNotification` çağrılabilir, cast gerekmez.

### Sözleşme (değiştirilmeyecek)

`ShowNotification`'ın imzası (`string title, string message,
NotificationSound sound = NotificationSound.Default`) ve `IWindowService`
arayüzündeki tanımı DEĞİŞMEYECEK — çağıran kod tabanında birden fazla
yerden kullanılıyor.

### İstenen (numaralı, somut)

1. `notification.SetBody(message)` sonrasına `notification.SetPriority(...)`
   eklemeye GEREK YOK (opsiyonel, dokunma) — yalnız asıl eksik olan
   gönderim satırını düzelt:
   ```csharp
   var app = MainWindow.Instance?.Application;
   if (app != null)
   {
       app.SendNotification($"multisych-{Guid.NewGuid():N}", notification);
   }
   else
   {
       Serilog.Log.Warning("ShowNotification: MainWindow.Instance.Application null, bildirim gönderilemedi");
   }
   ```
   (id her çağrıda benzersiz olsun ki art arda gelen bildirimler
   birbirinin yerine geçmesin/kaybolmasın — GNOME aynı id ile gelen
   bildirimi önceki bildirimin YERİNE koyar.)
2. `sound` parametresi hâlâ kullanılmıyor — bu iş paketinin kapsamı
   DIŞINDA, dokunma, ayrı bir konu (ses `SoundPlayerService.cs` üzerinden
   ayrı çağrılıyor olabilir, karıştırma).

### Fail-loud kuralları

`app` null ise (MainWindow henüz yoksa) sessizce hiçbir şey yapmadan
dönme — en azından `Serilog.Log.Warning` ile neden gönderilemediğini
söyle (yukarıdaki örnekte var).

### Kabul ölçütü

```
bash tools/bildirim-testi.sh
BILDIRIM-TESTI kapı yeşil
```
Ayrıca `dotnet build` 0 uyarı 0 hata, `dotnet test` mevcut 78 test
geçmeli.

**Manuel doğrulama (Claude Code + operatör bizzat yapacak):** Gerçek
uygulamada bir senkronizasyon tetikleyip masaüstünde gerçek bir GNOME
bildirimi göründüğünü doğrulamak.

### Sınırlar

- `dotnet build` 0 uyarı 0 hata kalacak.
- `IWindowService`/`ShowNotification` imzasına dokunma.
- Yalnız Linux'ta derlenip test edilebiliyor.

---

## Düzeltme günlüğü (varsa)

- **2026-09-20 · Codex:** `WindowService.ShowNotification`, her çağrı
  için benzersiz bir `multisych-<guid>` kimliğiyle gerçek
  `SendNotification` çağrısını yapacak şekilde düzeltildi. Uygulama
  örneği yoksa sessiz geçmek yerine warning logu eklendi. Metot imzası
  ve `sound` parametresi değiştirilmedi.
