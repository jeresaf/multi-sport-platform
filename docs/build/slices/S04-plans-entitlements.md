# S04 — Plans, subscriptions and entitlements

> Rewritten by the S03 slice review (2026-10-20) to match what S01–S03 actually built. It replaces the kit's version.
> Changes from the kit's version: the decisions below (D1–D20) are written out; entitlements are all defined in
> Tenancy now (it owns them, modules.md), so the plan table seeds in full although most entitlements gate nothing yet;
> subscription periods and overrides are tenant-owned under RLS; `READ_ONLY` uses a new action flag
> (`availableWhenReadOnly`) like S03's `availableWhenBlocked`; quotas are counted by the module that owns the items,
> under a per-tenant lock, with no usage-counter table (D12); a quota refusal is `403`, not `429` (D11); the
> capabilities list keeps `actions` and adds `unavailable` with reasons for what the roles grant but the tenant can't
> use now (D14); the demo works without a PRO-only action, because none exists before S05/S22 (D20, Q25); the S03
> follow-ups assigned to S04 are folded into the tasks ("Carried over from S03").

**Goal:** tenants subscribe to plans; access mode and entitlements are enforced with permissions on every admin
request (steps 1–3 of the decision formula); platform staff record subscriptions and overrides in the console; tenant
users see their plan, its limits and why something is unavailable.
**Modules touched:** Tenancy & Commercial (entitlements, plans, subscriptions, overrides, access mode), Identity
(staff-user quota, capabilities), Host (step 2, startup rules, catalogue export), admin web.
**Depends on:** S03.
**Demo:** a platform admin opens **Plans** in the console (STANDARD and PRO side by side) → opens KMSA (seeded on
STANDARD, `COMPLIMENTARY`) and sets the override `MAX_STAFF_USERS` to one more than KMSA uses (5 when its four seeded
people are invited), with a reason → KMSA's owner sees "4 of 5 staff users" with the 80 % warning, invites one more
person, and the next invitation is refused with "Your plan allows 5 staff users"
(`403 ENTITLEMENT_QUOTA_EXCEEDED`) → the admin removes the override and
the invitation goes through → the admin cancels ELSSC's subscription with a reason → ELSSC's owner sees the read-only
banner, the settings form is disabled and a PATCH through the API is `403 TENANT_READ_ONLY` → the admin assigns a new
period → full access returns. The audit trail shows every platform change with its reason. Expiry, grace and
renewal are proven by table-driven tests on a fake clock (no jobs, so a demo can't wait for a date).

Read first: docs/architecture/authorization.md ("Decision formula", "Entitlements", "UI behaviour"),
docs/domain/state-machines.md ("Subscription period", "Access mode"), docs/product/pricing-and-plans.md,
docs/architecture/multi-tenancy.md ("Tenant access mode"), docs/product/pilot-customer.md (KMSA's and ELSSC's plans),
docs/product/ux/admin-app.md (console and tenant settings). Background only: the pack's spec 07
(docs/reference/chatgpt-pack/specifications/07-subscription-entitlements-system-spec.md, its §30 acceptance list).

## What S01–S03 give you (use it, don't rebuild it)

- **The pipeline** (`SportsPlatformApp.UsePipeline`): … actor → audit → CSRF → tenant resolution → authorization
  (`Host/Authorization/ActionAuthorizationMiddleware`): `401` for an anonymous caller, then **step 1** for a tenant
  action (`ITenantAccess.CheckAdminAccessAsync`, Tenancy's `Features/Tenants/TenantDirectory`: `ACTIVE` → pass;
  `PROVISIONING` → pass if `ITenantSetupAccess` (Identity) says owner or admin, else `TENANT_PROVISIONING`; `BLOCKED` →
  `TENANT_BLOCKED` unless the action is `availableWhenBlocked`; the `ReadOnly` branch throws `NotSupportedException`
  "arrives with subscriptions (S04)"), then **step 3** (`IAuthorizationEvaluator`, Identity's
  `IdentityAuthorizationEvaluator`). There is no step 2 yet.
- **Actions** (`BuildingBlocks/Actions/ActionDefinition`): `Entitlement` (string, optional) already exists, is exported
  as the "Entitlement" column of `docs/generated/actions.md` and `contracts/actions.json`, and is set on no action.
  `AvailableWhenBlocked` is the model for D8's flag. Startup rules live in `Host/Authorization` (`EndpointActionRules`,
  `RoleRules`, `ScopeChainRules`); catalogues in `Host/Catalogues` (`ActionCatalogue`, `RoleCatalogueExport`, …),
  written by `export-catalogues` and checked by the `Contracts` CI job. `IActionCatalogue` (BuildingBlocks) lists
  every action at runtime.
