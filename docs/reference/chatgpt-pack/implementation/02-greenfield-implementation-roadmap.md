# Greenfield Implementation Roadmap

The repository starts empty. The goal is to produce a working vertical slice early while preserving the final architecture.

# Phase 0 — Repository & Architecture Control

Deliver:
- monorepo structure
- docs copied into repository
- architecture rules
- ADR template
- action registry
- error standards
- CI
- formatting/linting
- branch/build conventions

Exit criteria:
- CI runs on empty skeleton
- architecture docs versioned
- solution/apps compile

# Phase 1 — Backend Platform Foundation

Deliver:
- ASP.NET Core host
- module registration pattern
- PostgreSQL
- EF Core
- Redis
- Docker Compose
- configuration/secrets conventions
- Problem Details
- correlation IDs
- structured logging
- health/readiness endpoints
- OpenTelemetry baseline
- auth abstraction
- tenant context
- permission/entitlement evaluation interfaces
- audit interface
- idempotency building block
- outbox/domain-event baseline if selected

Exit criteria:
- API boots from Docker/local
- DB migrates
- Redis reachable
- health checks pass
- tenant context test passes
- integration-test harness established

# Phase 2 — Configuration Foundation

Implement:
1. Sports Definition
2. Competition Format
3. Ruleset Registry

Seed:
- Football
- Basketball
- Rugby 15s
- Rugby 7s
- Cricket T20
- baseline format templates
- demo rulesets

Exit criteria:
- configuration versions publish immutably
- compatibility validation works
- admin API can retrieve effective configuration

# Phase 3 — Tenancy & Identity

Implement:
- Tenant
- Subscription & Entitlements
- Person
- Participant/Athlete
- Organisation
- Team
- Staff
- Official
- Venue

Exit criteria:
- two tenants isolated
- same person can be athlete + staff + official
- duplicate-person warning works
- organisation/team separation demonstrated

# Phase 4 — Team Operations

Implement:
- Team Membership
- Roster
- Transfer capability

Exit criteria:
- membership history preserved
- competition roster distinct from membership
- transfer updates memberships transactionally
- historical roster unaffected

# Phase 5 — Competition Setup

Implement:
- Competition
- Edition
- Registration
- Eligibility
- Stage/Group instantiation

Exit criteria:
- tenant admin can create football league edition
- approve teams
- build competition rosters
- instantiate league/group structure

# Phase 6 — Scheduling & Pre-Match

Implement:
- Scheduling
- Official assignment
- Match squad/lineup
- readiness checks

Exit criteria:
- fixtures generated/manual
- venues assigned
- reschedule history works
- lineup validation works

# Phase 7 — First Full Vertical Slice: Football

Implement:
- Contest Operations
- Live Scoring
- Timeline
- Result Approval
- Progression
- basic Statistics
- Read Models

Acceptance:
```text
Create league
→ register teams
→ roster players
→ generate fixture
→ assign officials
→ submit lineup
→ start match
→ score goal
→ add assist/card/substitution
→ end match
→ submit result
→ approve result
→ standings update
→ public UI shows result/timeline/table
```

This is the first production-shaped milestone.

# Phase 8 — Public & Admin UI Foundations

Admin:
- navigation
- dashboards
- entity directories
- forms
- competition wizard
- match operations/scorer

Public:
- home/live
- competition detail
- match detail
- team/player pages
- standings

Shared:
- design tokens
- renderer registry
- loading/error/empty states
- responsive tablet/mobile/desktop

# Phase 9 — Multi-Sport Architecture Test

Add:
- Basketball
- Rugby 15s
- Rugby 7s
- Cricket T20

The objective is not only features; it is proving generic abstractions.

No core redesign should be required to support:
- quarters/overtime
- rugby variants/bonus points
- pools+knockout
- complex cricket score/innings/ball-by-ball

# Phase 10 — Extended Sports

Add:
- Golf
- Athletics
- MMA
- Boxing
- Volleyball
- Netball
- Hockey
- Tennis

# Phase 11 — Information & Data Operations

Implement:
- Statistics expansion
- Historical Records
- Search
- Reporting
- Historical Backfill
- Provider Integration
- Notifications

# Phase 12 — Commercial Productization

Implement:
- Branding/White Label
- Sponsorship
- plan/entitlement UI
- quotas/usage
- custom domain workflows

# Phase 13 — Mobile/Offline Scorer Hardening

- local database
- pending event queue
- sync/retry
- conflict UX
- score reconciliation
- reconnect replay
- offline E2E

# Phase 14 — Production Hardening

- load tests
- security review
- tenant isolation audit
- backup/restore
- disaster recovery runbook
- monitoring/alerts
- retention
- privacy/export/delete policies
- performance tuning

# Deferred Until Proven Necessary

Do not introduce prematurely:
- microservices
- Kubernetes
- RabbitMQ
- Elasticsearch/OpenSearch
- separate databases per module
- event sourcing for the entire platform
