# Empty-database UI use-case catalogue

This is the acceptance-test source of truth for refining the platform. Execute
the cases in ID order against the isolated walkthrough database. Do not use the
diverse staging database for these walkthroughs.

## Test protocol

For every case record one status:

- `NOT RUN` — prerequisites are not yet complete.
- `PASS` — the steps and expected UI/API outcome match.
- `BUG` — an existing control or connection behaves incorrectly.
- `GAP` — the required control, page, explanation, or workflow does not exist.
- `DESIGN` — the workflow works but its presentation or interaction needs refinement.
- `BLOCKED` — an external dependency prevents completion.

For `BUG`, `GAP`, or `DESIGN`, record the case ID, surface, viewport/device,
account, exact step, expected result, actual result, screenshot, and relevant
request status. Fix one case, rerun it, and rerun its listed regression cases
before moving on.

The catalogue covers every currently user-reachable workflow in public web,
admin web, and Flutter, plus expected account and lifecycle flows that the UI
must expose. Raw API operations without a user-facing entry are not treated as
completed UI; they become `GAP` candidates when encountered.

## Environment and fixed walkthrough data

Start the isolated environment from the repository root:

```powershell
.\infrastructure\deployment\staging\start-walkthrough.ps1 -Reset
```

URLs on the Docker host:

- Admin: `http://localhost:3000`
- Public: `http://localhost:3001/?tenant=walkthrough-league`
- API: `http://localhost:8080`
- Bootstrap account: `admin@sportsplatform.com` / (password redacted — never reuse prototype credentials)

Use the current Wi-Fi IPv4 address instead of `localhost` from a physical
device. Rebuild Flutter with matching `API_BASE_URL` and `PUBLIC_WEB_BASE_URL`
before the physical-device cases.

Create this fixed data so later cases refer to the same records:

- Tenant: **East Africa Walkthrough League** (`walkthrough-league`, `EAWL`)
- Primary contact: **Grace Njeri**, `grace@walkthrough.test`
- Venue labels: **Lakeview Arena**, **Riverside Grounds**, **Central Athletics Stadium**
- Teams: **Lakeview Rangers**, **Riverside Falcons**, **Highland Warriors**,
  **Coastal Mariners**, **Savannah Lions**, **Capital City Stars**,
  **Valley United**, **Forest Guardians**
- Athletes: use distinct sensible names; create at least 32 for team-sport and
  individual-sport variants
- Applicant: `applicant@walkthrough.test` / (password redacted — never reuse prototype credentials)
- Tenant owner: `owner@walkthrough.test` / (password redacted — never reuse prototype credentials)
- Competition administrator: `competitions@walkthrough.test` / (password redacted — never reuse prototype credentials)
- Fixture manager: `fixtures@walkthrough.test` / (password redacted — never reuse prototype credentials)
- Scorer: `scorer@walkthrough.test` / (password redacted — never reuse prototype credentials)
- Result approver: `approver@walkthrough.test` / (password redacted — never reuse prototype credentials)
- Read-only viewer: `viewer@walkthrough.test` / (password redacted — never reuse prototype credentials)

## Execution gates

1. Complete `ENV`, `AUTH`, `TEN`, and `IAM` before tenant operations.
2. Complete `SPORT`, `PART`, and `REGF` before creating full competitions.
3. Complete the relevant `COMP` case before scheduling or scoring that format.
4. Complete `REG` before readiness, scoring, result approval, and public outputs.
5. Public web and Flutter cases must consume data created through admin cases;
   do not insert records directly into the database.

---

## 1. Environment and empty-state baseline

### ENV-001 — Reset the isolated walkthrough database

Surface: Docker/operations. Steps: run `start-walkthrough.ps1 -Reset`; wait for
all services; open API health, admin, and public URLs. Expected: all return HTTP
200; the normal staging volume is preserved; only the reserved Platform
Administration tenant, system sports, system roles, and the bootstrap
administrator exist. There is no test/business tenant or competition data.

### ENV-002 — Verify honest admin empty states

Surface: Admin desktop and 390 px web viewport. Steps: sign in; visit every
sidebar page before creating a tenant. Expected: pages show useful empty-state
copy and the next valid action; no spinner remains indefinitely; no seeded
tenant, participant, competition, fixture, result, sponsor, or report appears.

### ENV-003 — Verify honest public empty state

Surface: Public desktop and mobile web. Steps: open the walkthrough tenant URL
before the tenant exists, then after `TEN-001` but before publishing content.
Expected: unknown tenant is explained without a raw exception; the new tenant
shows branded zero states without fake contests or another tenant's cache.

### ENV-004 — Verify API/network error states