- **Roles** (Identity `Domain/Roles/IdentityRoles`): explicit action lists. `PLATFORM_ADMIN` lists every platform
  action, `PLATFORM_SUPPORT` the platform reads; `TENANT_OWNER`'s description already says "subscription view". New
  actions must be added there, or the matrix test fails (an action no role grants).
- **Capabilities** (Identity `Features/Capabilities`): `GET /api/admin/v1/me/capabilities` → `{ actions, scope }`,
  step 3 only (`Permissions`, `HeldRoles.Allows`); answers only when step 1 passes. Also
  `GET /api/platform/v1/me/capabilities`.
- **Tenant access read** (Tenancy `Features/Access/GetTenantAccess`): `GET /api/admin/v1/tenant/access`
  (`tenant.access.read`, `TENANT_OWNER` only, available when blocked): status, access mode, reason, since.
- **Members and invitations** (Identity `Features/Members`, `Features/Invitations`): `MemberChanges` holds a
  per-tenant `pg_advisory_xact_lock` while it counts owners; `InviteHandler` serves `membership.invite` and the
  platform's `tenant.owner.invite`; accepting creates the membership (`AcceptInvitationHandler`).
- **Platform routes under a tenant:** `.RunsUnderRouteTenant()` (`/api/platform/v1/tenants/{tenantId}/…`), audited
  with the target tenant (e.g. `tenant.owner.invite`).
- **Tables and tests:** `ITenantOwned` + `EnableTenantRls` + isolation samples; global tables with
  `ReadOnlyForRuntimeRoles` and `TableAccessPolicies`; `RowVersion`/`If-Match`; `AuthorizationMatrixTests` (every admin
  and platform endpoint, discovered); `TenantTestData.SaveTenantAsync`; `PostgresFixture` keeps the reference tables
  across resets and seeds `REFERENCE` once; DEMO seeders `tenancy.demo.tenants` and `identity.demo.invitations`.
  `CONFIGURATION` seeds exist as a category (owner context, every environment) but no seeder uses it yet.
- **Admin web:** shell with capability-filtered navigation (`lib/navigation.ts`), `useTenantWorkspace().can()`,
  `ActionDialog` (reasons), `ConflictAlert`, `describeProblem` from `contracts/errors.json`, the blocked-tenant page,
  the console's tenant detail (subscription says "not available yet"), Playwright against the real API with Mailpit.

## Decisions (reviewer defaults; confirm or change in each task's plan)

