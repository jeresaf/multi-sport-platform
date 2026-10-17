# ADR-0008 — Competition rosters belong to the Competitions module; the UI stays on the team page

**Status:** Accepted
**Date:** 2026-10-17
**Deciders:** Owner, Reviewer

## Context

The owner wants a Roster Manager inside Team Management ("players can be on more than one roster for more
than one competition for the same team"; "team creation, then staff and players, then rosters"). The previous
plan put rosters in Team Operations, but a roster only exists for a competition edition, and registration
approval may depend on roster rules — which creates a two-way dependency between Team Operations and
Competitions.

## Decision

- **Team Operations** owns permanent relationships: player memberships, staff assignments, transfers.
- **Competitions** owns the competition entry: registration **and its roster** (team + edition = roster),
  roster members (picked from the team's memberships), competition shirt numbers, roster locking and
  eligibility evaluations.
- Dependencies point one way: Competitions reads Team Operations (through Contracts) to list candidate players;
  Team Operations never calls Competitions. When a transfer completes, Team Operations publishes
  `TransferCompleted` and Competitions reacts (flags affected rosters for action — never silently rewrites
  history).
- **UX is unchanged**: the team page has a "Competitions & Rosters" tab that calls the Competitions API. Users
  experience rosters as part of team management, in the order the owner described.

## Consequences

- Positive: no dependency cycle; roster rules (from the edition snapshot) sit next to the data they govern.
- Negative: team-page UI composes data from two modules — normal for a modular monolith.

## Rules affected

AR-02.

## Review trigger

None expected.
