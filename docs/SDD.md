# Software Design Document

**Version:** 2.0  
**Date:** 2026-08-10  
**Status:** Maintained

The current software design is maintained in
[Multi-Sport Platform architecture](architecture.md). Major decisions and their
trade-offs are maintained in the [architecture decision records](adr/README.md).

This file remains as the stable SDD entry point for existing references. The
previous version was retired because it mixed intended and implemented design,
including nonexistent gateway/middleware components, obsolete ports, and
server-side SQLite assumptions. Requirements remain in the [SRS](SRS.md), while
delivery evidence remains in the repository-level `task-progress.md`.

## Design coverage

The current architecture record covers:

- modular-monolith structure and module boundaries;
- the universal contest model and competition format engines;
- shared-schema multi-tenancy and authorization boundaries;
- durable ordered mobile offline synchronization;
- persistence-first SignalR updates;
- versioned and pinned sport rulesets;
- append-only domain audit history;
- deployment, persistence, observability, and hosted-worker boundaries.

## Revision history

| Version | Date | Change |
|---|---|---|
| 1.0 | 2026-07 | Initial intended design |
| 2.0 | 2026-08-10 | Replaced stale pseudocode with links to code-aligned architecture and ADRs |

