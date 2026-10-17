# Action, Permission & Entitlement Registry

This registry is the starting catalogue. The codebase should keep these identifiers centralized and documented. Additions require specification and registry updates.

## Authorization Formula

```text
Tenant lifecycle allows operation
AND Tenant entitlement allows capability
AND User has permission
AND Domain rule/precondition passes
= Action allowed
```

## Core Action Registry

| Action Code | Owning Module | Typical Permission | Typical Entitlement | Audit |
|---|---|---|---|---|
| tenant.create | Tenant | tenant.create | PLATFORM_ADMIN | Required |
| tenant.update | Tenant | tenant.update | — | Required |
| tenant.suspend | Tenant | tenant.lifecycle.manage | — | Required + reason |
| tenant.restore | Tenant | tenant.lifecycle.manage | — | Required |
| tenant.archive | Tenant | tenant.lifecycle.manage | — | Required + reason |
| tenant.domain.add | Tenant | tenant.domain.manage | CUSTOM_DOMAIN | Required |
| tenant.sport.enable | Tenant | tenant.sports.manage | sport must be entitled | Required |
| plan.manage | Entitlements | plan.manage | PLATFORM_ADMIN | Required |
| subscription.assign | Entitlements | subscription.manage | PLATFORM_ADMIN | Required |
| entitlement.override | Entitlements | entitlement.override | PLATFORM_ADMIN | Required |
| sport.create | Sports | sport.create | SPORTS_CONFIGURATION/CUSTOM_SPORTS | Required |
| sport.definition.publish | Sports | sport.definition.publish | SPORTS_CONFIGURATION | Required |
| competition_format.create | Formats | competition_format.create | FORMAT_CONFIGURATION | Required |
| competition_format.publish | Formats | competition_format.publish | FORMAT_CONFIGURATION | Required |
| ruleset.create | Rulesets | ruleset.create | CUSTOM_RULESETS if tenant custom | Required |
| ruleset.publish | Rulesets | ruleset.publish | CUSTOM_RULESETS | Required |
| person.create | Person | person.create | PERSON_REGISTRY | Required |
| person.update | Person | person.update | PERSON_REGISTRY | Required |
| person.merge | Person | person.merge | ADVANCED_DUPLICATE_DETECTION/FEDERATION_REGISTRY | Required + reason |
| participant.create | Participant | participant.create | PARTICIPANT_REGISTRY | Required |
| organisation.create | Organisation | organisation.create | ORGANISATION_REGISTRY | Required |
| team.create | Team | team.create | TEAM_REGISTRY/MAX_TEAMS | Required |
| staff.create | Staff | staff.create | TEAM_MANAGEMENT | Required |
| official.create | Official | official.create | OFFICIAL_MANAGEMENT | Required |
| venue.create | Venue | venue.create | VENUE_MANAGEMENT | Required |
| membership.create | Membership | membership.create | TEAM_MANAGEMENT | Required |
| membership.release | Membership | membership.lifecycle.manage | TEAM_MANAGEMENT | Required |
| transfer.create | Transfer | transfer.create | TRANSFER_MANAGEMENT | Required |
| transfer.approve | Transfer | transfer.approve | TRANSFER_MANAGEMENT | Required + decision |
| roster.create | Roster | roster.create | ROSTER_MANAGEMENT | Required |
| roster.manage | Roster | roster.manage | ROSTER_MANAGEMENT | Required |
| roster.approve | Roster | roster.approve | ROSTER_MANAGEMENT | Required |
| competition.create | Competition | competition.create | MAX_COMPETITIONS | Required |
| competition.update | Competition | competition.update | COMPETITION_MANAGEMENT | Required |
| edition.create | Edition | edition.create | MAX_ACTIVE_COMPETITIONS | Required |
| edition.publish | Edition | edition.publish | COMPETITION_MANAGEMENT | Required |
| registration.create | Registration | registration.create | REGISTRATION_MANAGEMENT | Required |
| registration.submit | Registration | registration.submit | REGISTRATION_MANAGEMENT | Required |
| registration.approve | Registration | registration.approve | REGISTRATION_MANAGEMENT | Required |
| eligibility.evaluate | Eligibility | eligibility.evaluate | COMPETITION_MANAGEMENT | Optional |
| eligibility.override | Eligibility | eligibility.override | ADVANCED_ELIGIBILITY | Required + reason |
| stage.instantiate | Stage | stage.structure.manage | COMPETITION_MANAGEMENT | Required |
| stage.draw.run | Stage | stage.draw.manage | COMPETITION_MANAGEMENT | Required |
| fixture.generate | Scheduling | fixture.generate | SCHEDULING | Required |
| fixture.create | Scheduling | fixture.create | SCHEDULING | Required |
| fixture.reschedule | Scheduling | fixture.reschedule | SCHEDULING | Required + reason |
| fixture.postpone | Scheduling | fixture.postpone | SCHEDULING | Required + reason |
| fixture.publish | Scheduling | fixture.publish | SCHEDULING | Required |
| lineup.create | Lineup | lineup.create | ROSTER_MANAGEMENT/LIVE_SCORING | Required |
| lineup.submit | Lineup | lineup.submit | LIVE_SCORING | Required |
| lineup.approve | Lineup | lineup.approve | LIVE_SCORING | Required |
| contest.start | Contest Ops | contest.start | LIVE_SCORING | Required |
| contest.interrupt | Contest Ops | contest.interrupt | LIVE_SCORING | Required + reason |
| contest.suspend | Contest Ops | contest.suspend | LIVE_SCORING | Required + reason |
| contest.end | Contest Ops | contest.end | LIVE_SCORING | Required |
| contest.reopen | Contest Ops | contest.reopen | LIVE_SCORING | Required + reason |
| score.apply_action | Live Scoring | contest.score | LIVE_SCORING | Required |
| score.correct | Live Scoring | contest.score.correct | LIVE_SCORING | Required |
| timeline.event.create | Timeline | contest.timeline.manage | LIVE_SCORING | Required |
| timeline.event.correct | Timeline | contest.timeline.correct | LIVE_SCORING | Required |
| timeline.event.reverse | Timeline | contest.timeline.correct | LIVE_SCORING | Required |
| result.submit | Results | result.submit | LIVE_SCORING | Required |
| result.approve | Results | result.approve | RESULT_APPROVAL | Required |
| result.reject | Results | result.reject | RESULT_APPROVAL | Required + reason |
| result.correct | Results | result.correct | RESULT_APPROVAL | Required + reason |
| result.reopen | Results | result.reopen | RESULT_APPROVAL | Required + reason |
| progression.recalculate | Progression | progression.manage | COMPETITION_MANAGEMENT | Required |
| progression.override | Progression | progression.override | ADVANCED_COMPETITION_ADMIN | Required + reason |
| report.run | Reporting | report.run | BASIC_REPORTS/ADVANCED_REPORTS | Logged |
| import.execute | Import | import.execute | HISTORICAL_BACKFILL/BULK_IMPORT | Required |
| provider.sync | Provider | provider.manage | API_INTEGRATIONS | Required |
| brand.publish | Branding | brand.manage | CUSTOM_BRANDING | Required |
| sponsor.manage | Sponsorship | sponsor.manage | SPONSORSHIP | Required |

