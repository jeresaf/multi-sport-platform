# Authorization, actions, roles and entitlements

Decision: ADR-0005. This document defines the vocabulary and the first catalogue.

## One catalogue: action codes

An **action code** names a business operation: lowercase, dotted, `<resource>.<verb>` or
`<resource>.<sub>.<verb>`. Examples: `person.create`, `contest.start`, `timeline.event.record`,
`result.approve`.

Every state-changing endpoint/handler declares exactly one action code. Admin read endpoints declare a read
action (e.g. `person.read`, `person.read_private`). Public read endpoints declare none.

Each action is declared once in code (e.g. a static registry class per module with attributes/metadata):

| Field | Meaning |
|---|---|
| `code` | `person.create` |
| `module` | Registry |
| `description` | Human description shown in role editors and docs |
| `scope` | `PLATFORM`, `TENANT` or `ACCOUNT` (the caller's own account, S03 D2) |
| `scopable_by` | Which assignment scopes can grant it: `TENANT`, `COMPETITION`, `TEAM`, `CONTEST` |
| `audit` | `NONE`, `REQUIRED`, `REQUIRED_WITH_REASON` |
| `entitlement` | Optional entitlement code that must be enabled (or quota checked) |
| `idempotent` | Whether an `Idempotency-Key` is required |
| `allows_anonymous` | Account actions only: a caller who is not signed in may perform it (sign-in, password reset, invitations) |
| `since` | Slice/version introduced |

The catalogue is exported to `docs/generated/actions.md` and `contracts/actions.json` by the CLI; a CI test
fails if the export is stale or if any mapped endpoint lacks an action. This is how the owner's requirement —
"each action should be documented and added to the subscription model … keep track of all system actions as we
expand the system" — is met automatically.

## Decision formula

For every admin request:

```text
1. Access mode   (Tenancy)     FULL / READ_ONLY / BLOCKED  → TENANT_READ_ONLY / TENANT_BLOCKED
2. Entitlement   (Tenancy)     if the action names one     → ENTITLEMENT_REQUIRED / ENTITLEMENT_QUOTA_EXCEEDED
3. Permission    (Identity)    scoped role assignment      → AUTH_FORBIDDEN
4. Domain rules  (owning module, inside the handler)       → e.g. CONTEST_NOT_READY
```

Steps 1–3 run in the Host's authorization middleware (after routing, before the request body is bound) using the
endpoint's declared action; quota checks that need a count run inside the handler via `IEntitlements.CheckQuota` in
the same transaction as the create.

**Pipeline as built (S02-T3a, D8; S03-T2a):** forwarded host (S02-T3b, trusted proxies only) → request logging and
correlation → routing → **actor** (authentication with the session cookie, then `ActorContext` from the principal's
`sub`; the anonymous actor otherwise, including a principal without a GUID `sub`) → **audit** → **CSRF** (a
state-changing request to `/api/account`, `/api/admin` or `/api/platform` without `X-CSRF: 1` → `403
AUTH_CSRF_REQUIRED`) → **tenant resolution** (docs/architecture/multi-tenancy.md) → **authorization** (401, then step
1, then the evaluator) → endpoint. Audit runs before
tenant resolution and authorization, so a refused tenant (`AUTH_TENANT_FORBIDDEN`, `TENANT_BLOCKED`) or a denied
action on an audited endpoint is audited as `DENIED`, with the tenant when one was resolved
(docs/architecture/events-and-jobs.md, "Audit as built"). `SportsPlatformApp.UsePipeline` is the single place the
order is written.

**401 vs 403 (S03 D4).** A caller who is not signed in gets `401 AUTH_UNAUTHENTICATED` on any action that doesn't
allow anonymous callers: tenant resolution answers it before looking at `X-Tenant-Id`, authorization for every other
action. `403` means signed in but not allowed (`AUTH_FORBIDDEN`, `AUTH_TENANT_FORBIDDEN`, access mode) or a missing
CSRF header. Public endpoints are anonymous and need neither.

**Account actions (S03 D1, D2).** Under `/api/account/v1` only, never public: the caller's own account (sign-in and
sign-out, `me`, passwords, invitation acceptance). The evaluator (`IdentityAuthorizationEvaluator`, D10) allows them
to every signed-in user; those marked `allowsAnonymous` also to anonymous callers. They have no tenant and no step 1.

