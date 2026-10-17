# Product vision

## What we're building

A **configurable, multi-tenant, multi-sport platform for running competitions, scoring them live, approving
results and publishing everything** — leased to organisations that run sport, starting in Uganda and East Africa.
It is not a football-only LiveScore clone: the same core must run a football league, a rugby sevens tournament,
basketball playoffs, a cricket T20 tournament, a school sports day and, later, golf, athletics and combat cards.

## Who it's for (tenants)

National federations · leagues · tournament organisers · schools and universities · clubs · corporate
competitions · short events and long-running competitions · combat promoters · golf and athletics events.

## Two user interfaces

1. **Admin app** (web; tablet-friendly scorer inside it) — workspaces shown according to role:
   platform administration, tenant administration, registry (people, teams, organisations, venues), competition
   administration, match-day operations and scoring, results approval, reports and data import.
2. **Public app** — each tenant's branded site (ADR-0010), designed deliberately for phone, tablet and desktop;
   later also Flutter apps (ADR-0012). Shows only approved public data.

## What a tenant should be able to do without a developer

```text
Create organisation (tenant) → configure branding → enable sports → register people, teams, venues
→ create competition + edition (pick sport, format, rules template; adjust parameters)
→ register teams/athletes → build rosters → instantiate stages → generate fixtures → assign officials
→ run matches live with structured play-by-play → correct mistakes safely → submit and approve results
→ tables/brackets/leaderboards update automatically → publish on web (and later mobile)
→ produce reports → archive or renew
```

## Flagship sports

In build order: Football · Rugby 15s and 7s · Basketball · Cricket T20. Later: netball, volleyball, hockey, golf,
athletics, boxing, MMA, tennis, and others by configuration (ADR-0003). The fictional pilot customer that drives
demos and seeds is described in docs/product/pilot-customer.md.

## First milestone (M1): a real football league, end to end

A tenant runs a small football league from setup to published table, including live scoring, a correction after
approval, standings recalculation, a public site that updates live, and an audit trail — with no manual database
edits (docs/build/roadmap.md).

## Principles

- Configuration over hard-coding — sports are data + a few scoring engines; competition policy is a versioned
  ruleset.
- One source of truth per fact — the timeline for what happened in a contest; approved results for what counts.
- History over overwrite — corrections, versions and effective dates; nothing important is silently changed.
- Modular monolith first; infrastructure is earned.
- Thin end-to-end slices with tests; every change reviewed.
- Privacy by design — many athletes are children (ADR-0015).
- Never fake data — show "not available" and record the gap.

## Non-goals for v1

Payments and invoicing (subscriptions are assigned by platform staff) · ticketing · fantasy/betting ·
video streaming · native mobile apps (Phase 8) · in-app sport/format editors (ADR-0006) · DLS calculation
(manual revised targets only) · multi-language UI (English first) · cross-tenant data sharing (national registry
features come later, deliberately — docs/architecture/national-registry.md).

## Success looks like

A pilot customer runs a real competition on the platform for a full season or tournament, their public site is
used by fans, results are trusted (approved, auditable), and onboarding a second customer requires no code
changes.
