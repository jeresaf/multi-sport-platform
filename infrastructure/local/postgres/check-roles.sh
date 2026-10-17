#!/usr/bin/env bash
# Checks the local database roles (S00-T3). Run from the repository root after `docker compose up -d`:
#   infrastructure/local/postgres/check-roles.sh
# Exits non-zero if a check fails.
set -uo pipefail

db=sportsplatform
failures=0
pass() { echo "ok   - $1"; }
fail() { echo "FAIL - $1"; failures=$((failures + 1)); }

# Runs SQL as a role over TCP (password auth), inside the postgres container.
as_role() {
  local role=$1 password=$2 sql=$3
  docker compose exec -T -e PGPASSWORD="$password" postgres \
    psql -h localhost -U "$role" -d "$db" -v ON_ERROR_STOP=1 -qAt -c "$sql" 2>&1
}

set -a
# shellcheck disable=SC1091
source .env
set +a

as_role sp_migrator "$SP_MIGRATOR_PASSWORD" "CREATE SCHEMA s00_check" >/dev/null \
  && pass "sp_migrator can create an application schema" \
  || fail "sp_migrator cannot create an application schema"

[[ "$(as_role sp_app "$SP_APP_PASSWORD" "SELECT current_user")" == "sp_app" ]] \
  && pass "sp_app can connect" || fail "sp_app cannot connect"

as_role sp_app "$SP_APP_PASSWORD" "CREATE TABLE s00_check.t (id int)" >/dev/null \
  && fail "sp_app created a table in an application schema" \
  || pass "sp_app cannot create tables in an application schema"

as_role sp_app "$SP_APP_PASSWORD" "CREATE TABLE public.t (id int)" >/dev/null \
  && fail "sp_app created a table in public" || pass "sp_app cannot create tables in public"

as_role sp_app "$SP_APP_PASSWORD" "CREATE SCHEMA s00_app" >/dev/null \
  && fail "sp_app created a schema" || pass "sp_app cannot create schemas"

as_role sp_migrator "$SP_MIGRATOR_PASSWORD" "CREATE TABLE s00_check.t (id int)" >/dev/null
[[ "$(as_role sp_app "$SP_APP_PASSWORD" "INSERT INTO s00_check.t VALUES (1); SELECT count(*) FROM s00_check.t")" == "1" ]] \
  && pass "sp_app can read and write tables created by sp_migrator" \
  || fail "sp_app cannot use tables created by sp_migrator"

[[ "$(as_role sp_app "$SP_APP_PASSWORD" "SELECT rolsuper OR rolbypassrls FROM pg_roles WHERE rolname = 'sp_app'")" == "f" ]] \
  && pass "sp_app is not superuser and does not bypass RLS" || fail "sp_app is superuser or bypasses RLS"

[[ "$(as_role sp_app "$SP_APP_PASSWORD" "SELECT rolbypassrls AND NOT rolsuper FROM pg_roles WHERE rolname = 'sp_platform'")" == "t" ]] \
  && pass "sp_platform bypasses RLS and is not superuser" || fail "sp_platform role attributes are wrong"

as_role sp_migrator "$SP_MIGRATOR_PASSWORD" "DROP SCHEMA s00_check CASCADE" >/dev/null

if ((failures > 0)); then
  echo "$failures check(s) failed"
  exit 1
fi
echo "all checks passed"
