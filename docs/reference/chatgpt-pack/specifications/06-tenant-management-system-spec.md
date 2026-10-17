# Tenant Management System — Full Specification

## 1. System Identity

**Name:** Tenant Management System  
**Short Code:** `TENANT`

### Purpose

Own the lifecycle, identity, isolation, core configuration, and administrative boundary for every customer organisation using the Sports Platform.

### Business Outcome

Each customer can operate as an isolated tenant with its own sports, users, competitions, branding, domains, locale, and commercial state without exposing or corrupting another tenant's data.

---

# 2. Scope

## In Scope

- Tenant creation and provisioning
- Tenant identity
- Tenant lifecycle
- Tenant ownership
- Tenant administrative contacts
- Locale/timezone/currency
- Enabled sports references
- Tenant domains
- Tenant status
- Tenant isolation metadata
- Tenant feature-linkage references
- Tenant archive/read-only transitions
- Tenant-level public visibility
- Tenant settings
- Tenant suspension/restoration

## Out of Scope

- Billing ledger
- Payment gateway processing
- User authentication internals
- Fine-grained permissions
- Feature entitlement rules
- Tenant branding assets
- Competition data
- Player/team data
- Sponsor campaigns

## Future Scope

- Federation hierarchy
- Parent/child tenants
- Regional sub-organisations
- Shared federation registries
- Cross-tenant data-sharing agreements

---

# 3. Ubiquitous Language

| Term | Definition | Not To Be Confused With |
|---|---|---|
| Tenant | Isolated customer organisation using the platform | Team, club, federation registry record |
| Tenant Owner | Primary accountable organisation/user relationship | Platform owner |
| Tenant Status | Operational state of a tenant | Subscription state |
| Tenant Domain | Web domain/subdomain routed to a tenant | Public competition URL |
| Tenant Setting | Tenant-wide operational configuration | Feature entitlement |
| Tenant Scope | Security/data boundary applied to tenant-owned records | Competition scope |
| Parent Tenant | Future hierarchical tenant controlling child tenants | Club parent organisation |

---

# 4. Actors

## Platform Administrator

- Provision tenant
- Suspend/restore/archive tenant
- Review tenant status
- Assign tenant owner/admin
- Inspect isolation/health metadata

## Tenant Owner

- Manage tenant-level profile/configuration
- Manage authorised administrators
- Request domain/branding changes

## Tenant Administrator

- Read tenant settings
- Update permitted tenant configuration
- Operate enabled modules according to permissions/entitlements

## System Provisioning Job

- Creates default tenant records
- Initializes tenant settings
- Applies default locale/timezone/currency

---

# 5. Dependencies

## Upstream

- Reference Data Registry
- Audit
- Data Governance & Integrity
- Identity/Auth subsystem
- Subscription & Entitlements

## Downstream

- Branding
- Sponsorship
- Registries
- Competition Management
- Competition Operations
- Reporting
- Public Presentation
- Analytics

## Dependency Rules

- Tenant Management is source of truth for tenant lifecycle.
- Entitlements may read tenant status but do not own it.
- Branding references tenant ID but does not modify tenant lifecycle.
- All tenant-owned modules must validate tenant scope.

---

# 6. Ownership

## Source-of-Truth Entities

```text
Tenant
TenantSetting
TenantDomain
TenantAdministratorLink
TenantSportEnablement
TenantLifecycleHistory
```

## Referenced External Entities

```text
User
Subscription
SportDefinition
ReferenceData
```

## Ownership Prohibitions

Tenant Management must not mutate:

- Competition records
- Team/player records
- Subscription plans
- Branding assets
- Sponsor campaigns
- Permission assignments owned elsewhere

---

# 7. Domain Model

## 7.1 Tenant

Suggested fields:

```text
id
slug
legal_name
display_name
description
country_code
timezone
currency_code
default_language
status
public_enabled
created_at
created_by
activated_at
suspended_at
archived_at
version
```

Constraints:

- `slug` globally unique
- status required
- timezone/currency must reference valid shared data
- archived tenant remains historically readable

## 7.2 TenantSetting

Fields:

```text
tenant_id
key
value
value_type
updated_at
updated_by
version
```

Only approved tenant-setting keys should be allowed.

## 7.3 TenantDomain

Fields:

```text
id
tenant_id
host
domain_type
status
verified_at
is_primary
created_at
```

Domain types:

```text
PLATFORM_SUBDOMAIN
CUSTOM_DOMAIN
API_DOMAIN
```

