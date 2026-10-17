# Participant / Athlete Registry System — Full Specification

## 1. System Identity

**Name:** Participant / Athlete Registry System  
**Short Code:** `PARTICIPANT`

### Purpose

Represent entities that can participate in sporting competitions while separating human identity from sport-specific participation identity.

### Business Outcome

The platform can support individuals, teams, pairs, relay teams, crews, schools, clubs, countries, and composite participants through one competition-facing abstraction.

---

# 2. Scope

## In Scope

- Participant identity
- Participant types
- Individual athlete profiles
- Sport association
- Sport variant association
- Athlete public profile metadata
- Participant aliases
- Participant external IDs
- Participant lifecycle
- Participant classification/categories
- Competition-facing participant abstraction

## Out of Scope

- Canonical human biodata ownership
- Team internal membership
- Competition registration
- Rosters
- Match squads
- Performance stats ownership
- Club/team master records where separate registry owns them

---

# 3. Ubiquitous Language

| Term | Definition |
|---|---|
| Participant | Any entity eligible to enter a sporting competition |
| Individual Participant | Participant representing a single person/athlete |
| Athlete Profile | Sport-specific participation identity linked to a Person |
| Team Participant | Participant representation linked to Team Registry |
| Composite Participant | Temporary/combined competition participant |
| Participant Type | Structural category of participant |
| Classification | Sport/competition categorization such as weight class or age class |

---

# 4. Participant Types

Initial:

```text
INDIVIDUAL
TEAM
PAIR
CREW
RELAY_TEAM
CLUB
SCHOOL
COUNTRY
COMPOSITE_TEAM
```

Not every type is used by every sport.

Sport Definition/Ruleset may constrain allowed types.

---

# 5. Actors

## Tenant Administrator
- Manage participant records

## Competition Administrator
- Search/select participants
- Cannot rewrite canonical person biodata

## Team Administrator
- View relevant team-linked participants

## Import System
- Resolve participants through canonical commands

## Public User
- View public participant profile

---

# 6. Dependencies

## Upstream

- Tenant Management
- Person Registry
- Team Registry
- Club/Organisation Registry
- Sports Definition
- Audit

## Downstream

- Competition Registration
- Rosters
- Scheduling
- Contest Participants
- Statistics
- Search
- Historical Records

---

# 7. Ownership

## Source-of-Truth Entities

```text
Participant
IndividualAthleteProfile
ParticipantAlias
ParticipantExternalIdentifier
ParticipantClassification
ParticipantLifecycleHistory
```

## References

```text
person_id
team_id
club_id
sport_id
sport_variant_id
```

depending on participant type.

---

# 8. Participant Entity

Suggested fields:

```text
id
tenant_id
participant_type
display_name
short_name
sport_id
sport_variant_id
status
public_enabled
created_at
created_by
updated_at
version
```

Type-specific reference:

```text
person_id
team_id
club_id
```

prefer explicit subtype/entity modelling over unsafe polymorphic foreign keys where database integrity would be lost.

---

# 9. Individual Athlete Profile

Fields may include:

```text
participant_id
person_id
sport_id
sport_variant_id
primary_position_code
dominant_side
competition_name/display_name override
public_bio
status
```

Sport-independent biodata remains in Person Registry.

---

# 10. Participant Lifecycle

```text
ACTIVE
INACTIVE
SUSPENDED
RETIRED
ARCHIVED
```

Competition eligibility is separate and may vary by competition.

A retired athlete can remain historically visible.

---

# 11. Business Rules

## RULE-PRT-001
Every individual participant references exactly one canonical person.

## RULE-PRT-002
Participant type must be compatible with sport definition where applicable.

## RULE-PRT-003
Participant archive does not erase historical registrations/results.

## RULE-PRT-004
Participant status does not replace competition-specific eligibility.

## RULE-PRT-005
Participant Registry does not own team membership.

## RULE-PRT-006
A team participant references Team Registry rather than duplicating team master data.

## RULE-PRT-007
External IDs are unique by provider/authority in scope.

## RULE-PRT-008
Sport-specific fields must not pollute the generic Participant aggregate unless broadly reusable.

---

# 12. Commands

```text
participant.create
participant.update
participant.activate
participant.deactivate
participant.suspend
participant.retire
participant.archive
participant.alias.add
participant.external_id.add
athlete_profile.create
athlete_profile.update
participant.classification.set
```

---

# 13. Queries

```text
List participants
Search participants
Get participant
Get public participant
List participants by sport
Get athlete profile
Get participant role/type
Get aliases
Get external IDs
```

---

# 14. Domain Events

```text
ParticipantCreated
ParticipantUpdated
ParticipantActivated
ParticipantSuspended
ParticipantRetired
ParticipantArchived
AthleteProfileCreated
AthleteProfileUpdated
ParticipantClassificationChanged
```

