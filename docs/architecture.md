# Multi-Sport Platform architecture

**Status:** Current implementation record  
**Last verified:** 2026-08-10

This document describes the architecture that exists in this repository. The
[SRS](SRS.md) defines requirements, [API reference](api-reference.md) defines the
HTTP/SignalR surface, and [ADRs](adr/README.md) preserve the reasons behind the
major choices. `task-progress.md` remains the evidence-based delivery record.

## System context

```text
 Browsers                          Mobile (Flutter)
 ┌───────────────┐                ┌────────────────────┐
 │ Admin Next.js │                │ Public + scorer UI │
 │ Public Next.js│                │ Drift offline queue│
 └───────┬───────┘                └─────────┬──────────┘
         │ HTTPS / REST / SignalR           │
         └──────────────┬───────────────────┘
                        ▼
             ┌─────────────────────┐
             │ nginx (deployment)  │
             └──────────┬──────────┘
                        ▼
             ┌─────────────────────┐
             │ ASP.NET Core API    │
             │ modular monolith    │
             │ controllers + hub   │
             │ hosted workers      │
             └───┬─────┬─────┬────┘
                 │     │     │
          ┌──────┘     │     └─────────────┐
          ▼            ▼                   ▼
 ┌──────────────┐ ┌──────────┐   ┌─────────────────┐
 │ PostgreSQL   │ │ Redis    │   │ MinIO / Firebase│
 │ authority    │ │ optional │   │ objects / push  │
 └──────────────┘ └──────────┘   └─────────────────┘
```

There is no separate API gateway or set of backend microservices. nginx is the
deployment reverse proxy; the API is one deployable process. PostgreSQL is the
server-side source of truth in every environment. Drift uses SQLite only inside
the mobile application. Redis is a degradable dependency, not an authority.

The staging topology also includes Mailpit, PostgreSQL backup, Prometheus,
Alertmanager, and Grafana. See the [monitoring](operations/monitoring.md),
[backup](operations/backup-recovery.md), and
[configuration](operations/configuration-secrets.md) runbooks.

## Domain terminology

| Term | Meaning in this system |
|---|---|
| Tenant | An organization whose users, branding, subscription, and business data are isolated from other organizations |
| Sport | The stable catalogue identity for a sporting discipline |
| Ruleset version | A published, numbered snapshot of a sport's contest shape, segments, events, metrics, result methods, ranking, display, and validation configuration |
| Competition | A tenant-owned sporting program pinned to a sport/ruleset and containing editions |
| Edition | A dated occurrence of a competition containing ordered stages |
| Stage | A phase such as league, group, knockout, heat, leaderboard, or fight card; it may contain groups and rounds |
| Contest | The universal scheduled/scored aggregate: match, game, race, bout, round, or equivalent event |
| Participant | A team or individual athlete; a contest participant/entry is its ordered placement in one contest |
| Segment | A nested unit of contest progression, such as a half, quarter, round, hole, innings, or over |
| Scoring event | An ordered, idempotent persisted fact that changes or describes contest state |
| Result | The submitted/reviewed outcome of a contest or participant, published only after the configured workflow |
| Derived projection | Rebuildable standings, bracket, leaderboard, or contest snapshot calculated from persisted authority |
| Idempotency key | A stable client/server key that makes retrying the same scoring command produce no duplicate fact |
| Audit entry | Append-only, actor-attributed evidence of a domain-significant or security-sensitive action |

## Repository and runtime boundaries

| Area | Responsibility | May depend on |
|---|---|---|
| `backend/BuildingBlocks` | Entity/aggregate primitives and shared domain contracts | No feature module |
| `backend/Shared` | Cross-cutting configuration and small shared extensions | Building blocks and framework packages |
| `backend/Modules/*` | Domain entities, contracts, policies, and focused engines | Building blocks; explicit module references only |
| `backend/Infrastructure` | EF Core persistence, repository/service implementations, workers, external delivery | Domain module contracts and shared configuration |
| `apps/api` | Composition root, HTTP controllers, middleware, authorization, SignalR | Modules and infrastructure |
| `apps/admin-web` | Authenticated tenant/platform operations UI | Published API contracts |
| `apps/public-web` | Anonymous tenant-branded projections | Public API contracts |
| `apps/mobile` | Public-first supporter and authenticated scorer flows | Public/auth/scoring APIs and SignalR |

The API composition root registers implementations in `Program.cs`. Modules do
not start processes, own HTTP hosts, or communicate through a broker. They share
one `SportsPlatformDbContext`, so their separation is a code ownership boundary,
not independent persistence. New cross-module dependencies must be explicit
project references and should use public contracts rather than internal entity
mutation.

### Domain modules

| Capability | Modules |
|---|---|
| Access and tenancy | Identity, Tenancy, Subscriptions, Branding |
| Competition structure | Sports, Competitions, Contests, Scheduling, FormatEngines |
| People and entry | Participants, Registrations, Officials |
| Scoring and projections | LiveScoring, Results, Standings, Brackets, Leaderboards |
| Operations and engagement | Audit, Reporting, Notifications, Sponsorship |

`FormatEngines` explicitly depends on competition and contest contracts.
`LiveScoring` depends on contest and result contracts. `Results` depends on
contest contracts. Most other modules depend only on BuildingBlocks;
Infrastructure supplies their persistence and orchestration.

## Universal contest model

The `Contest` aggregate is the common execution model for a match, race, bout,
round, or other scored event. It owns or associates:

