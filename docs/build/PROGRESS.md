# Progress log

Claude Code updates this file at the end of **every** task (keep it short and factual). The reviewer reads it
at the start of every review.

## Now

- **Current slice:** S01 — Backend platform (docs/build/slices/S01-backend-platform.md). S00 is on `main`; its
  "new contributor in under 30 minutes" check is still with the owner
- **Current task:** S01-T2b (grant narrowing), branch `s01/t2b-grant-narrowing`. Plan approved 2026-10-17:
  `ReadOnlyForRuntimeRoles` covers `sp_app` and `sp_platform`; the `AppendOnly` trigger also blocks `TRUNCATE`
- **Next:** S01-T3 (API conventions, errors, OpenAPI and generated client)
- **Blocked on:** nothing

## Next up

1. S01-T2b — Grant narrowing: `ReadOnlyForRuntimeRoles`, `AppendOnly` (+ trigger, also blocking `TRUNCATE`),
   migrations-history revoke, grants test, `check-roles.sh` `sp_platform` checks (acceptance criterion 3).
   Reviewer (#14): `ReadOnlyForRuntimeRoles` should make **`sp_platform`** read-only too (a platform service that
   must write a global table declares it in the grants test, with a reason) — wider than the spec, so confirm with
   the owner in the T2b plan. Prove the reset works with the trigger, including `TRUNCATE`. Stacked on #14: merge
   #14 first, then retarget T2b's PR to `main`
2. S01-T3 — API conventions, errors, OpenAPI and generated client

## Log

| Date | Task | PR | Summary | Notes / deviations |
|---|---|---|---|---|
| 2026-10-17 | — | — | Kit added: CLAUDE.md, .claude/, docs/ | Handoff from reviewer |
| 2026-10-17 | — | — | Kit v2: owner answers Q1, Q2, Q4, Q5; ADR-0016 (hosting); plans and pricing; fictional pilot customer; roadmap Phase 5 reordered (rugby, basketball, cricket); prototype review and selected prototype docs | Restart in the existing repo; prototype at tag `prototype-v0` |
| 2026-10-17 | S00-T1 | #7 | Root conventions (editorconfig, gitattributes, gitignore, global.json, README); `backend/SportsPlatform.slnx` with Host (`GET /health/live`), BuildingBlocks, Unit/Integration/Architecture tests, CLI; central package management; CLAUDE.md commands | xUnit v3 on Microsoft.Testing.Platform → test command is `dotnet test --solution backend` (agreed) |
| 2026-10-17 | S00-T2 | #8 (reached `main` via #10) | `web/` pnpm 12 + Turborepo workspace; Next.js 16.4 `apps/public` (3000) and `apps/admin` (3001) with Tailwind 4; `packages/config` (tsconfig, ESLint flat config with a colour-literal rule, shared Tailwind theme), `packages/tokens` (CSS variables light/dark + TS export), `packages/ui` (`Button` + Vitest), `packages/api-client` placeholder; Playwright smoke tests in `web/e2e` | Tailwind 4 shared `theme.css` instead of a preset; placeholder token colours (both agreed). ESLint 9 (eslint-config-next plugins don't support 10); TypeScript 6.0 (typescript-eslint needs < 6.1) |
| 2026-10-17 | S00-T3 | #9 (reached `main` via #10) | `docker-compose.yml`: `postgres:17` (named volume, TCP healthcheck, init script creating `sportsplatform` and roles `sp_migrator`/`sp_app`/`sp_platform` with default privileges for future schemas), Mailpit v1.31.6, optional `observability` profile (Aspire dashboard 13.6); `.env.example`; `infrastructure/local/postgres/check-roles.sh`; README Local setup | Roles get privileges via `ALTER DEFAULT PRIVILEGES FOR ROLE sp_migrator`, so S01+ migrations need no grant boilerplate |
| 2026-10-17 | S00-T4 | #10 (also landed T2 and T3 on `main`) | `.github/workflows/ci.yml`: backend (restore, build `-warnaserror`, test, format), web (frozen install, lint, typecheck, test, build), e2e (Playwright Chromium, smoke tests against production builds, report artifact on failure), infra (compose up `--wait`, role checks); NuGet and pnpm caches; superseded PR runs cancelled, every push to `main` keeps its own run | Added an `infra` job (not in the spec) so T3's role guarantees are regression-tested |
| 2026-10-17 | S01-T1 | #13 | BuildingBlocks primitives (`IModule`, `ApiEndpointGroups`, `Result`/`Error` with `AREA_CONDITION` check, `Entity`/`AggregateRoot` + domain events, `ITenantOwned`, `ICurrentActor` + `SystemActor`, `ITenantContext`, `ActionCode` + endpoint action metadata); Platform module + Contracts; Host module registry and `/api/{public,admin,platform}/v1` groups; NetArchTest architecture tests (AR-02, AR-26, AR-33, Domain layering) replace the S00 placeholders | BuildingBlocks references ASP.NET Core for `IModule` only (agreed, see Deviations). NetArchTest.Rules 1.3.2 (MIT) |
| 2026-10-17 | S01-T2a | #14 | `SportsPlatform.BuildingBlocks.Persistence`: `ModuleDbContext` (schema, snake_case, app UUIDv7 keys, shadow audit columns via `AuditColumnsInterceptor`, `xmin` row version on aggregates, `SaveChangesResultAsync` → `<MODULE>_CONCURRENCY_CONFLICT`), `App`/`Migrator`/`Platform` connection strings, `MigrationRunner`; `PlatformDbContext` + initial migration; CLI `migrate`; Development auto-migrate; `dotnet-ef` tool manifest; Testcontainers harness (compose image + `01-roles.sh`, app as `sp_app`, Respawn on a fixture-only superuser connection, `FakeTimeProvider`, `TestActor`); `Entity()` no longer generates IDs (#13 R3) | T2 split into T2a/T2b (agreed). EF migration names keep `yyyyMMddHHmmss_`. Pre-review caught `WebApplicationFactory` defaulting to Development (would migrate localhost) → `HostOnlyApp` + guard test |