## Permission Naming Rules

Use lowercase dotted action-oriented permission codes.

Examples:

```text
competition.read
competition.create
competition.update
fixture.reschedule
contest.start
contest.score
result.approve
person.merge
```

Avoid role names inside permission codes.

## Entitlement Types

```text
BOOLEAN
INTEGER_QUOTA
DECIMAL_QUOTA
ENUM_LEVEL
RESOURCE_LIST
DATE_WINDOW
```

## Initial Entitlement Catalogue

```text
PERSON_REGISTRY
PARTICIPANT_REGISTRY
ORGANISATION_REGISTRY
TEAM_REGISTRY
MAX_TEAMS
MAX_PARTICIPANTS
MAX_COMPETITIONS
MAX_ACTIVE_COMPETITIONS
MAX_ENABLED_SPORTS
CUSTOM_SPORTS
CUSTOM_RULESETS
ADVANCED_RULESETS
REGISTRATION_MANAGEMENT
ROSTER_MANAGEMENT
ADVANCED_ELIGIBILITY
TRANSFER_MANAGEMENT
LOAN_MANAGEMENT
SCHEDULING
LIVE_SCORING
OFFLINE_SCORING
RESULT_APPROVAL
BASIC_REPORTS
ADVANCED_REPORTS
HISTORICAL_BACKFILL
BULK_IMPORT
API_ACCESS
API_INTEGRATIONS
CUSTOM_DOMAIN
CUSTOM_BRANDING
WHITE_LABEL
DEDICATED_MOBILE_APP
SPONSORSHIP
STORAGE_GB
```
