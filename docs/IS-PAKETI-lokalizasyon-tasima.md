## İŞ PAKETİ — 99 sabit metni Loc.Get()'e taşı (FAZ 4, adım 2/2)

**Yazan:** Claude Code · 2026-09-20 · **Uygulayan:** Codex
**Referans:** `docs/YOL-HARITASI.md` FAZ 4 · `docs/KARARLAR.md` K11 ·
`docs/LOKALIZASYON-ENVANTERI.md` (Antigravity, kabul edildi)

### Neden

`MultiSych.Desktop/Localization/Loc.cs` + `tr.json`/`en.json` altyapısı
kuruldu, `docs/LOKALIZASYON-ENVANTERI.md` 99 literal kullanımını 93
benzersiz anahtara eşledi. Şimdi asıl taşıma yapılacak.

### Sözleşme (değiştirilmeyecek)

- **Anahtar adları `docs/LOKALIZASYON-ENVANTERI.md`'deki TABLODAN
  BİREBİR alınacak** — farklı bir isim icat etme. Aynı metin birden
  fazla kaynakta geçiyorsa (örn. `common.send`, `common.cancel`,
  `common.refresh_button`) TEK anahtar, TÜM kaynaklarda kullanılacak.
- `Loc.Get(string key)` imzası SABİT, `MultiSych.Desktop.Localization`
  namespace'inden gelir.

### İstenen (numaralı, somut)

1. **`tr.json` ve `en.json`'ı doldur** — envanterdeki 93 anahtarın
   HEPSİ, iki dilde de. Üç kategori için ÖZEL talimat:
   - **Marka/sürüm kimliği** (`app.title`, `app.window_title`,
     `app.version_label`): İKİ dilde de AYNI değer (çevrilmez).
     Envanterdeki değer doğru.
   - **Zaten İngilizce olan 4 anahtar** — `en.json`'da envanterdeki
     gibi kalsın, ama `tr.json`'da GERÇEK Türkçe çeviri kullan (baş
     mühendis kararı, envanterdeki "TR" sütununu bu 4'ü için YOK SAY):
     ```
     main.ai_assistants_title → tr: "AI ASİSTANLARI"
     main.ram_usage_label     → tr: "RAM Kullanımı:"
     common.calculating       → tr: "Hesaplanıyor..."
     common.ready             → tr: "Hazır."
     ```
   - **Diğer tüm anahtarlar:** `tr.json` = envanterin "TR" sütunu,
     `en.json` = envanterin "EN (önerilen)" sütunu, birebir.
2. **16 View dosyasının HEPSİNDE**, envanterdeki `Kaynak` sütununa göre,
   ilgili literal string'i `Loc.Get("anahtar")` ile değiştir. Örnek:
   ```csharp
   // önce:
   var title = Gtk.Label.New("Belgeler");
   // sonra:
   var title = Gtk.Label.New(Loc.Get("documents.title"));
   ```
   Gerekli dosyalara `using MultiSych.Desktop.Localization;` ekle.
3. **Emoji'ler anahtarın İÇİNDE kalsın** (örn. `documents.refresh_button`
   değeri `"Yenile 🔄"` olarak JSON'da durur) — kodda ayrıca emoji
   birleştirme yapma, envanterdeki değerler zaten emoji dahil.

### Fail-loud kuralları

Bir literal envanterde YOKSA (envanter 99 kullanım buldu, ama sen
taramada 100. bir tane bulursan) onu da makul bir anahtarla ekle,
`tr.json`/`en.json`'a işle — sessizce atlama, ama iş paketine not düş.

### Kabul ölçütü

```
bash tools/lokalizasyon-testi.sh
```
Bu iş paketinden sonra `sabit_metin=` sayısı **BASELINE (99)'un ÇOK
altına** düşmeli (idealde 0'a yakın — geriye yalnız gerçekten
çevrilmemesi gereken şeyler, örn. hata log mesajları kalabilir, bunlar
zaten script'in taradığı 5 API'ye girmiyor). Script'i DEĞİŞTİRME —
BASELINE'ı düşürmek Claude Code'un işi, kabul sonrası yapılacak.

Ayrıca:
```
dotnet build   → 0 Warning(s) 0 Error(s)
dotnet test    → mevcut 78 test hâlâ geçmeli
```

**Manuel doğrulama (Claude Code + operatör bizzat yapacak):** Gerçek
uygulamayı hem TR hem EN ayarıyla açıp (Ayarlar > Dil) gözle
karşılaştırmak.

### Sınırlar

- `dotnet build` 0 uyarı 0 hata kalacak.
- `Loc.cs`'e DOKUNMA (mantığı değil, yalnız JSON içeriğini ve View
  dosyalarını değiştiriyorsun).
- `tools/lokalizasyon-testi.sh`'e DOKUNMA.
- Yalnız Linux'ta derlenip test edilebiliyor.

---

## Düzeltme günlüğü (varsa)

- **2026-09-20 — Codex:** Envanterdeki 93 anahtar `tr.json` ve
  `en.json` dosyalarına eklendi; dört özel Türkçe çeviri iş paketindeki
  değerlerle uygulandı. 16 View dosyasındaki 99 literal kullanım,
  envanterdeki anahtar adları birebir korunarak `Loc.Get(...)` çağrılarına
  taşındı. Doğrulama: `dotnet build` 0 uyarı/0 hata; `dotnet test` 78/78;
  `bash tools/lokalizasyon-testi.sh` anahtar=93, sabit_metin=0, kapı yeşil.
