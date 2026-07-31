#!/usr/bin/env bash
# Smoke test post-despliegue: /healthz real (contra IGameStore, ver Program.cs) +
# handshake real de WebSocket contra el hub de SignalR (/hub/game). Un despliegue puede
# responder /healthz en 200 y aun así rechazar WebSocket (proxy/ingress mal
# configurado) — ese es precisamente el modo de falla que un curl simple no detecta,
# por eso la segunda verificación existe y no es opcional.
#
# Uso: ./smoke-test.sh --url https://<fqdn-o-dominio>
# Salida: 0 si ambas verificaciones pasan. Distinto de cero en cualquier otro caso —
# deploy.sh trata cualquier salida no-cero como "hacer rollback".

set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"

BASE_URL=""
while [[ $# -gt 0 ]]; do
  case "$1" in
    --url)
      BASE_URL="$2"
      shift 2
      ;;
    *)
      echo "Argumento desconocido: $1" >&2
      exit 2
      ;;
  esac
done

[[ -n "$BASE_URL" ]] || { echo "Uso: smoke-test.sh --url https://<fqdn>" >&2; exit 2; }
BASE_URL="${BASE_URL%/}"

log() { echo "[smoke-test] $*"; }
fail() {
  echo "[smoke-test] ERROR: $*" >&2
  exit 1
}

command -v curl >/dev/null 2>&1 || fail "curl no está instalado."
command -v node >/dev/null 2>&1 || fail "node no está instalado (necesario para el handshake real de WebSocket)."

log "1/2 — GET $BASE_URL/healthz"
HEALTHZ_TMP=$(mktemp)
HTTP_CODE=$(curl -s -o "$HEALTHZ_TMP" -w '%{http_code}' --max-time 20 "$BASE_URL/healthz") \
  || fail "curl a /healthz falló — ¿el servidor está arriba y accesible desde acá?"
BODY=$(cat "$HEALTHZ_TMP")
rm -f "$HEALTHZ_TMP"
[[ "$HTTP_CODE" == "200" ]] || fail "/healthz respondió $HTTP_CODE (esperaba 200). Cuerpo: $BODY"
log "   OK (200): $BODY"

log "2/2 — negotiate + WebSocket + handshake SignalR contra $BASE_URL/hub/game"
node "$SCRIPT_DIR/smoke-test-ws-check.mjs" "$BASE_URL" || fail "El handshake de WebSocket contra /hub/game falló."
log "   OK"

log "Smoke test OK — $BASE_URL responde HTTP y WebSocket correctamente."
