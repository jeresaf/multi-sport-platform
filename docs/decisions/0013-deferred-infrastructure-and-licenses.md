# ADR-0013 — Deferred infrastructure and dependency licence policy

**Status:** Accepted
**Date:** 2026-10-17
**Deciders:** Owner, Reviewer

## Context

Infrastructure complexity must be earned. Several popular .NET libraries changed to commercial licences in
2025–2026, which matters for a commercial SaaS product.

## Decision

**Start with:** PostgreSQL, the ASP.NET Core host (API + SignalR + background services), Hangfire (when first
needed), local file storage in development (ADR-0014), Docker Compose for local dependencies, GitHub Actions.

**Deferred until an ADR shows measured need:** Redis (SignalR backplane / distributed cache when running more
than one instance), RabbitMQ/Kafka, Elasticsearch/OpenSearch (PostgreSQL full-text + `pg_trgm` first),
Kubernetes, microservices, database-per-module, CQRS with separate read databases.

**Dependency licence policy**
- Prefer MIT, Apache-2.0, BSD, MPL-2.0. LGPL is acceptable only for unmodified libraries (e.g. Hangfire core).
- Do **not** add (commercial licences in current versions — check before any exception):
  MediatR v13+, AutoMapper v15+, MassTransit v9+, FluentAssertions v8+, SixLabors ImageSharp (split licence;
  paid above USD 1M revenue).
- Use instead: plain handler classes + DI; explicit mapping code; outbox + in-process dispatch; Shouldly or
  plain xUnit asserts; SkiaSharp or Magick.NET for image processing.
- Every PR that adds a dependency lists it with its licence in the PR description.

## Consequences

- Positive: fewer moving parts; no surprise licence costs.
- Negative: a little more hand-written code (mapping, handler wiring) — acceptable and easier to review.

## Review trigger

Load tests or production metrics show a bottleneck one of the deferred components solves.