- tenant, sport, competition/stage, and pinned ruleset identity;
- lifecycle state and authoritative clock state;
- ordered participants/entries and assigned officials;
- nested segments such as halves, quarters, rounds, holes, innings, and overs;
- ordered, idempotent contest/live-score events;
- result, qualification, and derived snapshot inputs.

Sport packages configure the shape and validate operations; they do not create
separate contest tables. Competition format engines create and connect contests
for round robins, knockouts, group-plus-knockout, best-of series, multi-round
leaderboards, heat qualification, and fight cards. Approved results rebuild
standings, brackets, leaderboards, and deterministic derived snapshots from
persisted authority.

See [ADR-0002](adr/0002-universal-contest-model.md).

## Tenancy and authorization

Tenant resolution uses the authenticated `tenant_id` claim first, then an
explicit `X-Tenant-Id` header, then `X-Tenant-Slug` for supported public flows.
Public routes also resolve the tenant slug in their resource query. The current
system does not infer a tenant from the DNS subdomain.

Tenant isolation is defense in depth:

1. JWT/permission policies establish the actor and allowed capability.
2. Middleware records the resolved tenant for downstream operations.
3. Services and repositories scope reads and writes by `TenantId` and validate
   ownership of referenced resources.
4. SignalR validates contest/competition/segment ownership before group joins.
5. Workers establish an explicit tenant scope rather than inheriting an HTTP
   context.
6. PostgreSQL foreign keys, unique indexes, and append-only audit protection
   preserve data integrity.

The shared database currently has no universal EF global tenant query filter and
does not claim PostgreSQL row-level security. Explicit scoping is therefore a
mandatory service/repository invariant and is covered by cross-tenant HTTP tests
against both in-memory EF and PostgreSQL.

See [ADR-0003](adr/0003-shared-schema-tenancy.md).

## Ruleset versioning

`Sport` owns ordered `SportDefinitionVersion` records. A version contains the
contest shape plus JSON definitions for segments, events, metrics, result
methods, ranking, display, and validation. Publishing makes a version available;
competitions and contests pin its ID so later sport-definition changes do not
rewrite historical behavior.

Football, basketball, rugby, golf, combat sports, athletics, and T20 cricket
configure published versions through sport-specific services. Scoring and
result workflows validate against the pinned version where applicable.

See [ADR-0006](adr/0006-versioned-sport-rulesets.md).

## Offline scoring and synchronization

The mobile app persists the exact HTTP method, endpoint, payload, UUID, and local
sequence number in Drift before replay. It processes the queue in sequence and
stops on transient failure so later actions cannot overtake earlier ones.

```text
 scorer action
      │
      ▼
 Drift queue ── offline/transient ──► remain PENDING
      │ online
      ▼
 ordered API replay ── 2xx ──► SYNCED
      ├─────────────── 409 ──► CONFLICT (operator review)
      └──── 400/403/422 ─────► REJECTED (operator review)
```

Server idempotency keys and unique constraints make replay safe. Conflicted or
rejected events remain reviewable and can be superseded; they are not silently
dropped. Authentication refresh and tenant headers are applied by the normal API
client. The outstanding physical-device rerun is delivery evidence, not a
different architecture.

See [ADR-0004](adr/0004-durable-ordered-offline-replay.md).

## Real-time updates

REST commands persist authoritative state before `ContestHubNotifier` publishes
SignalR notifications. Authenticated subscriptions use tenant-qualified contest,
competition, segment, and tenant groups after resource ownership checks.
Anonymous spectators may join only a publicly eligible contest (not Draft or
Cancelled) belonging to the supplied active tenant slug. A snapshot is sent on
join so clients do not depend on receiving every earlier event.

SignalR is an update channel, not persistence. Reconnecting clients reload a
snapshot/REST projection, and mobile offline writes always replay through the
HTTP command path.

See [ADR-0005](adr/0005-signalr-after-persistence.md).

## Audit and derived state

Security-sensitive and domain mutations record `AuditEntry` data including
tenant, actor, action, entity, before/after JSON, reason, timestamp, IP, and user
agent when available. `SportsPlatformDbContext` rejects tracked audit updates or
deletes, and staging/production install a PostgreSQL trigger that enforces the
append-only rule below EF Core.

Auditing is explicit for business meaning and augmented at persistence for live
scores and contest lifecycle transitions. It is not a claim that every database
column assignment is automatically audited. Corrections retain original and
proposed values; approved mutations rebuild dependent snapshots and projections
while preserving the decision history.

See [ADR-0007](adr/0007-append-only-domain-audit.md).

## Cross-cutting runtime behavior

- Startup validates mandatory configuration before service initialization.
- JWT authentication, dynamic permission policies, rate limits, tenancy,
  subscription read-only enforcement, and usage metering wrap command routes.
- Serilog JSON logs carry request/correlation/trace/tenant/user context.
- OpenTelemetry instruments ASP.NET Core traces and metrics.
- `/health/live` checks the process; `/health/ready` reports PostgreSQL and Redis.
- Hosted workers handle subscription lifecycle, privacy retention, reports, and
  notification delivery with explicit persistence boundaries.

## Decision and change rules

- Update this document when a system boundary or authoritative data flow changes.
- Add or supersede an ADR when changing a decision and its trade-offs.
- Update the SRS only when requirements change; do not use it as progress proof.
- Update `task-progress.md` only after relevant tests or runtime checks pass.
