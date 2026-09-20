#!/usr/bin/env bash
# lokalizasyon-testi.sh — FAZ 4 (docs/KARARLAR.md K12) kapısı.
#
# İKİ AYRI ŞEYİ SINAR:
#   1. Çapraz kapsam: tr.json ve en.json AYNI anahtar kümesine sahip mi.
#      (Project_Deviation'daki "çapraz kapsam ölçüldü, eksik 0" disiplini.)
#   2. Kalan sabit metin TAVANI: View dosyalarında Gtk metin API'lerine
#      (NewWithLabel, Label.New, SetTitle, SetPlaceholderText, SetTooltipText)
#      geçilen, en az 2 ardışık harf içeren, `Loc.Get(` KULLANMAYAN literal
#      string sayısı BASELINE'ı aşarsa kırmızı.
#
# SINIR (dürüstçe yazılı, gizlenmiyor): madde 2 bir grep sezgisi, %100 doğru
# değil — emoji/ikon-yalnız etiketler, log mesajları gibi yanlış pozitifler
# olabilir. Bu yüzden "0 olsun" değil "BASELINE'ı aşmasın" mantığı kullanılıyor;
# amaç yeni sabit metin eklenmesini yakalamak, geçmişi tek seferde temizlemek
# ayrı bir iştir (bkz. docs/YOL-HARITASI.md FAZ 4, Codex iş paketi).
#
# BASELINE 2026-09-20'de 99 olarak başladı, aynı gün Codex'in taşıma işi
# (docs/IS-PAKETI-lokalizasyon-tasima.md) 0'a indirdi — artık bu bir TAVAN
# değil, bir SIFIR TOLERANS: yeni eklenen hiçbir View sabit metin
# içermemeli, hepsi Loc.Get(...) kullanmalı.
set -uo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
TR="$ROOT_DIR/MultiSych.Desktop/Localization/tr.json"
EN="$ROOT_DIR/MultiSych.Desktop/Localization/en.json"
VIEWS_DIR="$ROOT_DIR/MultiSych.Desktop/Views"
BASELINE=0

HATA=0

# --- 1. Çapraz kapsam ---
if [ ! -f "$TR" ] || [ ! -f "$EN" ]; then
  echo "LOKALIZASYON-HATA: tr.json veya en.json bulunamadı"
  exit 1
fi

TR_KEYS=$(python3 -c "import json,sys; print('\n'.join(sorted(json.load(open('$TR')).keys())))" 2>&1) \
  || { echo "LOKALIZASYON-HATA: tr.json geçersiz JSON: $TR_KEYS"; exit 1; }
EN_KEYS=$(python3 -c "import json,sys; print('\n'.join(sorted(json.load(open('$EN')).keys())))" 2>&1) \
  || { echo "LOKALIZASYON-HATA: en.json geçersiz JSON: $EN_KEYS"; exit 1; }

EKSIK_EN=$(comm -23 <(echo "$TR_KEYS") <(echo "$EN_KEYS"))
EKSIK_TR=$(comm -13 <(echo "$TR_KEYS") <(echo "$EN_KEYS"))

if [ -n "$EKSIK_EN" ]; then
  echo "LOKALIZASYON-HATA: en.json'da eksik anahtarlar:"
  echo "$EKSIK_EN" | sed 's/^/  /'
  HATA=1
fi
if [ -n "$EKSIK_TR" ]; then
  echo "LOKALIZASYON-HATA: tr.json'da eksik anahtarlar:"
  echo "$EKSIK_TR" | sed 's/^/  /'
  HATA=1
fi

# --- 2. Kalan sabit metin tavanı ---
# NOT (2026-09-20 dersi): ilk sürüm yalnız DOĞRUDAN Label.New("...") gibi,
# tırnak paranteze bitişik çağrıları yakalıyordu; MainWindow.cs'teki
# AddNavigationRow("id", "Panel") gibi YARDIMCI METODA geçilen literal'ler
# kaçtı — operatör gerçek kullanımda fark etti (kenar çubuğu hep Türkçe
# kaldı). Bu yüzden AddNavigationRow için AYRI, hedefli bir ikinci desen
# eklendi. Bu iki desen bile tam kapsam GARANTİSİ değil (bkz. dosya başı
# SINIR notu) — örn. $"...gömülü metin..." biçimindeki interpolasyonlar
# BİLEREK kapsam dışı: onlar format-string bazlı ayrı bir lokalizasyon
# deseni gerektiriyor, bu FAZ'ın konusu değil (ayrı iş: format-string
# lokalizasyonu, henüz planlanmadı).
DOGRUDAN=$(grep -rEo '(NewWithLabel|Label\.New|SetTitle|SetPlaceholderText|SetTooltipText)\("[^"]*[A-Za-zÇĞİÖŞÜçğıöşü]{2,}[^"]*"\)' "$VIEWS_DIR"/*.cs 2>/dev/null \
  | grep -v "Loc\.Get(" | wc -l)
NAV_SATIRLARI=$(grep -rEo 'AddNavigationRow\("[^"]+", *"[^"]*[A-Za-zÇĞİÖŞÜçğıöşü]{2,}[^"]*"\)' "$VIEWS_DIR"/*.cs 2>/dev/null \
  | grep -v "Loc\.Get(" | wc -l)
SAYI=$((DOGRUDAN + NAV_SATIRLARI))

if [ "$SAYI" -gt "$BASELINE" ]; then
  echo "LOKALIZASYON-HATA: kalan sabit metin sayısı arttı ($SAYI > taban $BASELINE) — yeni kod Loc.Get(...) kullanmalı"
  HATA=1
fi

if [ "$HATA" -eq 0 ]; then
  echo "LOKALIZASYON-TESTI anahtar=$(echo "$TR_KEYS" | wc -l) sabit_metin=$SAYI taban=$BASELINE — kapı yeşil"
  exit 0
fi

exit 1