- **D1 — Entitlements are defined in code, in Tenancy.** `TenancyEntitlements` declares all 17 entitlements of
  authorization.md's table as `EntitlementDefinition`s: code, name, description, type (`BOOLEAN`, `QUOTA`, `LEVEL`;
  `LIST` waits for its first use), unit (`sports`, `editions`, `teams`, `users`, `GB`), levels in order for `LEVEL`
  (e.g. `BASIC < FULL`), the default value, the module that will enforce it (text, e.g. "Registry") and `since`.
  Tenancy owns entitlement definitions (modules.md), so modules that don't exist yet need nothing. Actions name their
  entitlement by code string, as roles name actions (Identity can't depend on later modules). The Host refuses to
  start if an action names an unknown entitlement or a non-tenant action names one (`EntitlementRules`).
  `export-catalogues` writes `docs/generated/entitlements.md` and `contracts/entitlements.json`: each entitlement with
  its type, unit, STANDARD and PRO values and the actions that name it.
- **D2 — Plans are configuration data.** `tenancy.plans` (code, name, rank, active) and `tenancy.plan_entitlements`
  (plan, entitlement code, value as `jsonb`) are global, read-only for runtime roles, written by the first
  `CONFIGURATION` seeder, `tenancy.configuration.plans`, with authorization.md's values; "unlimited" is a `null` limit.
  A test reads authorization.md's table and compares it with the seed, so the doc stays the source of truth. No plan
  editing in S04: a value changes by a reviewed PR to the seed (seeders never delete; a dropped plan is deactivated).
  `rank` orders plans (STANDARD 1, PRO 2) for D4's upgrade rule. A tenant with no period uses the definitions' defaults
  (`false`, `0`, no level).
- **D3 — Subscription periods are tenant-owned.** `tenancy.subscription_periods` (`ITenantOwned`, RLS, isolation
  sample) with state-machines.md's fields plus who and when (`created_by/at`, `cancelled_by`). Dates are `date`;
  "today" is the date in the tenant's time zone (settings) by the app's `TimeProvider`; `ends_on` is the last day
  included. A row never changes except to cancel it. Non-cancelled periods never overlap (checked under a per-tenant
  advisory lock, `tenancy.subscription:<tenantId>`). `kind` never affects access. Defaults for new periods:
  `expiring_window_days` 30, `grace_days` 14 (Q23). The **current period** is the non-cancelled period with the latest
  `starts_on` on or before today; with none, the latest period of any kind (so an expired or cancelled one still says
  why); with no rows, none. A later period is `SCHEDULED`. T2 adds this rule to state-machines.md (owner).
- **D4 — Subscription commands (platform, under the route tenant).** `subscription.assign` (a new period: plan, kind,
  dates, optional windows; refused if it overlaps), `subscription.renew` (a new period from the day after the latest
  period ends, or from today if that day has passed; optional plan, so a downgrade happens at renewal as
  pricing-and-plans.md says), `subscription.change_plan` (immediate, only to a higher-ranked plan: cancels the current
  period with the reason "Changed to PRO" and adds one from today to the same `ends_on`, in one save; a lower plan is
  `409 TENANT_SUBSCRIPTION_DOWNGRADE_AT_RENEWAL`), `subscription.cancel` (reason; cancels the current and any scheduled
  period now). Audit `REQUIRED` (cancel `REQUIRED_WITH_REASON`). `PLATFORM_ADMIN` only. An event pass, a trial and the
  pilot offer are ordinary assigns (plus an override for the event pass, D5); no presets in S04.
- **D5 — Overrides.** `tenancy.tenant_entitlement_overrides` (tenant-owned, RLS): entitlement code, value (typed by
  the definition), reason, `effective_until` (optional date, e.g. the event pass's `ends_on`), granted by/at, removed
  by/at and why. At most one unremoved override per tenant and entitlement; replacing one removes the old row and adds
  a new one in one save (history kept, AR-18). Actions `entitlement.override` and `entitlement.override.remove`
  (`REQUIRED_WITH_REASON`, `PLATFORM_ADMIN`). Override reasons are platform-only: tenants see the value, not the reason.
- **D6 — Effective entitlements: `IEntitlements` in Tenancy Contracts.** For the scope's tenant (call after it is
  set; RLS): `GetAsync(code)` → value and source (`DEFAULT`, `PLAN`, `OVERRIDE`, with the plan code),
  `CanUseAsync(code)` (BOOLEAN; a LEVEL above none), `GetLimitAsync(code)` (QUOTA; null = unlimited) and
  `CheckQuotaAsync(code, used)` → `Result` (D11) with a warning at 80 % (`QuotaCheck { Limit, Used, Warning }`).
  Effective value = default → the current period's plan → an unremoved override in effect today. Pack spec 07's
  "→ lifecycle policy" is step 1, not a value change.
- **D7 — Caching.** Tenancy caches each tenant's stored inputs (periods, unremoved overrides) in process for 60 s and
  plan values until restart (configuration); every change clears the tenant's entry after it commits, like
  `TenantHostCache`. Derived status and "today" are computed on every call, never cached. Other API instances catch up
  within 60 s (documented, as for hosts).
- **D8 — `READ_ONLY` and the new action flag.** Step 1 follows state-machines.md's table: an `ACTIVE` tenant whose
  current period is `ACTIVE`, `EXPIRING` or `GRACE` is `FULL`; `EXPIRED`, `CANCELLED`, `SCHEDULED` only, or none is
  `READ_ONLY`; `PROVISIONING` and `BLOCKED` are unchanged. `ActionDefinition` gains `availableWhenReadOnly` (tenant
  actions only); under `READ_ONLY` every other tenant action is `403 TENANT_READ_ONLY`. A startup rule requires it on
  every tenant action whose code ends in `.read` (reads must keep working); later write exceptions (finishing a live
  contest, submitting its result within 24 h, exports) set it and check their own window in the handler through
  `ITenantAccess` (S11, S13, S20). The public site stays on while `READ_ONLY` (pricing-and-plans.md);
  `CheckPublicAccessAsync` is unchanged.
- **D9 — Activation doesn't need a subscription.** The state machines are independent: activating a tenant without a
  period makes it `READ_ONLY` until one is assigned. The console's tenant detail says so before activating. No
  backfill: no deployed environment holds tenants yet. DEMO, test data and e2e give their tenants periods (T2).
- **D10 — Step 2 in the Host.** `ActionAuthorizationMiddleware` runs step 2 after step 1 and before the evaluator, for a
  tenant action that names an entitlement: `BOOLEAN` or `LEVEL` → `IEntitlements.CanUseAsync`, else
  `403 ENTITLEMENT_REQUIRED` (the handler checks the level it needs); `QUOTA` → passes (the handler counts, D12). Order
  of refusals: `TENANT_*` → `ENTITLEMENT_REQUIRED` → `AUTH_FORBIDDEN`, so a permission without the entitlement and an
  entitlement without the permission are both refused (pack spec 07, rules 1–3). Platform and account actions have no
  step 2. The Host's OpenAPI pipeline errors add `ENTITLEMENT_REQUIRED` and `TENANT_READ_ONLY` where they can happen.
- **D11 — Errors.** In `TenancyErrors`: `TENANT_READ_ONLY` (403), `ENTITLEMENT_REQUIRED` (403, `detail` names the
  entitlement), `ENTITLEMENT_QUOTA_EXCEEDED` (403, `detail` names the limit), `TENANT_PLAN_NOT_FOUND` (400 on a body
  field), `TENANT_SUBSCRIPTION_OVERLAP` (409), `TENANT_SUBSCRIPTION_NOT_FOUND` (409 for renew/cancel/change without a
  period), `TENANT_SUBSCRIPTION_DOWNGRADE_AT_RENEWAL` (409), `TENANT_OVERRIDE_INVALID_VALUE` (400),
  `TENANT_OVERRIDE_NOT_FOUND` (404). Plans and subscriptions use the `TENANT_` area (api-conventions.md lists no
  `PLAN_` or `SUBSCRIPTION_` prefix). A quota is `403`, not `429`: retrying never helps; T3 changes api-conventions.md's
  "429 rate/quota" to "429 rate limits".
- **D12 — Quotas are counted by the owning module, race-safe by a lock.** The module that creates the item counts its
  own rows inside its transaction while holding a per-tenant advisory lock (`<schema>.<purpose>:<tenantId>`, as
  `MemberChanges` does), then calls `CheckQuotaAsync(code, used)`. Two concurrent creates queue on the lock, so both
  can't pass at the limit. No usage-counter table in S04 (modules.md lists "usage counters": they wait for a count that
  is too costly to take live, such as storage). T2's subscription lock (D3) is the convention's second user, so T2
  writes the advisory lock keys into data-conventions.md (S03 follow-up).
