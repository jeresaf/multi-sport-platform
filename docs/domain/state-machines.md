# Canonical state machines

Decision: ADR-0007. These are the **only** lifecycle states in the system (AR-15). Each machine has one owning
module. Transitions are commands (action codes); "reason" means the request must include a reason that is
stored in status history and audit. Derived states are computed, never stored.

---

## Tenancy & Commercial

### Tenant (stored)

```text
PROVISIONING ──activate──► ACTIVE ◄──restore── SUSPENDED
                             │  └──suspend (reason)──►┘
                             └──archive (reason)──► ARCHIVED ◄──archive (reason)── SUSPENDED
ARCHIVED ──unarchive (platform, reason)──► SUSPENDED
```

### Tenant domain (stored)

Added by the owner, 2026-10-19 (S02-T3b). A host name that serves a tenant's public site (`tenancy.tenant_domains`).

```text
PENDING_VERIFICATION ──verify──► ACTIVE ──disable──► DISABLED
                                   ▲                    │
                                   └──────enable────────┘
```

- Only `ACTIVE` domains resolve a public request to their tenant.
- A platform subdomain (`<slug>.<base domain>`) is created `ACTIVE` with its tenant: it needs no verification.
- Custom domains start in `PENDING_VERIFICATION`. Verification arrives with Branding/PRO; until then no command
  changes a domain's status.

### Subscription period (derived from stored dates)

Stored: `plan_code, kind (PAID | TRIAL | COMPLIMENTARY), starts_on, ends_on, grace_days, expiring_window_days,
cancelled_at, cancel_reason`. `kind` is for reporting only and never affects the derived status or access.

```text
cancelled_at <= now                       → CANCELLED
now < starts_on                           → SCHEDULED
now <= ends_on - expiring_window_days     → ACTIVE
now <= ends_on                            → EXPIRING
now <= ends_on + grace_days               → GRACE
otherwise                                 → EXPIRED
```

Renewal = a new period row. History is never overwritten.

Agreed with the owner in the S04-T2a plan, 2026-10-23 (S04 D3, D4):
- Dates are `date`s in the tenant's time zone (settings); `now` above is the tenant's date today, by the app's clock.
  `ends_on` is the last day included; a cancellation takes effect when it is made.