## 7.4 TenantSportEnablement

Fields:

```text
tenant_id
sport_variant_id
enabled
enabled_at
disabled_at
source
```

Actual ability to use a sport may additionally depend on entitlement.

## 7.5 TenantLifecycleHistory

Fields:

```text
id
tenant_id
from_status
to_status
reason
changed_by
changed_at
correlation_id
```

---

# 8. State Machine

```text
PROVISIONING
    ↓ activate
ACTIVE
    ↓ expiry warning
EXPIRING
    ↓ grace
GRACE
    ↓ restrict
READ_ONLY
    ↓ suspend
SUSPENDED
    ↓ archive
ARCHIVED
```

Additional allowed transitions:

```text
EXPIRING -> ACTIVE
GRACE -> ACTIVE
READ_ONLY -> ACTIVE
SUSPENDED -> ACTIVE
```

subject to authorization and commercial state.

### State Meaning

- `PROVISIONING`: being configured
- `ACTIVE`: full permitted operation
- `EXPIRING`: active but approaching commercial expiry
- `GRACE`: temporary post-expiry access
- `READ_ONLY`: historical/public reads allowed; writes blocked except explicitly allowed account actions
- `SUSPENDED`: tenant access substantially blocked
- `ARCHIVED`: historical retention only

---

# 9. Business Rules

## RULE-TNT-001
Every tenant-owned record must carry tenant identity directly or through a non-ambiguous owned aggregate path.

## RULE-TNT-002
A user request cannot operate across tenants unless the user has platform-level authorization.

## RULE-TNT-003
Tenant slug is globally unique and cannot be silently reused after archive.

## RULE-TNT-004
Tenant lifecycle changes require audit records.

## RULE-TNT-005
Suspension does not delete data.

## RULE-TNT-006
Archive does not hard-delete historical competition data.

## RULE-TNT-007
Read-only tenants cannot create or mutate competition-operational data.

## RULE-TNT-008
Enabled sport references must point to published/non-invalid sport definitions.

## RULE-TNT-009
Custom domains require verification before becoming primary.

## RULE-TNT-010
Tenant settings may not override platform security/integrity policy.

---

# 10. Commands / Actions

```text
tenant.create
tenant.activate
tenant.update
tenant.suspend
tenant.restore
tenant.archive
tenant.setting.update
tenant.domain.add
tenant.domain.verify
tenant.domain.set_primary
tenant.sport.enable
tenant.sport.disable
tenant.admin.assign
tenant.admin.remove
```

Each command must evaluate:

```text
platform permission
tenant status
entitlement where relevant
domain rule
audit requirement
```

---

# 11. Queries

Required:

```text
List tenants
Get tenant
Get tenant lifecycle
Get tenant settings
Get tenant domains
Get tenant administrators
Get enabled sports
Get tenant public profile
Get tenant operational status
```

---

# 12. Domain Events

```text
TenantCreated
TenantActivated
TenantUpdated
TenantSuspended
TenantRestored
TenantArchived
TenantDomainVerified
TenantPrimaryDomainChanged
TenantSportEnabled
TenantSportDisabled
TenantAdministratorAssigned
```

---

# 13. Permissions

```text
tenant.read
tenant.create
tenant.update
tenant.lifecycle.manage
tenant.domain.manage
tenant.settings.manage
tenant.sports.manage
tenant.admins.manage
```

Tenant administrators should not automatically receive platform-level tenant lifecycle permissions.

---

# 14. Entitlements

Relevant entitlement checks may include:

```text
CUSTOM_DOMAIN
MAX_ENABLED_SPORTS
WHITE_LABEL
PUBLIC_SITE
API_ACCESS
```

Tenant Management references these but does not define pricing/plan policy.

---

# 15. Audit Requirements

Audit:

- Create/activate
- Status transitions
- Domain changes
- Primary-domain change
- Enabled sports changes
- Tenant admin changes
- Sensitive setting changes

Record:

```text
actor
tenant
action_code
entity
reason
correlation_id
timestamp
```

---

# 16. Data Integrity

- Global unique slug
- Unique active domain host
- Tenant-scoped unique setting key
- Tenant-sport unique pair
- No orphan admin links
- No hard delete after business use
- Optimistic concurrency on tenant updates

---

# 17. Security & Privacy

- Enforce tenant filter at repository/query boundary
- Prevent tenant ID from being trusted solely from client payload
- Resolve tenant from authenticated context/domain where possible
- Redact platform-internal commercial metadata from public APIs
- Keep private administrative contacts non-public

