#!/usr/bin/env bash
set -euo pipefail

for command_name in git dotnet node pnpm; do
  if ! command -v "$command_name" >/dev/null 2>&1; then
    printf 'Required command is missing: %s\n' "$command_name" >&2
    exit 1
  fi
done

new_local_secret() {
  if command -v openssl >/dev/null 2>&1; then
    openssl rand -hex 24
  else
    od -An -N24 -tx1 /dev/urandom | tr -d ' \n'
  fi
}

if [[ ! -f .env ]]; then
  erp_password="$(new_local_secret)"
  erp_migrator_password="$(new_local_secret)"
  erp_app_password="$(new_local_secret)"
  keycloak_db_password="$(new_local_secret)"
  keycloak_admin_password="$(new_local_secret)"
  keycloak_smoke_password="$(new_local_secret)"

  umask 077
  env_template="$(<.env.example)"
  env_template="${env_template/CHANGEME_LOCAL_ONLY_ERP_DB/$erp_password}"
  env_template="${env_template/CHANGEME_LOCAL_ONLY_ERP_MIGRATOR/$erp_migrator_password}"
  env_template="${env_template/CHANGEME_LOCAL_ONLY_ERP_APP/$erp_app_password}"
  env_template="${env_template/CHANGEME_LOCAL_ONLY_KEYCLOAK_DB/$keycloak_db_password}"
  env_template="${env_template/CHANGEME_LOCAL_ONLY_KEYCLOAK_ADMIN/$keycloak_admin_password}"
  env_template="${env_template/CHANGEME_LOCAL_ONLY_KEYCLOAK_SMOKE/$keycloak_smoke_password}"
  printf '%s\n' "$env_template" > .env
  printf '%s\n' 'Generated ignored .env with random local-only credentials.'
else
  missing_values=''
  if ! grep -q '^KAGU_ERP_MIGRATOR_USER=' .env; then
    missing_values="${missing_values}\nKAGU_ERP_MIGRATOR_USER=kagu_erp_migrator"
  fi
  if ! grep -q '^KAGU_ERP_MIGRATOR_PASSWORD=' .env; then
    missing_values="${missing_values}\nKAGU_ERP_MIGRATOR_PASSWORD=$(new_local_secret)"
  fi
  if ! grep -q '^KAGU_ERP_APP_USER=' .env; then
    missing_values="${missing_values}\nKAGU_ERP_APP_USER=kagu_erp_app"
  fi
  if ! grep -q '^KAGU_ERP_APP_PASSWORD=' .env; then
    missing_values="${missing_values}\nKAGU_ERP_APP_PASSWORD=$(new_local_secret)"
  fi
  if ! grep -q '^KAGU_KEYCLOAK_SMOKE_PASSWORD=' .env; then
    missing_values="${missing_values}\nKAGU_KEYCLOAK_SMOKE_PASSWORD=$(new_local_secret)"
  fi

  if [[ -n "$missing_values" ]]; then
    printf '%b\n' "$missing_values" >> .env
    printf '%s\n' 'Existing .env values preserved; missing local development settings were added.'
  else
    printf '%s\n' 'Existing .env preserved.'
  fi
fi

dotnet restore KaguERP.slnx --locked-mode
pnpm install --frozen-lockfile

if [[ "${KAGU_ERP_SKIP_SERVICES:-0}" == "1" ]]; then
  printf '%s\n' 'Service bootstrap skipped by request; dependency installation completed.'
elif command -v docker >/dev/null 2>&1; then
  docker compose config --quiet
  docker compose up --detach --wait
  ./scripts/test-db.sh
else
  printf '%s\n' 'WARNING: Docker is unavailable; local PostgreSQL and Keycloak were not started.' >&2
fi

printf '%s\n' 'Bootstrap completed. Run scripts/verify.sh for all available quality gates.'
