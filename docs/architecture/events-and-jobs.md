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
- No secrets or `PRIVATE`/`SENSITIVE` personal data in payloads — IDs and `PUBLIC` display fields only (ADR-0015).
  The outbox tables have no row-level security (Q15), so this rule is what keeps them safe.
- Catalogue exported to `docs/generated/events.md` and `contracts/events.json` (CI check, like actions).

### Outbox / inbox

```text
handler ──(same transaction)──► <schema>.outbox_messages
OutboxProcessor (hosted service, polls ~500 ms, batches, FOR UPDATE SKIP LOCKED)
   └─► for each subscriber: begin tx → check <consumer schema>.inbox_messages(event_id, consumer)
                            → handle → insert inbox row → commit
```

- Retries with exponential backoff; after N failures the message is marked `FAILED` and listed in the platform
  diagnostics page (re-drive button, audited; endpoints below).
- **Audit uses the same path:** for successful audit-required actions the command pipeline writes an
  `AuditRecorded` message to the module's outbox in the same transaction as the change, and Platform appends it
  to `audit.audit_entries` (so audit can never disagree with what committed). Authorization denials and failures
  that have no module transaction are written directly.
- Subscribers run under the event's tenant context.
- Ordering: per aggregate, events are delivered in publication (`sequence`) order, one after the other; a message
  that is retrying or `FAILED` holds back the later ones of its aggregate (owner, S01-T5a). Consumers must still
  tolerate duplicates and gaps (use versions, e.g. standings ignore a `ResultApproved` older than what they've
  applied).

### How it is built (S01-T5a)

- **Publishing.** A use-case handler or a domain event subscriber calls
  `context.Publish(new ResultApproved.V1(...), aggregateType, aggregateId)` on its module's DbContext; the message
  is saved by the same `SaveChangesAsync` as the change. Event types are records named `V<n>` nested in a static
  class named after the event (`IntegrationEventName`): `event_name` is the class name, `event_version` the number.
  Payloads are JSON (camelCase, enums as UPPER_SNAKE strings: `MessageSerialization`).
- **Domain events.** `IDomainEventSubscriber<TEvent>`, registered with `services.AddDomainEventSubscriber<…>()`.
  `MessagingInterceptor` dispatches them during `SaveChangesAsync`, before the audit-column interceptor, in rounds
  (events raised by subscribers go to the next round; more than 10 rounds throws). Synchronous `SaveChanges` throws
  while domain events are pending.
- **Envelope.** Stamped when the message is saved: `occurred_at` (injected `TimeProvider`), `tenant_id`
  (`ITenantContext`), `correlation_id` and `causation_id` (`CorrelationContext`: a request's `X-Correlation-Id`;
  while consuming, the consumed event's correlation ID and its ID as causation), `actor_id` (`ICurrentActor`).
- **Tables.** Every module schema has `outbox_messages` (envelope, `sequence` (identity, publication order),
  `payload jsonb`, `status`, `attempts`, `next_attempt_at`, `last_error`, `processed_at`) and `inbox_messages` (primary key `message_id, consumer`), mapped
  by `ModuleDbContext`. Both are read-write for the runtime roles. `tenant_id` is a nullable envelope column; the
  tables are not tenant-owned and have no RLS; the processor uses the `App` connection and never the superuser (Q15,
  owner 2026-10-18).
- **Consumers.** `IIntegrationEventConsumer<TEvent>`, registered with
  `services.AddIntegrationEventConsumer<TEvent, TConsumer, TContext>("<module>.<purpose>")`. The consumer name is
  stored in the inbox: never rename it. Each delivery gets its own scope (tenant and correlation set from the
  envelope) and a transaction on `TContext`: skip if the inbox has the message, else handle, add the inbox row, save,
  commit. The consumer changes data through `TContext` and does not commit itself.
- **Processor.** `OutboxProcessor.ProcessOnceAsync` takes each module in turn (a module that fails is logged and
  skipped): in one transaction it claims up to `BatchSize` due `PENDING` messages (`next_attempt_at <= now`, by
  `sequence`, `FOR UPDATE SKIP LOCKED`) that have no earlier message of the same aggregate still un-`PROCESSED`,
  delivers each to its consumers, records the outcome and commits. A message is `PROCESSED` when every consumer succeeded
  (or there is none). Any failure (exception, unreadable payload) is a failed attempt: retried after 2, 4, 8 … s
  (capped at 5 min), `FAILED` after `MaxAttempts` (state machine in docs/domain/state-machines.md). Consumers that
  already succeeded are skipped on retry by their inbox. `last_error` keeps consumer, exception type and message
  (2,000 characters), so exception messages must not contain personal data.
- **Hosting.** The Platform module's hosted service calls the processor every `PollInterval`, and again straight
  away while a call attempted something (so a burst for one aggregate doesn't wait a tick per message). Settings (section
  `Outbox`): `Enabled` (true; false in `Testing`, where tests call the processor), `PollInterval` (500 ms),
  `BatchSize` (50), `MaxAttempts` (10). A module whose outbox can't be processed (database down, schema not
  migrated) is logged and skipped for 1, 2, 4 … 30 s (`OutboxModuleBackoff`), reset by its first success; the other
  modules keep their normal pace. A message with no consumers is logged at Debug.
- **Ordering.** A batch holds at most one message per aggregate, so an aggregate's messages are delivered one per
  call, in `sequence` order. A message waits while an earlier one of its aggregate is retrying, locked by another
  processor, or `FAILED` (until it is re-driven, T5b). Outbox message IDs are strictly increasing UUIDv7s
  (`SequentialIds`) because EF Core orders one save's inserts by key: the messages of one save get their
  `sequence` in the order they were published.
