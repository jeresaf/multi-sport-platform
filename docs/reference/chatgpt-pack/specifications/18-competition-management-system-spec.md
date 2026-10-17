# Competition Management System — Full Specification

## 1. System Identity

**Name:** Competition Management System  
**Short Code:** `COMPETITION`

### Purpose

Own the persistent identity and high-level administration of competitions independently of any specific season/edition.

### Business Outcome

A competition such as `Uganda Premier League`, `Kampala Sevens`, or `Lake Victoria Open` can exist across multiple editions without duplicating its identity or history.

---

# 2. Scope

## In Scope

- Competition master identity
- Competition type/category
- Sport/variant association
- Governing organisation
- Public identity
- Default branding/presentation references
- Default venue/region metadata
- Competition lifecycle
- Competition aliases/external IDs
- Competition-level public visibility
- Edition list/history

## Out of Scope

- Edition-specific dates
- Participants
- Fixtures
- Results
- Ruleset version
- Registration windows
- Stage instances
- Standings
- Timeline/play-by-play

---

# 3. Ubiquitous Language

| Term | Definition |
|---|---|
| Competition | Persistent sporting property/identity |
| Edition | Operational occurrence/season of a competition |
| Competition Type | Classification such as league, tournament, cup, meet, event |
| Governing Organisation | Organisation responsible for competition |
| Competition Alias | Historic/alternative competition name |

---

# 4. Dependencies

Upstream:
- Tenant Management
- Sports Definition
- Organisation Registry
- Branding
- Audit

Downstream:
- Competition Edition
- Public Search
- Historical Records
- Sponsorship
- Reporting

---

# 5. Core Entity

```text
Competition
```

Suggested fields:

```text
id
tenant_id
sport_id
sport_variant_id
governing_organisation_id
code
name
short_name
description
competition_type
country_code
region
logo_asset_id
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
DRAFT
ACTIVE
INACTIVE
SUSPENDED
ARCHIVED
```

Edition lifecycle is separate.

---

# 7. Business Rules

## RULE-CMP-001
A Competition is persistent across seasons/editions.

## RULE-CMP-002
A competition sport/variant cannot be changed after published editions exist without explicit migration strategy.

## RULE-CMP-003
Historical competition identity cannot be hard-deleted.

## RULE-CMP-004
Edition-specific rules and dates must not be stored on Competition.

## RULE-CMP-005
Duplicate detection should consider normalized name, sport, governing organisation, location, aliases, and external IDs.

---

# 8. Commands

```text
competition.create
competition.update
competition.activate
competition.deactivate
competition.suspend
competition.archive
competition.alias.add
competition.external_id.add
competition.public_profile.update
```

---

# 9. Queries

```text
List competitions
Search competitions
Get competition
Get public competition
Get editions
Get active edition
Get history
```

---

# 10. Events

```text
CompetitionCreated
CompetitionUpdated
CompetitionActivated
CompetitionSuspended
CompetitionArchived
```

---

# 11. Permissions

```text
competition.read
competition.create
competition.update
competition.lifecycle.manage
competition.public_profile.manage
```

---

# 12. Entitlements

Possible:

```text
COMPETITION_MANAGEMENT
MAX_COMPETITIONS
MAX_ACTIVE_COMPETITIONS
```

---

# 13. UI

Competition Directory  
Create Competition  
Competition Detail  
Edit Identity  
Edit Public Profile  
Edition History

Tabs:

```text
Overview
Editions
Teams/Participants
Statistics
History
```

Edition-specific operational tabs belong to Edition.

---

# 14. Seed/Test

Seed persistent competitions with multiple editions and one archived competition.

---

# 15. Definition of Done

Complete when a competition can exist as a stable identity across many editions without storing season-specific operational data.
