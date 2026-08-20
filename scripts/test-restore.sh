#!/usr/bin/env bash
set -euo pipefail

if [[ -f .env ]]; then
  set -a
  # shellcheck disable=SC1091
  source .env
  set +a
fi

: "${KAGU_ERP_POSTGRES_DB:?KAGU_ERP_POSTGRES_DB is required}"
: "${KAGU_ERP_POSTGRES_USER:?KAGU_ERP_POSTGRES_USER is required}"

if [[ "$KAGU_ERP_POSTGRES_DB" == kagu_erp_restore_* ]]; then
  printf '%s\n' 'A restore smoke cannot use another restore database as its source.' >&2
  exit 2
fi

source_database="$KAGU_ERP_POSTGRES_DB"
restore_database="kagu_erp_restore_$(openssl rand -hex 16)"
dump_path="/tmp/${restore_database}.dump"
if [[ ! "$restore_database" =~ ^kagu_erp_restore_[0-9a-f]{32}$ ]]; then
  printf '%s\n' 'Generated restore database name failed the safety check.' >&2
  exit 2
fi

restore_created=false
cleanup() {
  export KAGU_ERP_POSTGRES_DB="$source_database"
  if [[ "$restore_created" == true ]] && [[ "$restore_database" =~ ^kagu_erp_restore_[0-9a-f]{32}$ ]]; then
    docker compose exec -T erp-db dropdb --username "$KAGU_ERP_POSTGRES_USER" --if-exists --force "$restore_database" || true
  fi
  if [[ "$dump_path" =~ ^/tmp/kagu_erp_restore_[0-9a-f]{32}[.]dump$ ]]; then
    docker compose exec -T erp-db rm -f "$dump_path" || true
  fi
}
trap cleanup EXIT

docker compose exec -T erp-db pg_dump --username "$KAGU_ERP_POSTGRES_USER" --dbname "$source_database" --format custom --file "$dump_path"
docker compose exec -T erp-db createdb --username "$KAGU_ERP_POSTGRES_USER" --owner kagu_erp_schema_owner "$restore_database"
restore_created=true
docker compose exec -T erp-db pg_restore --username "$KAGU_ERP_POSTGRES_USER" --dbname "$restore_database" --exit-on-error "$dump_path"

export KAGU_ERP_POSTGRES_DB="$restore_database"
./scripts/test-db.sh
printf '%s\n' 'Isolated local PostgreSQL restore, migration, scope and outbox smoke checks passed.'
