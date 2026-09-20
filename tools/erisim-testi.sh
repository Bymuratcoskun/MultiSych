#!/usr/bin/env bash
# erisim-testi.sh — DI'da kayıtlı her sayfa ViewModel'inin arayüzden
# GERÇEKTEN erişilebilir olduğunu doğrular.
#
# NEDEN VAR: 2026-09-20 denetiminde CalendarViewModel, DocumentAnalyzerViewModel,
# DocumentsViewModel, ErrorReportViewModel, AIOverviewViewModel DI'da
# AddTransient ile kayıtlıydı ama MainWindow.cs'teki sayfa switch'inde hiç
# geçmiyordu — kod var, kullanıcı hiç göremiyordu. "Dosya doğru mu" ile
# "ürün onu kullanabiliyor mu" ayrı sorulardır (bkz. docs/KURALLAR.md §2).
# Bu kapı ikinci soruyu, tek bir kod yolu için, otomatik sorar.
#
# Yöntem: Program.cs'teki "AddTransient<XViewModel>" kayıtlarından, bilinen
# diyalog/modal VM'leri (kendi penceresi olan, sayfa switch'inde YER ALMASI
# gerekmeyenler) çıkararak "sayfa VM" kümesini çıkarır. Sonra MainWindow.cs
# içinde "currentVm is XViewModel" desenini arar. Farkı = erişilemeyen VM.
#
# Bilinen istisnalar (bilinçli, buradan yönetiliyor — repo değişirse
# güncellenmeli, sessizce atlanmaz):
#   MainWindowViewModel   — sayfa değil, konteynerin kendisi
#   AddAccountViewModel   — WindowService.ShowAddAccountDialog ile modal açılır
#   NewEmailViewModel     — WindowService.ShowNewEmailDialog ile modal açılır
#   CalendarViewModel     — K5 (docs/KARARLAR.md) ile bilinçli ertelendi:
#                           ICalendarService'e hiç bağlı değil, View yazmak
#                           yarım bir özelliği erişilebilir kılardı. FAZ 2
#                           kapsamı dışı, ayrı bir iş paketi bekliyor.
set -uo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
PROGRAM_CS="$ROOT_DIR/MultiSych.Desktop/Program.cs"
MAIN_WINDOW_CS="$ROOT_DIR/MultiSych.Desktop/Views/MainWindow.cs"

if [ ! -f "$PROGRAM_CS" ] || [ ! -f "$MAIN_WINDOW_CS" ]; then
  echo "ERISIM-HATA: kaynak dosya bulunamadı ($PROGRAM_CS veya $MAIN_WINDOW_CS)"
  exit 1
fi

ISTISNALAR="MainWindowViewModel AddAccountViewModel NewEmailViewModel CalendarViewModel"

mapfile -t KAYITLI < <(grep -oP 'AddTransient<\K[A-Za-z0-9_]+ViewModel(?=>)' "$PROGRAM_CS" | sort -u)

ORPHAN=()
for vm in "${KAYITLI[@]}"; do
  if [[ " $ISTISNALAR " == *" $vm "* ]]; then
    continue
  fi
  if ! grep -q "is $vm " "$MAIN_WINDOW_CS"; then
    ORPHAN+=("$vm")
  fi
done

if [ "${#ORPHAN[@]}" -eq 0 ]; then
  echo "ERISIM-TESTI kayitli=${#KAYITLI[@]} erisilemez=0 — kapı yeşil"
  exit 0
fi

echo "ERISIM-TESTI kayitli=${#KAYITLI[@]} erisilemez=${#ORPHAN[@]}"
for vm in "${ORPHAN[@]}"; do
  echo "ERISIM-HATA: $vm — DI'da kayıtlı, MainWindow.cs sayfa switch'inde yok (nav'a bağla ya da kaydı kaldırıp docs/KARARLAR.md'e neden ertelendiğini yaz)"
done
exit 1
