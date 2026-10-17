# Club / Organisation Registry — Full Specification

## 1. System Identity

**Name:** Club / Organisation Registry  
**Short Code:** `ORG_REGISTRY`

### Purpose

Provide the persistent identity of sports organisations that may own or operate one or more teams, programmes, academies, or competition entries.

### Business Outcome

The platform can represent long-lived organisations separately from individual teams, allowing one organisation to operate multiple teams across sports, age groups, genders/classifications, or competition levels without duplicating organisation data.

---

# 2. Scope

## In Scope

- Organisation identity
- Organisation type
- Legal/public names
- Branding references
- Contact metadata
- Headquarters/location references
- Public profile
- Organisation aliases
- External identifiers
- Parent/child organisation relationships where enabled
- Organisation lifecycle

## Out of Scope

- Team master data
- Team membership
- Competition registration
- Staff employment/assignment
- Sponsor campaign management
- Tenant identity

---

# 3. Ubiquitous Language

| Term | Definition |
|---|---|
| Organisation | Persistent sports entity such as club, school, academy, federation, company, or institution |
| Club | Organisation whose purpose includes operating sports teams |
| Parent Organisation | Organisation that structurally owns or controls another organisation |
| Organisation Alias | Alternative/short/legacy name |
| Organisation Type | Classification such as CLUB, SCHOOL, ACADEMY, FEDERATION |

---

# 4. Organisation Types

Initial examples:

```text
CLUB
SCHOOL
UNIVERSITY
ACADEMY
COMPANY
FEDERATION
ASSOCIATION
COMMUNITY_ORGANISATION
GOVERNMENT_ENTITY
OTHER
```

Types should come from controlled reference/configuration, not arbitrary free text.

---

# 5. Actors

- Tenant Administrator
- Organisation Administrator
- Platform Administrator
- Import System
- Public User

---

# 6. Dependencies

## Upstream

- Tenant Management
- Reference Data
- Venue Registry
- Audit
- Data Governance

## Downstream

- Team Registry
- Staff Registry
- Competition Registration
- Public Search
- Historical Records
- Reporting

---

# 7. Ownership

## Source-of-Truth Entities

```text
Organisation
OrganisationAlias
OrganisationExternalIdentifier
OrganisationRelationship
OrganisationPublicProfile
OrganisationLifecycleHistory
```

## Must Not Own

- Teams
- Rosters
- Competitions
- Sponsors
- Tenant subscription

---

# 8. Organisation Entity

Suggested fields:

```text
id
tenant_id
organisation_type
legal_name
display_name
short_name
registration_number
country_code
city
primary_venue_id
logo_asset_id
website
email
phone
status
public_enabled
created_at
created_by
updated_at
version
```

---

# 9. Lifecycle

```text
ACTIVE
INACTIVE
SUSPENDED
DISSOLVED
ARCHIVED
```

Historical references remain valid after inactivity/archive.

---

# 10. Business Rules

## RULE-ORG-001
Organisation names need not be globally unique, but duplicate detection should consider tenant, type, normalized name, location, and external IDs.

## RULE-ORG-002
An organisation with historical teams/competitions cannot be hard-deleted.

## RULE-ORG-003
Parent-child relationships must not form cycles.

## RULE-ORG-004
Public APIs expose only public-approved fields.

## RULE-ORG-005
Organisation status does not automatically change team status.

---

# 11. Commands

```text
organisation.create
organisation.update
organisation.activate
organisation.deactivate
organisation.suspend
organisation.archive
organisation.alias.add
organisation.external_id.add
organisation.relationship.add
organisation.relationship.remove
```

---

# 12. Queries

```text
List organisations
Search organisations
Get organisation
Get public organisation
Get child organisations
Get parent organisation
Get teams by organisation
Get aliases
Get external IDs
```

`Get teams` should use a read model from Team Registry.

---

# 13. Domain Events

```text
OrganisationCreated
OrganisationUpdated
OrganisationSuspended
OrganisationArchived
OrganisationRelationshipChanged
```

---

# 14. Permissions

```text
organisation.read
organisation.create
organisation.update
organisation.lifecycle.manage
organisation.relationship.manage
organisation.external_id.manage
```

---

# 15. Entitlements

Possible:

```text
ORGANISATION_REGISTRY
MAX_ORGANISATIONS
ORGANISATION_HIERARCHY
```

---

# 16. Audit

Audit all lifecycle, relationship, external-ID, and public-profile changes.

---

# 17. Data Integrity

- Tenant scoped
- No parent cycles
- Unique provider+external ID
- Optimistic concurrency
- Archive instead of destructive delete

---

# 18. Security & Privacy

Organisation data is mostly public/business data, but private administrative contacts may be restricted.

---

# 19. Import / Export

CSV/Excel/JSON import must include duplicate preview and alias resolution.

---

# 20. API Direction

```text
GET  /api/admin/organisations
POST /api/admin/organisations
GET  /api/admin/organisations/{id}
PATCH /api/admin/organisations/{id}
GET  /api/public/organisations/{id}
```

---

# 21. UI

Screens:

```text
Organisation Directory
Create Organisation
Organisation Detail
Teams
Relationships
Public Profile
External IDs
History
```

---

# 22. Seed Data

Include organisations operating:
- one team
- multiple teams
- multiple sports
- school-based team structure
- academy
- federation

---

# 23. Error Catalogue

```text
ORGANISATION_NOT_FOUND
ORGANISATION_DUPLICATE_POSSIBLE
ORGANISATION_RELATIONSHIP_CYCLE
ORGANISATION_EXTERNAL_ID_EXISTS
ORGANISATION_ARCHIVED
```

---

# 24. Tests

- Create/update
- Parent relationship
- Cycle prevention
- Duplicate detection
- Archive/history
- Tenant isolation
- Public/private field separation

---

# 25. Definition of Done

Complete when teams and other downstream systems can reference persistent organisations without duplicating organisation identity or structure.