**Platform actions (S03-T3).** The evaluator allows a platform action when the user holds an unrevoked platform role
(`identity.platform_role_assignments`) that includes it, read per request (no claims cache). Platform roles grant no
tenant actions.

**Tenant actions (S03-T4a, D10).** The evaluator allows a tenant action when the user has an `ACTIVE` membership
(`identity.tenant_memberships`) in the request's tenant and an unrevoked role assignment (`identity.role_assignments`)
such that: the role includes the action; the assignment's scope type is in the action's `scopable_by` (so a
competition-scoped `COMPETITION_MANAGER` gets no `TENANT`-only action, #45 N1); and the scope is the tenant
(`TENANT`, no scope ID) or appears in the scope chain of the request's resource (`AuthorizationRequest.Scope`): the
resource itself plus what its module's `IScopeChainProvider` returns above it (no module provides one before S06;
`ScopeChains` holds them, one per resource type, checked when the Host starts).
Memberships and roles are read on every request under the request's tenant (RLS), so a removed or suspended
membership, or a revoked last role, is refused on the next request. A request whose tenant isn't the scope's tenant
is refused (fail closed). Only users hold roles; the system and anonymous actors get no tenant action.

**Members and owners (S03-T4b).** `/api/admin/v1/members` lists the tenant's members and changes them
(`membership.read`, `membership.suspend|restore|remove`, `role.assign|revoke`; owner and admin, and `VIEWER` reads).
Two rules that the handlers check after authorization:
- Only an active `TENANT_OWNER` grants or revokes `TENANT_OWNER`, or suspends, restores or removes an owner; anyone
  else gets `403 AUTH_OWNER_REQUIRED` (owner, S03-T4). The system actor (seeds) passes.
- A tenant keeps at least one active owner (D17): suspending, removing or revoking the owner role of the only `ACTIVE`
  membership holding an unrevoked tenant-wide `TENANT_OWNER` is `409 AUTH_LAST_OWNER`. Suspended owners don't count.
  The change holds a per-tenant transaction lock (`pg_advisory_xact_lock`) while it counts the other owners and saves,
  so two owners removing each other at once can't both succeed.
In S03 roles are assigned tenant-wide only; other scopes are `400 AUTH_ROLE_NOT_ASSIGNABLE` until the modules that own
competitions, teams and contests can confirm the resource exists (owner, S03-T4b).

