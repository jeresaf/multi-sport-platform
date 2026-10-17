# Admin app

Stack: ADR-0011. The admin app is for platform staff and tenant users. Menus and actions are filtered by the
user's capabilities (docs/architecture/authorization.md); the server still enforces everything.

## Shell

- Left navigation grouped by workspace; collapses to a rail on tablet; tenant switcher and user menu at top.
- Every list: server-side pagination, filters, search, sort; bulk actions where useful.
- Every entity detail: header with status badge and primary actions, tabs, and an **Audit** drawer
  (who did what, when, why).
- Concurrency conflicts show "This record changed since you opened it" with a reload/compare option
  (`*_CONCURRENCY_CONFLICT`).
- Errors are mapped from error `code` to friendly messages; unknown codes show the message and correlation ID.
- Actions that need a reason (postpone, override, reopen…) open a confirm dialog with a required reason field.

## Workspaces and screens

### Platform console (platform staff)
Tenants (list, create, detail: lifecycle, domains, subscription, members summary) · Plans & entitlements ·
Diagnostics (failed outbox messages with re-drive, background jobs) · Platform audit.

### Tenant settings
Organisation profile (name, time zone, locale, currency, public visibility, policies such as minors' consent) ·
Members & roles (invite, assign roles with scopes) · Sports (enable within entitlement) · Subscription
(read-only plan, usage vs limits) · Domains and Branding (later).

### Registry
- **People** — directory with filters; **search-first create**: type a name → see matches → open existing or
  continue → biodata → duplicate review (side-by-side, evidence, override reason if proceeding) → save.
  Detail tabs: Overview · Roles (athlete/staff/official profiles) · Teams & history · Competitions · Matches ·
  Statistics · Consent (minors) · External IDs · Audit.
- **Organisations** — directory, create, detail with teams and relationships.
- **Teams** — follows the owner's flow: **create team → add staff → add players → rosters**. Detail tabs:
  Overview · Players (memberships, add existing person or quick-create with duplicate check) · Staff ·
  Competitions & Rosters (entries and rosters per edition; build roster from current players) · Fixtures ·
  Results · Statistics · History.
- **Venues** — directory, create, detail.

### Competitions
- **Competition list** and **competition detail** (Overview · Editions · History).
- **New competition wizard** — short, because operating happens in the edition dashboard:
  1. Basics — competition (new or existing), edition label, dates, logo
  2. Sport — from the tenant's enabled variants
  3. Format — with parameters (e.g. single/double round robin)
  4. Rules — choose template; edit exposed parameters with plain-language help (points, periods, roster
     limits, tiebreak order); preview
  5. Entry — registration window or direct entry; roster requirements
  6. Review & create
- **Edition dashboard** — status, a **next-actions checklist** (open registration → approve entrants → rosters
  → draw/structure → generate fixtures → assign dates/venues → publish → assign officials), plus tabs:
  Entrants & registrations · Rosters · Structure · Fixtures · Results · Standings · Officials · Rules (frozen
  snapshot, read-only; amend with reason) · Audit.

### Match day
- **Match board** — today/this week by status; filters by competition, venue, assigned to me.
- **Contest operations page** — score/status hero, readiness checklist (with override + reason), lineups (both
  sides), officials, timeline (with correct/reverse), controls (start, periods, interrupt/suspend/abandon, end),
  audit.
- **Scorer workspace** (below).

### Results
Pending approvals inbox · result review (proposed outcome from the engine, period breakdown, key events,
lineup/coverage notes, approve/reject with reason) · correction workflow (reopen with reason → edit draft →
submit → approve) · version history.

### Data and reports (later slices)
Import centre (template download, upload, mapping, validation, duplicate resolution, dry run, execute, report) ·
historical results entry · reports hub.

## Scorer workspace (tablet-first, also works on phones)

- **Sticky header:** both sides, big score, phase, clock (start/stop where the sport needs it), connection badge
  (`Online` · `Reconnecting` · `3 pending` · `Rejected — action needed`).
- **Pad from metadata:** ADDITIVE sports render the definition's `scorer_pad` groups as large buttons (≥ 48 px
  touch targets, generous spacing); cricket uses the delivery pad (runs 0–6, 4/6 toggles, wide/no-ball/bye/leg-bye,
  wicket with dismissal type and fielder, next ball).
- **Actor pickers:** the side's on-field players first (shirt number + name), then bench; one tap to pick;
  optional roles can be skipped.
- **Period controls:** next period offered from the engine (e.g. "Start extra time" when `REQUIRES_EXTRA_TIME`);
  ending a period or the match asks for confirmation.
- **Undo last** = reversal with reason "entered in error" (available for a short window); **Correct** opens the
  event and records a superseding event.
- **Taking over** another device's scorer lease asks for confirmation and is audited.
- **Outdoor use:** high-contrast theme option, works in portrait and landscape, keeps screen awake while live.
- Scorer UI is online-only in v1 but queues briefly during short disconnects and replays on reconnect using
  `client_event_id`; full offline mode comes with the Flutter scorer (ADR-0012).
