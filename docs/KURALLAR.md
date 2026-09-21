# MultiSych — Kurallar (proje-özel ek)

Yazan: Claude Code (baş mühendis) · 2026-09-20 · güncellendi: aynı gün
(genel kurallar `~/Projelerim/KURALLAR.md`'e taşındı)

**Genel çok-ajanlı çalışma kuralları (roller, kabul/red süreci, sahte
başarı yasağı, kapı disiplini, rapor tarzı) artık burada değil,
`~/Projelerim/KURALLAR.md`'de — bütün projeler için ortak.** Bu dosya
yalnız o belgeye **multisych'e özgü eklenen** maddeleri taşır. Çelişki
olursa genel belge üsttedir, burası onu somutlaştırır.

Her iş paketinde ikisine birden referans verilir:
`~/Projelerim/KURALLAR.md` + bu dosya + `docs/YOL-HARITASI.md` +
`.kapilar.conf`.

---

## 1. Kod ve mimari kuralları (bu projeye özgü)

- **UI katmanı artık GTK4/libadwaita (GirCore), Avalonia/XAML DEĞİL.**
  Yeni View yazarken `.axaml` değil, `Views/*.cs` deseni izlenir
  (bkz. mevcut `Views/DashboardView.cs` örneği).
- **Her yeni sayfa ViewModel'i DI'ya eklenirken aynı commit'te
  `MainWindow.cs` sayfa switch'ine de bağlanır.** Ayrı commit'e
  bırakmak `erisim` kapısını kırar — bilerek yapılıyorsa
  `docs/KARARLAR.md`'e neden ertelendiği yazılır ve
  `tools/erisim-testi.sh` içindeki `ISTISNALAR` listesine eklenir.
- **`GirCore1007` uyarı bastırması** (`MultiSych.Desktop.csproj`)
  bilinçli ve dokümante: GirCore 0.8.0 kararlı bir subclass deseni
  yayınlayana kadar duruyor. Kaldırılmadan önce GirCore sürüm notları
  kontrol edilir.
- **`#if WINDOWS` bloklarına dokunurken** karşı platformu (Linux)
  kırmadığından emin ol — bu makinede yalnız Linux derlenip test
  edilebiliyor, Windows yolu gözle denetlenmeli.
- **Trimming/AOT ayarlarına (`TrimmerRootAssembly`) dokunma** EF Core
  reflection kırılmasını önlüyor; kaldırmadan önce publish çıktısını
  gerçekten çalıştırarak doğrula.

## 2. Bu projeye özgü kapılar

`.kapilar.conf` içindeki `guvenlik-baslangic`, `bildirim`, `erisim`,
`eksiklik` kapıları 2026-09-20 denetiminde bulunan gerçek kusurlara
karşı yazıldı ve negatif kontrolle sınandı (kırmızı çıktıkları
`docs/YOL-HARITASI.md` §0'da kayıtlı). Yeni bir kapı eklerken aynı
disiplin: önce bilinen kırmızı durumu göster, sonra düzelt.

## 3. Codex ve Antigravity'ye doğrudan bağlanma (2026-09-21'den beri)

Claude Code artık ikisine de kopyala-yapıştır olmadan, doğrudan terminalden
bağlanabiliyor:

- **Antigravity:** `~/.local/bin/agy` — çağırma yöntemi
  `~/.claude/CLAUDE.md` §8'de yazılı, headless güvenilmezliği hâlâ geçerli
  (yalnız oku+rapor işi ver).
- **Codex:** `~/.codex/packages/standalone/current/codex exec -C <proje>
  -s read-only|workspace-write "<prompt>"`. **Bilinen tuzak:** bu ikili
  dosya okuma/komut çalıştırma için ayrı bir `codex-code-mode-host`
  ikilisine ihtiyaç duyuyor; standart kurulumda eksik olabilir
  (`~/.codex/packages/standalone/current/` altında yoksa "Code Mode
  unavailable" hatası verir, TÜM araçlar sessizce başarısız olur).
  2026-09-21'de bu makinede eksikti, `~/.codex/plugins/.plugin-appserver/codex-code-mode-host`'ta
  bulunup doğru yere kopyalandı (`cp` + `chmod +x`). Yeni bir makinede/
  kurulumda aynı hata çıkarsa aynı çözüm denenir; hâlâ yoksa VS Code
  ChatGPT eklentisini yeniden kurmak/güncellemek gerekebilir.
  **Kabul öncesi her zaman salt-okunur bir sınama yap** (örn. bilinen bir
  dosyayı okutup içeriğin doğru geldiğini doğrula) — sandbox modu
  (`read-only`/`workspace-write`/`danger-full-access`) işe göre seçilir,
  varsayılan asla `danger-full-access` olmasın.

## 4. Sudo gerektiren adımlar

`.NET 10 SDK` kurulumu gibi sistem paket işlemleri önce operatöre
söylenir, onaysız çalıştırılmaz (`~/Projelerim/KURALLAR.md` §0'daki
"riskli işlemler" yetkisinin somut karşılığı).
