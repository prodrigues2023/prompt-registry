#!/usr/bin/env bash
# The registry's thesis, demonstrated end to end against a running instance (make up first):
# a prompt is published, tested, promoted, a regression is BLOCKED by its failing gate, and a
# bad promotion is rolled back in one operation. Uses only curl — no jq required.
set -euo pipefail

API="${REGISTRY_URL:-http://localhost:8080}"
NAME="checkout-summary"

say() { printf '\n\033[1m== %s\033[0m\n' "$1"; }
post() { curl -sf -X "$1" "$API$2" -H 'Content-Type: application/json' -d "$3"; echo; }
get()  { curl -sf "$API$1"; echo; }

say "1. Publish v1 of $NAME"
post POST "/prompts/$NAME/versions" \
  '{"template":"Summarise the order for {{customer}}.","variables":["customer"],"metadata":{"author":"paulo"}}'

say "2. Run its golden-set test — it passes"
post POST "/prompts/$NAME/versions/1/test" '{"passed":true,"details":{"win_rate":0.91}}'

say "3. Promote v1 to staging, then to production"
post PUT "/environments/staging/prompts/$NAME"    '{"version":1}'
post PUT "/environments/production/prompts/$NAME"  '{"version":1}'

say "4. Application resolves prompt://$NAME@production"
get "/environments/production/prompts/$NAME"

say "5. Publish v2 — and its test FAILS (a regression)"
post POST "/prompts/$NAME/versions" \
  '{"template":"ORDER SUMMARY: {{customer}} - be extremely brief.","variables":["customer"],"metadata":{"author":"paulo"}}'
post POST "/prompts/$NAME/versions/2/test" '{"passed":false,"details":{"win_rate":0.62}}'

say "6. Try to promote v2 to production — the gate BLOCKS it (expect 409)"
if curl -s -o /dev/null -w '%{http_code}' -X PUT "$API/environments/production/prompts/$NAME" \
   -H 'Content-Type: application/json' -d '{"version":2}' | grep -q 409; then
  echo "blocked as expected — production still serves v1"
else
  echo "UNEXPECTED: promotion was not blocked"; exit 1
fi

say "7. Force a bad promotion (simulating a human override), then roll it back"
post PUT  "/environments/production/prompts/$NAME"          '{"version":2,"force":true}'
echo "production now on v2 (the bad one). Rolling back..."
post POST "/environments/production/prompts/$NAME/rollback" ''

say "8. Production is back on v1 — verify"
get "/environments/production/prompts/$NAME"

say "Done. Immutable versions, a gate that blocks regressions, and a one-operation rollback."
