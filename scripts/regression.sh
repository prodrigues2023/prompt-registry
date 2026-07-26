#!/usr/bin/env bash
# Milestone 4, demonstrated: the golden set — not a human spot-check — decides a promotion.
# A complete version passes and ships; a "reads better, drops fields" rewrite is caught by the
# harness on the completeness slice, its failing gate blocks the promotion, and production is
# untouched. Run `make up` first. Uses only curl + the promptcheck harness.
set -euo pipefail

API="${REGISTRY_URL:-http://localhost:8080}"
NAME="checkout-summary"
GOLDEN="samples/golden/checkout-summary.golden.json"
HARNESS="dotnet run --project src/PromptRegistry.Harness/PromptRegistry.Harness.csproj -c Release --"

ver_of() { grep -o '"version":[0-9]*' | head -1 | grep -o '[0-9]*'; }
say() { printf '\n\033[1m== %s\033[0m\n' "$1"; }

say "1. Publish the complete version"
V1=$(curl -sf -X POST "$API/prompts/$NAME/versions" -H 'Content-Type: application/json' \
  -d '{"template":"Summarise the order for {{customer}}, including the order number and total.","variables":["customer"],"metadata":{"author":"paulo"}}' | ver_of)
echo "published v$V1"

say "2. Gate v$V1 against its golden set (no baseline yet -> passes, gate recorded)"
$HARNESS --prompt "$NAME" --candidate "$V1" --golden "$GOLDEN" --gate --registry "$API"

say "3. Promote v$V1 to production"
curl -sf -X PUT "$API/environments/production/prompts/$NAME" -H 'Content-Type: application/json' -d "{\"version\":$V1}"; echo

say "4. Publish the brevity rewrite (reads fine; quietly drops the order number and total)"
V2=$(curl -sf -X POST "$API/prompts/$NAME/versions" -H 'Content-Type: application/json' \
  -d '{"template":"Summarise the order for {{customer}} - be extremely brief.","variables":["customer"],"metadata":{"author":"paulo"}}' | ver_of)
echo "published v$V2"

say "5. Gate v$V2 against production (v$V1) -> the harness catches the completeness regression"
if $HARNESS --prompt "$NAME" --candidate "$V2" --golden "$GOLDEN" --gate --registry "$API"; then
  echo "UNEXPECTED: the harness did not flag a regression"; exit 1
else
  echo "(promptcheck exited non-zero on the regression, as it should)"
fi

say "6. Try to promote v$V2 -> the gate BLOCKS it (expect 409)"
if curl -s -o /dev/null -w '%{http_code}' -X PUT "$API/environments/production/prompts/$NAME" \
   -H 'Content-Type: application/json' -d "{\"version\":$V2}" | grep -q 409; then
  echo "blocked as expected"
else
  echo "UNEXPECTED: promotion was not blocked"; exit 1
fi

say "7. Production still serves v$V1 — the regression never reached it"
curl -sf "$API/environments/production/prompts/$NAME"; echo

say "Done. A property-based, per-slice, comparative test gated the promotion — no human spot-check."
