# S10 — Fixtures and schedule (outline)

> **Status: outline.** The reviewer writes the detailed spec before this slice starts.

**Goal:** fixtures are generated (or created manually), given dates/venues, checked for conflicts, published, and
then only changed through reschedules with reasons — and fans can see them.
**Modules:** Match Operations (new), Competitions, Registry (venues), Insights (first public read models).
**Reference:** pack spec 23; ADR-0007; docs/domain/formats.md; docs/product/ux/public-app.md.

## Scope

- Contests created from the published structure via generators; contest participants reference entrants or
  slots; generation run records (inputs, seed, output hash).
- Bulk date/time/venue assignment by round (date pattern, kick-off time, default venue = home team's venue);
  manual creation and editing while `DRAFT`.
- Conflict detection: an entrant twice in an overlap window; venue double-booked (with buffer); warnings vs.
  errors configurable.
- Publish (per round or all) → `FixturesPublished`. After publication: `fixture.reschedule` (reason, new schedule
  version, followers notified later), postpone/cancel through contest status.
- "Quick match" (SINGLE_CONTEST) creation flow.
- Public read model + pages: fixtures/results by date (simple home list), competition "Fixtures" tab, team
  fixtures; times in venue time zone.

## Key acceptance

- 6 entrants double round robin → 30 contests, deterministic for a seed; conflicts flagged.
- Published fixtures cannot be edited directly; a reschedule creates version 2 with reason and shows on the
  public site.
- Isolation/authorization tests.
