---
paths:
  - "mobile/**"
---

# Mobile (Flutter) — Phase 8 only

- Do not start mobile work before Phase 8 (ADR-0012). Re-confirm the framework with the owner first.
- Generate the Dart API client from `contracts/openapi.json`; never hand-write DTOs.
- White-label tenants via flavours that map to brand tokens (docs/product/ux/design-system.md).
- Offline scorer: local queue (Drift/SQLite) of timeline events with `client_event_id`, device order, scorer
  lease, sync with idempotent retries, and a rejection UI — no last-write-wins (docs/architecture/sport-engine.md §9).
- Same rules as web: no sport logic outside metadata-driven renderers/pads; tokens only; accessibility.
