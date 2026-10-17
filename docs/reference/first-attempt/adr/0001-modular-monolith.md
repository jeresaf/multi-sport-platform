# ADR-0001: Deploy the backend as a modular monolith

- **Status:** Accepted
- **Date:** 2026-08-10

## Context

The platform has many related sports-management capabilities, but competitions,
contests, scoring, results, and projections require strongly consistent changes.
The team needs clear ownership without distributed transaction and operational
cost at the current scale.

## Decision

Deploy one ASP.NET Core API process and divide domain code into explicit module
projects. Keep HTTP composition and SignalR in `apps/api`, domain contracts in
modules, and persistence/external implementations in Infrastructure. Use one
PostgreSQL database and one EF Core context.

## Consequences

- Commands can commit related capability changes atomically.
- One deployment and observability surface simplifies Windows/Docker operations.
- Module boundaries require review because the shared process/database cannot
  enforce every boundary mechanically.
- A capability can be extracted later only after its contract and consistency
  boundary are proven.

## Alternatives

Microservices were rejected because their network, deployment, and consistency
cost currently exceeds their isolation benefit. An unstructured single project
was rejected because it hides ownership and increases coupling.

