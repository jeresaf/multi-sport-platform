# Subscription & Entitlements System — Full Specification

## 1. System Identity

**Name:** Subscription & Entitlements System  
**Short Code:** `ENTITLEMENTS`

### Purpose

Control which product capabilities, quotas, sports, and commercial features a tenant is allowed to use without conflating commercial access with user permissions.

### Business Outcome

The platform can offer multiple commercial tiers and feature combinations while keeping authorization understandable:

```text
Tenant Entitlement
+
User Permission
+
Domain Rule
=
Action Allowed
```

---

# 2. Scope

## In Scope

- Plan/tier definitions
- Tenant subscription assignment
- Subscription lifecycle
- Entitlement definitions
- Quotas
- Boolean capabilities
- Enumerated feature levels
- Resource-list entitlements
- Tenant entitlement overrides
- Usage-counter inputs/reads
- Entitlement evaluation
- Grace/read-only commercial rules
- Commercial feature audit

## Out of Scope

- Payment gateway internals
- Invoice/accounting ledger
- User permission assignment
- Business-domain validation
- Tenant lifecycle ownership
- Branding implementation
- Feature implementation itself

---

# 3. Ubiquitous Language

| Term | Definition |
|---|---|
| Plan | Reusable commercial package |
| Subscription | Tenant's commercial assignment to a plan |
| Entitlement | Specific capability/value granted to a tenant |
| Quota | Numeric resource limit |
| Override | Tenant-specific value replacing plan default |
| Permission | User authorization; not an entitlement |
| Usage | Measured consumption against a quota |
| Grace | Temporary commercial access after normal expiry |

---

# 4. Actors

## Platform Commercial Administrator
- Create plans
- Define entitlements
- Assign subscriptions
- Apply approved overrides

## Platform Administrator
- View tenant entitlements
- Troubleshoot capability evaluation

## Tenant Administrator
- View plan/usage
- Cannot arbitrarily grant own entitlements

## System Authorization Layer
- Evaluates entitlements during actions

---

# 5. Dependencies

## Upstream

- Tenant Management
- Audit
- Reference Data
- Platform Analytics / Usage

## Downstream

Every capability-controlled system, including:

- Sports configuration
- Competition creation
- Live scoring
- Reporting
- Branding
- Domains
- APIs
- Storage

---

# 6. Ownership

## Source-of-Truth Entities

```text
EntitlementDefinition
Plan
PlanEntitlement
TenantSubscription
TenantEntitlementOverride
EntitlementEvaluationSnapshot(optional)
QuotaUsageReference
SubscriptionHistory
```

## Must Not Own

- User roles/permissions
- Competition limits enforcement details inside competition domain
- Payment transaction ledger
- Tenant lifecycle directly

---

# 7. Entitlement Types

Support:

```text
BOOLEAN
INTEGER_QUOTA
DECIMAL_QUOTA
ENUM_LEVEL
RESOURCE_LIST
DATE_WINDOW
```

Examples:

```text
LIVE_SCORING = true
MAX_COMPETITIONS = 20
MAX_STORAGE_GB = 50
REPORTING_LEVEL = ADVANCED
ENABLED_SPORTS = [football,basketball,rugby]
CUSTOM_DOMAIN = true
```

---

# 8. Subscription Lifecycle

Suggested:

```text
DRAFT
ACTIVE
EXPIRING
GRACE
EXPIRED
SUSPENDED
CANCELLED
```

Subscription state may trigger Tenant lifecycle policy but Tenant Management owns tenant status.

---

# 9. Business Rules

## RULE-ENT-001
Permissions and entitlements are evaluated independently.

## RULE-ENT-002
A user permission cannot grant a feature the tenant is not entitled to.

## RULE-ENT-003
A tenant entitlement does not grant a user permission.

## RULE-ENT-004
Plan changes must preserve history.

## RULE-ENT-005
Overrides require platform-authorized actors and audit.

## RULE-ENT-006
Quota usage must not become negative.

## RULE-ENT-007
Quota enforcement must define hard-limit vs soft-warning behavior.

## RULE-ENT-008
Historical data remains readable when a feature is removed unless policy explicitly restricts it.

## RULE-ENT-009
Disabling a sport prevents new eligible actions but does not invalidate historical competitions.

---

# 10. Entitlement Registry

Every entitlement needs:

```text
code
name
description
type
default_value
unit
hard_or_soft
module_owner
introduced_version
deprecated_at
```

Example codes:

```text
MAX_COMPETITIONS
MAX_ACTIVE_COMPETITIONS
MAX_REGISTERED_PARTICIPANTS
MAX_ENABLED_SPORTS
CUSTOM_DOMAIN
WHITE_LABEL
LIVE_SCORING
OFFLINE_SCORING
ADVANCED_REPORTS
API_ACCESS
CUSTOM_SPORTS
CUSTOM_RULESETS
TRANSFER_MANAGEMENT
HISTORICAL_BACKFILL
STORAGE_GB
```

---

# 11. Action Relationship

Action Registry may reference required entitlements.

Example:

```text
action: contest.score
requires entitlement: LIVE_SCORING
```

But domain modules still evaluate their own rules.

---

# 12. Commands

```text
entitlement.create_definition
plan.create
plan.update
plan.entitlement.set
subscription.assign
subscription.change_plan
subscription.renew
subscription.cancel
subscription.suspend
tenant_entitlement.override
tenant_entitlement.remove_override
```

