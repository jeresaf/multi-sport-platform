# MSP — multi-sport competition platform

A multi-tenant platform for running sports competitions: people, teams and venues → competitions and editions →
fixtures → live scoring → approved results → standings → public sites.

- New here? Start with [README-FIRST.md](README-FIRST.md).
- Documentation: [docs/](docs/) — architecture, domain, decisions (ADRs) and the build plan.
- Instructions for Claude Code: [CLAUDE.md](CLAUDE.md).
- CI: [.github/workflows/ci.yml](.github/workflows/ci.yml) runs on every pull request and on pushes to `main`:
  backend build/test/format, web lint/typecheck/test/build, Playwright smoke tests, and the local-infrastructure
  role checks.

## Repository layout

```text
backend/   .NET 10 modular monolith (SportsPlatform.slnx)
infrastructure/local/   PostgreSQL init and role-check scripts for docker compose
web/       pnpm + Turborepo: apps/public (port 3000), apps/admin (port 3001), packages/*, e2e/
docs/      product, architecture, domain, decisions, build plan
```

## Local setup

Prerequisites: Git, Docker (Docker Desktop on Windows/macOS; Docker Engine with the compose plugin on Linux),
the .NET 10 SDK and Node.js 24 (`corepack enable`).

```bash
git clone https://github.com/jeresaf/multi-sport-platform.git && cd multi-sport-platform
cp .env.example .env                              # development-only values; .env is git-ignored
docker compose up -d                              # PostgreSQL 17 + Mailpit
docker compose ps                                 # wait until both show "healthy"
infrastructure/local/postgres/check-roles.sh      # proves the database roles (sp_migrator, sp_app, sp_platform)
```

- PostgreSQL: `localhost:5432`, database `sportsplatform`. The API connects as `sp_app` (no DDL, subject to
  row-level security); migrations run as `sp_migrator`; `sp_platform` (bypasses RLS) is for platform services only.
  See docs/architecture/multi-tenancy.md.
- Connection strings: the backend uses three named connection strings, one per role — `App` (`sp_app`),
  `Migrator` (`sp_migrator`) and `Platform` (`sp_platform`). Development values are in
  `backend/src/SportsPlatform.Host/appsettings.Development.json` and match `.env.example` (public, development-only;
  if you changed `POSTGRES_PORT`, override them with `ConnectionStrings__App=...` etc.). Production values come
  from Azure Key Vault (ADR-0016), never from files.
- Migrations: `dotnet run --project backend/tools/SportsPlatform.Cli -- migrate` (as `sp_migrator`). In
  Development the API also migrates on startup (`Database:MigrateOnStartup`); in any other environment
  that setting stops the API at startup, so only the CLI ever holds the `sp_migrator` credentials.
- Seed data: `dotnet run --project backend/tools/SportsPlatform.Cli -- seed --category REFERENCE` (or
  `CONFIGURATION`, `DEMO`, `TEST`; lower categories run first, unchanged seeders are skipped). Add `--reset` to drop
  every module schema and migrate again first (Development only, and only a local database). `DEMO` and `TEST` run only in Development,
  Staging and Testing.
- Mailpit: SMTP `localhost:1025`, inbox at http://localhost:8025.
- Optional OpenTelemetry dashboard: `docker compose --profile observability up -d`, then http://localhost:18888
  (OTLP gRPC on `localhost:4317`). `dotnet run` of the API exports traces and metrics there (launchSettings sets
  `OTEL_EXPORTER_OTLP_ENDPOINT`); logs stay on the console as JSON.
- Health: http://localhost:5080/health/live (process) and http://localhost:5080/health/ready (database).
- Reset everything: `docker compose down -v` (deletes the database volume; the init script runs again next time).
- Port 5432 already in use (a local PostgreSQL)? Set `POSTGRES_PORT=5433` in `.env`.

Platform notes:

- **Windows:** use Docker Desktop with the WSL 2 backend and run the commands in Git Bash or WSL. The repository
  keeps `.sh` files with LF line endings (`.gitattributes`); if you cloned before that, re-clone.
- **macOS:** Docker Desktop (Apple silicon is fine; all images are multi-arch).
- **Linux:** Docker Engine + the compose plugin (`docker compose version`); add your user to the `docker` group or
  use `sudo`.

## Build and test (backend)

Requires the .NET 10 SDK (see `global.json`).

```bash
dotnet build backend -warnaserror
dotnet test --solution backend                    # integration tests start their own PostgreSQL container (Docker)
dotnet format backend --verify-no-changes
```

## Build and test (web)

Requires Node.js 24 with corepack enabled (`corepack enable`); pnpm is pinned in `web/package.json`.

```bash
cd web
pnpm install --frozen-lockfile
pnpm lint && pnpm typecheck && pnpm test && pnpm build
pnpm --filter @msp/e2e exec playwright install chromium   # once per machine
pnpm e2e
```
