# Team Registry — Full Specification

## 1. System Identity

**Name:** Team Registry  
**Short Code:** `TEAM_REGISTRY`

### Purpose

Provide the persistent identity of a playing team independently of competition registrations, season rosters, and match squads.

### Business Outcome

A team can participate in many competitions across years while preserving one canonical team identity and history.

---

# 2. Scope

## In Scope

- Team identity
- Sport/variant association
- Parent organisation
- Team type/category
- Public identity and branding
- Home venue
- Team aliases
- External identifiers
- Team lifecycle

## Out of Scope

- Player membership
- Staff assignment
- Competition registration
- Competition roster
- Match lineup
- Results/statistics ownership

---

# 3. Ubiquitous Language

| Term | Definition |
|---|---|
| Team | Persistent playing entity |
| Team Membership | Effective-dated relationship between person/athlete and team |
| Competition Roster | Competition-specific eligible set of players |
| Match Squad | Contest-specific selected set from roster |
| Team Category | Classification such as senior, U19, women, reserve |

---

# 4. Dependencies

Upstream:
- Tenant Management
- Club / Organisation Registry
- Sports Definition
- Venue Registry
- Audit

Downstream:
- Team Membership
- Staff Assignment
- Rosters
- Competition Registration
- Scheduling
- Contest Participants
- Statistics
- Search

---

# 5. Team Entity

Suggested fields:

```text
id
tenant_id
organisation_id
sport_id
sport_variant_id
team_type
name
short_name
code
motto
logo_asset_id
badge_asset_id
home_venue_id
country_code
city
status
public_enabled
created_at
created_by
updated_at
version
```

---

# 6. Lifecycle

```text
ACTIVE
INACTIVE
SUSPENDED
DISBANDED
ARCHIVED
```

Historical competition data remains intact.

---

# 7. Business Rules

## RULE-TEAM-001
A team belongs to one tenant.

## RULE-TEAM-002
A team may optionally belong to an organisation.

## RULE-TEAM-003
A team sport/variant must reference a valid published sport definition.

## RULE-TEAM-004
Competition participation is not stored directly on the Team aggregate.

## RULE-TEAM-005
A team with history cannot be hard-deleted.

## RULE-TEAM-006
A team may change display identity, but aliases/history must preserve prior names.

## RULE-TEAM-007
Duplicate detection should use normalized name, organisation, sport, location, aliases, and external IDs.

---

# 8. Commands

```text
team.create
team.update
team.activate
team.deactivate
team.suspend
team.archive
team.alias.add
team.external_id.add
team.home_venue.set
team.organisation.change
```

Organisation change should preserve history if business rules require.

---

# 9. Queries

```text
Search teams
Get team
Get public team
Get team history
Get team competitions
Get current memberships
Get current staff
Get aliases
Get external IDs
```

Most relationship-heavy results are read models.

---

# 10. Domain Events

```text
TeamCreated
TeamUpdated
TeamSuspended
TeamArchived
TeamOrganisationChanged
TeamHomeVenueChanged
```

---

# 11. Permissions

```text
team.read
team.create
team.update
team.lifecycle.manage
team.external_id.manage
team.organisation.manage
```

---

# 12. Entitlements

Possible:

```text
TEAM_REGISTRY
MAX_TEAMS
MULTI_SPORT_TEAMS
```

---

# 13. Audit

Audit identity, sport, organisation, status, alias, external-ID, and venue changes.

---

# 14. Integrity

- Tenant scoped
- Valid organisation reference
- Valid sport reference
- Unique provider+external ID
- Optimistic concurrency
- Historical alias preservation

---

# 15. Security

Public fields separated from internal administrative metadata.

---

# 16. Import / Export

Import must resolve organisation and sport before team creation.

---

# 17. API Direction

```text
GET  /api/admin/teams
POST /api/admin/teams
GET  /api/admin/teams/{id}
PATCH /api/admin/teams/{id}
GET  /api/public/teams/{id}
```

---

# 18. UI

Team Directory filters:

```text
Sport
Organisation
Status
Category
Competition (read projection)
```

Team Detail tabs:

```text
Overview
Players
Staff
Competitions
Fixtures
Results
Statistics
Documents
History
```

---

# 19. Seed Data

Include:
- independent team
- team under club
- same organisation with football/rugby teams
- youth/senior teams
- archived historical team

---

# 20. Error Catalogue

```text
TEAM_NOT_FOUND
TEAM_DUPLICATE_POSSIBLE
TEAM_SPORT_INVALID
TEAM_ORGANISATION_INVALID
TEAM_EXTERNAL_ID_EXISTS
TEAM_ARCHIVED
```

---

# 21. Tests

- Create/update
- Duplicate warning
- Organisation link
- Multi-sport organisation
- Archive/history
- Tenant isolation
- Public profile

---

# 22. Definition of Done

Complete when all team-based competition flows can reference one stable team identity across multiple seasons and competitions.
