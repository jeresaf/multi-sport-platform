# Query / Presentation Read Model System — Full Specification

## 1. Purpose

Provide optimized, denormalized, presentation-ready read models for public and admin UIs without making them the source of truth.

---

# 2. Scope

In scope:
- Public home feed
- Live feed
- Competition detail projections
- Match detail projections
- Team/player pages
- Admin dashboards
- Standings/bracket/leaderboard views
- Tenant-specific presentation metadata
- Realtime projection updates

Out of scope:
- Domain ownership
- Business-state transitions
- Authoritative writes

---

# 3. Dependencies

Consumes events/data from:
- Tenant
- Competition
- Scheduling
- Contest Operations
- Timeline
- Results
- Progression
- Statistics
- Branding
- Sponsorship

---

# 4. Core Model

Read models may include:

```text
PublicContestCard
PublicCompetitionSummary
PublicCompetitionDetail
PublicTeamSummary
PublicParticipantSummary
PublicMatchDetail
AdminCompetitionDashboard
AdminContestOperationsView
LiveFeedItem
StandingsView
BracketView
LeaderboardView
```

---

# 5. Business Rules

## RULE-RDM-001
Read models are disposable/rebuildable.

## RULE-RDM-002
Read-model mutation must originate from domain/integration events or controlled rebuilds.

## RULE-RDM-003
Public models include only public-approved data.

## RULE-RDM-004
Tenant branding/presentation applied at projection or view layer, not domain entities.

## RULE-RDM-005
Stale read models should expose version/updated-at metadata where useful.

---

# 6. Commands

Administrative only:

```text
readmodel.rebuild
readmodel.repair
readmodel.reindex
```

---

# 7. Queries

Primary use:
- Public APIs
- Admin dashboard APIs
- Search indexing
- Realtime clients

---

# 8. Realtime

SignalR update should target specific projection/entity where possible.

Avoid full-day/full-page refetches after every event.

---

# 9. Performance

Designed for:
- high read volume
- cacheability
- pagination
- denormalized display
- incremental update
- tenant-aware caching

---

# 10. Tests

- Rebuild from authoritative data
- Public/private separation
- Incremental timeline update
- Contest score update
- Result approval projection
- Tenant isolation
- Cache invalidation

---

# 11. Definition of Done

Complete when public/admin UI can render efficiently without coupling directly to transactional aggregates.