- **Catalogue (S01-T5b).** `export-catalogues` writes `docs/generated/events.md` and `contracts/events.json`: every
  `IIntegrationEvent` in each module's `*.Contracts` assembly (name, version, producer module, payload members as
  serialized with their type and nullability) and the consumers registered for it. Payload members are scalars or lists of
  scalars; enums are UPPER_SNAKE strings, listed with their values (never numbers, so a stored message keeps its
  meaning when members are added or reordered); `DateTime` and other types fail the export. The export also fails for an event declared twice, a badly
  named or struct event, a consumer subscribed twice, or a consumer subscribed to an event no Contracts declare; a test fails for an
  event declared outside a Contracts assembly. An event with no consumers is listed with none ("—"): its messages are
  marked `PROCESSED` undelivered.
- **Diagnostics (S01-T5b-2).** `GET /api/platform/v1/diagnostics/outbox` (`platform.outbox.read`) lists every
  module's messages newest first, by message ID (UUIDv7), with optional `module` (a module schema), `status`
  (`PENDING`/`PROCESSED`/`FAILED`), `limit` (1–100, default 25) and `cursor`. Items carry the envelope and delivery
  state, never the payload; `heldBackBy` is the earliest earlier message of the same aggregate that a `PENDING` message
  waits for. `POST /api/platform/v1/diagnostics/outbox/{module}/{messageId}/redrive` (`platform.outbox.redrive`, body
  `{"reason": "…"}`, 1–500 characters) locks the row and moves one `FAILED` message to `PENDING` with `attempts` 0, due
  now; `last_error` stays until the next outcome. Anything not `FAILED` is `409 PLATFORM_OUTBOX_MESSAGE_NOT_FAILED`. The
  code is `OutboxDiagnostics` (BuildingBlocks, next to the processor): like the processor it reads each module's table
  through that module's DbContext, never across schemas; the Platform module maps the endpoints. Fix the cause before
  re-driving: a message that fails again ends `FAILED` again after `MaxAttempts`.
- **Sizing.** The claim transaction stays open while its batch is delivered, and every delivery uses a second
  connection: count about two pooled connections per poll.

### Audit as built (S01-T6a)

- **Scope.** `AuditScope` (BuildingBlocks `Auditing`, scoped) holds the request's audited action (`REQUIRED` or
  `REQUIRED_WITH_REASON`; `NONE` is ignored), what it touched and why. The Host's `AuditMiddleware` begins it with the
  endpoint's action, after routing and before authorization. A handler describes the action:
  `audit.Describe(entityType, entityId, reason, metadata)`. `metadata` is a typed record, serialized at once by
  `AuditMetadata` with every `[PersonalData(PRIVATE | SENSITIVE)]` property written as `"[REDACTED]"`, so the value is
  in neither the outbox payload nor the table.
- **Through the outbox.** The first module DbContext save in the request's scope adds `AuditRecorded.V1` (declared in
  Platform's Contracts; aggregate type `AuditEntry`, one aggregate per entry, so an audit message never waits for
  another) to that module's outbox, in the save's transaction (`MessagingInterceptor`). Later saves add nothing. A save
  that throws or is cancelled detaches the audit message again, and the entry stays pending. Saves within one scope
  run one after another (the interceptor tracks one save at a time). Work that saves in a scope of its own (the
  outbox re-drive) gets `audit.CreateRecord(SUCCESS)` and calls `context.RecordAudit(record)`, then the handler calls
  `audit.MarkRecorded()` once that work has committed. Rule: one unit of work per command. A handler that saves
  inside an explicit transaction and then rolls it back loses the entry (owner, 2026-10-18). Platform's consumer
  `platform.audit` appends the entry; the entry ID is the record's UUIDv7, taken when the action happened.
- **Directly.** After the response, `AuditMiddleware` writes an entry no save recorded, through `IAuditLog`
  (Platform's Contracts; `PlatformDbContext` in its own scope): 401/403 → `DENIED`, any other 4xx/5xx or an exception
  (a client that disconnects included) → `FAILURE`, below 400 → `SUCCESS` (audited actions that save nothing, such as reads; owner, 2026-10-18). The Problem
  Details customizer records the response's `code` (`error_code`). A denied request's body is never read, so it has no
  reason. If the direct write fails, the response is already written: the middleware logs an Error and the entry is
  lost.
- **Table.** `audit.audit_entries` (Platform's second schema): `id, tenant_id (nullable), actor_kind, actor_id,
  action_code, outcome (SUCCESS | FAILURE | DENIED), error_code, entity_type, entity_id, occurred_at, source (API |
  SYSTEM), correlation_id, reason, metadata jsonb` and `created_*`; `AppendOnly` (runtime roles SELECT and INSERT;
  triggers reject UPDATE, DELETE and TRUNCATE for every role). No RLS until S02 (owner, 2026-10-18).
- **Query (S01-T6b).** `GET /api/platform/v1/audit-entries` (`platform.audit.read`, audited: each page writes a
  direct `SUCCESS` entry with the filters as metadata, so audit screens must not poll). Filters, combined with AND:
  `tenantId`, `actorId`, `action` (action-code format, not checked against the catalogue, so retired codes can be
  found), `entityType`, `entityId` (needs `entityType`), `correlationId`, `outcome`, `from` (inclusive) and `to`
  (exclusive) as ISO 8601 instants with an offset. Newest first by `(occurred_at, id)`; the cursor encodes both.
  Entries arriving through an outbox can land behind a cursor already read. `metadata` is returned as stored (already
  redacted).

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
