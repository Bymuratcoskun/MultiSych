#!/usr/bin/env bash
# eksiklik-testi.sh — provider'dan bağımsız, koşulsuz NotImplementedException
# sayısını izler. Sayı BASELINE'ı aşarsa kapı kırmızı olur.
#
# NEDEN VAR: 2026-09-20 denetiminde CloudStorageService.cs:1152'de
# SearchFilesAsync provider ayrımı olmadan doğrudan
# "=> throw new NotImplementedException();" idi. Diğerleri (NotSupportedException)
# en azından provider bazlı anlamlı ayrımlar; bu tek satır düz bir eksikti.
#
# BASELINE 2026-09-20'de 1 olarak başladı, aynı gün SearchFilesAsync gerçek
# bir uygulamaya (yerel önbellek araması, docs/KARARLAR.md K15) kavuşunca
# 0'a indirildi — artık bir tavan değil sıfır tolerans.
set -uo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
BASELINE=0

SAYI=$(grep -rn '=> throw new NotImplementedException();' \
  "$ROOT_DIR/MultiSych.Services" "$ROOT_DIR/MultiSych.Desktop" \
  --include="*.cs" 2>/dev/null | wc -l | tr -d ' ')

if [ "$SAYI" -gt "$BASELINE" ]; then
  echo "EKSIKLIK-HATA: koşulsuz NotImplementedException sayısı arttı ($SAYI > taban $BASELINE)"
  grep -rn '=> throw new NotImplementedException();' \
    "$ROOT_DIR/MultiSych.Services" "$ROOT_DIR/MultiSych.Desktop" --include="*.cs" 2>/dev/null
  exit 1
fi

echo "EKSIKLIK-TESTI sayi=$SAYI taban=$BASELINE — kapı yeşil"
exit 0