---

# 13. Queries

```text
Get effective tenant entitlements
Get plan
Get subscription
Get quota usage
Evaluate entitlement
List tenant overrides
Get entitlement history
```

Effective entitlement resolution:

```text
Platform default
→ Plan value
→ Tenant override
→ Lifecycle policy
= Effective value
```

---

# 14. Domain Events

```text
PlanCreated
PlanUpdated
TenantSubscriptionAssigned
TenantSubscriptionChanged
TenantSubscriptionRenewed
TenantSubscriptionExpired
TenantEntitlementChanged
TenantQuotaThresholdReached
```

---

# 15. Permissions

```text
plan.read
plan.manage
subscription.read
subscription.manage
entitlement.read
entitlement.manage
entitlement.override
usage.read
```

---

# 16. Audit

Audit all:

- Plan changes
- Subscription assignment/change
- Renewal/cancel/suspend
- Overrides
- Quota manual adjustments if supported

Never silently alter tenant commercial rights.

---

# 17. Data Integrity

- Unique entitlement code
- Unique plan code
- One active primary subscription per tenant unless multi-subscription model explicitly introduced
- Unique override per tenant+entitlement
- Type-safe entitlement values
- Effective dates validated
- Historical subscription rows retained

---

# 18. Security

- Tenants may read their effective entitlements/usage
- Only platform-authorized users modify plans/overrides
- Commercial internal notes remain private
- Entitlement evaluation must not trust client-supplied values

---

# 19. API Contracts

Examples:

```text
GET  /api/platform/plans
POST /api/platform/plans
GET  /api/platform/tenants/{tenantId}/subscription
POST /api/platform/tenants/{tenantId}/subscription
GET  /api/platform/tenants/{tenantId}/entitlements

GET  /api/admin/subscription
GET  /api/admin/entitlements
GET  /api/admin/usage
```

Internal check:

```text
CanUse(tenantId, entitlementCode)
GetQuota(tenantId, entitlementCode)
```

---

# 20. UI Workflows

## Platform Plan Manager

Tabs:

```text
Plans
Entitlements
Tenant Overrides
Usage
History
```

## Tenant Subscription Page

Show:

```text
Current Plan
Status
Renewal/Expiry
Enabled Capabilities
Usage vs Quotas
Upgrade/Contact path
```

Do not expose internal pricing logic unless product requirements call for it.

---

# 21. Notifications

Examples:

```text
SubscriptionExpiring
SubscriptionExpired
GraceStarted
Quota80Percent
Quota100Percent
FeatureRemoved
SubscriptionRenewed
```

---

# 22. Analytics

Track:

```text
plan_assigned
plan_changed
entitlement_denied
quota_threshold_reached
feature_usage_by_entitlement
```

---

# 23. Seed Data

Initial simple tier model may include:

```text
STANDARD
PRO
```

But entitlement registry should remain flexible enough to evolve beyond two tiers.

Seed multiple scenarios:

- active standard tenant
- active pro tenant
- custom override
- expiring
- grace
- expired
- quota reached

---

# 24. Error Catalogue

```text
ENTITLEMENT_NOT_FOUND
PLAN_NOT_FOUND
SUBSCRIPTION_NOT_FOUND
SUBSCRIPTION_INACTIVE
FEATURE_NOT_ENTITLED
QUOTA_EXCEEDED
INVALID_ENTITLEMENT_VALUE
INVALID_SUBSCRIPTION_TRANSITION
ENTITLEMENT_OVERRIDE_FORBIDDEN
```

---

# 25. Edge Cases

- Plan downgraded below existing resource count
- Feature removed while contest live
- Subscription expires during live event
- Sport entitlement removed with active season
- Custom domain removed from plan
- Quota counter temporarily delayed
- Tenant restored after renewal

Recommended general principle: do not corrupt active/historical sporting data due to a commercial transition.

---

# 26. Background Jobs

- Expiry evaluation
- Grace-state transition
- Quota snapshot/aggregation
- Threshold notification
- Entitlement cache refresh

All jobs idempotent.

---

# 27. Performance

Effective entitlement checks are frequent.

Use safe caching keyed by:

```text
tenant_id
entitlement_version
```

Invalidate on plan/override/lifecycle change.

---

# 28. Observability

Metrics:

- entitlement evaluations
- denied actions
- quota breaches
- subscription state changes
- cache hit/miss
- evaluation errors

---

# 29. Tests

- Plan resolution
- Override precedence
- Boolean entitlement
- Numeric quota
- Resource list
- Permission vs entitlement separation
- Expired subscription
- Grace policy
- Read-only transition integration
- Quota hard/soft limits
- Cache invalidation

---

# 30. Acceptance Criteria

1. Create entitlement definitions.
2. Create two plans.
3. Assign different values.
4. Subscribe tenant A to standard.
5. Subscribe tenant B to pro.
6. Verify feature availability differs.
7. Give user permission but remove tenant entitlement; action denied.
8. Give tenant entitlement but remove user permission; action denied.
9. Apply tenant override.
10. Verify audit.
11. Reach quota.
12. Verify warning/deny behavior.
13. Expire subscription.
14. Verify tenant lifecycle policy integration.
15. Renew.
16. Verify access restoration.

---

# 31. Definition of Done

Complete when every commercial feature restriction can be evaluated consistently without mixing commercial state with user authorization or business-domain rules.