Surface: All clients. Steps: stop the API temporarily; refresh one list and one
detail page in each client; restart the API and retry. Expected: readable error,
retry action, no destructive local reset, successful recovery.

### ENV-005 — Verify isolation and resume

Surface: Docker/operations. Steps: stop walkthrough without deleting staging;
resume staging; verify seeded staging; restart walkthrough without `-Reset`.
Expected: both databases retain their own data and never cross-contaminate.

## 2. Authentication, account, and session flows

### AUTH-001 — Valid and invalid admin sign-in

Steps: try a wrong password, then bootstrap credentials. Expected: generic
invalid-credential message, no stack trace, successful redirect, persisted user
and token, correct platform navigation.

### AUTH-002 — Admin sign-out and protected-route guard

Steps: sign out; navigate directly to `/tenants`, `/users`, and `/scoring`.
Expected: session is cleared and every protected route returns to login.

### AUTH-003 — Forgot-password request

Steps: open Forgot password, submit known and unknown email addresses, inspect
Mailpit. Expected: identical anti-enumeration UI response; known account gets a
usable reset message in the local mail environment.

### AUTH-004 — Password reset

Steps: open reset link, test expired/invalid token, then valid token; sign in
with old and new passwords. Expected: validation is clear, old password fails,
new password succeeds, existing refresh sessions are revoked.

### AUTH-005 — Email verification

Steps: create a user, request verification, follow link, repeat link. Expected:
verified state changes once and repeated/invalid links are handled safely.

### AUTH-006 — Change password from mobile profile

Steps: sign in on Flutter, open Profile, change password, sign out, sign in with
old then new password. Expected: real API update and accurate success/error UI.

### AUTH-007 — Edit own mobile profile

Steps: edit display name; save; restart the app; check admin Privacy profile.
Expected: name persists remotely and locally; email remains non-editable.

### AUTH-008 — Access/refresh token lifecycle

Steps: remain signed in past access-token expiry or force expiry in a test
build; perform a read. Expected: one transparent refresh, no duplicate request,
and logout only when refresh is invalid.

### AUTH-009 — Corrupt secure-storage recovery

Surface: Flutter. Steps: install over an APK signed/configured differently or
inject unreadable secure storage. Expected: saved credentials are cleared,
friendly sign-in prompt appears, and no crypto stack trace is rendered.

### AUTH-010 — Multi-device sessions and revocation

Expected UI flow: list active sessions, identify device, revoke another session,
and confirm it can no longer refresh. If there is no page/control, record `GAP`.

### AUTH-011 — MFA enrol, verify, use, and disable

Expected UI flow: display QR/secret, verify TOTP, require it on next login,
handle invalid code, and disable after confirmation. If absent, record `GAP`.

### AUTH-012 — Applicant account for the selected tenant

Surface: Public web and Flutter. Steps: select `walkthrough-league`, create the
applicant account, sign out/in, and try the same session against another tenant
slug. Expected: account is tenant-scoped; mismatched tenant offers explicit
sign-out/switch handling and cannot read or create another tenant's entries.

## 3. Tenant and platform administration

### TEN-001 — Create the walkthrough tenant

Steps: Admin > Tenants > New tenant; enter fixed name, short name, and slug.
Expected: created once, standard tenant roles provisioned, row/detail visible,
duplicate slug rejected inline.

### TEN-002 — Switch into and out of tenant workspace

Steps: use Active tenant workspace; enter EAWL; return to Platform. Expected:
dashboard, branding, role lists, APIs, and navigation all use the chosen scope;
same-named roles are clearly grouped by tenant.

### TEN-003 — Edit tenant profile

Steps: edit name, short name, description, and slug constraints; refresh.
Expected: values persist and invalid/duplicate inputs are explained.

### TEN-004 — Configure localization

Steps: set Kenya, Africa/Nairobi, KES, and en-KE; save. Expected: dates, times,
currency, and public/admin labels use tenant localization where applicable.

### TEN-005 — Manage tenant contacts

Steps: add Grace as primary; add secondary; promote secondary; edit and delete.
Expected: exactly one primary, valid email/phone handling, persisted table.

### TEN-006 — Suspend and reactivate tenant

Steps: suspend EAWL; test admin/public/applicant access; reactivate. Expected:
status and access policy are consistent and recovery preserves data.

### TEN-007 — Archive tenant

Steps: archive only after other lifecycle tests. Expected: explicit destructive
confirmation, public unavailability, historical admin/audit visibility.

### TEN-008 — Tenant deletion protection

Expected: active/non-empty tenant cannot be silently deleted; dependencies and
recovery implications are shown. Missing UI is `GAP`.

