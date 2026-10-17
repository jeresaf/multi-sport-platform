# Review protocol

Every pull request is reviewed by the reviewer (Claude in the claude.ai "MSP" project) before the owner merges.
The builder also runs a self-review subagent first, but that does not replace this review.

## How the owner requests a review

**Preferred (GitHub):** in a chat inside the MSP project, write:

> Review PR #12 in `<github-owner>/<repo>`.

The first time, the reviewer will ask to attach the repository through the GitHub connection. The reviewer
reads the PR description, diff, CI checks and the relevant specs, then posts the review as a PR comment when it
has access; otherwise it returns the review in chat for the owner to paste.

**Without GitHub access:** paste the output of `gh pr view 12` and `gh pr diff 12` into the chat (or upload a zip
of the branch), and say which task it is.

**Re-review:** "Re-review PR #12" — the reviewer checks only the changes since its last review plus anything the
fixes touched.

## What the builder must put in the PR (template)

Task ID and link to spec · summary · acceptance criteria checklist **with evidence** (test names, command
output, screenshots for UI) · new/changed action codes, error codes, events, endpoints · migrations (and RLS
for new tenant tables) · new dependencies with licences · deviations from spec (with owner agreement) · known
limitations / follow-ups.

## Review checklist

1. **Scope** — matches the task; nothing unrelated; every acceptance criterion has evidence.
2. **Architecture rules** (docs/architecture/rules.md) — module boundaries (AR-01/02), tenant isolation
   (AR-04/05), action codes + authorization (AR-06/07), timeline/score/result rules (AR-08–10), no sport
   hard-coding (AR-11), immutability and snapshots (AR-13/14), state machines (AR-15), events/projections
   (AR-16/17), history (AR-18), audit (AR-19), errors (AR-20), DTOs/PII (AR-21/24), time (AR-31).
3. **Correctness** — domain rules and edge cases, state transitions, concurrency (`version`), idempotency,
   time zones, validation, error codes.
4. **Tests** — meaningful assertions, failure paths, isolation coverage for new tenant entities, engine
   scenarios for sport rules, deterministic (no sleeps, fake clock), integration tests on real PostgreSQL.
5. **Data** — migrations (naming, constraints, indexes, RLS policy), no edits to merged migrations, seeds through
   commands, no cross-module FKs.
6. **Security & privacy** — every endpoint authorised; no PII/secrets in logs, analytics or audit metadata;
   public DTOs only expose `PUBLIC` fields; minors handled per ADR-0015.
7. **API & UI** — OpenAPI and generated client updated; error codes documented; UI loading/empty/error states;
   responsive layouts at the three breakpoints; accessibility basics; capabilities respected.
8. **Docs** — PROGRESS.md updated; docs changed with behaviour; ADR where needed; generated catalogues current.
9. **Dependencies** — licence acceptable (ADR-0013); no unnecessary packages.
10. **CI** — green.

## Severity and verdict

| Severity | Meaning | Merge? |
|---|---|---|
| **BLOCKER** | Violates a rule, introduces a bug, security/privacy issue, data loss risk, failing/missing critical tests | Must fix |
| **MAJOR** | Missing test for a stated rule, wrong ownership placement, misleading naming that will spread, incomplete acceptance evidence | Fix before merge unless owner accepts a follow-up |
| **MINOR** | Small improvement; fix now or log as tech debt | Owner's choice |
| **NIT** | Style or preference | Optional |

Verdicts: **APPROVE**, **APPROVE WITH NITS**, **REQUEST CHANGES**. The reviewer reports gaps that affect correctness,
rules or stated requirements; it does not demand extra abstraction or speculative features (over-engineering is
itself a finding).

## Review format

```markdown
## Review — PR #12 [S05-T03] ADDITIVE scoring engine
**Verdict:** REQUEST CHANGES
**Summary:** Engine and fold are correct; shoot-out outcome ignores `extra_period_sequence`; two rules lack
rejecting scenarios.

| # | Severity | Where | Finding | Why | Suggested fix |
|---|---|---|---|---|---|
| R1 | BLOCKER | `AdditiveEngine.cs:142` | Shoot-out proposed before extra time when sequence is non-empty | sport-engine.md §5 | Check remaining periods in `extra_period_sequence` first |
| R2 | MAJOR | `Scenarios/football/` | No rejecting scenario for SHOOTOUT_GOAL in PLAY phase | AR-12, §11 | Add scenario expecting `TIMELINE_EVENT_NOT_ALLOWED_IN_PHASE` |

**Questions:** …
**Good:** …
```

## Builder response (`/address-review N`)

For each finding: **Fixed** (commit SHA) · **Won't fix** (reason; owner decides) · **Question**. Push, then post
one PR comment summarising the responses. Don't silently skip findings.

## Slice reviews and specs

At the end of each slice the owner asks: "Slice review S05". The reviewer checks drift against architecture,
collects tech debt, confirms the roadmap status and **writes the next slice's detailed spec** (S06 onwards), which
the owner adds to docs/build/slices/ (or the reviewer opens a docs-only PR if it has push access).

## Reviewer memory

Reviews happen in fresh chats. The reviewer relies on the repository docs (ADRs, architecture, PROGRESS.md) and
the MSP project's reviewer guide — so keep PROGRESS.md accurate.
