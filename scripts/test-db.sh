#!/usr/bin/env bash
set -euo pipefail

if [[ -f .env ]]; then
  while IFS='=' read -r key value; do
    if [[ "$key" =~ ^[A-Z][A-Z0-9_]*$ ]] && [[ -z "${!key:-}" ]]; then
      export "$key=$value"
    fi
  done < .env
fi

required=(
  KAGU_ERP_POSTGRES_DB
  KAGU_ERP_MIGRATOR_USER
  KAGU_ERP_MIGRATOR_PASSWORD
  KAGU_ERP_APP_USER
  KAGU_ERP_APP_PASSWORD
)
for key in "${required[@]}"; do
  if [[ -z "${!key:-}" ]]; then
    printf 'Required local setting %s is missing.\n' "$key" >&2
    exit 2
  fi
done

database_host="${KAGU_ERP_POSTGRES_HOST:-127.0.0.1}"
database_port="${KAGU_ERP_POSTGRES_PORT:-55432}"
common="Host=${database_host};Port=${database_port};Database=${KAGU_ERP_POSTGRES_DB};Pooling=true;Include Error Detail=false"
export KAGU_ERP_MIGRATOR_CONNECTION_STRING="${common};Username=${KAGU_ERP_MIGRATOR_USER};Password=${KAGU_ERP_MIGRATOR_PASSWORD};Application Name=KaguERP.Migrator"
export KAGU_ERP_APP_CONNECTION_STRING="${common};Username=${KAGU_ERP_APP_USER};Password=${KAGU_ERP_APP_PASSWORD};Application Name=KaguERP.IntegrationChecks"

dotnet restore KaguERP.slnx --locked-mode
dotnet run --project src/Erp.Migrator/KaguERP.Migrator.csproj --no-restore
dotnet run --project src/Erp.Migrator/KaguERP.Migrator.csproj --no-restore
dotnet run --project tests/Integration/KaguERP.DatabaseIntegrationChecks.csproj --no-restore
