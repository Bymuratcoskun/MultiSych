#!/usr/bin/env bash
# guvenlik-baslangic-testi.sh — başlangıç güvenlik kontrollerinin (parola,
# 2FA) GTK4 arayüzünde gerçekten devrede olduğunu doğrular.
#
# NEDEN VAR: 2026-09-20 denetiminde Program.cs:148-149 kendi yorumuyla
# itiraf ediyordu: "Bu güvenlik kontrolleri şimdilik atlanıyor." Yani
# SecuritySettings.RequireStartupPassword / EnableTwoFactorAuth ayarları
# okunuyor ama hiçbir yerde UYGULANMIYOR — kullanıcı parolayı açık tutsa
# bile hiçbir engel yok. Bu statik bir kapı: gerçek bir login penceresinin
# GTK açılışına gerçekten bağlandığını arar; runtime davranışını sınamaz
# (o ayrı, elle yapılacak bir manuel adım — bkz. docs/YOL-HARITASI.md FAZ 1).
#
# Yöntem (iki koşul, ikisi de sağlanmalı):
#   1. "şimdilik atlanıyor" itiraf yorumu artık dosyada OLMAMALI.
#   2. Adw.Application.OnActivate içinde MainWindow açılmadan ÖNCE, güvenlik
#      ayarı açıkken bir login/parola akışını çağıran bir kod yolu OLMALI —
#      bu depoda karşılığı RequireStartupPassword okuyup bir kapı fonksiyonu
#      çağırmak (fonksiyon adı serbest, ama "RequireStartupPassword" adının
#      OnActivate bloğunun İÇİNDE veya ondan çağrılan bir yol üzerinde
#      geçmesi zorunlu tutulur).
set -uo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
PROGRAM_CS="$ROOT_DIR/MultiSych.Desktop/Program.cs"

if [ ! -f "$PROGRAM_CS" ]; then
  echo "GUVENLIK-HATA: $PROGRAM_CS bulunamadı"
  exit 1
fi

HATA=0

if grep -q "şimdilik atlanıyor" "$PROGRAM_CS"; then
  echo "GUVENLIK-HATA: Program.cs hâlâ başlangıç güvenlik kontrolünün atlandığını itiraf ediyor (\"şimdilik atlanıyor\" yorumu duruyor)"
  HATA=1
fi

# OnActivate bloğunu MainWindow'un Present() çağrısına kadar kabaca izole et.
ONACTIVATE_BLOK=$(awk '/OnActivate \+=/{f=1} f{print} f && /^\s*\};/{exit}' "$PROGRAM_CS")

if ! printf '%s' "$ONACTIVATE_BLOK" | grep -q "RequireStartupPassword"; then
  echo "GUVENLIK-HATA: OnActivate akışında RequireStartupPassword hiç kontrol edilmiyor — parola ayarı açık olsa da uygulanmıyor"
  HATA=1
fi

if [ "$HATA" -eq 0 ]; then
  echo "GUVENLIK-BASLANGIC-TESTI kapı yeşil"
  exit 0
fi

exit 1
