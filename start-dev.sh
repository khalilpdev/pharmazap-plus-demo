#!/usr/bin/env bash
set -euo pipefail

API_HEALTH_URL="https://localhost:5051/health"
API_HEALTH_TIMEOUT=60

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"

echo "Iniciando PharmazapPlus.API..."
(
  cd "$SCRIPT_DIR/src/PharmazapPlus.API"
  dotnet run
) &
API_PID=$!

wait_for_api() {
  local api_health_url="$API_HEALTH_URL"
  local max_attempts="$API_HEALTH_TIMEOUT"
  local attempt=1

  if ! command -v curl >/dev/null 2>&1; then
    echo "curl não encontrado; prosseguindo sem checagem de saúde da API."
    return 0
  fi

  while [ "$attempt" -le "$max_attempts" ]; do
    if curl --silent --fail --insecure "$api_health_url" >/dev/null; then
      return 0
    fi
    sleep 1
    attempt=$((attempt + 1))
  done

  return 1
}

echo "Aguardando API ficar pronta..."
if ! wait_for_api; then
  echo "API não respondeu em até ${API_HEALTH_TIMEOUT}s; iniciando WEB mesmo assim."
fi

echo "Iniciando PharmazapPlus.Web..."
(
  cd "$SCRIPT_DIR/src/PharmazapPlus.Web"
  dotnet run
) &
WEB_PID=$!

cleanup() {
  kill "$API_PID" "$WEB_PID" 2>/dev/null || true
}
trap cleanup EXIT INT TERM

wait "$API_PID" "$WEB_PID"
