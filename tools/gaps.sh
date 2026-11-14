#!/usr/bin/env bash
# Counts the open gaps in docs/GAPS.md by size, so the trend is visible. Run from the repository root.
#   ./tools/gaps.sh
set -euo pipefail
f="${1:-docs/GAPS.md}"
open=$(awk '/^## Closed/{exit} /^\| [A-Z][0-9]+ \|/{print}' "$f")
count() { printf '%s\n' "$open" | awk -F'|' -v s="$1" '{gsub(/ /,"",$4); gsub(/ /,"",$5); if ($4==s || $5==s || $4==s"each" || $5==s"each" || $5==s"each,ongoing") n++} END{print n+0}'; }
total=$(printf '%s\n' "$open" | grep -c . || true)
closed=$(awk '/^## Closed/{p=1} p && /^\| [A-Z]+-[a-z]+ \|/{n++} END{print n+0}' "$f")
echo "Open gaps: $total (small $(count S), a cycle $(count M), several cycles $(count L)) · closed: $closed"
