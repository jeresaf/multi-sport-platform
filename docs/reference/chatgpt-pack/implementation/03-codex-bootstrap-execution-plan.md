# Codex Bootstrap Execution Plan

This is the first implementation sequence for an empty repository.

## Commit 001 — Repository Skeleton

Create:
- root README
- .editorconfig
- .gitattributes
- .gitignore
- global.json
- Directory.Build.props
- backend solution
- web workspace
- Flutter app placeholder
- docs directories
- CI placeholder

Do not implement business modules yet.

Acceptance:
- backend builds
- web builds
- Flutter analyzes
- CI succeeds

## Commit 002 — Local Infrastructure

Create:
- docker-compose.yml
- PostgreSQL
- Redis
- environment sample files
- local setup docs

Acceptance:
- one command starts dependencies
- health connectivity tests pass

## Commit 003 — ASP.NET Core Host

Create:
- API host
- configuration
- dependency injection conventions
- Problem Details
- correlation middleware
- structured logging
- `/health/live`
- `/health/ready`

Acceptance:
- integration tests boot real host

## Commit 004 — Persistence Building Blocks

Create:
- EF Core baseline
- migration conventions
- PostgreSQL schemas
- optimistic-concurrency convention
- tenant-aware entity interfaces
- created/updated audit fields

Do not create domain tables not yet required.

## Commit 005 — Module Architecture

Create:
- module interface
- module registration
- architecture tests
- forbidden-reference tests
- module-owned migration registration pattern

Acceptance:
- sample module proves boundary enforcement

## Commit 006 — Identity/Tenant Context Interfaces

Create:
- authenticated actor abstraction
- tenant context
- tenant resolver
- platform vs tenant actor distinction
- test authentication scheme

Acceptance:
- cross-tenant integration test fails correctly

## Commit 007 — Authorization Building Blocks

Create:
- ActionCode
- Permission check interface
- Entitlement check interface
- Domain authorization result
- policy evaluation pipeline

Do not hard-code plan logic into controllers.

## Commit 008 — Audit

Implement append-only Audit module and shared recording interface.

Acceptance:
- sample action writes audit with correlation ID
- sensitive metadata redaction test

## Commit 009 — Domain Event Infrastructure

Implement:
- event envelope
- in-process dispatcher
- idempotent consumer conventions
- optional outbox table only if needed for reliability boundary

Do not introduce RabbitMQ.

## Commit 010 — Seed Framework

Implement:
- seed categories
- stable seed keys
- environment guard
- idempotent runner
- destructive reset guard

## Commit 011+ — Configuration Modules

Then implement, in order:
1. Sports Definition
2. Competition Format
3. Ruleset Registry

Every module task must include:
- domain model
- migration
- command handlers
- queries
- permissions
- audit
- tests
- seed
- API
- docs update

## Codex Working Rule

Never ask Codex to "build the sports platform" in one task.

Give bounded tasks such as:

```text
Implement Sports Definition System: version publication lifecycle.

Read:
- docs/specifications/03-sports-definition-system-spec.md
- docs/architecture/architecture-rules.md
- docs/architecture/entity-ownership-matrix.md
- docs/architecture/action-registry.md

Implement only:
- SportDefinitionVersion lifecycle
- publish command
- immutability rule
- audit
- tests

Do not implement Competition Format or Rulesets in this task.
```

This keeps changes reviewable and architecture-safe.