### TEN-009 — Tenant dashboard from empty to active

Steps: capture dashboard after create, participants, competitions, live contest,
and result. Expected: counts/actions change from real data without stale cache.

### TEN-010 — Platform dashboard and support actions

Steps: return to Platform; search tenant; run support action with reason; send a
platform notification; inspect audit. Expected: tenant target and reason are
explicit, action is audited, no cross-tenant mutation.

## 4. Roles, users, and authorization

### IAM-001 — Inspect standard roles by tenant

Expected: Platform roles and EAWL roles appear in separate understandable
groups with descriptions and capability lists.

### IAM-002 — Create, edit, and delete a custom role

Steps: create `Fixture Reviewer`, select permissions, edit description and
permissions, delete while unused. Expected: exact tenant category and safe
validation; system roles cannot be edited/deleted.

### IAM-003 — Create the walkthrough users

Steps: create all fixed staff users with least-privilege roles. Expected: table
shows name, email, tenant, roles, and state; duplicate email is rejected.

### IAM-004 — Assign and remove roles

Steps: assign a second role; sign in as user; remove it; refresh user session.
Expected: navigation and API authorization reflect current permissions.

### IAM-005 — Suspend and unsuspend user

Steps: suspend scorer with reason; test login/refresh; unsuspend. Expected:
revoked access while suspended, recovery afterward, audit trail.

### IAM-006 — Read-only permission boundaries

Steps: sign in as viewer; visit every sidebar route and direct URL. Expected:
readable pages remain available, mutation controls are hidden/disabled, direct
mutation calls return 403 without leaking data.

### IAM-007 — Cross-tenant authorization

Steps: create a second minimal tenant; attempt IDs/routes from EAWL while scoped
to it. Expected: no lists, files, forms, or records cross tenant boundaries.

## 5. Sports and rulesets

### SPORT-001 — Inspect global sport catalogue

Expected: Football, Basketball, Golf, Athletics, Cricket, Combat, and Rugby
Union are present with understandable participant/contest shapes.

### SPORT-002 — Enable/disable tenant sports

Steps: enable the seven flagship sports for EAWL; change one tenant label/icon;
disable and re-enable one unused sport. Expected: creation options and public
navigation follow tenant enablement without changing global definitions.

### SPORT-003 — Create and validate a custom sport definition

Steps: create a test sport with shapes/metrics; enter invalid then valid metric
definitions. Expected: server validation errors map to fields; saved definition
is readable. Archive/disable it after the case.

### SPORT-004 — Clone, edit, and publish a ruleset version

Steps: select a sport, clone/create version, edit scoring metrics and lifecycle,
publish it. Expected: published versions become selectable; used versions are
immutable; draft changes do not alter published competitions.

### SPORT-005 — Configure all flagship rulesets

Repeat configuration and retrieval for football, basketball, golf, athletics,
combat, cricket, and rugby. Expected: no football defaults leak into another
sport and each ruleset exposes sport-specific scoring actions.

### SPORT-006 — Rugby variants

Expected: a single Rugby sport supports distinct 7s and 15s competition
variants, halves/durations, squads, scoring, standings bonus points, and public
labels without creating unrelated duplicate sport records.

## 6. Participants, organizations, rosters, and imports

### PART-001 — Athlete list empty/create/view/edit

Steps: inspect empty table; create athlete; view detail; edit protected/public
profile fields; refresh. Expected: DataTable + view dialog + edit workflow,
tenant ownership, validation, and audit history.

### PART-002 — Athlete multi-sport membership

Steps: add football and athletics memberships; edit sport-specific attributes.
Expected: memberships remain distinct and public/private fields are respected.

### PART-003 — Create team and club profiles

Steps: create Lakeview Rangers as Team and a parent Club; view/edit profiles.
Expected: correct type labels, no card-list ambiguity, sensible public identity.

### PART-004 — Duplicate organization warning

Steps: try same/similar name and short name. Expected: duplicate candidates are
shown before creation and operator can cancel or make an explicit decision.

### PART-005 — Create seasonal and competition rosters

Steps: create active, future, and ended rosters for Lakeview Rangers. Expected:
dates/competition scope and active state render correctly.

### PART-006 — Add/transfer athlete to roster

Steps: choose athlete, jersey, position, reason; transfer between rosters.
Expected: one current membership, movement history, duplicate-number handling.

### PART-007 — Build all fixed participants

Create the eight teams and at least 32 named athletes required by later sport
cases. Expected: pagination/search/table counts and responsive list behavior.

### PART-008 — Download import templates

Repeat for athletes, teams, and roster entries. Expected: correct CSV headers,
tenant-independent template, downloadable filename/content type.