- **D13 — `MAX_STAFF_USERS`.** Counts the tenant's non-removed memberships (`ACTIVE` and `SUSPENDED`) plus pending,
  unexpired invitations to emails that aren't members, so every invitation reserves a seat (Q24).
  `membership.invite` names the entitlement and `InviteHandler` checks it (a re-invite that replaces a pending one to
  the same email adds nothing). The platform's `tenant.owner.invite` counts but is never refused (staff can always
  give a tenant an owner). Accepting isn't checked again: its seat was reserved. A downgrade or a lower override
  never removes anyone (quotas block only new items).
- **D14 — Capabilities with reasons and access.** `GET /api/admin/v1/me/capabilities` answers under `READ_ONLY` too
  (it is a read) and becomes `{ actions, unavailable, access, scope }`: `actions` = what the caller may do now (all
  three steps; unchanged meaning for the shell); `unavailable` = actions the caller's roles grant but the tenant can't
  use now, each `{ action, code }` with `code` `TENANT_READ_ONLY` or `ENTITLEMENT_REQUIRED` (plus `entitlement` and
  `plans`, the plan codes whose values allow it, for "Requires PRO plan"); `access` = `{ mode, subscriptionStatus,
  endsOn, graceEndsOn, timeZone }` for the banner. Actions the roles don't grant are left out, as today: the UI hides
  them (the kit listed `AUTH_FORBIDDEN` reasons; hiding is the rule since S03-T6). Quota usage is not counted here
  (the page would pay for it on every load); the Subscription page and the invite dialog show it. `scopeType` gets
  its enum in OpenAPI (S03 follow-up).
