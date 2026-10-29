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

### As built (S03-T6b2)

- **Workspaces and addresses.** A tenant's workspace is `/t/{slug}` (the user's tenants come from `GET
  /api/account/v1/me`); the platform console is `/platform` (platform staff only). `/` opens the last workspace used
  in this browser, else the first active tenant, else the console; someone with neither is told they belong to no
  organisation yet.
- **Navigation follows the capabilities.** Each entry names the action its screen needs (tenant workspace: Organisation
  `tenant.settings.read`, Members & roles `membership.read`; console: Tenants `tenant.read`, Plans `plan.read`, Diagnostics
  `platform.outbox.read`, Audit `platform.audit.read`); entries the user's capabilities
  (`GET /api/admin/v1/me/capabilities`, `GET /api/platform/v1/me/capabilities`) don't allow are hidden, and their
  addresses answer "You can't open this page". Entries whose screens don't exist yet open "This screen isn't available
  yet" (never a made-up screen).
- **Widths.** Desktop (≥ 1200 px): a sidebar with labels. Tablet: an icon rail (the label is the tooltip and the
  accessible name). Phone: the navigation is a sheet behind the menu button. The switcher and the user menu stay in
  the top bar at every width.
- **Switcher.** The user's tenants by name, suspended and archived ones with their status badge, and "Platform
  console" for platform staff. A suspended or archived tenant opens a page saying so; whoever may read
  `tenant.access.read` (the owner) also sees since when and why, the others are told to ask the owner.
- **401, 403, 404.** Without a session every signed-in page goes to sign-in and back to the same address afterwards.
  A refusal shows the code's message (for example `TENANT_PROVISIONING`) and points to the workspace menu; an unknown
  tenant or address is "Page not found". Forms can't be sent until the page is interactive (their button is
  disabled until then). Signing out reloads the sign-in page, so nothing the user saw stays in memory.

### As built (S03-T6c1)

- **Organisation** (`/t/{slug}/settings/organisation`): time zone and currency (pickers from the platform's reference
  data), language code, "starting soon" minutes, guardian consent for minors, public site. Without
  `tenant.settings.update` the form is shown disabled with a note saying who can change it. Saving sends only the
  settings that changed, with `If-Match`. The tenant's name is the platform's (`tenant.update`); the page says so.
- **Members & roles** (`/t/{slug}/settings/members`): members (active by default; suspended or removed on request) with
  their roles, status and the reason for it, and invitations (pending and expired by default; with when the email went
  out, or "Not sent yet" while pending and "Not sent" once it never will be; an expired one shows when it expired). A
  member's menu offers only what the capabilities and the membership state allow: change
  roles (tenant-wide), suspend and remove (each with a required reason, kept in the audit trail), restore. Only an owner
  is offered the owner role, suspending, restoring or removing an owner, and revoking an invitation carrying the owner
  role; the server refuses the rest (`AUTH_OWNER_REQUIRED`, `AUTH_LAST_OWNER`) and the dialog shows why. The user's own
  row is marked "You", and changing their own access or roles says so first. Role changes are one request per role, each
  with the version the previous one returned; if one is refused the dialog says which were already saved. On a phone a
  member's roles and status sit under their email. After a change that takes a row out of the list, focus goes to the
  section's heading.
- **Concurrency.** A change sends the version it was based on (`If-Match`); `*_CONCURRENCY_CONFLICT` shows "This record
  changed since you opened it" with **Reload**. Revoking an invitation is the exception: sending its email changes its
  version moments after it is listed, so the revoke relies on the invitation's state instead.
- **Dates** on these screens are in the tenant's time zone (from the settings), in UTC for someone who can't read them.
- **Change password** (`/account/password`, from the user menu): current and new password; the user's other sessions end within 5
  minutes, this one stays.

### As built (S03-T6c2a)

