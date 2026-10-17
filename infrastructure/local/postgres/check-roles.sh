#!/usr/bin/env bash
# Checks the local database roles (S00-T3). Run after `docker compose up -d` (from any directory):
#   infrastructure/local/postgres/check-roles.sh
# Exits non-zero if a check fails.
set -uo pipefail
cd "$(dirname "$0")/../../.." || exit 1

db=sportsplatform
failures=0
pass() { echo "ok   - $1"; }
fail() { echo "FAIL - $1"; failures=$((failures + 1)); }

# Runs SQL as a role inside the postgres container. Connects to the service name, not localhost: the image trusts
# 127.0.0.1 without a password, while other addresses require scram-sha-256, so passwords are really checked.
as_role() {
  local role=$1 password=$2 sql=$3
  docker compose exec -T -e PGPASSWORD="$password" postgres \
    psql -h postgres -U "$role" -d "$db" -v ON_ERROR_STOP=1 -qAt -c "$sql" 2>&1
}

if [[ ! -f .env ]]; then
  echo "No .env file: run 'cp .env.example .env' first."
  exit 1
fi
set -a
# shellcheck disable=SC1091
source .env
set +a

# Preflight: one clear error instead of eight misleading ones.
if ! preflight=$(as_role sp_migrator "$SP_MIGRATOR_PASSWORD" "SELECT 1"); then
  echo "Cannot connect to PostgreSQL as sp_migrator. Is 'docker compose up -d' healthy, and does .env match the"
  echo "passwords the volume was created with ('docker compose down -v' recreates it)?"
  echo "$preflight"
  exit 1
fi

# A denied check must fail with a permission error, not for some unrelated reason.
denied() { [[ "$1" == *"permission denied"* ]]; }

cleanup() { as_role sp_migrator "$SP_MIGRATOR_PASSWORD" "DROP SCHEMA IF EXISTS s00_check CASCADE" >/dev/null; }
cleanup
trap cleanup EXIT

as_role sp_migrator "$SP_MIGRATOR_PASSWORD" "CREATE SCHEMA s00_check" >/dev/null \
  && pass "sp_migrator can create an application schema" \
  || fail "sp_migrator cannot create an application schema"

[[ "$(as_role sp_app "$SP_APP_PASSWORD" "SELECT current_user")" == "sp_app" ]] \
  && pass "sp_app can connect with its password" || fail "sp_app cannot connect"

[[ "$(as_role sp_app "not-the-password" "SELECT 1")" == *"password authentication failed"* ]] \
  && pass "sp_app is refused with a wrong password" || fail "sp_app connected without the right password"

denied "$(as_role sp_app "$SP_APP_PASSWORD" "CREATE TABLE s00_check.t (id int)")" \
  && pass "sp_app cannot create tables in an application schema" \
  || fail "sp_app was not denied creating a table in an application schema"

denied "$(as_role sp_app "$SP_APP_PASSWORD" "CREATE TABLE public.t (id int)")" \
  && pass "sp_app cannot create tables in public" || fail "sp_app was not denied creating a table in public"

denied "$(as_role sp_app "$SP_APP_PASSWORD" "CREATE SCHEMA s00_app")" \
  && pass "sp_app cannot create schemas" || fail "sp_app was not denied creating a schema"

as_role sp_migrator "$SP_MIGRATOR_PASSWORD" "CREATE TABLE s00_check.t (id int)" >/dev/null
[[ "$(as_role sp_app "$SP_APP_PASSWORD" "INSERT INTO s00_check.t VALUES (1); SELECT count(*) FROM s00_check.t")" == "1" ]] \
  && pass "sp_app can read and write tables created by sp_migrator" \
  || fail "sp_app cannot use tables created by sp_migrator"

[[ "$(as_role sp_app "$SP_APP_PASSWORD" "SELECT rolsuper OR rolbypassrls FROM pg_roles WHERE rolname = 'sp_app'")" == "f" ]] \
  && pass "sp_app is not superuser and does not bypass RLS" || fail "sp_app is superuser or bypasses RLS"

[[ "$(as_role sp_app "$SP_APP_PASSWORD" "SELECT rolbypassrls AND NOT rolsuper FROM pg_roles WHERE rolname = 'sp_platform'")" == "t" ]] \
  && pass "sp_platform bypasses RLS and is not superuser" || fail "sp_platform role attributes are wrong"


if ((failures > 0)); then
  echo "$failures check(s) failed"
  exit 1
fi
echo "all checks passed"