- **D15 — What tenants read.** `GET /api/admin/v1/tenant/subscription` (`tenant.subscription.read`, `TENANT_OWNER`,
  `TENANT_ADMIN`, `VIEWER`; available when read-only; audit `NONE`): the current period (plan, kind, status, dates),
  a scheduled next period, every entitlement's effective value with its source (no override reasons), and usage for
  the quotas a module measures (`used`, `limit`, `warning`); the others say "not measured yet" (AR-30). Usage comes from
  `IQuotaUsage` (Tenancy Contracts), one per QUOTA entitlement, implemented by the owning module and found through DI
  (Identity implements `MAX_STAFF_USERS`; a startup check allows one per code), so Tenancy never references a
  downstream module. Every member sees the banner through the capabilities (D14) (Q26). `GET …/tenant/access` (owner,
  also when blocked) adds the subscription status, dates and the tenant's time zone (S03 follow-up: blocked dates in
  UTC).
- **D16 — Platform endpoints.** `GET /api/platform/v1/plans` (`plan.read`): plans with every entitlement's value and
  definition. Under `/api/platform/v1/tenants/{tenantId}/` (`.RunsUnderRouteTenant()`): `GET subscription`
  (`subscription.read`: periods with derived status, current period, effective entitlements with source, overrides
  with reasons and history, usage), `POST subscription/periods` (assign, 201), `POST subscription/renew`,
  `POST subscription/change-plan`, `POST subscription/cancel`, `POST entitlement-overrides` (201),
  `POST entitlement-overrides/{overrideId}/remove`. Platform reads are audited (`REQUIRED`), like `tenant.read`;
  `PLATFORM_SUPPORT` gets both reads. List query parameters are typed (enum, integer) in OpenAPI from the start (S03
  follow-up). No platform-wide "expiring subscriptions" list: it needs a cross-tenant platform service; with S21's
  notifications.
- **D17 — No jobs, no integration events.** Statuses are derived from dates, so nothing runs at midnight; reminders
  and "subscription expired" notices come with S21 (Hangfire). No module consumes subscription changes yet, so S04
  publishes no integration events (the audit trail keeps the history; add events with their first consumer).
- **D18 — Demo and test data.** DEMO: `tenancy.demo.subscriptions` (after `tenancy.demo.tenants`, through the
  commands as `SYSTEM`): KMSA on STANDARD, `COMPLIMENTARY`, six months from the seed date (pilot offer); ELSSC on
  STANDARD, `PAID`, 45 days, with the override `MAX_ACTIVE_EDITIONS = 1` until its end (event pass,
  pilot-customer.md). Skipped when the tenant has a period. An expired demo period needs `seed --reset` (documented in
  pilot-customer.md). Tests: `TenantTestData.SaveTenantAsync` gives a non-provisioning tenant an `ACTIVE` STANDARD
  period unless the test asks for none; `PostgresFixture` seeds `CONFIGURATION` once and keeps the plan tables across
  resets, like reference data. e2e: tenants created in specs get a period through the API in their setup; the S03
  demo spec assigns one in the console.
- **D19 — Screens.** Console: **Plans** (read-only table, entitlements × plans, `plan.read`); the tenant detail's
  **Subscription** panel replaces "not available yet": current period and history, assign/renew/change plan/cancel
  dialogs (reasons where required), overrides with reason and history, usage. Tenant workspace: Settings →
  **Subscription** (`tenant.subscription.read`): plan, status, dates, what the plan includes, usage vs limits with the
  80 % warning; "To change your plan, contact the platform" (no self-service). Shell: a banner for `EXPIRING` and
  `GRACE` (until when) and `READ_ONLY` (why, since when) from the capabilities' `access`. A control the roles allow
  but the tenant can't use is shown disabled with its reason ("Requires PRO plan", "Read-only: the subscription has
  ended"); controls the roles don't allow stay hidden. Dates in the tenant's time zone.
- **D20 — No PRO-only action yet.** Every action built so far is in both plans; the first PRO-only ones are
  `tenant.domain.add` (S22), `transfer.*` (S24) and the PRO levels. S04 doesn't build one to demo it (AR-30). The
  boolean gate is proven by integration tests with a test-only action and entitlement registered by `TestApp` (like
  `FIXTURE_READER`); the demo shows the commercial difference through the Plans page, the Subscription page and the
  staff quota. S05's `MAX_ENABLED_SPORTS` ("a 4th sport on STANDARD shows Requires PRO") is the first real upgrade
  prompt (Q25).