**Invitations (S03-T4c, D12).** `/api/admin/v1/invitations` invites a person by email with tenant-wide roles
(`membership.invite`), lists and reads invitations (`membership.read`) and revokes them (`invitation.revoke`); owner
and admin invite and revoke, `VIEWER` reads. Only an owner (or the system actor) invites with `TENANT_OWNER`, or
revokes or replaces a pending invitation that carries it, the platform's included (`AUTH_OWNER_REQUIRED`). The platform console invites a tenant's owner with `tenant.owner.invite` (`PLATFORM_ADMIN`,
`POST /api/platform/v1/tenants/{tenantId}/owner-invitations`, under the route's tenant). A person who is already a
member (active or suspended) is `409 AUTH_ALREADY_MEMBER`; a new invitation to an email with a pending one revokes the
older one. Suspending or removing a member revokes the pending, unexpired invitations they sent, in the same save (Q20,
S03-T5b; docs/domain/state-machines.md).

**Authorization matrix (S03-T5b).** `AuthorizationMatrixTests` reads the app's endpoints and, for every admin and
platform endpoint with an action, checks that a user holding every other role of the action's scope gets
`403 AUTH_FORBIDDEN` from the evaluator, and that a user holding a granting role passes authorization. A new endpoint is
covered without editing the test; one whose action no role grants, or with a route parameter the test can't fill,
fails it.

**Ending sessions (S03 D5, T2b).** Sessions are not stored on the server; a password change or reset changes the
user's security stamp, and every other session fails its next stamp check (at most 5 minutes later) with `401`. A
change renews the caller's own cookie; a reset ends every session. Wrong current passwords on a change count toward
the sign-in lockout (owner, T2b); a reset clears the lockout.

**Step 1 as built:** for a TENANT-scope action, `ITenantAccess.CheckAdminAccessAsync` on the resolved tenant:
`ACTIVE` → its current subscription period decides (S04-T2b, D8): `ACTIVE`, `EXPIRING` or `GRACE` → `FULL`; expired,
cancelled, scheduled only or none → `READ_ONLY`, where actions marked `availableWhenReadOnly` (every tenant read, a
startup rule) pass and every other tenant action is `TENANT_READ_ONLY` (state-machines.md, "How `READ_ONLY` is
applied"); `PROVISIONING` → `FULL` when `ITenantSetupAccess` (Identity) says the actor
holds `TENANT_OWNER` or `TENANT_ADMIN` tenant-wide, else `TENANT_PROVISIONING`; `SUSPENDED`, `ARCHIVED` → `BLOCKED` →
`TENANT_BLOCKED`, except for actions marked `availableWhenBlocked`, which pass to the evaluator (S03 D11). Only
`TENANT_OWNER` holds such an action (`tenant.access.read`), so an admin asking for it gets `AUTH_FORBIDDEN`. No resolved
tenant fails closed with `AUTH_TENANT_FORBIDDEN`. Platform actions have no step 1: platform staff work on blocked
tenants (restore, unarchive).

**Step 2 as built (S04-T3a2, D10):** after step 1 and before the evaluator, for a TENANT-scope action that names an
entitlement (`ActionDefinition.Entitlement`, a code the startup check `EntitlementRules` requires to be declared): a
`BOOLEAN` or `LEVEL` entitlement → `IEntitlements.CanUseAsync` (on, or any level; a handler that needs a particular
level checks it) or `403 ENTITLEMENT_REQUIRED`, whose `detail` names the entitlement; a `QUOTA` passes, and the handler
that creates the item counts it (`CheckQuotaAsync`, D12). So refusals come in the order `TENANT_*` →
`ENTITLEMENT_REQUIRED` → `AUTH_FORBIDDEN`: a permission without the entitlement and an entitlement without the
permission are both refused. A denied step 2 is audited like any denial. Platform and account actions have no step 2.
OpenAPI lists `ENTITLEMENT_REQUIRED` on the endpoints whose action names a boolean or level entitlement (none before
S05). Step 3 is `IAuthorizationEvaluator` (Identity).

## Roles and scopes

A **role** = set of action codes. A **role assignment** = (membership, role, scope type, scope id).

| Scope type | Example | Grants actions on |
|---|---|---|
| `TENANT` | Tenant admin | everything in the tenant the role allows |
| `COMPETITION` | Competition manager for "Metro Football League" | that competition, its editions, registrations, rosters, fixtures, contests |
| `TEAM` | Team manager for "Ntinda Swifts FC Senior Men" | that team's profile, memberships, staff, its rosters and lineups |
| `CONTEST` | Scorer for one match | that contest's live operations |

Owning modules implement `IScopeChainProvider` in their Contracts: given a resource (e.g. contest ID) they return
its chain (`contest → edition → competition → tenant`, and the participating team IDs). The evaluator grants an
action if any assignment's scope appears in the chain and the role includes the action.

### System roles (defined in code; tenant-custom roles later)

**As built (S03-T3, D9):** Identity's `IdentityRoles` defines each role: its code, the scopes it can be assigned at
("typical scope" below) and its action codes. The Host refuses to start if a role grants an undeclared action, an
action of the other scope (tenant roles only `TENANT` actions, platform roles only `PLATFORM` actions), or a tenant
action that none of the role's assignment scopes can grant (an action's `scopable_by`). Each slice that adds actions
adds them to the roles. `export-catalogues` writes the result to `docs/generated/roles.md`, the authority on who can
do what; the table below is the intent. Owner (S03-T3): every role assigned tenant-wide reads the tenant settings,
only `TENANT_OWNER` and `TENANT_ADMIN` change them; `TEAM_MANAGER` (team scope only) gets no tenant-wide actions.

| Role | Typical scope | Includes (summary) |
|---|---|---|
| `TENANT_OWNER` | TENANT | all tenant actions incl. members, roles, subscription view, tenant settings |
| `TENANT_ADMIN` | TENANT | all tenant actions except ownership transfer |
| `REGISTRAR` | TENANT | people, organisations, teams, venues: create/update, duplicate review; no merges |
| `COMPETITION_MANAGER` | TENANT / COMPETITION | competitions, editions, registrations, rosters approval, stages, fixtures, officials, contest admin, standings recalculation |
| `RESULTS_OFFICER` | TENANT / COMPETITION | result approve/reject/reopen/correct, progression overrides (with reason) |
| `SCORER` | TENANT / COMPETITION / CONTEST | contest check-in/start/periods/clock, timeline record/correct (during and shortly after the contest), result submit |
| `TEAM_MANAGER` | TEAM | team profile, memberships, staff assignments, roster draft/submit, lineup draft/submit, registration create/submit |
| `DATA_ENTRY` | TENANT / COMPETITION | historical results entry, import dry runs (execution needs COMPETITION_MANAGER or TENANT_ADMIN) |
| `VIEWER` | TENANT | read-only admin |

Platform roles: `PLATFORM_ADMIN` (tenants, plans, subscriptions, platform config view), `PLATFORM_SUPPORT`
(read-only platform views, audited). As built: `PLATFORM_ADMIN` holds every platform action; `PLATFORM_SUPPORT` holds
the platform `*.read` actions. Platform roles are granted through the CLI in S03 (`create-platform-admin`).

Rules: merges of people (`person.merge`) and entitlement overrides require explicit roles; reasons are
mandatory where the action says `REQUIRED_WITH_REASON`.

## Entitlements

An **entitlement** is a tenant-level commercial capability or limit, defined in code (like actions) and valued
per plan, with per-tenant overrides.

| Type | Example |
|---|---|
| `BOOLEAN` | `LIVE_SCORING = true` |
| `QUOTA` (integer) | `MAX_ACTIVE_EDITIONS = 5` |
| `LEVEL` (enum) | `REPORTS = BASIC | ADVANCED` |
| `LIST` | `ALLOWED_SPORTS = [FOOTBALL_ASSOCIATION, ...]` (optional; usually all sports allowed) |

Effective value = platform default → plan value → tenant override (→ access mode). Cached per tenant; cache
invalidated on plan/override/subscription change.

**Overrides as built (S04-T3a1, D5).** `tenancy.tenant_entitlement_overrides` (tenant-owned, RLS): an entitlement
code, a value of its type (`{ "enabled" }`, `{ "limit" }`, `{ "level" }`, as plans store them), a reason, an optional
last day (`effective_until`, the tenant's date, included) and who removed it, when and why. At most one unremoved
override per tenant and entitlement (a partial unique index); granting a second one removes the first ("Replaced by a
new override") in the same save, so the history is kept. `entitlement.override` and `entitlement.override.remove`
(`PLATFORM_ADMIN`, audit `REQUIRED_WITH_REASON`) run under the route's tenant
(`/api/platform/v1/tenants/{tenantId}/entitlement-overrides`) and hold `tenancy.entitlement_override:<tenantId>`;
`GET …/subscription` lists them with their reasons and history. Reasons are platform-only: tenants will see the value,
never the reason.

**Effective values as built (S04-T3a2, D6, D7).** `IEntitlements` (Tenancy Contracts), for the scope's tenant:
`GetAsync(code)` → value and source (`DEFAULT`, `PLAN`, `OVERRIDE`, with the current period's plan code),
`CanUseAsync`, `GetLimitAsync` (null = unlimited) and `CheckQuotaAsync(code, used)`. Effective value = the
definition's default (off, 0, no level) → the current period's plan value → an unremoved override in effect on the
tenant's date. The plan is the current period's whatever its status (owner, plan 2026-10-29): step 1 already refuses
writes when the period isn't in effect, and reads keep their plan's gates; an override applies with or without a
period. A stored value that isn't of the entitlement's type is ignored. `CheckQuotaAsync` takes the count before the
create: `used >= limit` is `403 ENTITLEMENT_QUOTA_EXCEEDED`; otherwise `QuotaCheck { Limit, Used, Warning }`, with the
warning when the new item reaches 80 % of the limit. Step 1 and `IEntitlements` read the tenant's time zone, periods
and unremoved overrides through Tenancy's `TenantAccessCache` (60 s; cleared after every subscription and override
command and every time-zone change commits) and the plans' values until restart (configuration, changed by a reviewed
seed); "today" and the statuses are derived on every call. Other API instances catch up within 60 s. The platform's
`GET …/subscription` lists every declared entitlement's effective value with its source.

**Quotas and usage as built (S04-T3b1, D12, D13, D15).** The module that owns the items counts them: it implements
`IQuotaUsage` (Tenancy Contracts, `CountAsync` for the scope's tenant) and registers it with
`AddQuotaUsage<TCounter>(code)`, a singleton `QuotaUsageRegistration` that the Host checks at startup (a declared
`QUOTA` entitlement, at most one counter per code) without creating a counter. Identity's `StaffUsers` counts
`MAX_STAFF_USERS`: memberships that aren't removed (`ACTIVE`, `SUSPENDED`) plus pending, unexpired invitations to
emails that aren't members. `membership.invite` holds `identity.staff_users:<tenantId>` while it counts (leaving out a
pending invitation to the same email, which the new one replaces), checks `CheckQuotaAsync` and saves; at the limit it
is `403 ENTITLEMENT_QUOTA_EXCEEDED` with the `detail` "Your plan allows N staff users." The platform's
`tenant.owner.invite` takes a seat but is never refused; accepting is not checked again. Usage is counted on every
read, never cached: `GET /api/admin/v1/tenant/subscription` (`tenant.subscription.read`: `TENANT_OWNER`,
`TENANT_ADMIN`, `VIEWER`; available while read-only; audit `NONE`) and the platform's `GET …/subscription` list every
quota as `{ code, measured, used, limit, warning }` (`warning` from 80 % of the limit, e.g. 4 of 5; `measured: false`
and `used: null` while no module counts it). The tenant's read also has the current and next periods (plan code and
name, kind, status, days) and every entitlement's name, type, unit, levels, value and source; never an override or
cancel reason.

### Entitlements and plan values (adopted 2026-10-17 — open question Q2)

Plain-language plan descriptions, indicative prices and the research behind them: docs/product/pricing-and-plans.md.
This table is the source of truth for the values seeded in S04. As built (S04-T1): Tenancy declares the values in
code (`TenancyPlans`); `tenancy.configuration.plans` seeds them into `tenancy.plans` and `tenancy.plan_entitlements`;
tests compare this table with the code and the seeded rows with the code. Platform staff read them at
`GET /api/platform/v1/plans` (`plan.read`).

| Entitlement | Type | Gates (examples) | STANDARD | PRO |
|---|---|---|---|---|
| `MAX_ENABLED_SPORTS` | QUOTA | `tenant.sport.enable`. Counts distinct **sports**, not variants: `RUGBY_UNION_15` + `RUGBY_UNION_7` = 1 | 3 | unlimited |
| `MAX_ACTIVE_EDITIONS` | QUOTA | `edition.create`; counts editions in `DRAFT`, `REGISTRATION_OPEN`, `REGISTRATION_CLOSED` or `IN_PROGRESS` | 8 | 60 |
| `MAX_TEAMS` | QUOTA | `team.create`; active teams | 120 | 2,000 |
| `MAX_STAFF_USERS` | QUOTA | `membership.invite`; users with a tenant membership (admins, managers, scorers, officials with logins). Public accounts don't count | 100 | 1,000 |
| `STORAGE_GB` | QUOTA | media upload (ADR-0014) | 10 | 100 |
| `LIVE_SCORING` | BOOLEAN | `contest.start`, `timeline.event.record` | ✓ | ✓ |
| `OFFLINE_SCORING` | BOOLEAN | mobile offline scorer (Phase 8) | ✓ | ✓ |
| `PUBLIC_SITE` | BOOLEAN | public site on `<tenant-slug>.<platform-domain>` | ✓ | ✓ |
| `HISTORICAL_RESULTS` | BOOLEAN | `contest.record_historical_result` (past results entered by hand) | ✓ | ✓ |
| `BRANDING` | LEVEL | `brand.publish` | `BASIC`: logo, primary + secondary colour, "Powered by" footer | `FULL`: brand versions, images, footer optional |
| `CUSTOM_DOMAIN` | BOOLEAN | `tenant.domain.add` (custom domain) | – | ✓ |
| `SPONSORSHIP` | LEVEL | `sponsor.*`, `campaign.*`, ad slots | `BASIC`: up to 5 sponsor logos in a site-wide strip | `FULL`: campaigns, placements, scheduling, impression reports |
| `BULK_IMPORT` | LEVEL | `import.*` | `REGISTRY`: people, teams, venues from templates | `FULL`: plus historical results and timelines |
| `REPORTS` | LEVEL | report formats and scheduling | `BASIC`: CSV | `ADVANCED`: XLSX/PDF, scheduled |
| `TRANSFERS` | BOOLEAN | `transfer.*` | – | ✓ |
| `PUBLIC_NOTIFICATIONS` | BOOLEAN | email/push alerts to the public (S21); staff notifications are never gated | – | ✓ |
| `API_ACCESS` | BOOLEAN | tenant API keys | – | ✓ |

- **Event pass** = a STANDARD subscription period of up to 45 days with the override `MAX_ACTIVE_EDITIONS = 1`.
- **Enterprise** (white-label mobile apps, national registry, SSO, dedicated hosting or region, SLA) = PRO plus
  per-tenant overrides until a third plan is justified.
- Subscription periods carry a `kind` — `PAID`, `TRIAL` or `COMPLIMENTARY` — for reporting only; it never changes
  access (docs/domain/state-machines.md).
- Quotas only block creating **new** items. A downgrade never hides or deletes existing data.

## UI behaviour

- The admin app fetches `GET /api/admin/v1/me/capabilities` (actions allowed for the current user/tenant,
  optionally per scope) and hides or disables controls accordingly, showing why (e.g. "Requires PRO plan").
- As built (S03-T5): `GET /api/admin/v1/me/capabilities` (`tenant.capabilities.read`, every role assignable
  tenant-wide) answers `{ actions, scope }`: the tenant actions the caller's roles grant tenant-wide, plus, with
  `scopeType` (`COMPETITION`, `TEAM`, `CONTEST`) and `scopeId`, those granted on that resource or the scopes above it.
  `GET /api/platform/v1/me/capabilities` (`platform.capabilities.read`, both platform roles) lists the platform
  actions. Both decide with the evaluator's own rule, so the list is exactly what the evaluator allows (step 3); the
  tenant endpoint answers only when step 1 lets it through (in S03 that means `FULL`; otherwise its 403 says why). S04
  adds entitlements, `READ_ONLY` (where this read still answers, so writes must be left out or marked) and the reason
  for each unavailable action. A member with no tenant-wide role can't read them until requests carry resource
  scopes (S06): `TEAM_MANAGER` (team only), or any role assigned only at a competition or contest, gets `403`, even
  for its own resource (#52 R1).
- The server remains the only enforcement point.

## Audit and analytics reuse

Audit entries and product analytics events record the action code, so "who did what" and "which features are
used" share the same vocabulary.
