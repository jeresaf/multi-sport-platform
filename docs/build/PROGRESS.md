# Progress log

Claude Code updates this file at the end of **every** task (keep it short and factual). The reviewer reads it
at the start of every review.

## Now

- **Current slice:** S00 — Repo foundation
- **Current task:** S00-T3 — Local infrastructure (PR open, stacked on PR #8)
- **Blocked on:** merges of PR #7 and PR #8, then review of the S00-T3 PR

## Next up

1. S00-T4 CI

## Log

| Date | Task | PR | Summary | Notes / deviations |
|---|---|---|---|---|
| 2026-10-17 | — | — | Kit added: CLAUDE.md, .claude/, docs/ | Handoff from reviewer |
| 2026-10-17 | — | — | Kit v2: owner answers Q1, Q2, Q4, Q5; ADR-0016 (hosting); plans and pricing; fictional pilot customer; roadmap Phase 5 reordered (rugby, basketball, cricket); prototype review and selected prototype docs | Restart in the existing repo; prototype at tag `prototype-v0` |
| 2026-10-17 | S00-T1 | #7 | Root conventions (editorconfig, gitattributes, gitignore, global.json, README); `backend/SportsPlatform.slnx` with Host (`GET /health/live`), BuildingBlocks, Unit/Integration/Architecture tests, CLI; central package management; CLAUDE.md commands | xUnit v3 on Microsoft.Testing.Platform → test command is `dotnet test --solution backend` (agreed) |
| 2026-10-17 | S00-T2 | #8 | `web/` pnpm 12 + Turborepo workspace; Next.js 16.4 `apps/public` (3000) and `apps/admin` (3001) with Tailwind 4; `packages/config` (tsconfig, ESLint flat config with a colour-literal rule, shared Tailwind theme), `packages/tokens` (CSS variables light/dark + TS export), `packages/ui` (`Button` + Vitest), `packages/api-client` placeholder; Playwright smoke tests in `web/e2e` | Tailwind 4 shared `theme.css` instead of a preset; placeholder token colours (both agreed). ESLint 9 (eslint-config-next plugins don't support 10); TypeScript 6.0 (typescript-eslint needs < 6.1) |
| 2026-10-17 | S00-T3 | (PR pending) | `docker-compose.yml`: `postgres:17` (named volume, TCP healthcheck, init script creating `sportsplatform` and roles `sp_migrator`/`sp_app`/`sp_platform` with default privileges for future schemas), Mailpit v1.31.6, optional `observability` profile (Aspire dashboard 13.6); `.env.example`; `infrastructure/local/postgres/check-roles.sh`; README Local setup | Roles get privileges via `ALTER DEFAULT PRIVILEGES FOR ROLE sp_migrator`, so S01+ migrations need no grant boilerplate |

## Deviations from spec (agreed)

- **S00-T1:** test command `dotnet test --solution backend` instead of `dotnet test backend`. xUnit v3 4.x needs
  Microsoft.Testing.Platform on the .NET 10 SDK (selected in `global.json`). Owner approved 2026-10-17. Noted in
  the S00 spec's Implementation notes.
- **S00-T2:** Tailwind 4 has no JS presets, so `web/packages/config` ships a shared `theme.css` (`@theme` mapped to
  the tokens) instead of a "Tailwind preset". Token colour values are placeholders (design-system.md names the
  groups but not the values) until the owner or the Branding work sets them. Owner approved 2026-10-17.

## Tech debt / follow-ups

- S01: replace the placeholder `BuildingBlocksAssemblyTests` once real unit tests exist; mark `GET /health/live`
  with `.AllowPublic()` when the action catalogue lands (PR #7 review R3 and question).
- Token colour values are placeholders (`web/packages/tokens/tokens.css`); owner/Branding to confirm.
- ESLint stays on 9.x until eslint-config-next's plugins support ESLint 10; TypeScript stays below 6.1 until
  typescript-eslint supports it.
- shadcn/ui is not initialised yet (ADR-0011); do it with the first real admin UI (S03). `Button` is hand-written.
- pnpm 12's minimum-release-age policy is on: new package versions are installable about a day after release.