---

# 18. Import / Export

Tenant import is primarily platform-admin tooling.

Export may support:

```text
tenant configuration
enabled sports
domains
settings
administrators
```

Tenant data export of competitions/people belongs to respective systems.

---

# 19. API Contracts

Examples:

```text
GET    /api/platform/tenants
POST   /api/platform/tenants
GET    /api/platform/tenants/{id}
PATCH  /api/platform/tenants/{id}
POST   /api/platform/tenants/{id}/activate
POST   /api/platform/tenants/{id}/suspend
POST   /api/platform/tenants/{id}/restore
POST   /api/platform/tenants/{id}/archive

GET    /api/admin/tenant
PATCH  /api/admin/tenant/settings
GET    /api/admin/tenant/domains
POST   /api/admin/tenant/domains
```

---

# 20. UI Workflows

## Platform Tenant List

Show:

```text
Tenant
Status
Plan/Entitlement Summary
Enabled Sports
Primary Domain
Expiry State
Created
Actions
```

## Tenant Detail

Tabs:

```text
Overview
Lifecycle
Administrators
Sports
Domains
Settings
Commercial
Audit
```

## Tenant Settings

Sections:

```text
Organisation
Locale
Time Zone
Currency
Public Visibility
Enabled Sports
Domains
```

---

# 21. Notifications

Examples:

```text
TenantActivated
TenantExpiring
TenantEnteredGrace
TenantReadOnly
TenantSuspended
TenantRestored
DomainVerificationRequired
```

Recipients depend on tenant owner/admin configuration.

---

# 22. Analytics

Emit product events:

```text
tenant_created
tenant_activated
tenant_suspended
tenant_restored
tenant_domain_added
tenant_sport_enabled
```

---

# 23. Seed Data

Provide:

- One platform demo tenant
- One secondary tenant for isolation tests
- Active/read-only/suspended tenant scenarios
- Multiple timezone/currency examples
- Platform subdomain and custom-domain examples

---

# 24. Error Catalogue

```text
TENANT_NOT_FOUND
TENANT_SLUG_EXISTS
TENANT_DOMAIN_EXISTS
TENANT_DOMAIN_UNVERIFIED
TENANT_SUSPENDED
TENANT_READ_ONLY
TENANT_ARCHIVED
TENANT_SPORT_NOT_ENABLED
TENANT_SCOPE_VIOLATION
TENANT_INVALID_TRANSITION
```

---

# 25. Edge Cases

- Expired tenant with live contest in progress
- Custom domain verification failure
- Restore from suspension after subscription renewal
- Attempted cross-tenant entity access
- Archived tenant with public historical pages
- Tenant sport disabled while historical competition exists

Default policy: disabling a sport prevents new use but must not invalidate historical data.

---

# 26. Background Jobs

- Expiry-state evaluator
- Domain verification checks where applicable
- Grace/read-only transition scheduler
- Tenant health consistency checker

Jobs must be idempotent and audited for lifecycle changes.

---

# 27. Performance

Tenant resolution occurs on almost every request; it must be efficient and cacheable where safe.

Indexes:

- slug
- domain host
- status
- tenant-sport pair

---

# 28. Observability

Track:

- tenant-resolution failures
- cross-tenant access denials
- lifecycle transition failures
- domain verification failures
- requests by tenant
- background transition jobs

---

# 29. Tests

Required:

- Tenant creation
- Lifecycle transitions
- Invalid transitions
- Tenant isolation
- Domain uniqueness
- Domain verification
- Read-only write rejection
- Suspension rejection
- Sport enablement
- Admin assignment
- Cross-tenant API attempts
- Historical read after archive

---

# 30. Acceptance Criteria

1. Create tenant.
2. Configure locale/timezone/currency.
3. Enable sports.
4. Assign tenant admin.
5. Add platform subdomain.
6. Activate tenant.
7. Create tenant-owned data in downstream module.
8. Verify second tenant cannot access it.
9. Mark tenant expiring/grace/read-only.
10. Confirm writes stop in read-only state.
11. Restore tenant.
12. Suspend tenant.
13. Confirm operational access stops.
14. Restore again.
15. Archive tenant.
16. Confirm historical records remain readable according to policy.

---

# 31. Definition of Done

Complete when all platform modules can reliably derive tenant scope, enforce lifecycle state, and isolate tenant-owned data without duplicating tenant logic.