### PART-009 — Preview valid import

Upload a valid file for each import type. Expected: row counts, normalized
values, duplicates, and errors appear before mutation.

### PART-010 — Preview invalid and mixed import

Use missing columns, invalid values, duplicates, and mixed valid/invalid rows.
Expected: actionable row-level errors and no mutation during preview.

### PART-011 — Execute import and download report

Expected: queued/completed states, imported/duplicate/failed totals, history,
and report CSV match actual persisted rows.

### PART-012 — Idempotent re-import

Re-import the same files. Expected: no duplicate participants or roster entries;
duplicates are reported consistently.

## 7. Registration-form administration

### REGF-001 — Create reusable form

Steps: create an unscoped form with name/description. Expected: it appears in
the tenant table and can be viewed/edited separately.

### REGF-002 — Create competition-specific form

After the first competition exists, create its form and verify it is offered
only on that competition's public/applicant entry flow.

### REGF-003 — Exercise every field type

Add Text, Number, Date, Select, multi-Checkbox, Consent, and File fields with
required/optional, order, help, placeholder, min/max, regex, content type, and
size constraints. Expected: admin preview, public web, and Flutter agree.

### REGF-004 — Conditional fields

Create show/hide conditions for at least two values. Expected: hidden values
are not incorrectly required or submitted; behavior matches all clients.

### REGF-005 — Server-side preview validation

Test empty, invalid, boundary, and valid sample data. Expected: field-specific
errors, no raw JSON/exception, successful valid response.

### REGF-006 — Admin preview file upload

Upload valid PDF, wrong MIME type, empty, and oversized files. Expected: limits
are enforced and file metadata is rendered safely.

### REGF-007 — Versioning, activation, deactivation, and cloning

Expected UI: activate a version, clone/new version, preserve old submission
snapshots, deactivate, and prevent public use. Any missing control is `GAP`.

## 8. Competition creation and format variants

For every case verify invalid input, save/persistence, hierarchy, registration,
fixture creation, publication validation, public tabs/labels, and mobile output.

### COMP-001 — Competition list, search, view, and empty state

Expected: shared table, understandable status/sport/format, separate view and
configuration actions, responsive behavior.

### COMP-002 — Round-robin football league

Create **EAWL Football Championship**, register eight teams, generate single
round robin, validate, publish. Expected: league fixtures and standings.

### COMP-003 — Group plus knockout football tournament

Create two groups of four and knockout progression. Expected: group standings,
qualified seeds, bracket, no premature progression.

### COMP-004 — Top-four playoff

Create league/top-four playoff variant. Expected: 1v4/2v3 logic, final and
third-place behavior where configured.

### COMP-005 — Single-elimination knockout

Expected: valid seeding, byes if supported, bracket progression and override
approval/rejection workflow.

### COMP-006 — Basketball competition

Expected: quarters, basketball scoring actions, overtime scenario, standings,
and basketball terminology.

### COMP-007 — Rugby 15s league/knockout

Expected: tries/conversions/penalties/drop goals/cards, halves, bonus-point
standings, squad/readiness, and Rugby 15s public identity.

### COMP-008 — Rugby 7s pools/knockout

Expected: seven-player variant, 7-minute halves, pool standings, knockout,
Rugby 7s labels, and no Rugby 15s duration/bonus assumptions unless configured.

### COMP-009 — Cricket tournament

Expected: teams, innings, overs/balls/wickets/extras, scorecard, qualification,
and cricket-specific public output.

### COMP-010 — Golf stroke-play tournament

Expected: golfers, rounds/holes, scorecards, cut, ties/withdrawal/DQ, leaderboard.

### COMP-011 — Athletics meet

Expected: track heats/finals, qualification, lanes, field attempts/fouls,
DNS/DNF/DQ, times/distances and meet output.

### COMP-012 — Combat fight card

Expected: weight classes, prelim/main/co-main/main event order, officials,
rounds, methods, scorecards, result correction approval, public fight card.

### COMP-013 — Best-of series

Expected: series score, game number, clinch behavior, remaining-game handling,
and series-specific public presentation.

### COMP-014 — Manual multi-stage hierarchy

Steps: add edition, stages, groups, rounds, and contests manually. Expected:
selectors follow dependencies and hierarchy persists without orphan records.

### COMP-015 — Presentation metadata

Edit score/standings/leaderboard variants and labels. Expected: admin preview,
public web, and Flutter consume metadata rather than guessing by sport name.

### COMP-016 — Publication validation and archive

Try publishing incomplete then complete competition; archive after lifecycle
testing. Expected: actionable validation list, no partial publish, public cache
updates, archived data handled consistently.

