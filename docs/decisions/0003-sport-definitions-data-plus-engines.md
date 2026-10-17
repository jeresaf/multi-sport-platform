# ADR-0003 — Sport definitions are versioned data plus a small set of scoring-family engines

**Status:** Accepted
**Date:** 2026-10-17
**Deciders:** Owner, Reviewer

## Context

The owner's requirement: "Sports Manager … will replace hard-coding of sports within the system." The
previous plan expressed sports purely as configuration (periods, scoring actions with `default_points`,
timeline events) but never specified how scores are computed for sports whose scoring is not additive:
cricket (runs/wickets/overs/extras/innings/targets), tennis and volleyball (points → games → sets), golf
and athletics (lowest/highest metric ranks a field), boxing/MMA (rounds, judges, method of victory).
Pure data cannot express those rules safely; scattering `if (sport == Cricket)` through generic code is
also forbidden.

## Decision

A sport variant is defined by two things:

1. **A sport definition (data)** — stored as YAML in the repository (docs/domain/sports/*.yaml is the
   reference source; the backend seeds from its own copy in S05), versioned with a content hash:
   terminology, contest shape, periods, event types (with categories, importance, points, actor roles,
   metadata schema), positions, staff roles, official roles, statistics, lineup defaults, coverage levels,
   scorer-pad layout, and the **scoring family** it uses.
2. **A scoring-family engine (code)** — one implementation per family, selected by the definition's
   `scoring_family` code, never by sport name:

| Family | Sports (initial) | Score model |
|---|---|---|
| `ADDITIVE` | Football, Rugby 15s, Rugby 7s, Basketball, later Netball, Hockey, Handball | Points per side from event `points`; tie resolution by rules (draw / extra periods / shootout / sudden death) |
| `INNINGS` | Cricket T20 (later other formats) | Runs, wickets, legal balls, extras per innings; targets; super over |
| `SETS` | later Volleyball, Tennis, Badminton | Points → (games) → sets; win-by-2, caps, deciding set |
| `RANKED_METRIC` | later Golf, Athletics, Swimming | Per-participant metric (strokes, time, distance); rank asc/desc; rounds/attempts |
| `COMBAT` | later Boxing, MMA | Rounds, judges' scorecards, method/round/time of finish |

Engine contract (pure, deterministic, no I/O):

```csharp
public interface IScoringEngine
{
    string Family { get; }
    ScoreState Initial(ContestContext ctx);
    ValidationResult Validate(ContestContext ctx, ContestLiveState live, TimelineEventDraft draft);
    ScoreState Apply(ContestContext ctx, ScoreState state, TimelineEvent evt);
    OutcomeProposal ProposeOutcome(ContestContext ctx, ScoreState state);
    ScoreSummary Summarize(ContestContext ctx, ScoreState state);
}
```

`ContestContext` carries the resolved sport definition, the edition's effective rules snapshot and the
contest participants. Details: docs/architecture/sport-engine.md.

**Contest shape** is reduced to two values: `HEAD_TO_HEAD` (two sides; teams or individuals) and `FIELD`
(many participants ranked). Series, fight cards and heats are competition-format concepts, not shapes.
UI renderers are chosen by `(shape, scoring_family)`.

## Alternatives considered

- **Pure configuration (pack).** Rejected: cannot express cricket/sets/combat without an ad-hoc
  rule language that would be worse than code.
- **One engine per sport.** Rejected: duplicates logic; four of the five flagship sports share `ADDITIVE`.
- **Scripting language in the DB (e.g. Lua/JS rules).** Rejected for v1: security, testing and tooling cost.

## Consequences

- Positive: adding hockey or netball is a YAML file + tests, no engine code.
- Positive: the multi-sport proof (Phase 5) needs only one new engine (`INNINGS`) for the flagship set.
- Negative: a genuinely new scoring model needs engine code and an ADR — intended.
- Generic modules must never branch on sport code; architecture tests and review check for it.

## Rules affected

AR-11, AR-12.

## Review trigger

Tenants need custom sports frequently enough to justify an in-app definition editor (Enterprise feature);
see ADR-0006.
