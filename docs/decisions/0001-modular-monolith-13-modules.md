# ADR-0001 — Modular monolith with 13 modules

**Status:** Accepted
**Date:** 2026-10-17
**Deciders:** Owner, Reviewer

## Context

The previous plan (docs/reference/chatgpt-pack) described 43 "systems" and a repository with ~38 backend
modules, each potentially split into Domain/Application/Infrastructure/Contracts/Tests projects. Many of
those "systems" are aggregates inside one business area (e.g. Person, Athlete Profile, Staff Profile and
Official Profile are all registry records; Scheduling, Contest Operations, Lineups, Live Scoring, Timeline
and Results all operate on the same Contest). The pack's own database map already grouped them into ~15
PostgreSQL schemas.

Splitting tightly-coupled concepts into separate modules creates cross-module reconciliation work
(e.g. "reconcile live score with timeline", "reconcile result with score") and a large amount of
scaffolding before any feature works — a known failure mode for AI-assisted builds.

## Decision

One deployable ASP.NET Core application (modular monolith) with **13 modules**, each owning one
PostgreSQL schema:

| # | Module | Schema |
|---|---|---|
| 1 | Platform | `platform`, `audit` |
| 2 | Identity & Access | `identity` |
| 3 | Tenancy & Commercial | `tenancy` |
| 4 | Sports Configuration | `config` |
| 5 | Registry | `registry` |
| 6 | Team Operations | `teamops` |
| 7 | Competitions | `competition` |
| 8 | Match Operations | `operations` |
| 9 | Standings & Progression | `progression` |
| 10 | Insights | `insights` |
| 11 | Data Exchange | `exchange` |
| 12 | Engagement | `engagement` |
| 13 | Branding & Sponsorship | `commercial` |

Full ownership is defined in docs/architecture/modules.md.

Physical layout per module: two projects — `SportsPlatform.Modules.<Name>` (domain, features,
infrastructure, endpoints) and `SportsPlatform.Modules.<Name>.Contracts` (the only thing other modules may
reference: query interfaces, DTOs, integration events). Modules are added to the solution **only when the
first slice that needs them starts** — never scaffold empty modules.

Boundary rules:
- A module never reads or writes another module's tables, DbContext or internal types.
- Cross-module **foreign keys are not created**; cross-module references are IDs validated through the
  owning module's Contracts at command time. Foreign keys inside a module are required.
- Cross-module reactions happen through integration events (ADR-0009).
- Architecture tests enforce the project-reference and namespace rules.

## Alternatives considered

- **Keep ~38 modules (pack).** Pros: very granular ownership. Cons: hundreds of projects/files before value;
  forced reconciliation between halves of one concept; slow builds; high review load.
- **Single project, folders only.** Pros: simplest. Cons: boundaries erode quickly with AI-generated code;
  harder to extract services later.
- **Microservices.** Rejected: operational cost far exceeds current scale (AR-024 in the pack agrees).

## Consequences

- Positive: each module is meaningful; the football vertical slice touches ~8 modules, not ~25.
- Positive: future extraction (e.g. Match Operations as a separate service) stays possible because of
  Contracts + no cross-module FKs.
- Negative: some modules are large (Registry, Match Operations). Internal folders by feature keep them
  navigable.
- The 43 pack specs remain useful reference for field lists, commands and error ideas; docs/architecture/modules.md
  maps every spec to its new home.

## Rules affected

AR-01, AR-02, AR-03 (docs/architecture/rules.md).

## Review trigger

A module exceeds what one person can hold in their head, or measured load requires independent scaling of
one area (most likely Match Operations live traffic or Insights read traffic).