## 9. Applicant registration and staff review

### REG-001 — Public registration availability

Steps: view competition with no active form, inactive form, then active form.
Expected: Register CTA appears only when entry is genuinely open.

### REG-002 — Public applicant authentication return path

Steps: choose Register while signed out; create/sign in account. Expected:
return to the same competition application, retaining tenant and intent.

### REG-003 — Create individual applicant participant

Expected: applicant owns it, sees it in selector, other applicant/tenant cannot.

### REG-004 — Create team applicant participant

Expected: same ownership isolation with Team type and sensible short name.

### REG-005 — Start and save draft

Fill part of form, upload evidence, save, sign out/in, resume from My
Registrations. Expected: same versioned values/files and Draft status.

### REG-006 — Client and server validation

Submit missing/invalid/conditional/boundary data. Expected: field-level errors,
draft preserved, no canonical submission transition.

### REG-007 — Final submission

Expected: form and canonical registration become Submitted exactly once;
double-click/retry is idempotent; My Registrations shows revision/status.

### REG-008 — Applicant withdrawal

Withdraw with reason from an eligible state. Expected: both relevant status
views update; invalid later transitions are unavailable.

### REG-009 — Staff-assisted draft/submission

Admin > Registration review: select participant/type, create draft and immediate
submission variants. Expected: queue updates and audit identifies staff action.

### REG-010 — Form review start

Open linked submitted form and Start review. Expected: form and canonical
registration synchronize to Under review without losing snapshot/version.

### REG-011 — Request changes and resubmit

Staff requests specific changes; applicant sees note, edits draft, uploads new
file, resubmits. Expected: revision increments and history remains understandable.

### REG-012 — Approve/reject form

Expected: required reason rules, status notification, and separation between
form correctness and canonical competition eligibility decision.

### REG-013 — Eligibility policy and check

Configure squad/age/weight/deadline/document/number rules; test pass/fail.
Expected: precise issues and stable calculation date.

### REG-014 — Canonical review decisions

Exercise Submitted > UnderReview > Approved/Rejected/Waitlisted, eligibility
override with reason, confirm, withdraw, cancel, and no-show where legal.
Expected: only legal actions appear; every decision is audited/notified.

### REG-015 — Registration ownership/file security

Try direct IDs and uploaded-file URLs as another applicant and tenant. Expected:
404/403 with no metadata/content disclosure.

## 10. Scheduling, readiness, and fixture changes

### SCHED-001 — Generate round-robin fixtures

Expected: correct unique pairings, home/away behavior, count, and no duplicate
generation on retry.

### SCHED-002 — Configure schedule constraints

Set venues, blackout dates, rest/travel/duration constraints. Expected:
validation persists and conflicts are understandable.

### SCHED-003 — Conflict detection and override

Create venue/time/rest conflicts, validate, add mandatory override reason.
Expected: publish blocked until resolved/overridden and audit records reason.

### SCHED-004 — Reschedule request

Change time/venue/duration with reason. Expected: adjustment enters pending
review and public schedule does not change prematurely.

### SCHED-005 — Approve/reject schedule adjustment

Use a different authorized administrator. Expected: separation of duties,
review notes, accepted change or preserved original schedule.

### READY-001 — Open check-in

Expected: contest readiness checklist opens only in a legal pre-start state.

### READY-002 — Participant check-in, withdrawal, DQ

Exercise every participant readiness state and reason. Expected: counts and
start eligibility update consistently.

### READY-003 — Confirm roster, officials, and venue

Expected: each requirement has an understandable control/history and blocks
start when incomplete.

### READY-004 — Complete readiness and start

Expected: successful checklist allows scoring session; incomplete checklist
blocks with precise issues.

### READY-005 — Readiness override

Authorized override with mandatory reason. Expected: role restriction, warning,
audit entry, and visible overridden requirement.

## 11. Live scoring and sport-specific event flows

### SCORE-001 — Scorer contest selection and authorization

Admin and Flutter scorer: show assigned/eligible contests, deny viewer, open the
same contest. Expected: tenant/sport/status are clear and no UUID entry is needed
for normal operation.

### SCORE-002 — Start/pause/resume/end scoring session

Expected: legal lifecycle controls, clock/segment persistence across refresh,
SignalR updates, and safe double-click/retry.

### SCORE-003 — Generic event add/correct/reverse

Record event, request correction, approve/reject as authorized role, reverse
with reason. Expected: immutable history and rebuilt score/timeline.

### SCORE-004 — Football event set

Exercise goal, own goal, penalty scored/missed, yellow, second yellow, red,
substitution, half transitions, stoppage/extra time, shootout where configured.

