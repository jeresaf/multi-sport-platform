# Progress log

Claude Code updates this file at the end of **every** task (keep it short and factual). The reviewer reads it
at the start of every review.

## Now

- **Current slice:** S00 — Repo foundation: all four tasks on `main`, CI green on `main` (run 39021057377). Open:
  the owner's "new contributor in under 30 minutes" check (slice acceptance); then roadmap S00 → ☑
- **Current task:** none
- **Blocked on:** nothing. The S00 slice review (2026-10-17) reconciled the stand-in reviews on #7–#10 and amended
  the S01 spec.

## Next up

1. S01 Backend platform (docs/build/slices/S01-backend-platform.md, amended by the S00 slice review) — S01-T1

## Log

| Date | Task | PR | Summary | Notes / deviations |
|---|---|---|---|---|
| 2026-10-17 | — | — | Kit added: CLAUDE.md, .claude/, docs/ | Handoff from reviewer |
| 2026-10-17 | — | — | Kit v2: owner answers Q1, Q2, Q4, Q5; ADR-0016 (hosting); plans and pricing; fictional pilot customer; roadmap Phase 5 reordered (rugby, basketball, cricket); prototype review and selected prototype docs | Restart in the existing repo; prototype at tag `prototype-v0` |
| 2026-10-17 | S00-T1 | #7 | Root conventions (editorconfig, gitattributes, gitignore, global.json, README); `backend/SportsPlatform.slnx` with Host (`GET /health/live`), BuildingBlocks, Unit/Integration/Architecture tests, CLI; central package management; CLAUDE.md commands | xUnit v3 on Microsoft.Testing.Platform → test command is `dotnet test --solution backend` (agreed) |
| 2026-10-17 | S00-T2 | #8 (reached `main` via #10) | `web/` pnpm 12 + Turborepo workspace; Next.js 16.4 `apps/public` (3000) and `apps/admin` (3001) with Tailwind 4; `packages/config` (tsconfig, ESLint flat config with a colour-literal rule, shared Tailwind theme), `packages/tokens` (CSS variables light/dark + TS export), `packages/ui` (`Button` + Vitest), `packages/api-client` placeholder; Playwright smoke tests in `web/e2e` | Tailwind 4 shared `theme.css` instead of a preset; placeholder token colours (both agreed). ESLint 9 (eslint-config-next plugins don't support 10); TypeScript 6.0 (typescript-eslint needs < 6.1) |
| 2026-10-17 | S00-T3 | #9 (reached `main` via #10) | `docker-compose.yml`: `postgres:17` (named volume, TCP healthcheck, init script creating `sportsplatform` and roles `sp_migrator`/`sp_app`/`sp_platform` with default privileges for future schemas), Mailpit v1.31.6, optional `observability` profile (Aspire dashboard 13.6); `.env.example`; `infrastructure/local/postgres/check-roles.sh`; README Local setup | Roles get privileges via `ALTER DEFAULT PRIVILEGES FOR ROLE sp_migrator`, so S01+ migrations need no grant boilerplate |
| 2026-10-17 | S00-T4 | #10 (also landed T2 and T3 on `main`) | `.github/workflows/ci.yml`: backend (restore, build `-warnaserror`, test, format), web (frozen install, lint, typecheck, test, build), e2e (Playwright Chromium, smoke tests against production builds, report artifact on failure), infra (compose up `--wait`, role checks); NuGet and pnpm caches; superseded PR runs cancelled, every push to `main` keeps its own run | Added an `infra` job (not in the spec) so T3's role guarantees are regression-tested |

## Deviations from spec (agreed)

- **S00-T1:** test command `dotnet test --solution backend` instead of `dotnet test backend`. xUnit v3 4.x needs
  Microsoft.Testing.Platform on the .NET 10 SDK (selected in `global.json`). Owner approved 2026-10-17. Noted in
  the S00 spec's Implementation notes.
- **S00-T2:** Tailwind 4 has no JS presets, so `web/packages/config` ships a shared `theme.css` (`@theme` mapped to
  the tokens) instead of a "Tailwind preset". Token colour values are placeholders (design-system.md names the
  groups but not the values) until the owner or the Branding work sets them. Owner approved 2026-10-17.
- **S00-T4:** an extra `infra` CI job (compose up + `check-roles.sh`) beyond the spec's backend/web/e2e jobs, so
  T3's role guarantees are regression-tested. Owner approved the plan 2026-10-17.

## Tech debt / follow-ups

Consolidated by the S00 slice review (2026-10-17). Each item names where it gets done.

**Assigned to S01** (see the spec's "Carried over from S00")
- Replace the placeholder `BuildingBlocksAssemblyTests` and architecture test with real tests (T1; PR #7 R3).
- Mark `GET /health/live` and `/health/ready` with `.AllowPublic()`; the startup test covers every endpoint (T4).
- Narrow `sp_app`/`sp_platform` default DML grants: read-only reference/global tables, append-only tables
  (audit now, timeline later), migrations-history tables; grants test (T2, T5–T7).
- Testcontainers fixture runs `infrastructure/local/postgres/init/01-roles.sh`; app connects as `sp_app`,
  migrations as `sp_migrator`, Respawn on a test-only superuser connection (T2).
- `check-roles.sh`: add checks that `sp_platform` cannot create tables or schemas (T2, small).

**Owner decisions / housekeeping**
- `main` has no branch protection: GitHub doesn't offer it for private repositories on the Free plan. Options:
  GitHub Pro (then require PR + checks `Backend`, `Web`, `E2E (Playwright)`, `Local infrastructure`, later
  `Contracts`), or accept the risk (Claude Code's settings already deny pushes to `main`).
- Merge style: README-FIRST says squash-and-merge; S00 used merge commits. Pick one. For stacked PRs, retarget
  the next PR to `main` before merging it.
- Delete merged branches `s00/t1-repo-skeleton`, `s00/t2-web-workspace`, `s00/t3-local-infra`, `s00/t4-ci`,
  `restart/kit`. The prototype's old branches (`agent/*`, `codex/*`, `feature/*`, `ci/*`) are not all covered by
  tag `prototype-v0`; keep them or tag them before deleting.
- Owner's "new contributor in under 30 minutes" check (S00 slice acceptance); then roadmap S00 → ☑.

**Later**
- Token colour values are placeholders (`web/packages/tokens/tokens.css`); owner/Branding (S22) to confirm. The
  dark palette is written twice in that file; keep both blocks in sync.
- ESLint stays on 9.x until eslint-config-next's plugins support ESLint 10; TypeScript stays below 6.1 until
  typescript-eslint supports it.
- shadcn/ui is not initialised yet (ADR-0011); do it with the first real admin UI (S03). `Button` is hand-written.
- Colour-literal checks have known gaps and false positives (S00 Implementation notes, T2).
- pnpm 12's minimum-release-age policy is on: new package versions are installable about a day after release.
- Dependency updates are manual (exact pins everywhere): add Dependabot or Renovate for NuGet, npm and GitHub
  Actions, and pin actions by commit SHA (Phase 9 hardening at the latest).
