# Events, projections, realtime and jobs

Decision: ADR-0009.

## Three kinds of messages

| Kind | Scope | Delivery | Example |
|---|---|---|---|
| Domain event | Inside one module | In-process, before commit | `PersonCreated` handled by the module's own duplicate index |
| Integration event | Between modules | Outbox → processor → subscribers (at-least-once) | `ResultApproved` → Progression recalculates standings |
| Realtime message | To browsers/apps | SignalR, after commit | `contest.timeline.event.recorded` to `contest:{id}` |

## Integration events

- Defined in the producer's `*.Contracts` project as immutable records, past tense, versioned
  (`ResultApproved.V1`). Additive changes only; breaking change → new version, publish both during migration.
- Envelope: `event_id, event_name, event_version, occurred_at, tenant_id, correlation_id, causation_id,
  actor_id, aggregate_type, aggregate_id, payload`.
- No secrets or `PRIVATE`/`SENSITIVE` personal data in payloads — IDs and display names only.
- Catalogue exported to `docs/generated/events.md` and `contracts/events.json` (CI check, like actions).

### Outbox / inbox

```text
handler ──(same transaction)──► <schema>.outbox_messages
OutboxProcessor (hosted service, polls ~500 ms, batches, FOR UPDATE SKIP LOCKED)
   └─► for each subscriber: begin tx → check <consumer schema>.inbox_messages(event_id, consumer)
                            → handle → insert inbox row → commit
```

- Retries with exponential backoff; after N failures the message is marked `FAILED` and listed in the platform
  diagnostics page (re-drive button, audited).
- **Audit uses the same path:** for successful audit-required actions the command pipeline writes an
  `AuditRecorded` message to the module's outbox in the same transaction as the change, and Platform appends it
  to `audit.audit_entries` (so audit can never disagree with what committed). Authorization denials and failures
  that have no module transaction are written directly.
- Subscribers run under the event's tenant context.
- Ordering: per aggregate, events are delivered in `occurred_at`/sequence order; consumers must still tolerate
  duplicates and gaps (use versions, e.g. standings ignore a `ResultApproved` older than what they've applied).

## Projections

- Owned by the consuming module (Insights read models, Progression standings, Engagement deliveries).
- Each projection has: a handler per event, a **rebuild command** that recomputes from authoritative data via
  Contracts queries, and a version/updated-at for staleness display.
- Rebuilds are idempotent and can run per tenant, per edition, or per contest.

## Realtime (SignalR)

- Hub at `/hubs/live`. Groups: `tenant:{tenantId}`, `competition:{competitionId}`, `edition:{editionId}`,
  `contest:{contestId}`, `user:{userId}` (admin notifications).
- Live contest changes are pushed **directly after commit** by Match Operations (low latency), not via the
  outbox. Message shape:

```json
{ "type": "contest.timeline.event.recorded", "contestId": "…", "version": 57,
  "occurredAt": "2027-03-14T15:42:10Z", "payload": { "event": { }, "summary": { } } }
```

- Message types: `contest.status.changed`, `contest.phase.changed`, `contest.clock.changed`,
  `contest.timeline.event.recorded`, `contest.timeline.event.corrected`, `contest.timeline.event.reversed`,
  `contest.result.approved`, `edition.standings.updated`.
- Clients keep the last applied `version` per contest; on a gap or reconnect they fetch
  `GET …/contests/{id}/timeline?afterVersion=n` instead of reloading the page. Never refetch whole day feeds
  after each event.
- Public clients join groups anonymously (tenant resolved by host); admin clients authenticate.
- One instance in v1; a Redis backplane needs an ADR when scaling out (ADR-0013).

## Background jobs

| Mechanism | Use |
|---|---|
| Hosted services | Outbox processor; lightweight periodic housekeeping |
| Hangfire (PostgreSQL storage) | Scheduled business jobs: "starting soon" notifications, reminders, subscription expiry notices, report generation, projection rebuilds, import execution |

Rules: jobs are idempotent (natural keys or dedup tables), carry `tenant_id` explicitly, log with correlation
IDs, and record audit entries for state changes they cause (actor type `SYSTEM`). The Hangfire dashboard is
platform-admin only.
