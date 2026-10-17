# ADR-0009 — In-process domain events, transactional outbox for integration events, Hangfire for scheduled jobs

**Status:** Accepted
**Date:** 2026-10-17
**Deciders:** Owner, Reviewer

## Context

Read models, statistics, standings, notifications and search all react to changes in other modules. They must
not miss events (a lost `ResultApproved` means a wrong league table), but the platform should not need a message
broker yet. The previous plan left "outbox if selected" open and dropped Hangfire from the first summary's stack.

## Decision

- **Domain events** (inside one module) are raised by aggregates and handled in-process before the
  transaction commits.
- **Integration events** (between modules) are written to the producing module's
  `<schema>.outbox_messages` table **in the same transaction** as the state change. A background
  `OutboxProcessor` (hosted service) delivers them to in-process subscribers with retries and backoff.
  Each consumer records processed message IDs in its own `<schema>.inbox_messages` table — consumers are
  idempotent. Poison messages are marked failed after N attempts and surfaced in an admin diagnostics view.
- Event envelope: `event_id, event_name, event_version, occurred_at, tenant_id, correlation_id,
  causation_id, actor_id, aggregate_type, aggregate_id, payload`.
- **Scheduled/business jobs** (reminders, "starting soon" notifications, subscription expiry notices, report
  generation, projection rebuilds) use **Hangfire with PostgreSQL storage**, introduced in the first slice that
  needs a scheduled job. Jobs are idempotent and carry tenant context explicitly.
- Read models and statistics must be **rebuildable** from authoritative data, not only from the event stream.
- Audit entries for successful actions travel through the producing module's outbox (`AuditRecorded`), so they
  commit atomically with the change without sharing DbContexts across modules.
- No RabbitMQ/Kafka/MassTransit until an ADR shows a measured need (ADR-0013).

## Alternatives considered

- **Fire-and-forget in-memory events.** Rejected: lost on crash.
- **Message broker now.** Rejected: operational cost; MassTransit v9+ is commercially licensed.
- **Quartz.NET instead of Hangfire.** Viable (Apache-2.0). Hangfire chosen for its dashboard and the first
  summary's choice; note Hangfire core is LGPL-3.0, acceptable for use as an unmodified library.

## Consequences

- Positive: at-least-once delivery with idempotent consumers, using only PostgreSQL.
- Negative: outbox polling adds small latency (target < 1 s); live score pushes go directly through SignalR
  after commit, not through the outbox, to keep live latency low (docs/architecture/events-and-jobs.md).

## Rules affected

AR-16, AR-17.

## Review trigger

Multiple app instances with high event volume, or an external system needing a durable event feed.
