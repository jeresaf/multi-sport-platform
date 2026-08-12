# ADR-0002: Use one universal contest aggregate

- **Status:** Accepted
- **Date:** 2026-08-10

## Context

Matches, races, bouts, golf rounds, and cricket innings differ in rules but share
scheduling, participants, officials, lifecycle, scoring, results, and public
projection needs.

## Decision

Represent all scheduled competitive events with `Contest`, ordered participants,
nested segments, officials, events, clock/lifecycle state, and a pinned ruleset.
Express sport differences through versioned definitions, validation services,
event payloads, and derived projections rather than separate sport tables.

## Consequences

- Cross-sport workflows and clients reuse one API vocabulary.
- New sports usually add configuration/validation instead of a parallel stack.
- The aggregate must preserve strong generic invariants while allowing typed
  sport detail; indiscriminate JSON is not a substitute for core fields.

## Alternatives

One aggregate per sport was rejected because it duplicates lifecycle, security,
offline replay, and real-time behavior. A completely schema-less event document
was rejected because relational ownership and reporting need typed authority.

