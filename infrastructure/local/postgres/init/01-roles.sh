#!/usr/bin/env bash
# Runs once, when the postgres container initialises an empty data volume.
# Creates the application database and the three roles from docs/architecture/multi-tenancy.md:
#   sp_migrator  owns the database and the module schemas (runs migrations)
#   sp_app       the API's role: data access only, no DDL, subject to RLS
#   sp_platform  BYPASSRLS, used only by explicit platform services
# Passwords come from the environment (.env), never from this file.
set -euo pipefail

psql -v ON_ERROR_STOP=1 --username "$POSTGRES_USER" --dbname postgres \
  -v db="$SP_DATABASE" \
  -v migrator_pw="$SP_MIGRATOR_PASSWORD" \
  -v app_pw="$SP_APP_PASSWORD" \
  -v platform_pw="$SP_PLATFORM_PASSWORD" <<'SQL'
CREATE ROLE sp_migrator LOGIN NOSUPERUSER NOCREATEDB NOCREATEROLE NOBYPASSRLS PASSWORD :'migrator_pw';
CREATE ROLE sp_app      LOGIN NOSUPERUSER NOCREATEDB NOCREATEROLE NOBYPASSRLS PASSWORD :'app_pw';
CREATE ROLE sp_platform LOGIN NOSUPERUSER NOCREATEDB NOCREATEROLE BYPASSRLS   PASSWORD :'platform_pw';

CREATE DATABASE :"db" OWNER sp_migrator;
REVOKE ALL ON DATABASE :"db" FROM PUBLIC;
GRANT CONNECT ON DATABASE :"db" TO sp_app, sp_platform;
SQL

psql -v ON_ERROR_STOP=1 --username "$POSTGRES_USER" --dbname "$SP_DATABASE" <<'SQL'
-- No one but the owner creates objects in public.
REVOKE CREATE ON SCHEMA public FROM PUBLIC;
ALTER SCHEMA public OWNER TO sp_migrator;

-- Schemas and tables created later by sp_migrator (module migrations) are usable, never creatable, by the
-- runtime roles. RLS policies (S02) decide which rows sp_app sees.
ALTER DEFAULT PRIVILEGES FOR ROLE sp_migrator GRANT USAGE ON SCHEMAS TO sp_app, sp_platform;
ALTER DEFAULT PRIVILEGES FOR ROLE sp_migrator GRANT SELECT, INSERT, UPDATE, DELETE ON TABLES TO sp_app, sp_platform;
ALTER DEFAULT PRIVILEGES FOR ROLE sp_migrator GRANT USAGE, SELECT ON SEQUENCES TO sp_app, sp_platform;
ALTER DEFAULT PRIVILEGES FOR ROLE sp_migrator GRANT EXECUTE ON FUNCTIONS TO sp_app, sp_platform;
SQL