### SCORE-005 — Basketball event set

Exercise 1/2/3 points, fouls, timeouts, quarters, overtime, corrections.

### SCORE-006 — Rugby 15s event set

Exercise try, conversion, penalty, drop goal, yellow/red, halves, bonus-related
outcome and correction/reversal.

### SCORE-007 — Rugby 7s event set

Repeat with correct 7s duration/variant and pool/knockout context.

### SCORE-008 — Cricket innings and delivery scoring

Create/start innings; legal ball, runs, boundary, extras, wicket types, over,
innings complete, chase/qualification, delivery correction.

### SCORE-009 — Golf scorecards

Record holes/rounds, submit scorecards, approve/reject, apply cut, correction,
withdrawal/DQ/tie and leaderboard recalculation.

### SCORE-010 — Athletics track and field

Record heats/races, times/statuses, qualification; record field attempts/fouls,
rank/tie behavior and finals.

### SCORE-011 — Combat bout scoring

Start/end rounds, enter decision/KO/submission, scorecards, invalid method,
result correction request and separate approval.

### SCORE-012 — Offline mobile scoring queue

Disconnect after session load; record supported offline actions; restart app;
reconnect/sync. Expected: explicit queued state, ordered idempotent delivery,
conflict handling, no duplicate score.

### SCORE-013 — Live reconnect

Interrupt SignalR on public web and Flutter. Expected: reconnect banner, cached
state, resubscription, and authoritative refresh without duplicated timeline.

## 12. Result review, publication, and derived outputs

### RESULT-001 — Submit contest results

Expected: scoring completion produces/updates canonical results and enters
review queue once.

### RESULT-002 — Review and approve results

Use result approver, not scorer. Expected: separation of duties, reason/notes,
approved snapshot, standings/bracket/series update.

### RESULT-003 — Reject and correct result

Expected: rejection reason visible; corrected score/rank/performance preserves
history and can be resubmitted.

### RESULT-004 — Publish result

Expected: public web/mobile show only allowed published state, correct labels,
timeline, statistics and result status.

### RESULT-005 — Reopen published result

Authorized reason, correct, reapprove. Expected: public/derived output updates,
old state remains audited, cache invalidated.

### RESULT-006 — Snapshot rebuild

Rebuild after a known correction. Expected: deterministic snapshot matching
event history and an auditable reason.

### RESULT-007 — Records and bests

Exercise personal best, season best, and competition record where applicable.
Expected: valid sport/output only and public athlete/meet rendering.

### RESULT-008 — Progression outputs

Verify league/group standings, bracket, top-four, series, golf leaderboard,
athletics qualification, cricket qualification, and combat card after each
approved result.

## 13. Public web discovery and supporter flows

### PUB-001 — Tenant-branded public home

Desktop/tablet/mobile: logo/name/colors/contact, real counts, date navigator,
status/sport filters, live/upcoming/finished sections and zero states.

### PUB-002 — Global navigation and all sports

Expected: flagship and More sports derive from published tenant data; Rugby is
one entry with 7s/15s content; no Premier League hard-coding.

### PUB-003 — Date and status filtering

Exercise previous/next/date picker and All/Live/Upcoming/Finished. Expected:
URL/state/back behavior and server/local filtering agree.

### PUB-004 — Search

Search competition, contest, team, athlete, and venue; test no result and typo.
Expected: categorized results and working detail links.

### PUB-005 — Competition directory/detail

Expected: published competitions only, sport/format/status, relevant dynamic
tabs, contextual empty states and no irrelevant football terminology.

### PUB-006 — Contest/match centre

Expected: overview, timeline, statistics, details, participants, status, share,
live reconnect, and sport-specific score presentation.

### PUB-007 — Team page

Expected: public identity, roster/fixtures/results where allowed, registration
or medical private data absent.

### PUB-008 — Athlete page

Expected: public profile, sports/results/bests where allowed, private fields
absent and missing participant handled gracefully.

### PUB-009 — Results and standings

Expected: tenant/competition selection, correct published-only data, league and
group/conference variants, responsive tables.

### PUB-010 — Brackets/playoffs and series

Expected: responsive bracket navigation, seeds/byes/progression and series game
state; narrow layouts scroll/stack intentionally.

### PUB-011 — Golf leaderboard

Expected: position/ties/to-par/today/thru/round details/statuses and mobile row
expansion.

### PUB-012 — Athletics meet

Expected: heats/finals/lanes/times/qualification and field-event attempts.

### PUB-013 — Cricket scorecard

Expected: innings, runs/wickets/overs, batting/bowling/deliveries and result.