## Out of scope

Payments, invoices and billing integration (pricing-and-plans.md: manual in v1), self-service upgrades, trials and
sign-up, plan editing screens (D2), `LIST` entitlements, usage-counter tables (D12), quotas other than
`MAX_STAFF_USERS` (sports S05, storage S06, teams S07, editions S09), jobs, notifications and integration events
(D17, S21), a cross-tenant subscriptions list (D16), event-pass and trial presets, platform staff management (Q22),
tenant-custom roles.

## Tasks

### T1 — Entitlement catalogue and plans
Deliver:
- `EntitlementDefinition` and `TenancyEntitlements` with all 17 entitlements (D1); `IEntitlementCatalogue` for the
  Host; `EntitlementRules` at startup (unknown code, non-tenant action); `membership.invite` names `MAX_STAFF_USERS`.
- `tenancy.plans`, `tenancy.plan_entitlements` (global, `ReadOnlyForRuntimeRoles`, `TableAccessPolicies`), the
  `tenancy.configuration.plans` seeder (D2), deterministic IDs; `PostgresFixture` seeds `CONFIGURATION` once and keeps
  the plan tables across resets.
- `GET /api/platform/v1/plans` (`plan.read`; `PLATFORM_ADMIN`, `PLATFORM_SUPPORT`).
- `export-catalogues`: `docs/generated/entitlements.md`, `contracts/entitlements.json`; the `Contracts` CI job checks
  them. CLAUDE.md's `export-catalogues` line lists the new files; `seed --category CONFIGURATION` is in the commands.

Acceptance:
- [x] The entitlement catalogue lists every entitlement with its STANDARD and PRO values and the actions that name it.
- [x] Seeded plan values match authorization.md's table (a test reads the table).
- [x] An action naming an unknown entitlement, or a platform action naming one, stops the Host at startup (test).

### T2 — Subscriptions and access mode
Deliver:
- `tenancy.subscription_periods` (D3; isolation sample), the derived status and current-period rule (domain, unit
  tests on a fake clock), the four commands and `GET …/tenants/{tenantId}/subscription` without overrides and usage
  (D4, D16); errors (D11).
- Step 1 with subscriptions (D8): `READ_ONLY` produced, `availableWhenReadOnly` on `ActionDefinition` with its startup
  rule and a "When read-only" column in actions.md; `ITenantAccess` exposes the access mode with the subscription
  status and dates for later handlers (S11, S13).
- `GET /api/admin/v1/tenant/access` adds the subscription status, dates and the tenant's time zone (D15).
- state-machines.md: the current-period rule and how `READ_ONLY` is applied (owner); multi-tenancy.md "Tenant access
  mode" as built; data-conventions.md: advisory lock keys `<schema>.<purpose>:<id>` (D12).
- Test data, DEMO seeder and e2e setup (D18); the S03 e2e specs assign periods so they keep passing.
- Likely two PRs (T2a periods, commands and read; T2b access mode, flag, `tenant/access`, seeds, test data); say so in
  the plan.

Acceptance:
- [x] Table-driven tests cover every row of state-machines.md's subscription and access-mode tables on a fake clock,
      including the day boundaries in the tenant's time zone.
- [x] An `ACTIVE` tenant with an expired, cancelled or no subscription reads (`*.read`) and gets
      `403 TENANT_READ_ONLY` on a write; renewing restores writes on the next request.
- [x] Overlapping periods are refused under concurrency (test).
- [x] Period history is never overwritten: a change plan or cancel keeps the earlier rows (test reads the table).

### T3 — Overrides, effective entitlements, quotas and the full pipeline
Deliver:
- `tenancy.tenant_entitlement_overrides` (D5; isolation sample) and its two actions and endpoints; overrides and
  effective values in the platform subscription read.
- `IEntitlements` (D6) with its cache (D7); `IQuotaUsage` (D15) and Identity's `MAX_STAFF_USERS` usage.
- Step 2 in the middleware (D10); `IAuthorizationEvaluator`'s comment says it is step 3 only (it still says the Host's
  default denies everything).
- `MAX_STAFF_USERS` on `membership.invite` under a per-tenant lock (D12, D13).
- Capabilities with `unavailable` and `access` (D14); `GET /api/admin/v1/tenant/subscription` (D15).
- Docs: authorization.md (step 2 as built, entitlements as built, capabilities), api-conventions.md (quota is 403,
  D11), modules.md (Tenancy provides `IEntitlements`, `IQuotaUsage`; usage
  counters later).
