#!/usr/bin/env bash
set -euo pipefail

# Usage:
#   ./scripts/postgres-backup.sh [container_name] [pg_user] [output_dir]
# Example:
#   ./scripts/postgres-backup.sh pharmazap-postgres pharmazap ./backups

CONTAINER_NAME="${1:-pharmazap-postgres}"
PG_USER="${2:-postgres}"
OUTPUT_DIR="${3:-./backups}"

if ! command -v docker >/dev/null 2>&1; then
  echo "Erro: docker nao encontrado no PATH."
  exit 1
fi

if ! docker ps --format '{{.Names}}' | grep -Fxq "$CONTAINER_NAME"; then
  echo "Erro: container '$CONTAINER_NAME' nao esta em execucao."
  exit 1
fi

mkdir -p "$OUTPUT_DIR"

TIMESTAMP="$(date +%Y%m%d_%H%M%S)"
SAFE_CONTAINER_NAME="$(echo "$CONTAINER_NAME" | tr '/:' '__')"
OUTPUT_FILE="$OUTPUT_DIR/postgres_backup_${SAFE_CONTAINER_NAME}_${TIMESTAMP}.sql"

echo "Gerando backup completo (pg_dumpall) do container '$CONTAINER_NAME'..."
docker exec -t "$CONTAINER_NAME" pg_dumpall -U "$PG_USER" > "$OUTPUT_FILE"

echo "Backup concluido: $OUTPUT_FILE"
