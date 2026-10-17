# MSP — multi-sport competition platform

A multi-tenant platform for running sports competitions: people, teams and venues → competitions and editions →
fixtures → live scoring → approved results → standings → public sites.

- New here? Start with [README-FIRST.md](README-FIRST.md).
- Documentation: [docs/](docs/) — architecture, domain, decisions (ADRs) and the build plan.
- Instructions for Claude Code: [CLAUDE.md](CLAUDE.md).

## Repository layout

```text
backend/   .NET 10 modular monolith (SportsPlatform.slnx)
web/       pnpm + Turborepo: apps/public (port 3000), apps/admin (port 3001), packages/*, e2e/
docs/      product, architecture, domain, decisions, build plan
```

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
