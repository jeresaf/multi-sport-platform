# S04 — Plans, subscriptions and entitlements

**Goal:** tenants subscribe to plans; entitlements and access mode are enforced alongside permissions — the
owner's "Subscription/Feature Manager", driven by the action catalogue.
**Modules touched:** Tenancy & Commercial, Identity (evaluator), admin web.
**Depends on:** S03.
**Demo:** tenant A on STANDARD and tenant B on PRO see different capabilities; a permission without the
entitlement is denied; an entitlement without the permission is denied; a quota warning then block; expiry turns
a tenant read-only; renewal restores it.

Read first: docs/architecture/authorization.md (entitlements and tiers), docs/domain/state-machines.md
(subscription and access mode), docs/product/pricing-and-plans.md (plans in plain words; Q2 answered).

## Out of scope

Payments/invoices, self-service upgrades, Hangfire (derived states need no jobs; notices come with S21).

## Tasks

### T1 — Entitlement catalogue and plans
Deliver:
- Entitlement definitions **in code** (code, name, type `BOOLEAN | QUOTA | LEVEL | LIST`, unit, hard/soft,
  owning module, introduced in) exported to `docs/generated/entitlements.md`; CI staleness check.
- `tenancy.plans`, `tenancy.plan_entitlements` seeded (CONFIGURATION) with STANDARD and PRO values from the table in
  authorization.md; `tenancy.tenant_entitlement_overrides` (platform action `entitlement.override`, reason).
- Actions link to entitlements in the action catalogue (e.g. `tenant.domain.add` → `CUSTOM_DOMAIN`).

Acceptance:
- [ ] Catalogue export lists every entitlement with the actions it gates.

### T2 — Subscriptions and access mode
Deliver:
- `tenancy.subscription_periods` (plan, kind `PAID | TRIAL | COMPLIMENTARY`, starts_on, ends_on, grace_days,
  expiring_window_days, cancelled_at, cancel_reason); derived status per state-machines.md; platform actions `subscription.assign`,
  `subscription.renew`, `subscription.change_plan`, `subscription.cancel` (reason). History never overwritten.
- Access mode computed from tenant status × subscription; READ_ONLY rules (live contests may finish; exports
  allowed) implemented as a policy other modules query.
- Admin banner data in `GET /api/admin/v1/tenant/access` (mode, reason, dates).

Acceptance:
- [ ] Table-driven tests for every row of the access-mode table using a fake clock.

### T3 — Effective entitlements, quotas and the full pipeline
Deliver:
- Resolution default → plan → override; per-tenant cache invalidated by plan/override/subscription events.
- `IEntitlements.CanUse(code)`, `GetLimit(code)`, `CheckQuota(code, countInTransaction)`; quota checks run inside
  the creating transaction and are race-safe (row lock on a usage counter or serialisable check — explain the
  choice in the PR).
- First quota wired: `MAX_STAFF_USERS` on `membership.invite` (soft warning at 80%, hard stop at 100%).
- Authorization evaluator order: access mode → entitlement → permission; capabilities endpoint includes the
  reason for unavailable actions (`ENTITLEMENT_REQUIRED`, `TENANT_READ_ONLY`, `AUTH_FORBIDDEN`).

Acceptance:
- [ ] Pack spec 07's acceptance list (steps 1–16) passes as integration tests.

### T4 — Platform and tenant screens
Deliver:
- Platform console: plans (read-only values), tenant subscription panel (assign/renew/change/cancel), overrides
  with reason.
- Tenant settings → **Subscription**: plan, status, dates, usage vs limits.
- Capability-aware UI: unavailable actions show why (e.g. "Requires PRO plan").

Acceptance:
- [ ] Playwright: STANDARD tenant sees "Requires PRO" on a gated action; after override, the action is available.

## Slice acceptance

- [ ] Commercial rules are enforced server-side and visible in the UI.
- [ ] Seeded STANDARD/PRO values match the table in docs/architecture/authorization.md (adopted for Q2).
