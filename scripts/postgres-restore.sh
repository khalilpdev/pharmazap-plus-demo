#!/usr/bin/env bash
set -euo pipefail

# Usage:
#   ./scripts/postgres-restore.sh <dump_file.sql|dump_file.sql.gz> [container_name] [pg_user] [--yes]
# Example:
#   ./scripts/postgres-restore.sh /tmp/backups/postgres_backup.sql pharmazap-postgres postgres --yes
#
# Observacao:
# - Este script restaura um dump completo (pg_dumpall).
# - Para evitar conflitos de roles/databases existentes, prefira restaurar em um cluster vazio.

if [[ $# -lt 1 ]]; then
  echo "Uso: $0 <dump_file.sql|dump_file.sql.gz> [container_name] [pg_user] [--yes]"
  exit 1
fi

DUMP_FILE="$1"
CONTAINER_NAME="${2:-pharmazap-postgres}"
PG_USER="${3:-postgres}"
CONFIRM_FLAG="${4:-}"

if [[ ! -f "$DUMP_FILE" ]]; then
  echo "Erro: arquivo de dump nao encontrado: $DUMP_FILE"
  exit 1
fi

if ! command -v docker >/dev/null 2>&1; then
  echo "Erro: docker nao encontrado no PATH."
  exit 1
fi

if ! docker ps --format '{{.Names}}' | grep -Fxq "$CONTAINER_NAME"; then
  echo "Erro: container '$CONTAINER_NAME' nao esta em execucao."
  exit 1
fi

if [[ "$CONFIRM_FLAG" != "--yes" ]]; then
  echo "Restore nao executado. Para confirmar, passe --yes como 4o argumento."
  echo "Exemplo: $0 $DUMP_FILE $CONTAINER_NAME $PG_USER --yes"
  exit 1
fi

echo "Restaurando dump em '$CONTAINER_NAME' como usuario '$PG_USER'..."

if [[ "$DUMP_FILE" == *.gz ]]; then
  gzip -cd "$DUMP_FILE" | docker exec -i "$CONTAINER_NAME" psql -v ON_ERROR_STOP=1 -U "$PG_USER" -d postgres
else
  cat "$DUMP_FILE" | docker exec -i "$CONTAINER_NAME" psql -v ON_ERROR_STOP=1 -U "$PG_USER" -d postgres
fi

echo "Restore concluido com sucesso."
