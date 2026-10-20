#!/usr/bin/env bash
# Starts the real API for the e2e tests (Development, http://localhost:5080) on a migrated database with the DEMO
# tenants and, when MSP_E2E_ADMIN_EMAIL is set (playwright.config.ts sets it), this run's platform administrator.
# Needs Docker's PostgreSQL and Mailpit (`docker compose up -d`) and the .NET SDK. Seeds before the API starts: a
# running API would keep its cached "unknown host" answer for a newly seeded host for up to 60 s.
set -euo pipefail

root="$(cd "$(dirname "$0")/../.." && pwd)"
cli="$root/backend/tools/SportsPlatform.Cli"

dotnet build "$root/backend" --nologo --verbosity quiet
dotnet run --no-build --project "$cli" -- migrate
dotnet run --no-build --project "$cli" -- seed --category DEMO
if [[ -n "${MSP_E2E_ADMIN_EMAIL:-}" ]]; then
  MSP_ADMIN_PASSWORD="$MSP_E2E_ADMIN_PASSWORD" \
    dotnet run --no-build --project "$cli" -- create-platform-admin --email "$MSP_E2E_ADMIN_EMAIL"
fi
exec dotnet run --no-build --project "$root/backend/src/SportsPlatform.Host"
