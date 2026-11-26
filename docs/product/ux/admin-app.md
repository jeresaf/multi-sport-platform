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
- **Navigation follows the capabilities.** Each entry names the action its screen needs (tenant workspace: People
  `person.read`, Organisation `tenant.settings.read`, Members & roles `membership.read`, Enabled sports
  `tenant.sport.read`, Subscription `tenant.subscription.read`; console: Tenants `tenant.read`, Plans `plan.read`,
  Sports, Formats and Rules templates `platform.sport.read`, Diagnostics `platform.outbox.read`, Audit
  `platform.audit.read`); entries the user's capabilities (`GET /api/admin/v1/me/capabilities`, `GET
  /api/platform/v1/me/capabilities`) don't allow are hidden, and their addresses answer "You can't open this page".
  Entries whose screens don't exist yet open "This screen isn't available yet" (never a made-up screen). A tenant
  workspace's entries are grouped (S06-T7a): Overview, then **Registry** (People), **Sports configuration** (Sports,
  Formats, Rules templates) and **Settings** (Organisation, Members & roles, Enabled sports, Subscription); a group with
  no entry the user may open isn't shown. On the tablet rail a group starts after a rule and its heading is for screen
  readers only. The console's entries aren't grouped.
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
  and announces the change; after a refusal that means someone changed the subscription meanwhile (a `409`, or an
  override already removed) it reads the subscription again, and the dialog's button is disabled when the command no
  longer applies (nothing to cancel, no plan to change, the override gone). Reasons are platform-only: the dialogs say the tenant sees the value, not the reason.
  Support staff see the panel without controls. Who created, cancelled or removed something isn't shown (the API gives
  user IDs; the audit trail has the names). The rest of the tenant page says its status history, members and domains
  aren't available yet.
- **Activate** says, for a tenant with no period in effect today (none, scheduled only, expired or cancelled), that it
  will be read-only until one starts or is assigned (S04 D8, D9); while the subscription can't be read, it says it
  couldn't be checked.

### As built (S04-T4b1)

- **Subscription** (`/t/{slug}/settings/subscription`, `tenant.subscription.read`: owners, admins and viewers): the
  current period (plan, kind, status, dates, and the last day of full access while grace applies) or "No subscription";
  when no period is in effect today, until when the workspace is read-only (the first day of a current period that
  hasn't started, else the next period's first day, or until the platform team renews it; S05-T5a1); the next period
  (the one after the current period); what the plan includes, each entitlement with its description,
  its value and where it comes from ("Standard plan", "Arranged for your organisation" for an override, whose reason
  stays the platform's, or "No plan"), and the quotas' usage as in the console. Days are the tenant's, in its time
  zone. No self-service: the page says to contact the platform team.
- **Banner** (every member, every page of the tenant workspace, from the capabilities' `access`): "The subscription
  ends soon" with its last day while `EXPIRING`; "The subscription has ended" with the last day of full access while in
  `GRACE`; "This workspace is read-only" with why and since or until when (expired: since the day after grace;
  cancelled: since the day it was cancelled; not started: until its first day; no subscription). Nothing while access
  is full.
- **Suspended or archived tenant:** "Since" is in the tenant's time zone (`/tenant/access`).
- A refusal at a quota shows the limit the server names ("Your plan allows 5 staff users.").

### As built (S04-T4b2)

- **Disabled with a reason.** A write control the user's roles allow but the tenant can't use now is shown disabled,
  with one line of text saying why (not a tooltip, so it reads on a phone; the controls point to it with
  `aria-describedby`). The reason comes from the capabilities' `unavailable`: "Read-only: the subscription has ended",
  "…was cancelled", "…hasn't started yet" or "…the organisation has no subscription" (`TENANT_READ_ONLY`), and
  "Requires PRO plan" (the lowest plan that includes it) or "Not included in your plan" (`ENTITLEMENT_REQUIRED`).
  Controls the roles don't allow stay hidden. While read-only: the Organisation form and its **Save changes** button,
  each member's menu, **Invite someone** and **Revoke** are disabled, each section saying why once.
- **Staff users.** The invite dialog shows the staff users against the plan ("Used: 4 of 5 staff users", "Near the
  limit" from 80 %); once every seat is taken, **Invite someone** is disabled with "Your plan allows 5 staff users."
  (from `GET /api/admin/v1/tenant/subscription`, for whoever can read it; the server refuses past the limit anyway).
  Revoking an invitation frees a seat, so it stays enabled. Inviting, revoking or a member change reads the usage again.
  At the limit a pending address can't be invited again from the screen either, although the server would allow it
  (it takes no new seat, S04 D13); "Invite again for a new link" isn't offered then. Only someone who can invite reads
  the usage on this page.

### As built (S05-T5b1a)

- **Sports** (`/t/{slug}/sports`, navigation entry after Overview, `sport.read`: every tenant role except Team
  manager, who has no actions yet): every published variant with its sport, scoring family, shape and latest version;
  each opens its definition. Read-only; which sports the tenant uses is Settings → Sports (T5b2).
- **A variant** (`/t/{slug}/sports/{variant}`): a header with the name, sport, version, publication date (tenant time
  zone) and content hash, then tabs **Overview** (terminology, contest, lineup; the lineup numbers and period lengths
  are shown as defaults a competition's rules can change), **Periods**, **Events** (one card per
  event: who takes part, required or optional and from which coverage, the periods it may be recorded in, points (defaults a competition's rules can change; "No
  points" for 0), details, effects, what it needs before it, the rules limiting it, what reverses it; then the events every sport has), **Positions & roles** (positions by group, roles in events,
  staff, officials), **Statistics** (how each is worked out, in words from the definition's labels) and **Scorer pad**.
  The tab is in the address (`?tab=`, the overview for a missing or unknown one) and `?version=` opens an older
  version; an unknown variant or version, or a `?version=` that isn't a version number, is "Page not found". On a phone the tab list wraps onto more rows, and tables keep only their main columns: the others' values are stacked under the row's name (the Members pattern). An empty list says "None".
- **Scorer pad preview:** the definition's `scorer_pad` groups in order, each button labelled by its event (a generic
  event by its generic label), a group's phases written as the definition's period labels ("Only during …") and its
  coverage ("From detailed coverage"). The buttons are list items, not controls: the preview records nothing. A pad kind
  the preview can't draw (cricket's delivery pad) says "Preview not available".
- **Words:** every sport word comes from the definition (labels, terminology). Codes with no label in the definition
  (actor roles, scopes, position groups, closed vocabularies) are written as words from the code (`RINGER_ON` → "Ringer
  on"). The viewer (`components/sports`) takes the definition rather than reading it, so the console's Sports (T5b2)
  reuses it.
- **No sport codes in app code:** ESLint (`@msp/config`'s `apps` config) refuses a string literal equal to a sport or
  variant code from docs/domain/sports, or a name that is one (object key, class field or method, type member, enum
  member, property read such as `renderers.FOOTBALL`), in `apps/**`, except under `renderers/` and in tests (S05 D6). Not caught: a code inside JSX
  text or built from parts.

### As built (S05-T5b1b1)

- **Formats** (`/t/{slug}/formats`, navigation entry after Sports, `format.read`: every tenant role except Team
  manager, who has no actions yet): every published format with its description, participants ("2 to 40
  participants"), contest shapes and latest version; each opens the format. Read-only: a competition chooses its
  format and parameters when it is set up (S09).
- **A format** (`/t/{slug}/formats/{code}`): a header like a variant's (name, version, publication date in the tenant
  time zone, content hash), then **Overview** (description, participants, contest shapes, the stages in order with
  their type and generator, the generator left out when it is named as the stage type) and **Parameters** (label and
  description; type: whole number, yes or no, one of a list; default; what a competition may choose: "1 to 4", "One
  of: Seeded, Random, Manual", "Either" for a yes-or-no one). `?version=` opens an older version; an unknown format or
  version, or a `?version=` that isn't a version number, is "Page not found". On a phone the list keeps the name and
  the parameters table the name and default; the other columns are stacked under the name. No parameters or no
  description says "None".
- **Parameter wording** (`lib/parameters.ts`) is shared with the Rules templates page (T5b1b2), which labels its own
  codes (tiebreakers) and uses the ordered list type. `FormatView` takes the format, so the console (T5b2) reuses it.

### As built (S05-T5b1b2)

- **Rules templates** (`/t/{slug}/rules-templates`, navigation entry after Formats, `ruleset.read`: every tenant role
  except Team manager, who has no actions yet): every published ruleset template with its description, sport, formats
  and latest version; each opens the template. The sport and format names come from the Sports and Formats lists when
  the user may read them, else the codes are written as words. Read-only: a competition picks a template and changes
  its parameters when it is set up (S09); the preview waits for the competition wizard.
- **A template** (`/t/{slug}/rules-templates/{code}`): a header like a format's, then **Overview** (description; the
  pinned sport version ("Football, version 1"), linking to that version's page; the formats, each linking
  to its page), **Parameters** (the Formats table; tiebreakers labelled from the catalogue), **Rules** in sections:
  Contest (period lengths, when the scores are level, extra periods, default coverage), Lineup (starters, bench,
  approval, positions: a position by its label, a group as "Any of: …"), Substitutions, Roster, Standings (points,
  forfeit score, tiebreakers in order, head-to-head applied again), Contest operations (readiness, officials at least,
  scorer corrections, starting soon) and Eligibility (membership, age, the date it is worked out on, consent for
  minors); then **Tiebreakers** (each one the template lists or allows, with its description). A rule a parameter
  changes is marked "(default; a competition may change it)". Period, position and official labels come from the
  pinned sport definition version; without it (no `sport.read`, or its read failed) they are words from the code and
  the page still shows every rule. `?version=` and "Page not found" as for a format.
- **Shared pieces:** the viewers' building blocks are `components/viewer/facts.tsx` (was `components/sports/`, review
  #90 N2) and the parameters table `components/viewer/parameters-table.tsx` (formats and templates).
  `RulesetTemplateView` takes the template, the definition and the format names, so the console (T5b2) reuses it.

### As built (S05-T5b2a)

- **Enabled sports** (`/t/{slug}/settings/sports`, navigation entry after Members & roles, `tenant.sport.read`: every
  tenant role except Team manager). Named so it isn't confused with **Sports** (the definitions). Every published
  variant with its sport and status (an "Enabled" badge with "since 16 Nov 2026" in the tenant's time zone, or "Not enabled"); a variant's
  name opens its definition for someone with `sport.read`. Owners and admins see **Enable** (at once, no dialog; "… is
  enabled." is announced) and **Disable** (a dialog asks for the reason, kept with the enablement and in the audit
  trail; nothing that uses the variant changes). Each change answers the list as it is after it.
- **The sport quota** (for whoever may read the subscription, as `GET …/tenant/sports` sends it): "Used: 1 of 3 sports"
  with the 80 % warning (`QuotaUsage`), and "Two variants of one sport count as one sport". At the limit, Enable on a
  variant of a sport the tenant doesn't have yet is disabled with "Your plan allows N sports." (as invitations, S04-T4b2);
  a variant of a sport already enabled stays enabled (D13). Without the usage the server's refusal is shown.
- **Refusals:** `ENTITLEMENT_QUOTA_EXCEEDED` shows its `detail`; it, `TENANT_SPORT_ALREADY_ENABLED` and
  `TENANT_SPORT_NOT_ENABLED` read the list again (someone else changed it). Read-only: both buttons disabled with the
  reason.
- **e2e** `admin-sports-demo`: the slice demo's enablement on a tenant of its own (enable, disable with a reason, an
  override of 0 refuses by screen and API, removed, enabled again; the audit trail has both enablements, the refusal and
  the reasons).

### As built (S05-T5b2b)

- **Console: Sports, Formats and Rules templates** (`/platform/sports`, `/platform/formats`, `/platform/rules-templates`,
  navigation entries after Plans, `platform.sport.read`: platform administrators and support staff). The workspace's
  pages over the platform reads (`GET /api/platform/v1/…`): the same tables (`SportsTable`, `FormatsTable`,
  `RulesTemplatesTable`) and viewers (`SportViewer`, `FormatView`, `RulesetTemplateView`), with `?tab=` and
  `?version=` and "Page not found" as in a workspace. Links stay in the console (a template's pinned sport version and
  formats). The intro says tenants see the same pages. Read-only: these change only by publishing a new version.
- **Publication dates in UTC say so.** The console has no time zone, so the viewers' publication date is in UTC and
  reads "15 Nov 2026 (UTC)". In a workspace the viewers wait for the tenant's settings (Loading), so the date is
  never shown first in UTC and then on another day (review #93 Q1); for someone who can't read the settings, or when
  their read fails, it is in UTC and labelled (`publishedOn`, `components/viewer/facts.tsx`, with `BackLink`;
  `useTenantTimeZoneOnceRead`). Other workspace dates that fall back to UTC (Enabled sports, members, invitations)
  aren't labelled.
- **e2e** `admin-screenshots`: the console's lists, a variant (Overview, Scorer pad), a format and a template at phone,
  tablet and desktop widths, light and dark.

## Workspaces and screens

### Platform console (platform staff)
Tenants (list, create, detail: lifecycle, domains, subscription, members summary) · Plans & entitlements · Sports,
Formats and Rules templates (read-only, as in a workspace) · Diagnostics (failed outbox messages with re-drive,
background jobs) · Platform audit.

### Tenant settings
Organisation profile (name, time zone, locale, currency, public visibility, policies such as minors' consent) ·
Members & roles (invite, assign roles with scopes) · Sports (enable within entitlement) · Subscription
(read-only plan, usage vs limits; as built S04-T4b1) · Domains and Branding (later).

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

#### As built (S06-T7a, read-only)

- **People** (`/t/{slug}/people`, `person.read`: every tenant role but Scorer and Team manager): the directory, sorted
  by last name, 25 a page with **Show more**. **Search** (name, alias by prefix or similarity, an external identifier
  exactly) follows the typing after a 300 ms pause; filters **Status** (default "All but archived"), **Age** (Adult,
  Minor, Age unknown), **Role** (Athlete, Staff, Official) and **Sport** (the tenant's enabled variants, shown when there
  are any). The search and filters are in the address, so going back from a person keeps them. Columns: name (with a
  **Minor** or **Age unknown** badge), born (the year), nationality, status; on a phone the three are stacked under the
  name. A search or filter changed elsewhere (the navigation's People entry, going back) is shown, not overwritten.
  No photos in the directory (its list carries no URLs; owner, T7a plan). Empty: "No people yet", or "No one
  matches" with a search or filter.
- **A person** (`/t/{slug}/people/{id}`): a back link to People; the header with the photo (the medium variant, or the
  initials in a circle), the display name in full (it wraps), the status, the Minor or Age unknown badge and "Public
  profile on/off". Tabs, in the address (`?tab=`): **Overview** (biodata; the date of birth only with
  `person.read_private`, otherwise "Born" with the year; then the status history with each change's time in the
  tenant's zone and its reason), **Roles** (athlete profiles by sport, the staff and official profiles with their
  capabilities, archived ones included, each with its status; sports, positions and roles named by the definitions,
  AR-29; an expired licence is marked; "No roles yet" without any), **Consent** (shown when the age status isn't
  `ADULT` or a consent exists: the scopes agreed now, then each record, active first, with its scopes, dates, status
  and any withdrawal; the guardian's name, relationship and contact and a download of the signed form only with
  `person.read_private`, otherwise "On file" for evidence), **External IDs & aliases** (identifiers with their
  authority and validity, ended ones included; current aliases with their kind). The private read (audited) is asked
  only with `person.read_private`, only on Overview and Consent, once per visit, and isn't kept after it; the photo's URLs are read again a
  minute before they expire, the evidence's only while Consent is open.
  Changes come with T7c (Roles: T7c-1a; Consent and the public profile: T7c-1b; the photo, identifiers and aliases:
  T7c-2a; editing the details and the lifecycle: T7c-2b).

#### As built (S06-T7b, Add person)

- **Add person** on People (`person.create`: owner, administrator, registrar; disabled with the reason on a read-only
  tenant) opens `/t/{slug}/people/new`, three steps with "Step n of 3" and the focus on each step's heading:
  1. **Who are you adding?** First and last name (required), middle and other names. The people the name already
     finds (the directory's search on "first last", 300 ms after typing, the first 10) are listed with their birth year,
     nationality, status and age badge, each linking to their page. **Continue**.
  2. **Their details.** Display name (defaults to "first last"), gender, date of birth, nationality, second nationality,
     height, weight, and up to 10 **external identifiers** (code of the issuer, the identifier, valid from; the code is
     upper-cased). **Check and add** runs the duplicate check: with no one found, the person is added with the check's
     token and their page opens.
  3. **Check for duplicates** (when the check finds anyone). Each candidate next to the new person: how sure the check is
     ("Same identifier", "Likely the same person", "Possibly the same person"), name, birth year (never a date of birth), status (archived ones marked), sports
     played, the evidence in words ("Names 86% alike", "Holds the same KMSA_REG_NO identifier") and **Open {name}**.
     A shared identifier blocks the save ("This identifier is already in use", **Change the details**); otherwise **Add
     as a new person** with a reason (1–500 characters, kept for good, no personal details).
- A check that went stale before the save (someone added a match) runs again and shows the new list with a note; an
  expired one (30 minutes) runs again and saves at once if it finds the same people with the same confidence,
  otherwise it is reviewed again. Empty identifier rows are dropped before the check.
  A refused field opens its step with the error under it; an identifier taken meanwhile goes back to the details.

#### As built (S06-T7c-1a, Roles)

- **Roles** gains its changes, each shown only with its action (`athlete_profile.*`, `staff_profile.*`,
  `official_profile.*`: owner, administrator, registrar) and disabled with the reason on a read-only tenant (one note
  above the profiles). **Add athlete profile** is offered while an enabled sport has no open profile of the person's;
  **Add staff profile** and **Add official profile** while the person has no open one of that kind ("open": not
  archived). Without an enabled sport they are disabled with "No sport is enabled for your organisation yet…" (and
  an error if the tenant's sports can't be read). An
  archived or deceased person is offered none ("… no new role can be added").
- **Athlete profile** dialog: the sport (enabled ones only; only when adding, it never changes), primary position,
  other positions (checkboxes, up to five, never the primary one), dominant side, public name (empty: the display
  name). Positions are the sport's latest definition's, named in its words. One the profile holds that the latest
  definition dropped is shown "(no longer in the sport's definition)" and must be replaced before saving: the API
  refuses it (T4a), so the page says so first.
- **Staff** and **official profile** dialogs: 1–10 roles, each a sport and a role of its definition; a staff role adds a
  qualification, an official one a grade, a licence number and the licence's last day. The same sport and role twice is
  refused on the page. The roles are replaced as a whole. A role held on a sport since disabled may stay (that sport is
  offered only on its own row); a role the latest definition dropped is marked and must be replaced or removed. On an
  archived or deceased person the roles held are shown by name and can be removed or changed in their details, none
  added; a refusal of one shows under it.
- Each open profile has **Edit** and its status moves, each with a reason (`ActionDialog`): **Make inactive**,
  **Retire**, **Archive** from active; **Make active**, **Retire**, **Archive** from inactive; **Make active**,
  **Archive** from retired; none for an archived profile (final). An archived or deceased person's profiles are never
  made active again. After a change the dialog closes, the change is announced, and focus goes to the profile's heading.
- A refused field is shown under it (`otherPositions[i]`, `capabilities[i].role`); a concurrency conflict offers
  **Reload** in the dialog (the shared `CommandDialog`). A change is based on the person as the dialog opened: a
  later read while it is open (the photo's URLs are read again before they expire) doesn't move its `If-Match`, so
  someone else's change in between is a conflict, not overwritten (review #110 R1).
- Until a sport's definition is read, the positions and roles a profile holds are still shown; if it can't be read
  the dialog says so. The Add buttons appear together once the tenant's sports are read. An archived or deceased
  person's only role, if its sport's definition dropped it, can't be corrected: the dialog says to retire or archive
  the profile instead (review #110 R2; owner, Q1: no backend change for it).

#### As built (S06-T7c-1b, Consent and the public profile)

- The header's **Public profile on/off** gains **Turn on public profile** or **Turn off public profile**
  (`person.public_profile.enable`, `.disable`: owner, administrator, registrar), confirmed in an `ActionDialog`
  without a reason. The dialog says what the public site shows: an adult's display name, nationality, photo and
  athlete profiles; for anyone else the display name only, and the photo with consent to it. Turning on is disabled,
  with the reason under it, for an archived or deceased person and for anyone not known to be an adult without an
  active consent to the public profile ("… record it on the Consent tab"); turning off works in any status. A profile
  that is on but not shown says why: "(not shown while the person isn't active)" or "(not shown without a guardian's
  consent to the public profile)". After a change, focus goes to that state text (the button may now be disabled).
- **Consent** gains **Record consent** (`person.consent.record`) for anyone whose age status isn't `ADULT`; an archived
  or deceased person gets a note instead. The dialog: guardian's name, relationship, contact (a phone number or email
  address), what the guardian agrees to (Data processing, Public profile, Photo, each explained), the date given (the
  tenant's today by default, no later; offered once the tenant's time zone is read) and the evidence (optional). Data processing's wording is a placeholder marked
  "Placeholder wording, to be replaced by the approved text" (D14, Q13). With a file, the consent is recorded first and
  the file uploaded next, on the version that answered (D19). If the upload is refused, the consent stays recorded:
  the dialog says so (announced, and the focus goes to the file), locks the consent's fields, clears the refused file
  and offers **Upload evidence** once another is chosen; closing it announces "Consent recorded without its evidence." The private read is asked again once
  for the two calls, at their end.
- Each active consent has **Upload evidence** or **Replace evidence** (`person.consent_evidence.upload`; not on an
  archived or deceased person) and **Withdraw** (`person.consent.withdraw`, at any age and in any status): the
  consent's own scopes as checkboxes (at least one) and a reason. What is left goes on in a new record, which takes
  the focus. The evidence is a PDF, JPEG or PNG up to 10 MB: the picker offers those, a larger file is refused before
  it is sent, and the API's refusal (`MEDIA_*`) shows under the file.
- Every control shows only with its action and is disabled with the reason on a read-only tenant. After a change the
  dialog closes, the change is announced, and focus goes to the consent's heading. A concurrency conflict offers
  **Reload**. The private read (guardians, evidence) is asked again after a consent change.

#### As built (S06-T7c-2a, the photo, external identifiers and aliases)

- The header gains **Add photo** or **Change photo** (`person.photo.set`) and, with a photo, **Remove photo**
  (`person.photo.remove`, confirmed without a reason). The photo dialog takes one JPEG, PNG or WebP up to 5 MB (the
  picker offers those, a larger file is refused before it is sent) and previews it; the API's refusal of the file (`MEDIA_*`)
  shows under it, the storage quota's ("Your plan allows N GB of storage.") in the dialog's alert, and either clears
  the file and focuses the input; a conflict or a server failure keeps it. An archived or deceased person gets no new photo
  (the button is disabled with the reason); removing works in any status. After a change, focus goes to Add or
  Change photo.
- **External IDs & aliases** gains **Add identifier** (`person.external_id.add`): the issuer's code (upper-cased,
  checked as Add person does), the identifier and "valid from", which can't be later than the tenant's today (owner,
  Q46; the API still accepts a later one). A value someone has or had, and the national identity documents, are
  refused under their field. Each open identifier has **End** (`person.external_id.end`): the last day valid (the
  tenant's today by default, not before the start nor after today) and an optional reason. One that starts after
  today can't be ended yet ("Can be ended from …"). Ended identifiers stay listed.
- **Add alias** (`person.alias.add`): the name and its kind (Former name, Nickname, Another spelling); a name the
  person already has is refused under it. Each alias has **Remove** (`person.alias.remove`) with a reason.
- An archived or deceased person takes no new identifier or alias (a note instead of Add); ending and removing work in
  any status. Every control shows only with its action and is disabled with the reason on a read-only tenant. After a
  change the dialog closes, the change is announced, and focus goes to the section's heading. A concurrency conflict
  offers **Reload**; each dialog sends the version it opened on.
- Add person's identifier rows cap "valid from" at the tenant's today too.

#### As built (S06-T7c-2b, Edit details and the status)

- The header gains **Edit details** for whoever holds `person.update`, `person.read_private` and
  `person.duplicate_check` (owner, T7c-2b plan: the update replaces the whole biodata, the date of birth included,
  which only the private read gives; every role with `person.update` holds the other two). It opens
  `/t/{slug}/people/{id}/edit`, a page of its own, in any status (an archived or deceased person can be corrected):
  Add person's names and details, filled from the person and the private read (a default display name, "first last",
  is left empty so it follows the names), without identifiers or aliases (their own tab). The page waits for the list
  of countries, so a nationality is never shown, and saved, empty. It reads the person and the private details afresh
  on opening (one audited read per visit), so a date of birth kept from an earlier read never goes back under a newer
  version (review #113 R1); if either read fails, the error shows and the form doesn't open (R3).
- **Save**: a change to the names, the date of birth or a nationality, as typed, runs the duplicate check with the
  person left out (`personId`, no identifiers); anything else saves at once. The people the check finds are reviewed
  as on Add person, next to "With your changes", and **Save the changes** needs a reason. A check that went stale or
  expired runs again once. A refused field shows under it; a concurrency conflict offers **Reload**, which reads the
  person again and starts the form over. After the save the person's page opens and reads the private details again.
- The header's status moves, each with its action and only from the statuses D9 allows: **Deactivate** (active) and
  **Reactivate** (inactive), with an optional reason; **Archive** (active or inactive) and **Unarchive** (archived),
  with a reason; **Mark as deceased** (active or inactive), with a reason, in the danger colour, the dialog saying it is
  final. A deceased person is offered none. Each dialog sends the version it opened on; after a move it closes, the
  move is announced, the status history is read again and focus goes to the status. On a read-only tenant Edit
  details and the moves are disabled with one reason.

#### As built (S06-T7d-2, slice demo)
- **e2e** `admin-people-demo`: the slice demo on a tenant of its own (Coucal Sports Club). A registrar searches, opens
  a likely match from the review instead of adding it, and adds a twin past the review with a reason, after which the
  pair isn't warned again. They then add an athlete profile and a photo, record a minor's consent with the form and
  turn on her public profile. The public read shows only her name and photo until the photo scope is withdrawn. The
  spec also checks three profiles on one person, an archived person refused a new profile (on the screen and through
  the API), and the second tenant's owner (Francolin Schools Sports Circle) seeing none of the people. The audit
  trail keeps the reasons. Screenshots `add-person-likely-match` and `person-archived-roles`.

#### As built (S07-T6a, Venues)

- **Venues** (`/t/{slug}/venues`, `venue.read`), in the Registry group after People: sorted by name, 25 a page with
  **Show more**. **Search** (name or alias by prefix or similarity, an external identifier exactly) and **City** follow
  the typing after a 300 ms pause; **Status** (default "All but archived") and **Sport** (every published variant: a
  ground can host a sport the tenant doesn't run yet, S07 D3) change at once. All four are in the address. Columns:
  name, type, city and country, sports (named from their definitions), status; on a phone they stack under the name.
- **A venue** (`/t/{slug}/venues/{id}`): the header (name, status, type and city, **Edit details**, the status moves),
  then the tabs
  in `?tab=`: **Overview** (name, short name, type, capacity, surfaces, sports; address, city, region, country,
  coordinates with **Open in a map** (OpenStreetMap, a new tab), time zone), **Teams** (the teams whose home venue it
  is, archived ones left out, with category, sport, organisation and status; needs `team.read`; the names become links
  with the team screens, T6c), **History** (the status changes, newest first, as People's), **Names & IDs** (below).
- **Status moves** (`venue.status.change`, a reason required): from Active **Mark unavailable**, **Close venue**,
  **Archive**; from Temporarily unavailable **Mark available**, **Close venue**, **Archive**; from Closed **Reopen**,
  **Archive**; from Archived **Unarchive**, which names the status it goes back to (the one before archiving, from the
  history). A viewer sees no control; on a read-only tenant the moves are disabled with the reason.
- **Add venue** (`/t/{slug}/venues/new`, `venue.create` and `venue.duplicate_check`; the directory's button, disabled
  with the reason on a read-only tenant): three groups — the venue (name, short name, type, capacity), where it is
  (address, city or town, region, country, time zone, latitude and longitude, both or neither) and playing areas
  (surfaces, up to 5; the sports it can host: every published variant, named from its definition). The time zone
  starts as the organisation's; the country is the organisation's unless one is chosen. No web address: the server
  proposes one from the name (S07 D9; owner, T6a-2 plan). Saved, the venue opens.
- **The duplicate warning** (S07 D13, a panel between "Where it is" and "Playing areas"): 300 ms after the name, the
  city or the coordinates change, `POST …/venues/duplicate-check` lists the venues that may be the same, each with how
  sure (Likely, Possibly), its type, city and status, why it matches (same name, names N% alike, same city, about N m
  away) and **Open {name}**. The save waits until the user opens one or presses **This is a different one** (with
  **Undo** and an optional reason the audit trail keeps); a venue that appears after that shows the choice again. A
  save the server sends back (`VENUE_DUPLICATE_REVIEW_REQUIRED`: someone added a match meanwhile) shows the server's
  list with "Someone added a venue that may match", and moves the focus to the panel. The panel
  (`components/registry/duplicate-panel.tsx`) is shared with the organisation and team forms.
- **Edit details** (`/t/{slug}/venues/{id}/edit`, `venue.update` and `venue.duplicate_check`): every field of Add
  venue plus the **Web address** (the slug: lower case, digits and single hyphens, not shaped like an ID), in any
  status. The `PUT` sends the version the page opened on (`If-Match`) and the venue's country; a conflict shows "This
  record changed since you opened it" with **Reload**. The warning runs only once the name, the city or the
  coordinates change, and never lists the venue itself. "Change" is judged loosely here (case and spacing only);
  the server compares name and city keys, which also ignore accents and punctuation, so a punctuation-only fix may
  show the warning although the server wouldn't check it, and the decision then sent is ignored (review #138 Q1).
- **Names & IDs** (`venue.alias.add`/`.remove`, `venue.external_id.add`/`.end`): the aliases (any kind; removed ones
  not listed) with **Add alias** and **Remove** (a reason required), then the external identifiers (ended ones
  included) with **Add identifier** (authority code in capitals, value, valid from no later than today) and **End**
  (last day valid, optional reason). Each dialog sends the version it opened on and offers **Reload** on a conflict.
  A closed or archived venue takes no new one ("… is closed: no new one can be added."); its aliases can still be
  removed and its identifiers ended. The tab and its dialogs (`components/registry/names-and-ids/`) are shared with
  the organisation and team screens; People's tab stays its own (owner, T6a-2 plan).
- Screenshots `venues`, `venue-overview`, `venue-teams`, `venue-history`, `venue-status-dialog`, `venue-names`,
  `venue-identifier-dialog`, `venue-edit`, `add-venue-duplicates`.

#### As built (S07-T6b-1, Organisations)

- **Organisations** (`/t/{slug}/organisations`, `organisation.read`), in the Registry group between People and Venues:
  sorted by name, 25 a page with **Show more**. **Search** (name, alias or external identifier) follows the typing
  after a 300 ms pause; **Type** and **Status** (default "All but archived") change at once. All three are in the
  address. Columns: name (the short name under it), type, place (city and country, or the country alone), status; on
  a phone they stack under the name. Add organisation comes with T6b-2.
- **An organisation** (`/t/{slug}/organisations/{id}`): the header (logo, or its initials in a square without one;
  name, status, type and city; the status moves), then the tabs in `?tab=`: **Overview** (Organisation: name, legal
  name, short name, type, founded, description; Contact: city, country, website (a link only for an `http(s)` address,
  in a new tab), email, phone, primary venue (a link with `venue.read`); Structure: the parent and the member
  organisations, each a link with its status when not active, and **Set parent** or **Change parent**), **Teams** (its
  teams but archived ones, with category, sport named from its definition and status; the names become links with the
  team screens, T6c) and **History** (the status changes, then the parent links with their dates and reasons, the
  current one first).
- **Status moves** (`organisation.status.change`, a reason required): from Active **Make inactive**, **Suspend**,
  **Dissolve**, **Archive**; from Inactive **Reactivate**, **Suspend**, **Dissolve**, **Archive**; from Suspended
  **Make inactive**, **Lift suspension**, **Dissolve**, **Archive**; from Dissolved **Archive**; from Archived
  **Unarchive**, which names the status it goes back to (a dissolved organisation stays dissolved). Each dialog says
  the teams keep their own statuses. A viewer sees no control; on a read-only tenant the moves are disabled with the
  reason.
- **Set parent / Change parent** (`organisation.parent.set`, a reason required): **Find the parent** searches the
  directory 300 ms after the typing pauses (archived ones aren't listed) and lists the matches as choices with their
  type and city; the organisation itself, its current parent and its direct members aren't offered, and a dissolved one
  is shown unavailable ("Dissolved: it can't take member organisations."). With a parent, **No parent** clears it. A
  member further down is refused by the server and the refusal shows under the choice. The dialog sends the version it
  opened on and offers **Reload** on a conflict. A dissolved or archived organisation takes no new parent: without one,
  **Set parent** is disabled with "Dissolved: it can't take a parent." (or "Archived: …"); with one, **Remove parent**
  offers only **No parent**, without the search.
- Screenshots `organisations`, `organisation-overview`, `organisation-teams`, `organisation-status-dialog`,
  `organisation-history`, `organisation-parent-dialog`.

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
