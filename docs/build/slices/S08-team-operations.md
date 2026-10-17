# S08 — Team operations (outline)

> **Status: outline.** The reviewer writes the detailed spec before this slice starts.

**Goal:** the owner's team flow — create team → add staff → add players — with full history, and team managers
limited to their own team.
**Modules:** Team Operations (new), Registry, Identity (TEAM scope).
**Reference:** pack spec 15 (+ the staff-assignment gap), ADR-0008, docs/domain/state-machines.md.

## Scope

- **Player memberships:** effective dates, status machine (`PENDING → ACTIVE ⇄ SUSPENDED → ENDED` with
  end reason), position, default shirt number, loan flag. Concurrency policy: by default one active membership
  per sport variant (+ loans); tenant-configurable.
- **Staff assignments:** staff profile + role from the sport definition's staff roles, effective dates.
- Shirt-number rule: unique among a team's active members (tenant-configurable warning vs. error).
- Team page tabs: **Players** (add existing person — search-first; quick-create a person with the duplicate check
  inline; end membership with reason) and **Staff**; player page "Teams & history".
- `TeamMembershipChanged` integration event; Contracts query "members of team on date".
- Scope chain provider for `TEAM`; Members & roles can now assign `TEAM_MANAGER` scoped to a team.

## Key acceptance

- Moving a player to another team ends the old membership and keeps history (transfers workflow comes in S24).
- A team manager can edit only their team (authorization tests).
- Isolation tests for memberships and assignments (composite tenant FKs where specified in multi-tenancy.md).
