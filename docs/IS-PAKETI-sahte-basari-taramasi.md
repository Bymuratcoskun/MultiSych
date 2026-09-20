## İŞ PAKETİ — "Sahte başarı" deseni taraması (kod sağlığı denetimi)

**Yazan:** Claude Code · 2026-09-20 · **Uygulayan:** Antigravity
(yalnız oku + rapor yaz, kod DEĞİŞTİRME, kabuk komutu ÇALIŞTIRMA —
salt okunur `rg`/`sed -n` kullanabilirsin, önceden onaylandı)
**Referans:** `~/Projelerim/KURALLAR.md` §2 · `docs/KARARLAR.md`

### Neden

Bu oturumda ard arda şu desende gerçek hatalar bulundu: bir metod
çağrılıyor ama hiçbir şey yapmıyordu (`ShowNotification`), bir diyalog
gerçekte olmayan bir davranışı vaat ediyordu ("arka planda çalışacak"),
bir servis yer tutucu bir URL'e sahipti (`ErrorReportViewModel`'deki
`yourusername`). Bunların HEPSİ kapılardan geçmişti çünkü kapılar
"derleniyor mu" soruyordu, "gerçekten iddia ettiğini yapıyor mu"
sormuyordu. Bu iş paketi, kod tabanının geri kalanında AYNI ailenin
başka örneklerini arıyor — Codex başka bir işte (lokalizasyon) meşgul
olduğu için paralel ilerleyecek.

### Görev

`MultiSych.Services/Implementations/*.cs` ve
`MultiSych.Desktop/ViewModels/*.cs` dosyalarının TAMAMINI oku. Şu
kalıpları ara ve her birini KANITLA (dosya:satır, ilgili kod parçası):

1. **Sessiz yutulan hata:** `catch { }` (boş gövde) ya da
   `catch (Exception ex) { }` gibi hiçbir işlem yapmayan/loglamayan
   catch blokları — özellikle kullanıcı eylemine cevap veren kod
   yollarında (buton tıklama, form gönderme). Yalnız `try { X; } catch
   { }` desenini say; `catch { return false; }` gibi en azından bir
   sinyal döndürenler daha düşük öncelikli, ayrı listelensin.
2. **Yer tutucu/sahte değerler:** `"yourusername"`, `"TODO"`, `"FIXME"`,
   `"example.com"`, `"changeme"`, `"placeholder"`, `"test@test.com"`
   gibi üretime sızmış görünen sabit değerler (test dosyaları HARİÇ —
   `MultiSych.Tests/` klasörünü TARAMA, orada bunlar normal).
3. **Hiçbir şey yapmadan `return` eden metodlar:** İmzası bir iş
   yapacağını vaat eden (adı `Save`, `Send`, `Sync`, `Delete`, `Update`
   ile başlayan) ama gövdesi boş ya da yalnız `return true;`/
   `return Task.CompletedTask;` olan metodlar.
4. **UI metninde iddia ile kodun uyuşmazlığı:** Bir buton/diyalog
   metni bir şey vaat ediyor (örn. "otomatik yedeklenecek", "arka
   planda senkronize edilecek") ama arkasındaki kod bunu yapmıyor ya
   da kısmen yapıyor. (Bu tür bir örnek zaten bulunup düzeltildi:
   `MainWindow.cs`'teki eski çıkış diyaloğu — o ARTIK düzeltilmiş
   durumda, ARANMAYACAK; BENZERİ başka yerler aranacak.)

### Çıktı

TEK dosyaya yaz: `multisych/docs/RAPOR-sahte-basari-taramasi.md`

Her bulgu için:
```markdown
## <kısa başlık>
**Dosya:Satır:** ...
**Kanıt:** (kod alıntısı)
**Kategori:** 1/2/3/4 (yukarıdaki numaralardan)
**Neden şüpheli:** (bir cümle)
```

Sonda özet: toplam kaç bulgu, kategoriye göre dağılım.

### Sınırlar

- `MultiSych.Tests/` klasörünü TARAMA (test kodunda yer tutucu normaldir).
- Hiçbir `.cs` dosyasını DEĞİŞTİRME.
- Yalnız `docs/RAPOR-sahte-basari-taramasi.md`'ye yaz.
- Emin olmadığın bir bulgu için "❓ EMİN DEĞİLİM, kontrol edilmeli"
  notuyla listele, atlama — ama kesin olmayan bir şeyi kesinmiş gibi
  sunma.

### Kabul ölçütü

Rapor dosyası var olacak, her bulgu dosya:satır kanıtına dayanacak.
Claude Code bulguları tek tek doğrulayıp gerçek olanlar için ayrı iş
paketleri açacak.
