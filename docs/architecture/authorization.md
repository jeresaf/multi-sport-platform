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
| `scope` | `PLATFORM` or `TENANT` |
| `scopable_by` | Which assignment scopes can grant it: `TENANT`, `COMPETITION`, `TEAM`, `CONTEST` |
| `audit` | `NONE`, `REQUIRED`, `REQUIRED_WITH_REASON` |
| `entitlement` | Optional entitlement code that must be enabled (or quota checked) |
| `idempotent` | Whether an `Idempotency-Key` is required |
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

Steps 1–3 run in an endpoint filter using the declared action; quota checks that need a count run inside the
handler via `IEntitlements.CheckQuota` in the same transaction as the create.

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

### System roles (seeded; tenant-custom roles later)

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
(read-only platform views, audited).

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

### Entitlements and plan values (adopted 2026-10-17 — open question Q2)

Plain-language plan descriptions, indicative prices and the research behind them: docs/product/pricing-and-plans.md.
This table is the source of truth for the values seeded in S04.

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
- The server remains the only enforcement point.

## Audit and analytics reuse

Audit entries and product analytics events record the action code, so "who did what" and "which features are
used" share the same vocabulary.
