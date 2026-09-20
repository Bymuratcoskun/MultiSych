#!/usr/bin/env bash
# bildirim-testi.sh — ShowNotification'ın gerçekten bir bildirim
# gönderdiğini, sessiz no-op olmadığını doğrular.
#
# NEDEN VAR: 2026-09-20 denetiminde WindowService.ShowNotification içindeki
# asıl çağrı yorum satırı hâlindeydi: "// app.SendNotification(null,
# notification);". Fonksiyon çağrılıyor, log basmıyor, hata da vermiyor —
# sessizce hiçbir şey yapmıyordu. Bu, docs/KURALLAR.md §2'deki "sessiz
# geçiş yasak" kuralının klasik ihlali.
set -uo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
FILE="$ROOT_DIR/MultiSych.Desktop/Services/WindowService.cs"

if [ ! -f "$FILE" ]; then
  echo "BILDIRIM-HATA: $FILE bulunamadı"
  exit 1
fi

# ShowNotification gövdesini izole et.
GOVDE=$(awk '/public void ShowNotification/{f=1} f{print; if(/^\s{4}\}/ && NR>1) c++} f && c==1{exit}' "$FILE")

if printf '%s' "$GOVDE" | grep -qE '^\s*//\s*(app\.)?SendNotification'; then
  echo "BILDIRIM-HATA: ShowNotification içinde asıl gönderim satırı yorum satırı — bildirim hiç gitmiyor"
  exit 1
fi

if ! printf '%s' "$GOVDE" | grep -qE '\bSendNotification\b'; then
  echo "BILDIRIM-HATA: ShowNotification gövdesinde aktif bir SendNotification çağrısı bulunamadı"
  exit 1
fi

echo "BILDIRIM-TESTI kapı yeşil"
exit 0
