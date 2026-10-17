# ADR-0007 — Simplified, non-overlapping status model

**Status:** Accepted
**Date:** 2026-10-17
**Deciders:** Owner, Reviewer

## Context

The previous documents correctly separated contest status, sport phase and result status, but introduced
overlaps elsewhere:
- `POSTPONED`/`CANCELLED` existed both as schedule status (Scheduling) and contest status (Contest Operations).
- Tenant status and subscription status both had `EXPIRING`/`GRACE`.
- Timeline events had six statuses (`ACTIVE`, `PENDING_REVIEW`, `CONFIRMED`, `CORRECTED`, `REVERSED`, `VOID`).
- Result had `CORRECTED` as a status while also having result versions.
- Edition had `SCHEDULED` and `UNDER_REVIEW` states that are derivable.
- Phase names differed between documents (`FIRST_QUARTER` vs `Q1`), and the first summary listed
  `NOT_STARTED`/`FINISHED` as phases.

Overlapping state machines owned by different code paths drift apart.

## Decision

The canonical state machines are in docs/domain/state-machines.md. Key simplifications:

1. **Contest status** (Match Operations) owns `POSTPONED` and `CANCELLED`. The schedule is a list of
   **versions** (time, venue, reason) plus a publication state (`DRAFT` / `PUBLISHED`). "Rescheduled" is
   derived (version > 1). Once published, direct edits are blocked; changes go through `fixture.reschedule`
   with a reason (owner requirement: "direct editing of approved fixtures should not be allowed").
2. **"Starting soon" is derived** (scheduled start within a configurable window), not a stored status.
3. **"Break" is a phase kind**, not a status: phases come from the sport definition and have
   `kind: PLAY | BREAK | SHOOTOUT`. Statuses never encode phases (`LIVE_SECOND_HALF` is forbidden).
4. **Tenant status** is operational only: `PROVISIONING`, `ACTIVE`, `SUSPENDED`, `ARCHIVED`.
   **Subscription status is computed from dates** (`ACTIVE`, `EXPIRING`, `GRACE`, `EXPIRED`) plus a stored
   `CANCELLED` flag. The **access mode** (`FULL`, `READ_ONLY`, `BLOCKED`) is computed from both.
5. **Timeline events are immutable**; effective status is derived: `ACTIVE`, `SUPERSEDED`, `REVERSED`
   (ADR-0002). `PENDING` exists only on offline clients.
6. **Results are versioned**: each version is `DRAFT → SUBMITTED → APPROVED | REJECTED`, and an approved
   version becomes `SUPERSEDED` when a correction is approved. While a correction is pending, the previous
   approved version stays official.
7. **Edition**: `DRAFT → REGISTRATION_OPEN → REGISTRATION_CLOSED → IN_PROGRESS → COMPLETED → CLOSED → ARCHIVED`,
   with `CANCELLED`; registration states are optional (direct entry allowed).
8. **Registration**: `DRAFT → SUBMITTED → APPROVED | REJECTED | WAITLISTED`, then `WITHDRAWN` / `DISQUALIFIED`.
9. Walkovers/forfeits are **result methods**, not contest statuses.

## Consequences

- Positive: each state lives in exactly one place; fewer background jobs (derived states need none).
- Negative: some pack specs list states we no longer store; modules.md notes this.

## Rules affected

AR-15.

## Review trigger

A real competition workflow that cannot be represented without a new stored state — add it via ADR.
