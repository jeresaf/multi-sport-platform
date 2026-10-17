# S12 — Live scoring and timeline (outline)

> **Status: outline.** The reviewer writes the detailed spec before this slice starts.

**Goal:** a scorer runs a football match on a tablet; the timeline is the record; the score is derived; the public
match page updates live.
**Modules:** Match Operations, Sports Configuration (engine), Insights (match read model), admin + public web.
**Reference:** ADR-0002, docs/architecture/sport-engine.md, events-and-jobs.md (realtime),
docs/product/ux/admin-app.md (scorer workspace), pack spec 27.

## Scope

- `operations.timeline_events` + actors; record endpoint (idempotent `clientEventId`, server `sequence`,
  engine validation with live state incl. personnel); corrections (supersede) and reversals; correction window
  for scorers.
- Period controls (`PERIOD_START/END`) guided by engine `REQUIRES_*` hints; clock state and clock events;
  `STATUS_CHANGED` system events from now on.
- Score snapshot written in the same transaction; rebuild command.
- Scorer lease (acquire, renew, take over with confirmation + audit).
- SignalR hub, groups and versioned messages; gap detection and catch-up by version.
- **Scorer workspace** (tablet-first): football pad from definition metadata, actor pickers, undo/correct,
  connection badge, short reconnect queue.
- Public contest page live (Summary, Timeline, Lineups) via read model + SignalR.

## Key acceptance

- The scripted match (start → goal with scorer + assist → yellow → substitution → half-time → second half →
  second goal → VAR overturn → end) works in the UI; the public page reflects each change within 2 seconds.
- Re-sending the same `clientEventId` never duplicates; reconnecting fetches missed events by version.
- A user without a scorer assignment for the contest is denied (test).