- Likely two PRs (T3a overrides, `IEntitlements`, step 2; T3b quota, capabilities, tenant read).

Acceptance:
- [x] Pack spec 07's §30 steps 1–16 pass as integration tests, with the D20 test-only action for the boolean steps
      (two plans with different values, a permission without the entitlement denied, an entitlement without the
      permission denied, an override applied and audited, the quota warning then the stop, expiry → read-only,
      renewal → full).
- [x] Two invitations at the last free seat at the same time: one succeeds, one gets
      `403 ENTITLEMENT_QUOTA_EXCEEDED` (test).
- [x] Capabilities match the decision for every system role in `FULL`, `READ_ONLY` and without the entitlement
      (table-driven, extending `CapabilitiesTests`).
- [x] The matrix test covers the new endpoints without changes.

### T4 — Platform and tenant screens
Deliver:
- Console: Plans page and navigation entry; the tenant detail's Subscription panel (D19); activation warns when the
  tenant has no subscription (D9).
- Tenant workspace: Settings → Subscription; the shell's banner; disabled controls with their reason (the invite
  button at the staff limit, every write control while read-only).
- Generated client and `describeProblem` messages for the new codes.
- Playwright: the slice demo against the real API (quota with an override, cancel → read-only → assign), screenshots
  of the new screens and banners at phone, tablet and desktop widths, light and dark.
- Likely two PRs (T4a console; T4b tenant screens, banner, demo e2e, slice close).

Acceptance:
- [ ] The demo flow passes in CI against the real API, including the API's `403 TENANT_READ_ONLY` and
      `403 ENTITLEMENT_QUOTA_EXCEEDED` when called directly.
- [ ] A read-only tenant shows the banner and no enabled write control; every member sees the banner.
- [ ] Every new screen works at phone, tablet and desktop widths (screenshots).

## Carried over from S03 (slice review 2026-10-20)

Each is assigned above; remove it from PROGRESS.md "Tech debt" when its task merges.
- Capabilities under `READ_ONLY` leave out or mark writes; `scopeType` as an enum in OpenAPI (S03-T5a pre-review) — T3
  (D14).
- Blocked tenant's "since" date shown in UTC: `/tenant/access` carries the tenant's time zone (S03-T6b2 pre-review) —
  T2 (D15); the blocked page uses it in T4.
- Advisory lock key convention written into data-conventions.md with its second user (S03-T4b1 pre-review) — T2 (D3,
  D12).
- New list endpoints type `status`/`limit` as enum and integer in OpenAPI (S03-T6c1) — T2, T3 (D16); the three
  existing lists keep their own trigger.

## Slice acceptance

- [ ] Steps 1–3 of the decision formula run on every admin request; the commercial rules are enforced server-side
      and visible in the UI.
- [ ] Seeded STANDARD and PRO values match authorization.md's table (test).
- [ ] Subscription periods and overrides are covered by the isolation harness; the RLS exception list is unchanged.
- [ ] Docs updated: authorization.md, state-machines.md (current period, read-only), multi-tenancy.md,
      api-conventions.md, data-conventions.md, modules.md, admin-app.md (as built), pilot-customer.md (demo
      subscriptions), CLAUDE.md commands, `docs/generated/entitlements.md`.
- [ ] The S03 carry-overs above are done and removed from PROGRESS.md "Tech debt / follow-ups".

## Implementation notes

(The builder adds notes here as tasks merge.)

