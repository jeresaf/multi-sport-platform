# Architecture rules

Mandatory for all code in this repository. Each rule names the ADR or document that explains it. The reviewer
checks every PR against these rules; architecture tests enforce the ones marked **[test]**.

## Structure and ownership

- **AR-01 Modular monolith, 13 modules.** One deployable backend. Modules are business boundaries
  (docs/architecture/modules.md). Create a module only when the first slice that needs it starts. (ADR-0001)
- **AR-02 No cross-module data access.** A module may reference only another module's `*.Contracts`
  project. No shared DbContexts, no cross-schema queries, no cross-module foreign keys. **[test]** (ADR-0001, ADR-0008)
- **AR-03 One owner per entity.** If ownership is unclear, the task is not ready — ask. (ADR-0001)
- **AR-26 Minimal shared kernel.** `SportsPlatform.BuildingBlocks` holds only technical primitives (IDs, Result/
  Error types, base entity, tenant/actor context abstractions, action attribute, entitlement definition (S04, owner on #63 R3), outbox
  abstractions, clock).
  No business logic, no domain enums. **[test]**
- **AR-32 ADR for boundary or technology changes.** Moving ownership, adding a module, or changing a major
  technology needs an ADR first.

## Tenancy, identity, authorization

- **AR-04 Tenant isolation is structural.** Every tenant-owned table has `tenant_id`; tenant context comes from
  authentication/host, never the request body; EF Core global filters + PostgreSQL RLS. (ADR-0004)
- **AR-05 Isolation is tested.** Every tenant-owned entity is covered by the isolation harness; every tenant
  table has an RLS policy. **[test]** (ADR-0004)
- **AR-06 Every state-changing operation has an action code.** Permission = action code. Actions are declared in
  code and exported to docs/generated/actions.md; CI fails if the export is stale or an endpoint lacks one.
  **[test]** (ADR-0005)
- **AR-07 Authorization = access mode ∧ entitlement ∧ scoped permission ∧ domain rule**, all evaluated
  server-side. The UI may hide actions but is never the enforcement point. (ADR-0005, docs/architecture/authorization.md)

## Sports, contests and results

- **AR-08 Timeline events are append-only.** Corrections are new events (`supersedes_event_id`,
  `reverses_event_id`); no UPDATE/DELETE on recorded events. (ADR-0002)
- **AR-09 Score and phase are derived** from the timeline by the scoring engine; snapshots are caches that can
  be rebuilt. (ADR-0002)
- **AR-10 Live score ≠ official result.** Standings, qualification and official statistics use **approved**
  result versions; anything provisional is labelled provisional. (ADR-0002)
- **AR-11 No sport-specific branching in generic code.** `if (sport == ...)`, `switch` on sport codes, or
  sport-named classes outside definitions/engines/renderers are defects. **[test where feasible]** (ADR-0003)
- **AR-12 Sport behaviour = definition data + scoring-family engine.** Engines are pure, deterministic and
  unit-tested with sport scenarios. (ADR-0003, docs/architecture/sport-engine.md)
- **AR-13 Published configuration is immutable.** Sport definitions, formats and ruleset templates change only
  by publishing a new version. **[test: hash check]** (ADR-0006)
- **AR-14 Editions run on a frozen rules snapshot.** Operational code reads the edition snapshot, never the
  live template; amendments are explicit, audited, versioned. (ADR-0006)
- **AR-15 Canonical state machines only.** Use docs/domain/state-machines.md. Status ≠ phase ≠ result; no
  combined enums; no duplicate states in two modules. (ADR-0007)

## Events, projections, history

- **AR-16 Cross-module reactions use outbox integration events**, delivered at-least-once; consumers are
  idempotent (inbox table). (ADR-0009)
- **AR-17 Projections are rebuildable and read-only to others.** Read models, statistics, standings and search
  can be rebuilt from authoritative data and are never written by other modules or used as a source of truth.
  (ADR-0009)
- **AR-18 History over overwrite.** Effective dates, versions and status history; no hard delete of business
  data that has been referenced (drafts may be deleted). (docs/architecture/data-conventions.md)
- **AR-19 Audit is append-only and automatic** for actions marked audit-required; audit ≠ application logs ≠
  analytics. **[test: DB trigger blocks UPDATE/DELETE]**
- **AR-28 Idempotency** for timeline recording, offline sync, imports, provider ingestion and any client-retried
  command (`Idempotency-Key` header or `client_event_id`).

## APIs, data, privacy

- **AR-20 Stable error codes.** Problem Details with `code` (`<AREA>_<CONDITION>`); UI branches on `code`,
  never on message text. (docs/architecture/api-conventions.md)
- **AR-21 DTOs are not entities.** Persistence entities never cross the API boundary; public DTOs expose only
  `PUBLIC` fields. **[test]**
- **AR-22 Imports use the front door.** Imports, backfill, seeds (beyond reference data) and provider sync call
  the same application commands as the UI. No raw table inserts.
- **AR-23 Canonical IDs never change.** External/provider/federation IDs live in external-identifier tables.
- **AR-24 Personal data by design.** Field classification, Registry as the PII boundary, minors private by
  default with guardian consent, no medical data, redaction in logs/analytics/audit. **[test: redaction]** (ADR-0015)
- **AR-31 Time.** Store instants as `timestamptz` (UTC); date-only values as `date`; display in venue or tenant
  time zone; inject `TimeProvider` — never `DateTime.Now`.

## Process and quality

- **AR-25 No new infrastructure or commercially-licensed dependency without an ADR.** (ADR-0013)
- **AR-27 Tests are part of every task.** Domain unit tests, integration tests against real PostgreSQL
  (Testcontainers), isolation tests for new tenant entities, Playwright for user flows. Architecture tests
  must stay green.
- **AR-29 UI uses presentation metadata.** Terminology (Match/Game/Bout), renderers, scorer pads and coverage
  come from sport definitions and API metadata — the UI doesn't hard-code sport behaviour.
- **AR-30 Never fake unsupported data.** If something isn't supported, show "not available", record the gap in
  docs/build/open-questions.md, and implement it deliberately later.
- **AR-33 Cloud-neutral application code.** Cloud-provider SDKs (e.g. `Azure.*`) are referenced only by
  infrastructure adapters registered in the host (file storage, secrets, telemetry exporters). Domain and
  application code never reference them, so changing provider touches only adapters and deployment scripts.
  **[test: architecture test on assembly references]** (ADR-0016)
