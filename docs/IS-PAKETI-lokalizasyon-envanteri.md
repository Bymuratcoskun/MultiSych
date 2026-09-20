## İŞ PAKETİ — Sabit UI metni envanteri (FAZ 4, adım 1/2)

**Yazan:** Claude Code · 2026-09-20 · **Uygulayan:** Antigravity
(yalnız oku + rapor yaz, kod DEĞİŞTİRME, komut ÇALIŞTIRMA)
**Referans:** `docs/YOL-HARITASI.md` FAZ 4 · `docs/KARARLAR.md` K11

### Neden

`MultiSych.Desktop/Localization/Loc.cs` + `tr.json`/`en.json` altyapısı
kuruldu (K11). Şimdi `MultiSych.Desktop/Views/*.cs` içindeki ~99 sabit
metnin `Loc.Get(key)`'e taşınması gerekiyor — ama bu HACİMLİ işi Codex'e
vermeden önce, aynı anahtarın iki View'da farklı isimle icat edilmesini
önlemek için tam bir envanter lazım.

### Görev

`MultiSych.Desktop/Views/*.cs` dosyalarının HEPSİNİ oku. Şu API'lere
geçilen, en az 2 harf içeren HER literal string'i bul:
`NewWithLabel("...")`, `Gtk.Label.New("...")`, `SetTitle("...")`,
`SetPlaceholderText("...")`, `SetTooltipText("...")`. (Bu tam olarak
`tools/lokalizasyon-testi.sh`'in aradığı desen — o script'e bakıp
kendi taramanı ona göre kalibre edebilirsin.)

Her biri için:
1. **Önerilen anahtar adı** — nokta ile ayrılmış, küçük harf, İngilizce,
   dosya/bağlama göre (örn. `accounts.add_button`, `settings.title`,
   `dashboard.header.ai_summary`). AYNI metin birden fazla yerde
   geçiyorsa AYNI anahtarı öner (tekrar anahtar icat etme).
2. **Kaynak** — dosya:satır.
3. **Türkçe metin** (zaten var) ve **önerilen İngilizce çevirisi**
   (sen üret, doğal İngilizce olsun, birebir çeviri değil).

### Çıktı

TEK dosyaya yaz: `multisych/docs/LOKALIZASYON-ENVANTERI.md`

Format:
```markdown
| Anahtar | TR | EN (önerilen) | Kaynak |
|---|---|---|---|
| accounts.title | Bağlı Hesaplar | Connected Accounts | AccountsView.cs:44 |
| accounts.add_button | Yeni Hesap Ekle ➕ | Add Account ➕ | AccountsView.cs:51 |
```

Sonda bir özet: toplam kaç benzersiz anahtar, kaç dosyada tekrar eden
metin bulundu.

### Sınırlar

- Hiçbir `.cs` dosyasını DEĞİŞTİRME.
- Hiçbir kabuk komutu ÇALIŞTIRMA.
- Yalnız `docs/LOKALIZASYON-ENVANTERI.md` dosyasını oluştur/güncelle.
- Emin olmadığın bir metin için (örn. bir log mesajı mı UI metni mi
  belirsizse) listeye "❓ EMİN DEĞİLİM" notuyla ekle, atlamadan.

### Kabul ölçütü

`docs/LOKALIZASYON-ENVANTERI.md` var olacak ve `tools/lokalizasyon-testi.sh`
çıktısındaki `sabit_metin=96` sayısına yakın (birebir eşleşmesi
gerekmez, script emoji-only vb. bazı satırları farklı sayabilir) bir
toplam anahtar sayısı içerecek — çok küçükse (örn. 10) bir şey
atlanmış demektir.