- **T2b:** a `SCHEDULED` period with none in effect counts as none (`READ_ONLY`). `ITenantAccess.GetAccessAsync`
  (status, mode, time zone, current period) replaces `GetAccessModeAsync`; `/tenant/access` shows the current period's
  status and days but not the cancel reason (platform text; owner default, plan 2026-10-23). Step 1 reads the periods
  uncached (T3 adds D7's cache). DEMO: ELSSC's `MAX_ACTIVE_EDITIONS` override waits for overrides (T3). The e2e specs
  assign periods through the API until the console's panel (T4). Capabilities still list writes under `READ_ONLY`
  until T3 (D14).
- **T3a1:** overrides live in `tenancy.tenant_entitlement_overrides` with a partial unique index (one unremoved per
  tenant and entitlement) and the lock `tenancy.entitlement_override:<tenantId>`; a replacement removes the earlier row
  ("Replaced by a new override") and adds the new one in one save. The override commands answer with the whole
  subscription, like the period commands; `GET …/subscription` gains `overrides` (reasons, history, `inEffect` today).
  `effectiveUntil` before the tenant's today is `VALIDATION_INVALID_REQUEST` on the field. ELSSC's DEMO override runs
  in `tenancy.demo.subscriptions` (version 2). Effective values, the cache and step 2 are T3a2.
- **T3a2:** `IEntitlements` (Tenancy Contracts) is `TenantEntitlements` over the domain's `EffectiveEntitlements`;
  the plan is the current period's whatever its status, and a scheduled-only tenant's scheduled plan applies (owner
  defaults, plan 2026-10-29). Step 1 and `IEntitlements` read through `TenantAccessCache` (`TenantAccessReader`);
  `SubscriptionChanges` clears the tenant after each commit, and `UpdateTenantSettingsHandler` and the platform's
  `UpdateTenantProfileHandler` on a time-zone change (#68 R1);
  `DatabaseTest` clears it before each test. `ENTITLEMENT_REQUIRED`'s `detail` names the entitlement; a quota refusal
  is the error alone (a `Result` carries no detail), so T3b's invite endpoint adds "Your plan allows N staff users".
  The D20 tests run on `Tenancy/EntitlementTestApp` (test entitlement `FIXTURE_PUBLISHING`, action `fixture.publish`,
  plan values written as the owner and removed after each test). `MapModules` takes the entitlement catalogue from
  the services (OpenAPI's `ENTITLEMENT_REQUIRED`), so bare test apps register one.
- **T3b1:** usage counters register with `AddQuotaUsage<TCounter>(code)` (Tenancy Contracts), a singleton
  `QuotaUsageRegistration` the Host checks at startup without creating a counter (they need a database). Identity's
  `StaffUsers` counts `MAX_STAFF_USERS`; `membership.invite` holds `identity.staff_users:<tenantId>` while it counts and
  saves, and its endpoint adds "Your plan allows N staff users." (the limit is read again: a `Result` has no detail).
  Tenancy Contracts shares the code `EntitlementErrorCodes.QuotaExceeded` (errors are declared only in module `*Errors`
  classes); the Host documents `ENTITLEMENT_QUOTA_EXCEEDED` on every tenant action that names a quota. `GET /api/admin/v1/tenant/subscription` reads the cached
  access inputs (as step 1 does) and labels entitlements and plans itself, since tenants can't read `/plans` (owner
  default, plan 2026-10-29); the 80 % warning shows only in the usage reads, not on the invite response (owner default).
- **T3b2:** `GET /api/admin/v1/me/capabilities` is `TenantCapabilitiesResponse` (`{ actions, unavailable, access,
  scope }`); the platform list keeps `{ actions, scope }`. For an `ACTIVE` tenant, step 1's rule is `TenantAccess.Admits`
  (Tenancy Contracts), which `TenantDirectory` uses too; an action both steps refuse shows `TENANT_READ_ONLY` only (the middleware's order).
  `plans` comes from `IEntitlements.PlansAllowingAsync` (active plans in rank order; empty when only an override could
  allow). The codes are shared as `TenantAccessErrorCodes.ReadOnly` and `EntitlementErrorCodes.Required`. A text query
  parameter's values show in OpenAPI through `.DocumentsQueryValues(name, values)` (BuildingBlocks `QueryValues`); only
  `scopeType` uses it so far. The entitlement case is tested through the handler on `EntitlementTestApp` with the test
  role `FIXTURE_PUBLISHER` (`MembershipTestData`). Owner defaults, plan 2026-10-29.
- **T4b1:** the capabilities' `access` adds `startsOn` and `cancelledOn` (the tenant's date of the cancellation,
  `TenantCalendar.DayOf`), so the banner says since or until when a tenant is read-only (owner, plan 2026-10-29);
  an expired period is read-only from the day after its grace. The banner is `AccessBanner` in the tenant layout (every
  member, every page); Settings → Subscription reads `GET /api/admin/v1/tenant/subscription`; `QuotaUsage` is shared
  with the console's panel. `describeProblem` shows `ENTITLEMENT_QUOTA_EXCEEDED`'s `detail` (it names the limit).
  `admin-subscription.spec.ts` walks one tenant of its own through expiring, grace and cancelled; the slice demo e2e
  (T4b2) also runs on a tenant of its own, since parallel specs share KMSA and ELSSC (owner, plan 2026-10-29).