## Deviations from spec (agreed)

- **S00-T1:** test command `dotnet test --solution backend` instead of `dotnet test backend`. xUnit v3 4.x needs
  Microsoft.Testing.Platform on the .NET 10 SDK (selected in `global.json`). Owner approved 2026-10-17. Noted in
  the S00 spec's Implementation notes.
- **S00-T2:** Tailwind 4 has no JS presets, so `web/packages/config` ships a shared `theme.css` (`@theme` mapped to
  the tokens) instead of a "Tailwind preset". Token colour values are placeholders (design-system.md names the
  groups but not the values) until the owner or the Branding work sets them. Owner approved 2026-10-17.
- **S00-T4:** an extra `infra` CI job (compose up + `check-roles.sh`) beyond the spec's backend/web/e2e jobs, so
  T3's role guarantees are regression-tested. Owner approved the plan 2026-10-17.
- **S01-T1:** BuildingBlocks has a `FrameworkReference` to `Microsoft.AspNetCore.App` because `IModule.MapEndpoints`
  needs `IEndpointRouteBuilder`. Only the `SportsPlatform.BuildingBlocks.Modules` namespace may use ASP.NET Core;
  no BuildingBlocks namespace may use EF Core (`LayeringTests`). This replaces S00's "BuildingBlocks references no
  ASP.NET Core" placeholder test. Owner approved the plan 2026-10-17.
- **S01-T2:** delivered as two PRs (T2a conventions + harness, T2b grant narrowing). Migration files keep EF's
  `yyyyMMddHHmmss_` prefix; docs/architecture/data-conventions.md updated. A published CLI defaults to Production
  (`dotnet run` uses Development via launchSettings). The planned "EF Core only in module `Infrastructure`"
  architecture rule was dropped (handlers use the DbContext as unit of work); "Contracts don't use EF Core" added
  instead. Owner approved the plan 2026-10-17.

## Tech debt / follow-ups

Consolidated by the S00 slice review (2026-10-17). Each item names where it gets done.

**Assigned to S01** (see the spec's "Carried over from S00")
- Mark `GET /health/live` and `/health/ready` with `.AllowPublic()`; the startup test covers every endpoint (T4).
- Narrow `sp_app`/`sp_platform` default DML grants: read-only reference/global tables, append-only tables
  (audit now, timeline later), migrations-history tables; grants test (T2, T5–T7).
- Respawn reset must keep working with T6's append-only trigger: superuser + `session_replication_role = replica`
  (T2b decides and tests it).
- `xmin`/`updated_*` cover only the aggregate root's row: a change to only a child entity or owned type doesn't
  bump the root's version, so two edits to different children both succeed. Mark the root `Modified` when a
  dependent changes, with a test, before the first aggregate with child tables (S06 at the latest) (S01-T2a
  pre-review).
- `ITenantOwned.TenantId` stays get-only: S02's interceptor sets it with `entry.Property(...).CurrentValue`, never
  a public setter. Moving `BuildingBlocks/Modules` to a `BuildingBlocks.Web` project is optional, not planned
  (reviewer guidance on #13).
- `check-roles.sh`: add checks that `sp_platform` cannot create tables or schemas (T2, small).
- The per-test reset will wipe reference data (countries, currencies…): exclude those tables from Respawn or
  re-run the reference seed after each reset (T7; reviewer note on #14).

**Owner decisions / housekeeping**
- `main` has no branch protection: GitHub doesn't offer it for private repositories on the Free plan. Options:
  GitHub Pro (then require PR + checks `Backend`, `Web`, `E2E (Playwright)`, `Local infrastructure`, later
  `Contracts`), or accept the risk (Claude Code's settings already deny pushes to `main`).
- Merge style: README-FIRST says squash-and-merge; S00 used merge commits. Pick one. For stacked PRs, retarget
  the next PR to `main` before merging it.
- The prototype's old branches (`agent/*`, `codex/*`, `feature/*`, `ci/*`) are not all covered by tag
  `prototype-v0`; keep them or tag them before deleting. (The merged S00 branches and `restart/kit` were deleted
  on 2026-10-17.)
- Owner's "new contributor in under 30 minutes" check (S00 slice acceptance); then roadmap S00 → ☑.

**Later**
- Token colour values are placeholders (`web/packages/tokens/tokens.css`); owner/Branding (S22) to confirm. The
  dark palette is written twice in that file; keep both blocks in sync.
- ESLint stays on 9.x until eslint-config-next's plugins support ESLint 10; TypeScript stays below 6.1 until
  typescript-eslint supports it.
- shadcn/ui is not initialised yet (ADR-0011); do it with the first real admin UI (S03). `Button` is hand-written.
- Colour-literal checks have known gaps and false positives (S00 Implementation notes, T2).
- pnpm 12's minimum-release-age policy is on: new package versions are installable about a day after release.
- The AR-33 test exempts the whole Host. When the first infrastructure adapter project lands (file storage, from
  S06), replace the exemption with an explicit allow-list of adapter assemblies (S01-T1 pre-review).
- Dependency updates are manual (exact pins everywhere): add Dependabot or Renovate for NuGet, npm and GitHub
  Actions, and pin actions by commit SHA (Phase 9 hardening at the latest).