---

# 15. Permissions

```text
participant.read
participant.create
participant.update
participant.lifecycle.manage
participant.external_id.manage
participant.classification.manage
athlete_profile.manage
```

---

# 16. Entitlements

Possible:

```text
PARTICIPANT_REGISTRY
MAX_PARTICIPANTS
INDIVIDUAL_SPORTS
TEAM_SPORTS
FEDERATION_REGISTRY
```

---

# 17. Audit

Audit:

- Participant creation/update
- Lifecycle changes
- Sport/variant changes where permitted
- Athlete profile updates
- External ID changes
- Classification changes

---

# 18. Data Integrity

- Tenant scoped
- Unique provider+external ID
- Individual participant requires person reference
- Team participant requires team reference
- No historically referenced hard delete
- Optimistic concurrency
- Sport/variant compatibility validated

---

# 19. Security & Privacy

Participant public profile may expose:

```text
display_name
public photo
sport
team/public affiliation via read model
public statistics
public nationality
```

Private person biodata must not be duplicated into public participant records.

---

# 20. Import / Export

Import:

```text
CSV
Excel
JSON
API
```

Individual participant import flow:

```text
Resolve Person
→ Resolve/Create Athlete Profile
→ Resolve Participant
→ Validate Sport
→ Create/Update
```

Team participants should resolve against Team Registry.

---

# 21. API Contracts

Admin:

```text
GET  /api/admin/participants
POST /api/admin/participants
GET  /api/admin/participants/{id}
PATCH /api/admin/participants/{id}

POST /api/admin/athletes
GET  /api/admin/athletes/{id}
```

Public:

```text
GET /api/public/participants/{id}
```

---

# 22. UI Workflows

## Participant Directory

Filters:

```text
Type
Sport
Variant
Status
Classification
```

## Individual Athlete Creation

```text
1 Search Person Registry
2 Select/Create Person
3 Select Sport/Variant
4 Athlete Profile
5 Public Profile
6 External IDs
7 Save
```

Do not re-enter canonical biodata when an existing person is selected.

## Participant Detail

Tabs:

```text
Overview
Competitions
Teams/Affiliations
Matches
Statistics
History
External IDs
```

Most of these tabs are read projections from downstream systems.

---

# 23. Notifications

Potential:

- participant suspended
- athlete retired
- registry conflict/import issue

Operational competition notifications belong elsewhere.

---

# 24. Analytics

Track:

```text
participant_created
athlete_profile_created
participant_suspended
participant_retired
participant_imported
```

---

# 25. Seed Data

Seed:

- Football individual athletes linked to people
- Basketball athletes
- Rugby athletes
- Golf individual participants
- Athletics individual participants
- MMA/boxing individuals
- Team participants linked to teams
- Retired athlete
- Suspended participant
- Multiple sport profiles for one person where valid

---

# 26. Error Catalogue

```text
PARTICIPANT_NOT_FOUND
PARTICIPANT_TYPE_INVALID
PARTICIPANT_SPORT_INCOMPATIBLE
ATHLETE_PERSON_REQUIRED
TEAM_REFERENCE_REQUIRED
PARTICIPANT_EXTERNAL_ID_EXISTS
PARTICIPANT_ARCHIVED
PARTICIPANT_ALREADY_RETIRED
```

---

# 27. Edge Cases

- Same person competes in two sports
- Same person has multiple sport variants
- Retired athlete returns
- Participant suspended in one context but historically active elsewhere
- Composite team exists only for one tournament
- Relay team composition changes between competitions
- Athlete changes public display name while person legal name remains

---

# 28. Background Jobs

Mostly projection/search jobs:

- search index update
- public participant projection
- optional external ID synchronization

---

# 29. Performance

Participant registry can grow large.

Indexes:

```text
tenant_id
participant_type
sport_id
sport_variant_id
status
display_name_normalized
external_id
```

Use paginated search/read models.

---

# 30. Observability

Track:

- participant creation failures
- person-resolution conflicts
- import mismatches
- external ID conflicts
- search latency

---

# 31. Tests

- Individual participant creation
- Team participant linkage
- Multi-sport person
- Sport compatibility
- Public/private field separation
- Tenant isolation
- Archive/history
- External IDs
- Imports
- Retired/suspended states

---

# 32. Acceptance Criteria

1. Create a person.
2. Create football athlete profile linked to that person.
3. Create football participant.
4. Create second sport profile for same person.
5. Verify no duplicate person created.
6. Link team participant to Team Registry.
7. Register participant into competition downstream.
8. Retire athlete.
9. Confirm historical matches remain.
10. Verify public profile contains only approved fields.
11. Verify cross-tenant isolation.

---

# 33. Definition of Done

Complete when every competition-facing individual/team participant can be resolved through one canonical participant abstraction without duplicating person or team master data.
