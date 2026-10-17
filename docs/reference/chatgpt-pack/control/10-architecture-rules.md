# Architecture Rules

These rules are mandatory for the greenfield repository.

## AR-001 — Modular Monolith First

The initial backend is one deployable ASP.NET Core modular monolith. Module boundaries are business boundaries, not microservices.

## AR-002 — No Cross-Module Table Mutation

A module cannot directly update another module's owned data.

## AR-003 — Source-of-Truth Ownership

Every entity has exactly one owning module.

## AR-004 — Configuration Over Sport Hard-Coding

Generic modules must not scatter conditions such as:

```csharp
if (sport == Football)
```

Sport Definition + Ruleset + renderer/adapter contracts provide specialization.

## AR-005 — Universal Contest

Generic backend code models `Contest`, not always `Match`.

## AR-006 — Version Published Configuration

Sport definitions, competition formats, and rulesets become immutable after publish.

## AR-007 — Preserve History

Use versioning, effective dates, corrections, reversals, merge records, and status history instead of destructive overwrite.

## AR-008 — Tenant Isolation Is Structural

Tenant filtering is enforced below controllers/UI and tested continuously.

## AR-009 — Permission ≠ Entitlement ≠ Domain Rule

All three are independently evaluated.

## AR-010 — Person Is Canonical Human Identity

Athletes, staff, and officials reference Person.

## AR-011 — Organisation ≠ Team

Organisations may operate many teams.

## AR-012 — Membership ≠ Roster ≠ Squad

These relationships remain separate.

## AR-013 — Schedule Status ≠ Contest Status ≠ Sport Phase

Never combine these into one enum.

## AR-014 — Live Score ≠ Official Result

Approved results own official truth; live score remains operational/provisional.

## AR-015 — Progression Uses Approved Results

Standings/brackets/qualification do not consume provisional scores as official state.

## AR-016 — Timeline Is Structured

Timeline events have codes, actors, sequence, correction semantics, and optional sport metadata.

## AR-017 — Imports Use Domain Commands

No direct-table historical import.

## AR-018 — Provider IDs Are Secondary

Internal canonical IDs remain stable regardless of provider.

## AR-019 — Read Models Are Rebuildable

Public/admin projections are not source of truth.

## AR-020 — Statistics Are Derived

Statistics consume authoritative sport data and can be recalculated.

## AR-021 — Audit Is Append-Only

Audit is not equivalent to application logging.

## AR-022 — No Generic Constants Dumping Ground

Domain-owned statuses/rules remain in their domains.

## AR-023 — Postgres One Database Initially

Use logical schemas/module ownership, not one database per module.

## AR-024 — Infrastructure Complexity Must Be Earned

Do not introduce Kubernetes, RabbitMQ, Elasticsearch, or microservices without measured need and ADR.

## AR-025 — Shared Contracts Are Minimal

Do not create a giant common/shared project containing business logic.

## AR-026 — Tests Enforce Architecture

Architecture tests must prevent forbidden dependencies and tenant leaks.

## AR-027 — API Contracts Are Stable DTOs

Persistence entities do not cross API boundary.

## AR-028 — Offline Scoring Is Idempotent

Client-generated event IDs and sequence/version conflict logic are mandatory.

## AR-029 — UI Uses Presentation Metadata

Public/admin clients consume generic renderer/presentation contracts rather than hard-coded sport navigation behavior.

## AR-030 — ADR for Boundary Changes

Any change to module ownership or major technology choice requires an Architecture Decision Record.