### PUB-014 — Combat fight card

Expected: ordered sections, bout status, fighters, rounds, method, officials.

### PUB-015 — Favourites, sharing, and persistence

Add/remove competition and contest favourites; refresh/restart; share links.
Expected: tenant-preserving deep links, no duplicate items, empty state.

### PUB-016 — Public applicant navigation

Expected: Login while signed out, My registrations while correctly signed in,
explicit handling for a different tenant's session, sign-out path.

### PUB-017 — Browser back/forward and deep links

Exercise home filters, tabs, details, registration return path, and direct URLs.
Expected: state is recoverable and no navigation dead end.

## 14. Flutter public, applicant, and scorer flows

### MOB-001 — Fresh install public-first launch

Expected: tenant selection/resolution leads to public home without forced login;
login remains available.

### MOB-002 — Tenant code selection and persistence

Enter invalid then `walkthrough-league`; restart app; switch tenant. Expected:
validation, branding/API scope persistence, authenticated-tenant mismatch handled.

### MOB-003 — Home responsive matrix

Test Redmi phone portrait/landscape and available tablets. Expected: no overflow,
single-line navigation, readable filters/cards/date strip.

### MOB-004 — Root back behavior

On Home press Android back once, wait, then twice. Expected: first press gives
exit confirmation/double-back prompt; navigation stacks pop before app exit.

### MOB-005 — Live, favourites, competitions, and search tabs

Expected: each bottom-navigation destination is real, preserves its own stack,
quick actions navigate, and back returns predictably.

### MOB-006 — Competition detail/output tabs

Repeat all relevant format outputs. Expected: metadata-driven tabs/labels,
registration CTA only when open, bracket/leaderboard/fight-card routes.

### MOB-007 — Contest detail/timeline/statistics

Expected: all available tabs, participant links, live updates, status/time labels,
sharing and refresh/retry.

### MOB-008 — Favourite and alert preferences

Favourite contest/competition, choose alert types, grant/deny notification
permission, disable/re-enable, restart. Expected: state and topic sync are clear.

### MOB-009 — Notification list

Expected: unread count, mark one/read all/delete, deep link to referenced entity,
empty/error/offline behavior.

### MOB-010 — Push receipt states

Foreground, background, terminated, direct-device, tenant-topic and favourite
topic notifications. Expected: receipt, correct deep link and tenant context.

### MOB-011 — Applicant registration

Repeat `REG-002` through `REG-007` in Flutter including every field type and
file picker. Expected: equivalent status/data to public web.

### MOB-012 — My Registrations

Expected: empty/list/status/note/revision, refresh, resume changes, withdraw and
tenant/account boundaries.

### MOB-013 — Profile/settings

Expected: profile edit, password change, theme, low-data mode, notifications,
account-deletion request, logout; unsupported social linking is not shown.

### MOB-014 — Participant details

Open from search/contest/team/athlete routes. Expected: public data for guests,
authorized data where appropriate, graceful unavailable state.

### MOB-015 — Scorer login and selection

Expected: only scorer-capable user sees scoring; viewer gets clear denial;
assigned contests are discoverable without manual IDs.

### MOB-016 — Mobile scoring parity

Repeat common and flagship scoring cases supported by Flutter; verify online,
offline queue, corrections and results handoff.

### MOB-017 — Cached/offline public browsing

Load home/details online, disconnect/restart, revisit. Expected: saved-data
indicator, last valid data, no false “no contests”, clear stale/retry state.

### MOB-018 — Loading and slow-network behavior

Throttle network. Expected: progressive/lazy loading, stable skeletons, no
blocked whole page, cancellation-safe navigation.

### MOB-019 — Accessibility

Large text, TalkBack labels/order, contrast, tap targets, keyboard where
applicable. Expected: no clipped critical control and understandable semantics.

### MOB-020 — Upgrade and secure-storage migration

Install previous APK, sign in/select tenant, install new APK over it. Expected:
safe session recovery or friendly reauthentication, preserved non-secret prefs.

## 15. Notifications, sponsors, reports, audit, and privacy

### OPS-001 — Notification campaign

Create tenant-wide, competition-scoped, and scheduled/immediate campaigns.
Expected: validation/readiness, history/status/counts and recipient delivery.

### OPS-002 — Notification denial/failure/retry

Expected: denied permission and provider failure are visible without false
success; campaign history remains auditable.

### OPS-003 — Sponsor profile lifecycle

Create/edit/deactivate/reactivate sponsor with logo/website/tier. Expected:
table/detail persistence and public visibility rules.

### OPS-004 — Sponsorship placement lifecycle

