# İŞ PAKETİ ŞABLONU

Claude Code, Codex veya Antigravity'ye iş devrederken bu şablonu
kullanır ve dosyayı `docs/IS-PAKETI-<kısa-ad>.md` olarak kaydeder.
Boş/eksik bölüm bırakan iş paketi gönderilmez.

---

## İŞ PAKETİ — <başlık>

**Yazan:** Claude Code · <tarih> · **Uygulayan:** <Codex / Antigravity>
**Referans:** `docs/YOL-HARITASI.md` FAZ <n> · `docs/KURALLAR.md` ·
`.kapilar.conf` kapısı: `<kapı adı>`

### Neden

<Bu iş neden gerekli, hangi ölçülmüş bulguya dayanıyor. Kaynak: satır
numarasıyla dosya referansı.>

### Bulgu

<Somut kanıt — kod parçası, komut çıktısı, grep sonucu. Tahmin değil.>

### Sözleşme (değiştirilmeyecek)

<Veri şeması, API imzası, dosya adı deseni gibi sabit kalması gereken
şeyler. Uygulayıcı bunları değiştiremez; değiştirmesi gerektiğini
düşünüyorsa iş paketine geri döner, kendi kararını vermez.>

### İstenen (numaralı, somut)

1. ...
2. ...

### Fail-loud kuralları

<Hangi durumlar sessizce geçilemez, hangi hata/log/istisna ile
bildirilmeli.>

### Kabul ölçütü

<Kapı komutu + beklenen çıktı, birebir. Örnek:>
```
bash tools/<ad>-testi.sh
<AD>-TESTI ... kapı yeşil
```

### Sınırlar

- `dotnet build` 0 uyarı 0 hata kalacak.
- <Dokunulmayacak dosya/alan varsa açıkça yaz.>
- <Platform kısıtı varsa (yalnız Linux'ta test edilebiliyor vb.) yaz.>

---

## Düzeltme günlüğü (varsa)

<İş geri gönderildiyse buraya tarihli not: ne kırmızıydı, neden geri
gönderildi, ne değişti.>
