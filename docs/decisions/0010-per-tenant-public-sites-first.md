# ADR-0010 — Per-tenant public sites first; a cross-tenant platform hub later

**Status:** Accepted (owner confirmed 2026-10-17: "each customer gets its own public site" — Q1)
**Date:** 2026-10-17
**Deciders:** Owner, Reviewer

## Context

The first summary describes a LiveScore-style public UI (date-centric home, sports navigation, favourites).
The commercial model is leasing a white-labelled platform to federations, leagues, schools and organisers.
These can mean two different products: (a) each tenant's own branded public site/app, or (b) one platform-wide
aggregator showing every tenant's matches.

## Decision

- v1 public web is **per tenant**, resolved by host: `<tenant-slug>.<platform-domain>` or a verified custom
  domain. It shows only that tenant's public data, in that tenant's branding.
- Public read models are designed so a **platform hub** (aggregating tenants that opt in via a
  `public_listing_enabled` setting) can be added later without changing domain modules.
- The date-centric home, live page, competition pages and match pages in docs/product/ux/public-app.md apply
  to the tenant site; sports navigation shows the tenant's enabled/flagship sports.

## Consequences

- Positive: matches the leasing/white-label model; simpler privacy story (no cross-tenant exposure).
- Negative: a national "everything in one place" experience waits for the hub (which also fits the national
  registry path, docs/architecture/national-registry.md).

## Review trigger

The owner decides the aggregator is the primary product, or a federation wants a national hub early.