Create/edit/activate/terminate competition and contest placements with date,
value/currency/order. Expected: public visibility only inside active window.

### OPS-005 — Generate and download report

Repeat every report catalog type with valid and invalid parameters. Expected:
queued/completed/failed status, downloadable correct file, tenant isolation.

### OPS-006 — Contest sheet PDF

Archive, retry failure, and download. Expected: sensible name, correct contest
data/timeline/results, valid PDF.

### OPS-007 — Tenant export ZIP

Generate, retry, download, inspect contents. Expected: scoped complete export,
no other tenant data, stable job history.

### OPS-008 — Audit filtering and pagination

Filter by tenant, actor, action, entity, date; page results. Expected: every
tested mutation has old/new/reason where applicable and readable identity.

### OPS-009 — Own privacy profile/settings/consents

Correct profile, change visibility/activity/analytics/marketing, grant/revoke
each consent. Expected: persisted current state and audit records.

### OPS-010 — Data access request/download

Submit request, download JSON package. Expected: authenticated owner only,
accurate data and request history.

### OPS-011 — Account-deletion request review

Submit from mobile/admin privacy; approve/reject; complete approved request.
Expected: separation of states, anonymization/session revocation only on complete.

### OPS-012 — Retention policy lifecycle

Create policy, validate unsupported anonymization target, run, inspect count,
delete with reason. Expected: safe scope and audit trail.

### OPS-013 — Subscription and entitlements

Create/activate subscription if UI exists, inspect usage/entitlements, toggle
hard limits, exceed a limit, suspend/renew. Missing lifecycle controls are `GAP`.

### OPS-014 — Branding propagation

Edit colors/contact/logo; verify admin shell, public web, Flutter after refresh
and cache expiry. Expected: valid asset URLs and fallback for invalid colors.

## 16. Cross-surface consistency and non-functional cases

### XUI-001 — Naming consistency

Compare all surfaces: Fixtures/Schedule, Scores/Results, Live, Competitions,
Registrations, Teams/Athletes, statuses. Expected: deliberate contextual terms,
not accidental synonyms.

### XUI-002 — Status consistency

For every contest, registration, form, result, job, tenant, sponsorship and
notification state, compare API/admin/public/mobile label, color and actions.

### XUI-003 — Empty/loading/error/permission states

Every list and detail page must have all four states with next action/retry and
without raw exception text.

### XUI-004 — Table/view/create/edit standard

Admin resources use DataTable lists, separate readable view details, and
create/edit dialogs or pages; no dense card list for operational records.

### XUI-005 — Responsive web matrix

Run key public/admin cases at 1440, 1280, 1024, 768, 430, 390, and 360 widths.
Expected: no horizontal document overflow, reachable navigation, usable dialogs.

### XUI-006 — Keyboard and screen-reader baseline

Tab through dialogs/tables/forms, use Escape, verify focus return, labels,
headings, status announcements and destructive confirmations.

### XUI-007 — Double-submit and retry safety

Double-click create/save/submit/publish/approve/scoring actions and repeat after
timeout. Expected: disabled busy state and idempotent server result.

### XUI-008 — Concurrent-editor conflict

Open same resource/session in two clients and make conflicting changes.
Expected: version/conflict explanation or deterministic last-write policy; no
silent data corruption.

### XUI-009 — Tenant cache isolation

Switch tenant/slug repeatedly in each client. Expected: branding, lists, auth,
favourites, notifications and cached data never bleed across tenants.

### XUI-010 — Security/error disclosure

Try guessed IDs, malformed payloads, unauthorized files/routes and expired
tokens. Expected: 400/401/403/404 as appropriate and no stack trace, SQL, path,
secret or another tenant's metadata in UI/API.

### XUI-011 — Performance baseline

Populate through imports and normal UI; measure first home render, incremental
contest loading, large admin tables, search, scoring event response and restart.
Expected: pagination/lazy loading and responsive interaction under test volume.

### XUI-012 — End-to-end traceability

Choose one approved result and trace tenant > sport/ruleset > competition >
participant/registration > fixture/readiness > scoring timeline > review >
derived output > public/mobile display > audit/report. Expected: IDs and states
remain connected with no manual database repair.

---

## Suggested first execution batch

Execute only these before implementing more features:

1. `ENV-001`
2. `AUTH-001`
3. `ENV-002`
4. `TEN-001`
5. `TEN-002`
6. `TEN-003`
7. `TEN-004`
8. `TEN-005`
9. `OPS-014`
10. `ENV-003`

This establishes the tenant, exposes the earliest empty-state and workspace
problems, and gives public/mobile clients a real tenant identity for all later
walkthroughs.
