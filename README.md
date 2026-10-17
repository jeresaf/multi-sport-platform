# MSP — multi-sport competition platform

A multi-tenant platform for running sports competitions: people, teams and venues → competitions and editions →
fixtures → live scoring → approved results → standings → public sites.

- New here? Start with [README-FIRST.md](README-FIRST.md).
- Documentation: [docs/](docs/) — architecture, domain, decisions (ADRs) and the build plan.
- Instructions for Claude Code: [CLAUDE.md](CLAUDE.md).

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
- Mailpit: SMTP `localhost:1025`, inbox at http://localhost:8025.
- Optional OpenTelemetry dashboard: `docker compose --profile observability up -d`, then http://localhost:18888
  (OTLP gRPC on `localhost:4317`).
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
dotnet test --solution backend
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