- **Tenants** (`/platform/tenants`, `tenant.read`): every tenant by slug with its name, country, status and creation
  date (UTC: the list carries no time zone); a status filter (all by default) and "Show more". **New tenant**
  (`tenant.create`) asks for the slug (it never changes), name, legal name, country, time zone, language, currency and
  public site, with the country, zone and currency from the platform's reference data (the zones narrowed to the
  chosen country's, plus those of no country such as Etc/UTC); a taken slug is said under the slug. The new tenant
  opens on its detail page, still provisioning.
- **A tenant** (`/platform/tenants/{id}`): its profile, dates in the tenant's own time zone, and the lifecycle changes
  its status and the user's capabilities allow (state-machines.md, Tenant): activate and restore are confirmed;
  suspend, archive and unarchive ask for a reason, and the dialog says the tenant's owners see it (after an unarchive,
  instead of the earlier one). Each change sends `If-Match`, and a conflict (someone changed the tenant since it was
  read) offers Reload. Should the API still refuse a change for the status (`TENANT_INVALID_TRANSITION`; not expected,
  since the version is checked first), the tenant is read again, the refusal stays in the dialog and the change can't
  be sent again. After a change, focus moves to the tenant's name and the new status is announced. **Invite an owner**
  (`tenant.owner.invite`, not offered for an archived tenant) sends an email invitation to `TENANT_OWNER` and says
  until when the link works (date, time and the tenant's zone). The owner can work in the tenant while it is
  provisioning. Platform support staff (`tenant.read` only) see the same pages without controls, and are told that
  changing its status or inviting its owner needs a platform administrator. Owner invitations sent earlier, the status
  reason and history, members, domains and subscription say they aren't available yet; profile editing
  (`tenant.update`) is not offered.
- Diagnostics and Audit are still "not available yet".

### As built (S04-T4a)

- **Plans** (`/platform/plans`, `plan.read`): every entitlement (name, description, the module that enforces it)
  against every active plan in rank order, read-only: "Included" / "Not included", a limit with its unit ("100
  users") or "Unlimited", a level by name or "None". A value the configuration seed hasn't written yet says "Not
  seeded", never unlimited or none. Plans change by a reviewed seed, not here.
- **A tenant's Subscription panel** (on its page, `subscription.read` and `plan.read`, for the definitions): the current
  period (plan, kind, status, dates, expiring from, grace until; cancelled with when and why) or "No subscription"
  (read-only once active), and when no period is in effect today (scheduled only, expired, cancelled) that the tenant
  is read-only until one starts or is assigned; the next scheduled period, every period with its status and cancel reason, the effective
  entitlements with their source (the plan, an override, or the default without a plan) and the quotas' usage ("Used:
  4 of 5 users", "Near the limit" from 80 %, "At the limit"; "not measured yet" where no module counts), and the
  overrides in place with value, reason and last day, the removed ones under "Earlier overrides". Days are the
  tenant's dates in its time zone. Platform administrators get **Assign a period** (plan, kind, first and last day,
  optional expiring and grace days), **Renew** (says its first day; plan and kind stay unless chosen), **Change plan**
  (only to a higher plan, while the current period is active or expiring and its plan is still active), **Cancel subscription** (reason), **Set an
  override** (the value's input follows the entitlement's type: on/off, a number or Unlimited, a level; reason;
  optional last day) and **Remove** (reason). A refusal (`TENANT_SUBSCRIPTION_OVERLAP`, `…_NOT_FOUND`,
  `…_DOWNGRADE_AT_RENEWAL`, a field) stays in its dialog; the panel shows the subscription the command answered with
  and announces the change; after a conflict refusal (`409`) it reads the subscription again, since someone may have
  changed it meanwhile. Reasons are platform-only: the dialogs say the tenant sees the value, not the reason.
  Support staff see the panel without controls. Who created, cancelled or removed something isn't shown (the API gives
  user IDs; the audit trail has the names). The rest of the tenant page says its status history, members and domains
  aren't available yet.
- **Activate** says, for a tenant with no period in effect today (none, scheduled only, expired or cancelled), that it
  will be read-only until one starts or is assigned (S04 D8, D9); while the subscription can't be read, it says it
  couldn't be checked.

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
