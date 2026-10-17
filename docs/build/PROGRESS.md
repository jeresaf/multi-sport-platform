# Progress log

Claude Code updates this file at the end of **every** task (keep it short and factual). The reviewer reads it
at the start of every review.

## Now

- **Current slice:** S00 — Repo foundation
- **Current task:** S00-T1 — Repository skeleton & conventions (PR open, awaiting review)
- **Blocked on:** review of the S00-T1 PR; T2 starts after it merges

## Next up

1. S00-T2 Web workspace (needs Node 24 + pnpm)
2. S00-T3 Local infrastructure
3. S00-T4 CI

## Log

| Date | Task | PR | Summary | Notes / deviations |
|---|---|---|---|---|
| 2026-10-17 | — | — | Kit added: CLAUDE.md, .claude/, docs/ | Handoff from reviewer |
| 2026-10-17 | — | — | Kit v2: owner answers Q1, Q2, Q4, Q5; ADR-0016 (hosting); plans and pricing; fictional pilot customer; roadmap Phase 5 reordered (rugby, basketball, cricket); prototype review and selected prototype docs | Restart in the existing repo; prototype at tag `prototype-v0` |
| 2026-10-17 | S00-T1 | (this PR) | Root conventions (editorconfig, gitattributes, gitignore, global.json, README); `backend/SportsPlatform.slnx` with Host (`GET /health/live`), BuildingBlocks, Unit/Integration/Architecture tests, CLI; central package management; CLAUDE.md commands | xUnit v3 on Microsoft.Testing.Platform → test command is `dotnet test --solution backend` (agreed) |

## Deviations from spec (agreed)

- **S00-T1:** test command `dotnet test --solution backend` instead of `dotnet test backend`. xUnit v3 4.x needs
  Microsoft.Testing.Platform on the .NET 10 SDK (selected in `global.json`). Owner approved 2026-10-17. Noted in
  the S00 spec's Implementation notes.

## Tech debt / follow-ups

_None yet._
