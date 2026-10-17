# S00 — Repository foundation

**Goal:** an empty-but-real monorepo that builds, tests and runs in CI, so every later task starts from green.
**Modules touched:** none yet (Host + BuildingBlocks skeleton only).
**Depends on:** this kit committed to the repository's `main` branch.
**Demo:** fresh clone → `docker compose up -d` → backend tests pass → both web apps open in a browser → CI green
on a pull request.

## Out of scope

Business modules, database tables, authentication, real UI. Do not scaffold modules that later slices create.

## Tasks

### T1 — Repository skeleton and conventions
Deliver:
- Root: `README.md` (short: what this is, links to README-FIRST.md and docs/), `.editorconfig`, `.gitattributes`
  (`* text=auto eol=lf`; keep `*.cmd`/`*.bat` CRLF), `.gitignore` (.NET, Node, IDEs, `.env*` except
  `.env.example`, `CLAUDE.local.md`, `.claude/settings.local.json`), `global.json` (.NET 10 SDK,
  `rollForward: latestFeature`).
- `backend/`: solution (`dotnet new sln`), `Directory.Build.props` (`net10.0`, nullable, implicit usings,
  `TreatWarningsAsErrors`, latest analyzers, deterministic builds), `Directory.Packages.props` (central package
  management).
- Projects: `src/SportsPlatform.Host` (minimal API with `GET /health/live` → `200 {"status":"ok"}`),
  `src/SportsPlatform.BuildingBlocks` (empty class library), `tests/SportsPlatform.UnitTests`,
  `tests/SportsPlatform.IntegrationTests` (WebApplicationFactory test calling `/health/live`),
  `tests/SportsPlatform.ArchitectureTests` (one placeholder test), `tools/SportsPlatform.Cli` (console app that
  prints its commands).
- Update the **Commands** section of CLAUDE.md with the exact build/test/format commands.

Acceptance:
- [ ] `dotnet build backend -warnaserror` and `dotnet test backend` pass with zero warnings.
- [ ] `dotnet format backend --verify-no-changes` passes.
- [ ] CLAUDE.md commands are copy-paste correct.

### T2 — Web workspace
Deliver:
- `web/package.json` (private; `packageManager` pinned to a pnpm version via corepack), `pnpm-workspace.yaml`,
  `turbo.json` with `lint`, `typecheck`, `test`, `build`, `e2e` pipelines.
- `web/apps/public` (port 3000) and `web/apps/admin` (port 3001): Next.js 16 App Router, TypeScript strict,
  Tailwind, ESLint. Public home shows "Coming soon"; admin home shows "MSP Admin".
- `web/packages/config` (shared tsconfig, ESLint, Tailwind preset), `web/packages/tokens` (CSS variables from
  docs/product/ux/design-system.md: neutral, surfaces, semantic colours, spacing, radius, light/dark),
  `web/packages/ui` (one `Button` using tokens + a Vitest test), `web/packages/api-client` (placeholder README:
  generated from OpenAPI in S01).
- Playwright in `web/e2e` with smoke tests for both apps.
- Update CLAUDE.md commands for web.

Acceptance:
- [ ] `pnpm install --frozen-lockfile`, `pnpm lint`, `pnpm typecheck`, `pnpm test`, `pnpm build` pass from `web/`.
- [ ] `pnpm e2e` runs both smoke tests against production builds.
- [ ] No colour literals in components — tokens only.

### T3 — Local infrastructure
Deliver:
- `docker-compose.yml`: `postgres:17` (named volume, healthcheck, port 5432) with an init script creating the
  database `sportsplatform` and roles `sp_migrator` (owner of schemas), `sp_app` (subject to RLS, no
  ownership), `sp_platform` (`BYPASSRLS`, used only by explicit platform services); `mailpit` (SMTP 1025, UI 8025).
  An optional `observability` profile may add the standalone Aspire dashboard container for OpenTelemetry.
- `.env.example` with development-only values; real secrets never committed.
- README "Local setup" section (Windows/macOS/Linux notes).

Acceptance:
- [ ] `docker compose up -d` → services healthy; `psql` connects as `sp_app` and cannot create tables in
      application schemas.
- [ ] README steps work from a fresh clone.

### T4 — Continuous integration
Deliver `.github/workflows/ci.yml` running on pull requests and pushes to `main`:
- **backend:** setup .NET from `global.json` → restore → build `-warnaserror` → test → `dotnet format
  --verify-no-changes`.
- **web:** setup Node 24 + pnpm → install (frozen lockfile) → lint → typecheck → test → build.
- **e2e:** install Playwright Chromium → run smoke tests against built apps.
- Caching for NuGet and the pnpm store; `concurrency` cancels superseded runs.
- README-FIRST already tells the owner to protect `main` (require PR + passing CI).

Acceptance:
- [ ] CI passes on this PR (link the run in the PR).
- [ ] Workflow uses pinned major versions of actions.

## Slice acceptance

- [ ] All four tasks merged; CI green on `main`.
- [ ] A new contributor can follow README and get everything running in under 30 minutes.
- [ ] PROGRESS.md and roadmap updated.
