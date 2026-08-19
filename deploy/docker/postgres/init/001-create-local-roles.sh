#!/usr/bin/env bash
set -Eeuo pipefail

: "${POSTGRES_DB:?POSTGRES_DB is required}"
: "${POSTGRES_USER:?POSTGRES_USER is required}"
: "${KAGU_ERP_MIGRATOR_PASSWORD:?KAGU_ERP_MIGRATOR_PASSWORD is required}"
: "${KAGU_ERP_APP_PASSWORD:?KAGU_ERP_APP_PASSWORD is required}"

psql --quiet --set ON_ERROR_STOP=1 \
  --username "$POSTGRES_USER" \
  --dbname "$POSTGRES_DB" \
  --set database_name="$POSTGRES_DB" \
  --set migrator_password="$KAGU_ERP_MIGRATOR_PASSWORD" \
  --set app_password="$KAGU_ERP_APP_PASSWORD" <<'SQL'
SELECT 'CREATE ROLE kagu_erp_schema_owner NOLOGIN NOSUPERUSER NOCREATEDB NOCREATEROLE NOREPLICATION NOBYPASSRLS'
WHERE NOT EXISTS (SELECT FROM pg_catalog.pg_roles WHERE rolname = 'kagu_erp_schema_owner') \gexec

SELECT format(
  'CREATE ROLE kagu_erp_migrator LOGIN PASSWORD %L NOSUPERUSER NOCREATEDB NOCREATEROLE NOINHERIT NOREPLICATION NOBYPASSRLS',
  :'migrator_password'
)
WHERE NOT EXISTS (SELECT FROM pg_catalog.pg_roles WHERE rolname = 'kagu_erp_migrator') \gexec

SELECT format(
  'CREATE ROLE kagu_erp_app LOGIN PASSWORD %L NOSUPERUSER NOCREATEDB NOCREATEROLE NOINHERIT NOREPLICATION NOBYPASSRLS',
  :'app_password'
)
WHERE NOT EXISTS (SELECT FROM pg_catalog.pg_roles WHERE rolname = 'kagu_erp_app') \gexec

ALTER ROLE kagu_erp_migrator PASSWORD :'migrator_password';
ALTER ROLE kagu_erp_app PASSWORD :'app_password';
ALTER ROLE kagu_erp_schema_owner SET search_path = pg_catalog, platform, org;
ALTER ROLE kagu_erp_migrator SET search_path = pg_catalog;
ALTER ROLE kagu_erp_app SET search_path = pg_catalog, platform, org;

GRANT kagu_erp_schema_owner TO kagu_erp_migrator;
ALTER DATABASE :"database_name" OWNER TO kagu_erp_schema_owner;
GRANT CONNECT ON DATABASE :"database_name" TO kagu_erp_migrator, kagu_erp_app;
REVOKE CREATE ON SCHEMA public FROM PUBLIC;
SQL
