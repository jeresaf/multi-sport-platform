# S14 — Public web v1 and Milestone M1 (outline)

> **Status: outline.** The reviewer writes the detailed spec before this slice starts.

**Goal:** a polished, responsive, tenant-branded public site for the football league, and the M1 acceptance
scenario automated end to end.
**Modules:** Insights (read models), public web; touches all earlier modules through seeds and E2E.
**Reference:** docs/product/ux/public-app.md, design-system.md; ADR-0010, ADR-0011.

## Scope

- Pages: Home (date-centric with filters), Live, Competitions list, Competition (Overview, Fixtures, Results,
  Table, Teams), Contest (Summary, Timeline, Lineups, Info), Team, Player (public profile, minors' rules), Venue;
  device favourites; not-found and unavailable pages; a per-tenant **Privacy** page (template text naming the
  storage location and sub-processors from ADR-0016, editable by the tenant).
- Responsive layouts designed for phone/tablet/desktop; light/dark; accessibility checks; SEO metadata;
  performance budget (Largest Contentful Paint < 2.5 s on a mid-range phone profile).
- Read models complete and rebuildable; live updates throughout.

## Milestone M1 — acceptance scenario (automated Playwright + manual demo)

1. Platform admin provisions "Kampala Metro Sports Association" (`kmsa`) on STANDARD with a `COMPLIMENTARY`
   period (docs/product/pilot-customer.md) and invites the owner, Grace Nakato; she accepts.
2. Owner enables Football; invites a registrar, a competition manager, two team managers, a scorer and a results
   officer with the right scopes.
3. Registrar creates organisations, teams, venues and players (one duplicate warning resolved with a reason).
4. Team managers add staff and players to their teams.
5. Competition manager (Moses Okello) creates "Metro Football League — 2027" (double round robin,
   FOOTBALL_LEAGUE); enters 4 teams directly; rosters are submitted and approved.
6. Structure instantiated; fixtures generated, dated, given venues and published.
7. Officials assigned; lineups submitted and approved; readiness passes.
8. Scorer runs a match: start, goal with scorer and assist, yellow card, substitution, half-time, second half,
   second goal, end.
9. Result submitted and approved; contest closed; table updates.
10. Public site shows the fixture, live score, timeline, lineups and table — updating live during the match.
11. Correction: results officer reopens with a reason; the scorer is corrected (supersede) and a goal is reversed
    (VAR); result re-approved; the table recalculates; the public page shows "Corrected".
12. Top scorers on the competition overview reflect the corrected events.
13. Audit explorer shows every administrative and scoring action with actors, reasons and correlation IDs.
14. **No step required direct database access or developer intervention.**

## Exit

Owner demo of M1; reviewer's slice review; the infrastructure task (ADR-0016); detailed spec for S15 (Rugby).
