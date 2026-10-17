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
- [x] `dotnet build backend -warnaserror` and `dotnet test backend` pass with zero warnings (see Implementation notes).
- [x] `dotnet format backend --verify-no-changes` passes.
- [x] CLAUDE.md commands are copy-paste correct.

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
- [x] `pnpm install --frozen-lockfile`, `pnpm lint`, `pnpm typecheck`, `pnpm test`, `pnpm build` pass from `web/`.
- [x] `pnpm e2e` runs both smoke tests against production builds.
- [x] No colour literals in components — tokens only.

### T3 — Local infrastructure
Deliver:
- `docker-compose.yml`: `postgres:17` (named volume, healthcheck, port 5432) with an init script creating the
  database `sportsplatform` and roles `sp_migrator` (owner of schemas), `sp_app` (subject to RLS, no
  ownership), `sp_platform` (`BYPASSRLS`, used only by explicit platform services); `mailpit` (SMTP 1025, UI 8025).
  An optional `observability` profile may add the standalone Aspire dashboard container for OpenTelemetry.
- `.env.example` with development-only values; real secrets never committed.
- README "Local setup" section (Windows/macOS/Linux notes).

Acceptance:
- [x] `docker compose up -d` → services healthy; `psql` connects as `sp_app` and cannot create tables in
      application schemas.
- [x] README steps work from a fresh clone.

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

## Implementation notes

- **T1 — test command (owner-approved deviation).** xUnit v3 (4.x) runs only on Microsoft.Testing.Platform with
  the .NET 10 SDK, so `global.json` selects that runner and the test command is `dotnet test --solution backend`
  (the form `dotnet test backend` is rejected by the SDK). Use this form wherever the spec says `dotnet test`,
  including the T4 CI workflow.
- **T2 — Tailwind 4 (owner-approved).** Tailwind 4 is configured in CSS and has no JS presets, so the shared
  "Tailwind preset" is `web/packages/config/theme.css` (`@theme inline` mapping utilities to the token variables,
  default palette removed). Token colour values are placeholders until the owner or the Branding work sets them.
- **T2 — colour literals** fail `pnpm lint`. Rules: `web/packages/config/colour-rules.js`, covered by Vitest in
  `@msp/config`. In JS/TS, ESLint (`no-restricted-syntax`) rejects hex, colour functions and Tailwind arbitrary
  colours (`bg-[#fff]`, `text-[red]`, `[color:red]`). In app CSS, `msp-check-css-colours` rejects hex, colour
  functions, arbitrary colours in `@apply`, and colour properties whose value isn't `var(…)` or a keyword (comments
  are ignored). Known gaps: named colours in inline `style` objects; colour inside shorthand values other than
  `background` (e.g. `border: 1px solid red`). Known false positives: hex-like strings (`"#add"`, `"Fixture #101"`).
  Colour values live only in `web/packages/tokens/tokens.css`.
- **T3 — database roles.** `infrastructure/local/postgres/init/01-roles.sh` creates the roles once per volume.
  `sp_migrator` owns the database (and so the schemas migrations create); `sp_app` and `sp_platform` get
  `USAGE`/DML on future schemas, tables, sequences and functions through `ALTER DEFAULT PRIVILEGES FOR ROLE
  sp_migrator`, never `CREATE`. `infrastructure/local/postgres/check-roles.sh` proves this (9 checks) and is meant
  for reuse in CI. The S01 Testcontainers fixture should run the same init script so tests use the same roles.
  The defaults grant DML on **every** table `sp_migrator` creates. From S01, migrations must narrow this where a
  table needs it: `REVOKE INSERT, UPDATE, DELETE … FROM sp_app` for global/reference tables (read-only to the app),
  append-only tables (timeline, audit; plus the trigger from AR-19) and each schema's migrations history table.
  Local ports are bound to `127.0.0.1` because the development passwords are public.
