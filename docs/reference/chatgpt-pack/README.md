# Sports Platform — Greenfield Implementation Pack

This is the implementation-control package for starting the Sports Platform from an **empty repository**.

It consolidates the 43 domain/system specifications produced during architecture design and adds the cross-cutting controls required to implement them safely.

## Package Structure

```text
specifications/
    01..43 source system specifications

control/
    canonical glossary
    entity ownership matrix
    system dependency matrix
    action/permission/entitlement registry
    domain event catalogue
    state machine catalogue
    database schema ownership map
    API contract standards
    error code standards
    architecture rules

implementation/
    repository structure
    greenfield roadmap
    Codex bootstrap execution plan
    first football vertical slice acceptance
    definition of ready/done

templates/
    canonical system specification template
    ADR template
```

## Architecture Starting Position

```text
Backend:
ASP.NET Core modular monolith

Database:
PostgreSQL, one database with logical schemas

Cache / transient coordination:
Redis

Web:
Next.js + TypeScript + Tailwind + TanStack Query + SignalR

Mobile:
Flutter + local SQLite/Drift + offline scorer queue

Infrastructure:
Docker Compose initially

Deferred until justified:
Microservices
Kubernetes
RabbitMQ
Elasticsearch/OpenSearch
Database-per-module
```

## Implementation Rule

Do not start by generating every domain module.

Start with:
1. repository controls
2. platform foundation
3. configuration systems
4. identity/registry systems
5. competition setup
6. one complete football vertical slice
7. use basketball/rugby/cricket to prove generic architecture
8. expand product features only after the vertical slice is sound

## Source-of-Truth Hierarchy

```text
Specifications
    ↓
Control documents
    ↓
ADRs for implementation decisions
    ↓
Code + migrations + tests
    ↓
Generated API/OpenAPI/event schemas
```

When code and specification disagree, do not silently choose one. Record the discrepancy and update the appropriate source deliberately.