- Non-cancelled periods never share a day (checked under the tenant's lock `tenancy.subscription:<tenantId>`). A row
  never changes except to be cancelled.
- The **current period** is the latest-started period in effect today (`ACTIVE`, `EXPIRING`, or `GRACE` while no later
  non-cancelled period has started: grace only bridges the gap to the next period, #65 R5); with none,
  the latest-started period of any kind (so an expired or cancelled one says why); with none started, the latest
  period (`SCHEDULED`); with no periods, none. A period cancelled by mistake never hides a valid one still in effect
  (refined by the owner in #65 R2). A later non-cancelled period is `SCHEDULED` (the next one).
- Commands (platform, `PLATFORM_ADMIN`): `subscription.assign` (a new period, refused if it overlaps),
  `subscription.renew` (from the day after the latest non-cancelled period ends, or today if that day has passed),
  `subscription.change_plan` (now, to a higher-ranked plan only, while the current period is `ACTIVE` or `EXPIRING`:
  cancels it with "Changed to <plan>" and adds one from today to the same `ends_on`), `subscription.cancel` (reason;
  every period not cancelled or `EXPIRED`: those in effect, an earlier one still in `GRACE` included, and every
  scheduled one).

### Access mode (derived)

| Tenant status | Current subscription | Access mode |
|---|---|---|
| PROVISIONING | any | `FULL` for tenant owners/admins (setup); public site hidden |
| ACTIVE | ACTIVE, EXPIRING, GRACE | `FULL` (banner when EXPIRING/GRACE) |
| ACTIVE | EXPIRED, CANCELLED, SCHEDULED only, none | `READ_ONLY` (live contests may finish; results may be submitted for 24 h) |
| SUSPENDED, ARCHIVED | any | `BLOCKED` |

How (S03-T4b2, D11):
- `PROVISIONING`: `FULL` for a member holding `TENANT_OWNER` or `TENANT_ADMIN` tenant-wide (an `ACTIVE` membership,
  an unrevoked assignment); every other member gets `403 TENANT_PROVISIONING` on every tenant action until the tenant
  is activated.
- `BLOCKED`: every tenant action is `403 TENANT_BLOCKED` except those marked `availableWhenBlocked` in the action
  catalogue, which only `TENANT_OWNER` holds: `tenant.access.read` (`GET /api/admin/v1/tenant/access`), so the owner
  sees the status and the reason for it. Platform actions are not affected (staff restore and unarchive).

How `READ_ONLY` is applied (S04-T2b, D8; plan approved by the owner 2026-10-23):
- Step 1 reads an `ACTIVE` tenant's current period (above) on the tenant's date: `ACTIVE`, `EXPIRING` or `GRACE` is
  `FULL`; `EXPIRED`, `CANCELLED`, a `SCHEDULED` period with none in effect, or no period is `READ_ONLY`. Activating a
  tenant doesn't need a period (D9): until one is assigned it is `READ_ONLY`.
- While `READ_ONLY`, tenant actions marked `availableWhenReadOnly` in the action catalogue pass; every other tenant
  action is `403 TENANT_READ_ONLY`. A startup rule requires the flag on every tenant action whose code ends in
  `.read`. Later writes that must work while read-only (finishing a live contest, submitting its result within 24 h,
  exports) set the flag and check their own window in the handler through `ITenantAccess.GetAccessAsync` (status,
  mode, time zone, current period).
- Nothing is cached and no job runs: the mode is derived on every request, so it changes at midnight in the tenant's
  time zone and a renewal restores writes on the next request. Platform actions and the public site are not affected.

---

## Identity & Access

### Tenant membership (stored)

Added with the owner's approval, 2026-10-20 (S03-T4). A user's membership of a tenant (`identity.tenant_memberships`).

```text
ACTIVE ──suspend (reason)──► SUSPENDED ──restore──► ACTIVE
ACTIVE | SUSPENDED ──remove (reason)──► REMOVED
```

- Only an `ACTIVE` membership passes the `X-Tenant-Id` check and grants its roles' actions. A `SUSPENDED` one keeps
  its roles for when it is restored.
- `REMOVED` is terminal and takes no role changes; a user who rejoins gets a new membership (through an
  invitation), so the old row stays as history. At most one membership per tenant and user is not `REMOVED`.
- A role assignment is granted and revoked, never changed; revoking keeps the row with who and when.
- A tenant keeps at least one active `TENANT_OWNER` (S03 D17): removing, suspending or revoking the last one is
  refused (`409 AUTH_LAST_OWNER`, S03-T4b). Only `ACTIVE` memberships count as owners.

### Invitation (stored; `EXPIRED` derived)

Added with the owner's approval, 2026-10-20 (S03-T4c, D12). An invitation to join a tenant with roles
(`identity.invitations`).

```text
PENDING ──accept──► ACCEPTED (accepted_by, accepted_at, membership_id)
PENDING ──revoke | a new invitation to the same email | the inviter suspended or removed──► REVOKED
PENDING and now ≥ expires_at (7 days after it was made) ⇒ EXPIRED (derived, never stored; final)
```

- Suspending or removing a membership revokes the pending, unexpired invitations that member sent in the tenant, in
  the same save (Q20, owner, S03-T5); the member change's audit entry lists their IDs. Invitations are matched by the
  inviting user, so a platform user who is also a member of the tenant loses the owner invitations they sent from the
  platform console too (#54 R1); the system's are never touched. Expired and accepted ones stay as they are, and
  restoring the member brings none back. An invitation the member sends at the same instant as their suspension can commit after it and stay pending
  (accepted, owner).

- At most one invitation per tenant and email is `PENDING`: a new one revokes the pending one, expired or not.
- An expired invitation can't be accepted or revoked; inviting again makes a new one.
- The token is made when the email is sent; only its SHA-256 is stored, and a resent email's token replaces the
  earlier one. A token works once: accepting ends the `PENDING` state.
- Accepting (S03-T4c2) creates the membership with the invitation's roles, tenant-wide and granted by the inviter, in
  the same save as `ACCEPTED`; a new user gets the invited email, confirmed. An expired invitation is
  `410 AUTH_INVITATION_EXPIRED`, an accepted one `409 AUTH_INVITATION_ALREADY_ACCEPTED`, a revoked one answers like an
  unknown token (`404 AUTH_INVITATION_TOKEN_INVALID`). No one joins an `ARCHIVED` tenant.

---

## Sports Configuration (file metadata in v1, ADR-0006)

```text
Definition / format / ruleset-template version: PUBLISHED → DEPRECATED → ARCHIVED
```
Drafts exist only on branches. Published content is immutable (hash-checked). `DEPRECATED` = not selectable for
new editions; existing editions keep working. `ARCHIVED` = hidden from pickers, still readable.

---

## Registry

| Entity | States |
|---|---|
| Person | `ACTIVE`, `INACTIVE`, `DECEASED`, `MERGED` (+ `merged_into_id`), `ARCHIVED` |
| Athlete / Staff / Official profile | `ACTIVE`, `INACTIVE`, `SUSPENDED`, `RETIRED`, `ARCHIVED` |
| Organisation | `ACTIVE`, `INACTIVE`, `SUSPENDED`, `DISSOLVED`, `ARCHIVED` |
| Team | `ACTIVE`, `INACTIVE`, `SUSPENDED`, `DISBANDED`, `ARCHIVED` |
| Venue | `ACTIVE`, `TEMPORARILY_UNAVAILABLE`, `CLOSED`, `ARCHIVED` |

Merged persons accept no new relationships. Archive never deletes history.

---

## Team Operations

### Player membership / staff assignment

```text
PENDING ──activate──► ACTIVE ◄──reinstate── SUSPENDED
                        │  └──suspend (reason)──►┘
                        └──end (end_reason)──► ENDED
end_reason ∈ RELEASED | TRANSFERRED_OUT | CONTRACT_EXPIRED | RETIRED | LOAN_RETURNED | OTHER
```
Concurrent ACTIVE memberships in different teams are allowed or forbidden by tenant/sport policy (default:
one active membership per sport variant, plus loans).

### Transfer (Phase 7)

```text
DRAFT ──submit──► SUBMITTED ──approve──► APPROVED ──complete──► COMPLETED
                      └──reject (reason)──► REJECTED
DRAFT | SUBMITTED | APPROVED ──cancel (reason)──► CANCELLED
```
Completion ends the source membership and starts the destination membership in one transaction.

---

## Competitions

### Competition

`DRAFT → ACTIVE ⇄ INACTIVE`, `ACTIVE | INACTIVE → ARCHIVED`.

### Edition

```text
DRAFT ──open_registration──► REGISTRATION_OPEN ──close_registration──► REGISTRATION_CLOSED
  │                                                                          │
  └───────────────start (direct entry)──────────────┐                        │
                                                    ▼                        ▼
                                              IN_PROGRESS ◄──────────── start
                                                    │
                       (auto: all contests CLOSED/CANCELLED, or complete)
                                                    ▼
                                               COMPLETED ──close──► CLOSED ──archive──► ARCHIVED
                                                    ▲                   │
                                                    └──reopen (reason)──┘
DRAFT | REGISTRATION_OPEN | REGISTRATION_CLOSED | IN_PROGRESS ──cancel (reason)──► CANCELLED
```
The effective-rules snapshot is frozen when the edition first leaves `DRAFT` or when fixtures are first
generated, whichever comes first (ADR-0006). `IN_PROGRESS` is entered explicitly or automatically when the first
contest goes `LIVE`.

### Registration (competition entry)

```text
DRAFT ──submit──► SUBMITTED ──approve──► APPROVED ──withdraw──► WITHDRAWN
                      │                     └──disqualify (reason)──► DISQUALIFIED
                      ├──reject (reason)──► REJECTED
                      └──waitlist──► WAITLISTED ──approve──► APPROVED / ──withdraw──► WITHDRAWN
(direct entry by a competition manager creates the registration as APPROVED)
```

### Roster (belongs to an approved registration, ADR-0008)

```text
DRAFT ──submit──► SUBMITTED ──approve──► APPROVED ──lock (manual / deadline / first contest)──► LOCKED
                      └──return (notes)──► DRAFT
LOCKED ──reopen (reason)──► REOPENED ──submit──► SUBMITTED
```
Roster entry: `ACTIVE`, `REMOVED` (date + reason), `TRANSFERRED_OUT`. If rules say approval isn't required,
`submit` goes straight to `APPROVED`.

### Eligibility evaluation

Result: `ELIGIBLE`, `INELIGIBLE`, `CONDITIONAL`. Override → `OVERRIDDEN` (reason, by, optional expiry).
Evaluations are recomputed when inputs change; the latest is current, older ones are history.

### Stage structure

`DRAFT → PUBLISHED`; `PUBLISHED → DRAFT` (reopen, reason) only while no contest in that stage has started.

---

## Match Operations

### Fixture publication (per contest)

`DRAFT → PUBLISHED`. After publication, time/venue changes only via `fixture.reschedule` (reason), which adds a
schedule version and notifies followers. "Rescheduled" = schedule version > 1 (derived).

### Contest status (stored) — sport phase is separate

```text
NOT_STARTED ──start (readiness passed or overridden)──► LIVE
NOT_STARTED ──delay (reason)──► DELAYED ──start──► LIVE
NOT_STARTED | DELAYED ──postpone (reason)──► POSTPONED ──reschedule──► NOT_STARTED
NOT_STARTED | DELAYED | POSTPONED ──cancel (reason)──► CANCELLED

LIVE ──interrupt (reason)──► INTERRUPTED ──resume──► LIVE
INTERRUPTED ──suspend (reason)──► SUSPENDED ──resume──► LIVE
LIVE | INTERRUPTED | SUSPENDED ──abandon (reason)──► ABANDONED

LIVE ──end──► ENDED ──(result approved)──► CLOSED
ENDED ──reopen (reason, e.g. ended by mistake)──► LIVE
CLOSED ──(result reopened for correction)──► ENDED
```

Derived display states (not stored): **Starting soon** = `NOT_STARTED` and scheduled start within the tenant's
window (default 30 min); **Late** = `NOT_STARTED` past start + tolerance. Walkover/forfeit are result methods:
the contest goes to `ENDED → CLOSED` with no timeline. An abandoned contest's outcome (awarded, void + replay) is
set by the result workflow; a replay is a new contest linked by `replay_of_contest_id`.

### Sport phase (derived from PERIOD events, stored for convenience)

`current_period_code` + `period_occurrence` + `phase_kind` ∈ `PLAY | BREAK | SHOOTOUT`. Phase codes come from
the sport definition (e.g. football `FIRST_HALF`, `HALF_TIME`, `SECOND_HALF`; basketball `Q1`…`Q4`, `OVERTIME`;
cricket `FIRST_INNINGS`, `INNINGS_BREAK`, `SECOND_INNINGS`, `SUPER_OVER`). Between regulation and extra periods
the phase is a `BREAK` such as `AWAITING_EXTRA_TIME`. Never combine status and phase (`LIVE_SECOND_HALF` is
forbidden).

### Lineup

```text
DRAFT ──submit──► SUBMITTED ──approve──► APPROVED ──lock (at start)──► LOCKED
                      └──return──► DRAFT
LOCKED: late changes before start via lineup.late_change (reason) — history kept
```
If rules say lineup approval isn't required, `submit` goes straight to `APPROVED`.

### Timeline event (derived effective status — ADR-0002)

`ACTIVE` · `SUPERSEDED` (a later event has `supersedes_event_id` = this) · `REVERSED` (a later event has
`reverses_event_id` = this). Offline clients additionally show `PENDING`, `SYNCING`, `REJECTED` locally.

### Result version

```text
DRAFT ──submit──► SUBMITTED ──approve──► APPROVED ──(newer version approved)──► SUPERSEDED
                      └──reject (reason)──► REJECTED   (a new DRAFT is created to fix and resubmit)
APPROVED ──reopen (reason)──► new DRAFT version (the APPROVED one stays official until the new one is approved)
```
Contest result holds `official_version_id`, `pending_version_id`, `dispute_open` (disputes: later slice).
Result methods: `REGULATION`, `EXTRA_TIME`, `OVERTIME`, `SUDDEN_DEATH`, `SHOOTOUT`, `SUPER_OVER`, `WALKOVER`,
`FORFEIT`, `AWARDED`, `NO_RESULT`, `ABANDONED_VOID`.

---

## Data Exchange, Insights, Engagement, Branding

| Machine | States |
|---|---|
| Import job | `CREATED → UPLOADED → MAPPED → VALIDATED → DRY_RUN_COMPLETE → EXECUTING → COMPLETED | COMPLETED_WITH_ERRORS | FAILED`; `CANCELLED` before executing |
| Report run | `QUEUED → RUNNING → COMPLETED | FAILED`; `COMPLETED → EXPIRED` |
| Notification delivery | `PENDING → SENT | FAILED | SUPPRESSED` |
| Brand version | `DRAFT → PUBLISHED → SUPERSEDED` |
| Sponsor campaign | `DRAFT → ACTIVE ⇄ PAUSED → ENDED` (active window also bounded by dates) |

---

## Platform

### Outbox message (stored, in each module's `outbox_messages`)

```text
PENDING ──every consumer handled it (or it has none)──► PROCESSED
PENDING ──attempt failed, attempts < max──► PENDING   (next_attempt_at = now + backoff)
PENDING ──attempt failed, attempts = max──► FAILED
FAILED  ──re-drive (platform.outbox.redrive, reason)──► PENDING   (attempts reset)
```

Delivery state, not a business lifecycle: no status history table. Per aggregate, messages move in `sequence`
order: one stays `PENDING` while an earlier message of its aggregate is not `PROCESSED`. The outbox processor makes the automatic
transitions; the re-drive is a platform action and is audited (docs/architecture/events-and-jobs.md).

---

## Match-day flow (how the machines meet)

```text
Fixture PUBLISHED ─► officials assigned ─► lineups APPROVED ─► readiness passes
   ─► contest LIVE (lineups LOCKED) ─► PERIOD events … scoring events … ─► contest ENDED
   ─► result DRAFT (proposed from score fold) ─► SUBMITTED ─► APPROVED ─► contest CLOSED
   ─► ResultApproved ─► standings recalculated ─► public pages updated ─► notifications
```

Readiness checks are configured by rules (defaults per sport): lineups approved, minimum officials assigned,
venue set, both participants confirmed. A competition manager can override a failed check with a reason.
