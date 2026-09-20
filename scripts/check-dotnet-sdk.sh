#!/usr/bin/env bash
# check-dotnet-sdk.sh — kaynaktan derleyen katkıcı için .NET 10+ SDK kontrolü.
#
# NEDEN VAR: Bu proje net10.0 hedefliyor. 2026-09-20'de bu makinede yalnız
# .NET 8 SDK kuruluydu ve `dotnet build` NETSDK1045 ile patladı — sebep
# açık değildi, kullanıcı kendi başına çözmek zorunda kalırdı.
#
# ÖNEMLİ SINIR: Bu script yalnız KAYNAKTAN DERLEYEN katkıcılar için anlamlı.
# Son kullanıcıya dağıtılan .deb ve AUR paketleri self-contained yayınlanıyor
# (bkz. build-deb-package.sh, PKGBUILD) — çalışma zamanı pakete gömülü,
# kurulu uygulamayı çalıştıran hiçbir kullanıcının sisteminde .NET kurulu
# olması gerekmiyor. Bu script o senaryoyu KAPSAMAZ, çünkü o senaryoda
# zaten tetiklenecek bir "eksik runtime" durumu yok.
set -uo pipefail

GEREKEN_ANA_SURUM=10
INDIRME_URL="https://dotnet.microsoft.com/download/dotnet/10.0"

if ! command -v dotnet >/dev/null 2>&1; then
  echo "HATA: 'dotnet' komutu bulunamadı. .NET SDK hiç kurulu değil."
  echo "Kurulum sayfası: $INDIRME_URL"
  if command -v xdg-open >/dev/null 2>&1 && [ -n "${DISPLAY:-}${WAYLAND_DISPLAY:-}" ]; then
    xdg-open "$INDIRME_URL" >/dev/null 2>&1 &
  fi
  exit 1
fi

BULUNAN=$(dotnet --list-sdks 2>/dev/null | awk '{print $1}' | cut -d. -f1 | sort -rn | head -1)

if [ -z "$BULUNAN" ] || [ "$BULUNAN" -lt "$GEREKEN_ANA_SURUM" ]; then
  echo "HATA: .NET $GEREKEN_ANA_SURUM veya üzeri SDK bulunamadı."
  echo "Kurulu SDK'lar:"
  dotnet --list-sdks 2>/dev/null | sed 's/^/  /'
  echo "Kurulum sayfası: $INDIRME_URL"
  if command -v xdg-open >/dev/null 2>&1 && [ -n "${DISPLAY:-}${WAYLAND_DISPLAY:-}" ]; then
    xdg-open "$INDIRME_URL" >/dev/null 2>&1 &
    echo "(Tarayıcı açılmaya çalışıldı.)"
  fi
  exit 1
fi

echo "DOTNET-SDK-TESTI en_yuksek_ana_surum=$BULUNAN kapı yeşil"
exit 0
