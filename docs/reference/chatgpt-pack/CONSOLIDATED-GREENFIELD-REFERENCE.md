# Sports Platform — Consolidated Greenfield Reference

## Package README
# Sports Platform — Greenfield Implementation Pack

This is the implementation-control package for starting the Sports Platform from an **empty repository**.

It consolidates the 43 domain/system specifications produced during architecture design and adds the cross-cutting controls required to implement them safely.

## Package Structure

```text
specifications/
    01..43 source system specifications

control/
    canonical glossary
    entity ownership matrix
    system dependency matrix
    action/permission/entitlement registry
    domain event catalogue
    state machine catalogue
    database schema ownership map
    API contract standards
    error code standards
    architecture rules

implementation/
    repository structure
    greenfield roadmap
    Codex bootstrap execution plan
    first football vertical slice acceptance
    definition of ready/done

templates/
    canonical system specification template
    ADR template
```

## Architecture Starting Position

```text
Backend:
ASP.NET Core modular monolith

Database:
PostgreSQL, one database with logical schemas

Cache / transient coordination:
Redis

Web:
Next.js + TypeScript + Tailwind + TanStack Query + SignalR

Mobile:
Flutter + local SQLite/Drift + offline scorer queue

Infrastructure:
Docker Compose initially

Deferred until justified:
Microservices
Kubernetes
RabbitMQ
Elasticsearch/OpenSearch
Database-per-module
```

## Implementation Rule

Do not start by generating every domain module.

Start with:
1. repository controls
2. platform foundation
3. configuration systems
4. identity/registry systems
5. competition setup
6. one complete football vertical slice
7. use basketball/rugby/cricket to prove generic architecture
8. expand product features only after the vertical slice is sound

## Source-of-Truth Hierarchy

```text
Specifications
    ↓
Control documents
    ↓
ADRs for implementation decisions
    ↓
Code + migrations + tests
    ↓
Generated API/OpenAPI/event schemas
```

When code and specification disagree, do not silently choose one. Record the discrepancy and update the appropriate source deliberately.


# Control Documents


---

# File: 01-canonical-glossary.md

# Canonical Glossary / Ubiquitous Language

This glossary is authoritative for implementation naming. New code, APIs, database objects, events, and UI labels should prefer these terms unless a domain specification explicitly defines a narrower term.

| Term | Canonical Meaning | Must Not Be Used As |
|---|---|---|
| Platform | Entire multi-tenant sports product | A tenant |
| Tenant | Isolated customer organisation using the platform | Club, team, federation record |
| Person | Canonical human identity | Player record, staff assignment, official assignment |
| Athlete Profile | Sport-specific profile linked to Person | Canonical biodata |
| Participant | Entity capable of entering a competition | Always a person |
| Organisation | Long-lived sports organisation such as club, school, academy, federation | Team |
| Team | Persistent playing entity | Competition roster |
| Team Membership | Effective-dated relationship between athlete/participant and team | Competition roster |
| Staff Profile | Staff capability linked to Person | Team assignment |
| Official Profile | Sports-official capability linked to Person | Contest assignment |
| Venue | Persistent place where contests occur | Scheduled fixture |
| Sport | Broad discipline, e.g. Rugby | Competition |
| Sport Variant | Distinct form of sport, e.g. Rugby 7s / Rugby 15s | Ruleset |
| Sport Definition Version | Immutable structural definition of a sport variant | Competition-specific rules |
| Competition Format | Structural organisation of competition, e.g. round robin | Sport scoring rules |
| Ruleset | Versioned policy combining sport + format + competition-specific values | Sport definition |
| Competition | Persistent sporting property, e.g. Uganda Premier League | Season |
| Competition Edition | Specific operational occurrence/season | Competition master |
| Registration | Application/acceptance into an edition | Roster |
| Eligibility | Contextual determination that requirements are satisfied | Participant status |
| Stage | Edition-specific structural phase, e.g. Group Stage | Sport period |
| Group / Pool | Participant grouping inside a competition stage | Team |
| Round | Competition structural round | Sport period |
| Contest | Universal unit of sporting competition | Always "match" |
| Fixture | Scheduled/planned contest | Live contest state |
| Schedule Status | State of published/scheduled arrangement | Contest Status |
| Contest Status | Universal operational lifecycle of contest | Sport Phase |
| Sport Phase | Sport-specific phase of play, e.g. Second Half | Contest Status |
| Period | Structured segment of a contest | Competition round |
| Match Squad | Contest-specific selection from competition roster | Roster |
| Lineup | Contest-specific starters/positions/bench declaration | Team membership |
| Live Score | Provisional live score state | Official result |
| Timeline Event | Structured chronological event in contest | Audit event |
| Result | Official sporting outcome workflow | Live score |
| Progression | Derived standings/qualification/bracket advancement | Result |
| Statistic | Sporting metric | Platform analytics |
| Historical Record | Durable historical projection derived from authoritative sporting data | Operational aggregate |
| Read Model | Rebuildable presentation/query projection | Source of truth |
| External Identifier | Provider/federation/import identifier mapped to canonical entity | Internal primary key |
| Audit | Immutable record of meaningful system/user action | Application logs |
| Platform Analytics | Product usage telemetry | Sports statistics |
| Entitlement | Tenant-level commercial capability/quota | User permission |
| Permission | User authorization to perform action | Tenant subscription capability |
| Action Code | Canonical state-changing/business action identifier | UI button label |
| Seed | Deterministic initialization/test/demo data | Historical import |
| Historical Backfill | Controlled import of past sporting data | Software backlog |
| Branding | Tenant identity/theme | Sponsorship campaign |
| Sponsor | Commercial partner content/placement | Tenant brand |
| Archive | Historical retention state | Hard delete |
| Merge | Controlled consolidation of duplicate identities | Overwrite |
| Correction | New version/supersession preserving history | Silent edit |

## Naming Rules

1. Use `contest` in generic backend/domain code; UI may say Match, Game, Bout, Heat, Race, etc. via presentation metadata.
2. Use `person` for the human identity; use `athlete` or `player` only within sport-facing contexts.
3. Use `competition_edition` rather than overloading `season`.
4. Use `organisation` for persistent institutions and `team` for playing entities.
5. Use `roster` only for competition-specific eligible membership.
6. Use `match_squad` / `contest_squad` for contest-specific selection.
7. Use `result` only for official outcome workflow.
8. Never use `analytics` to mean sporting statistics.
9. Never use a generic `constants` bucket for domain-owned statuses or rules.
10. New terms require glossary update before widespread implementation.


---

# File: 02-entity-ownership-matrix.md

# Entity Ownership Matrix

This document identifies the source-of-truth module for major entities. Other modules may reference IDs or consume projections/events, but they must not directly mutate another module's aggregates.

| Entity / Concept | Owning Module | Referenced By | Mutation Rule |
|---|---|---|---|
| Country / Currency / Timezone / Unit | Reference Data | All modules | Foundation only |
| Sport | Sports Definition | Rulesets, Competitions, UI | Sports only |
| Sport Variant | Sports Definition | Rulesets, Teams, Participants | Sports only |
| Sport Definition Version | Sports Definition | Rulesets, Editions, Operations | Immutable after publish |
| Competition Format | Competition Format | Rulesets, Editions | Format module only |
| Competition Format Version | Competition Format | Rulesets, Editions | Immutable after publish |
| Ruleset Version | Ruleset Registry | Editions, Eligibility, Operations | Immutable after publish |
| Tenant | Tenant Management | All tenant modules | Tenant module only |
| Tenant Domain | Tenant Management | Branding/Public Routing | Tenant module only |
| Subscription | Subscription & Entitlements | Tenant lifecycle, authorization | Entitlements only |
| Entitlement Definition | Subscription & Entitlements | All capability checks | Entitlements only |
| Person | Person Registry | Athletes, Staff, Officials | Person Registry only |
| Person Alias | Person Registry | Search, imports | Person Registry only |
| Participant | Participant Registry | Registration, Contests | Participant Registry only |
| Athlete Profile | Participant Registry | Teams, Statistics | Participant Registry only |
| Organisation | Organisation Registry | Teams, Competitions | Organisation Registry only |
| Team | Team Registry | Memberships, Registration, Contests | Team Registry only |
| Staff Profile | Staff Registry | Team assignments | Staff Registry only |
| Official Profile | Official Registry | Scheduling, Contests | Official Registry only |
| Venue | Venue Registry | Teams, Scheduling | Venue Registry only |
| Team Membership | Team Membership | Rosters, History | Membership module only |
| Transfer Request | Transfer Management | Memberships, Rosters | Transfer module only |
| Competition Roster | Roster Management | Lineups, Eligibility | Roster module only |
| Roster Member | Roster Management | Lineups | Roster module only |
| Competition | Competition Management | Editions, Public UI | Competition module only |
| Competition Edition | Edition System | Registration, Stages, Scheduling | Edition module only |
| Competition Registration | Registration | Eligibility, Rosters | Registration module only |
| Eligibility Evaluation | Eligibility | Registration, Roster, Lineup | Eligibility module only |
| Competition Stage / Group / Round | Stage Instantiation | Scheduling, Progression | Stage module only |
| Scheduled Contest / Fixture Schedule | Scheduling | Contest Ops, Public UI | Scheduling only |
| Contest | Contest Operations | Scoring, Timeline, Result | Contest Ops owns operational state |
| Contest Status History | Contest Operations | Audit, History | Contest Ops only |
| Contest Period | Contest Operations | Score, Timeline | Contest Ops only |
| Match/Contest Squad | Lineup System | Contest Ops, Timeline | Lineup only |
| Starting Lineup | Lineup System | Timeline, Public UI | Lineup only |
| Live Score State | Live Scoring | Timeline, Read Models | Scoring only |
| Timeline Event | Timeline | Stats, Public UI | Timeline only |
| Contest Result / Result Version | Results & Approval | Progression, Stats | Results only |
| Standing / Bracket / Series State | Progression | Public UI | Derived by Progression |
| Sport Statistic / Aggregate | Sports Statistics | Public UI, History | Statistics only |
| Historical Record Projection | Historical Records | Public UI | Rebuildable projection |
| Public/Admin Read Model | Read Models | UI, Search | Rebuildable projection |
| Search Document | Search | Public/Admin search | Rebuildable index |
| Report Artifact | Reporting | Users | Reporting only |
| Import Job | Historical Backfill | Domain modules | Import module only |
| Provider Mapping / External ID | Provider Integration | All provider-aware modules | Provider integration / agreed registry |
| Notification Preference/Delivery | Notifications | UI | Notifications only |
| Sponsor / Campaign | Sponsorship | Public UI | Sponsorship only |
| Tenant Brand | Branding | UI / Notifications | Branding only |
| Audit Entry | Audit | Support, Reports | Append-only Audit |
| Analytics Event | Platform Analytics | Platform dashboards | Analytics only |
| Seed Definition / Scenario | Seed Tooling | Dev/Test | Seed tooling only |

## Mandatory Rule

A module may never update an owning module's table directly to simulate a business operation. It must call an approved application command/service or consume an integration contract.


---

# File: 03-system-dependency-matrix.md

# System Dependency Matrix

Legend:
- `R` = reads/reference
- `C` = sends command/application request
- `E` = consumes emitted events
- `P` = consumes projection
- `—` = no normal dependency

## High-Level Dependency Order

```text
Foundation
  ↓
Configuration
  ↓
Tenancy & Commercial
  ↓
Registries
  ↓
Team Operations
  ↓
Competition Management
  ↓
Competition Operations
  ↓
Information & Insights
  ↓
Integrations / Presentation
```

## Module Dependencies

| Module | Allowed Upstream Dependencies |
|---|---|
| Reference Data | None |
| Audit | Foundation infrastructure only |
| Data Governance | Cross-cutting policy, no business aggregate ownership |
| Sports Definition | Reference Data, Audit |
| Competition Format | Reference Data, Audit |
| Ruleset Registry | Sports Definition, Competition Format, Audit |
| Tenant Management | Reference Data, Audit, Identity boundary |
| Subscription & Entitlements | Tenant, Audit, Analytics/usage reads |
| Person Registry | Tenant, Reference Data, Audit |
| Participant Registry | Tenant, Person, Sports, Audit |
| Organisation Registry | Tenant, Venue reference, Audit |
| Team Registry | Tenant, Organisation, Sports, Venue, Audit |
| Staff Registry | Person, Sports, Tenant, Audit |
| Official Registry | Person, Sports, Tenant, Audit |
| Venue Registry | Tenant, Reference Data, Audit |
| Team Membership | Participant, Team, Sports, Tenant, Audit |
| Transfer Management | Membership, Team, Participant, Ruleset, Audit |
| Roster Management | Membership, Participant, Team, Edition, Ruleset, Eligibility, Audit |
| Competition | Tenant, Sports, Organisation, Branding reference, Audit |
| Competition Edition | Competition, Sports, Format, Ruleset, Tenant, Audit |
| Registration | Edition, Participant/Team, Eligibility, Audit |
| Eligibility | Ruleset, Person, Participant, Membership, Edition, Registration, Roster references, Audit |
| Stage Instantiation | Edition, Format, Ruleset, Registration/Participants, Audit |
| Scheduling | Edition, Stage, Ruleset, Participant, Venue, Official availability, Audit |
| Match Squad / Lineup | Roster, Eligibility, Contest reference, Ruleset, Sports, Audit |
| Contest Operations | Scheduling, Ruleset, Sports, Lineup, Official assignment, Audit |
| Live Scoring | Contest Ops, Sports, Ruleset, Lineup, Timeline contract, Audit |
| Timeline | Sports, Contest Ops, Lineup, Participant, Score, Audit |
| Results & Approval | Contest Ops, Score, Timeline, Ruleset, Officials, Audit |
| Progression | Edition, Stage, Format, Ruleset, Approved Results, Participants, Audit |
| Sports Statistics | Sports, Timeline, Results, Lineup, Participants, Teams, Edition |
| Historical Records | Competition, Results, Progression, Statistics, Memberships, Registries |
| Read Models | All authoritative domains via events/queries |
| Search | Read Models + approved registry projections |
| Reporting | Approved module queries/read models |
| Historical Backfill | Approved application commands across modules |
| Provider Integration | Provider adapters + approved application commands |
| Notifications | Domain events + Branding + Preferences + Entitlements |
| Sponsorship | Tenant, Competition references, Audit |
| Branding | Tenant, Entitlements, Audit |
| Platform Analytics | Product events only |
| Seed | Approved bootstrap/application commands |

## Forbidden Dependency Examples

- Person Registry → Team Membership
- Sports Definition → Competition Edition
- Competition Format → Live Scoring
- Search → transactional table mutation
- Read Models → authoritative domain mutation
- Statistics → result approval
- Reporting → direct domain-table update
- Historical Backfill → raw table insert that bypasses commands


---

# File: 04-action-permission-entitlement-registry.md

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


---

# File: 05-domain-event-catalogue.md

# Domain Event Catalogue

Event names are past-tense facts. Domain events represent facts inside the modular monolith; integration events may be published externally later.

## Event Envelope

Every event should support:

```text
event_id
event_name
event_version
occurred_at
tenant_id (nullable only for platform-global events)
correlation_id
causation_id
actor_id / actor_type where relevant
aggregate_type
aggregate_id
payload
```

## Configuration

```text
SportCreated
SportVariantCreated
SportDefinitionPublished
SportDefinitionDeprecated
CompetitionFormatCreated
CompetitionFormatVersionPublished
RulesetCreated
RulesetVersionPublished
```

## Tenancy / Commercial

```text
TenantCreated
TenantActivated
TenantUpdated
TenantSuspended
TenantRestored
TenantArchived
TenantDomainVerified
TenantSportEnabled
TenantSportDisabled

PlanCreated
TenantSubscriptionAssigned
TenantSubscriptionChanged
TenantSubscriptionRenewed
TenantSubscriptionExpired
TenantEntitlementChanged
TenantQuotaThresholdReached
```

## Registries

```text
PersonCreated
PersonUpdated
PersonArchived
PersonRestored
PersonDuplicateCandidateDetected
PersonMerged

ParticipantCreated
ParticipantUpdated
ParticipantSuspended
ParticipantRetired
AthleteProfileCreated

OrganisationCreated
OrganisationUpdated
OrganisationArchived

TeamCreated
TeamUpdated
TeamSuspended
TeamArchived

StaffProfileCreated
StaffSuspended
StaffRetired

OfficialCreated
OfficialAvailabilityChanged
OfficialSuspended
OfficialRetired

VenueCreated
VenueUpdated
VenueAvailabilityChanged
VenueArchived
```

## Team Operations

```text
TeamMembershipCreated
TeamMembershipActivated
TeamMembershipSuspended
TeamMembershipReleased
TeamMembershipTransferredOut

RosterCreated
RosterMemberAdded
RosterSubmitted
RosterApproved
RosterLocked
RosterReopened
RosterMemberEligibilityChanged

TransferRequested
TransferSubmitted
TransferApproved
TransferRejected
TransferCancelled
TransferCompleted
ParticipantTeamChanged
```

## Competition Management

```text
CompetitionCreated
CompetitionUpdated
CompetitionActivated
CompetitionArchived

CompetitionEditionCreated
RegistrationOpened
RegistrationClosed
CompetitionEditionActivated
CompetitionEditionCompleted
CompetitionEditionClosed
CompetitionEditionReopened
CompetitionEditionArchived

RegistrationCreated
RegistrationSubmitted
RegistrationApproved
RegistrationRejected
RegistrationWaitlisted
RegistrationWithdrawn
RegistrationDisqualified

EligibilityEvaluated
EligibilityPassed
EligibilityFailed
EligibilityChanged
EligibilityOverridden

StageStructureInstantiated
CompetitionDrawCompleted
StageParticipantsAssigned
StageStructurePublished
```

## Scheduling / Operations

```text
FixturesGenerated
SchedulePublished
ContestScheduled
ContestRescheduled
ContestPostponed
ContestCancelledBySchedule

LineupCreated
LineupSubmitted
LineupApproved
LineupLocked
LineupLateChanged

ContestStartingSoon
ContestStarted
ContestPeriodChanged
ContestInterrupted
ContestResumed
ContestSuspended
ContestEnded
ContestAbandoned
ContestClosed

ContestScoreChanged
ContestScoreCorrected
ContestScoreReconciled
ScoreConflictDetected

TimelineEventCreated
TimelineEventCorrected
TimelineEventReversed
TimelineVersionChanged

ResultSubmitted
ResultApproved
ResultRejected
ResultDisputed
ResultCorrected
ResultReopened

StandingsRecalculated
ParticipantQualified
BracketAdvanced
SeriesUpdated
StageCompleted
ProgressionOverridden
CompetitionCompleted
```

## Insights / Integration

```text
ContestStatisticsUpdated
ParticipantStatisticsUpdated
TeamStatisticsUpdated
CompetitionStatisticsRecalculated

HistoricalProjectionRebuilt
MilestoneCreated

ReadModelRebuilt
SearchIndexRebuilt
ReportCompleted
ImportCompleted
ImportPartiallyFailed
ProviderSyncCompleted
ProviderConflictDetected
NotificationDelivered
NotificationFailed
BrandPublished
SponsorCampaignActivated
```

## Event Rules

1. Consumers must be idempotent.
2. Event version changes require backward-compatibility strategy.
3. Events must not contain secrets.
4. Avoid embedding full aggregates unnecessarily.
5. Result/progression consumers must distinguish provisional from approved facts.
6. Read models/statistics should be rebuildable from authoritative sources.


---

# File: 06-state-machine-catalogue.md

# State Machine Catalogue

This catalogue centralizes lifecycle states so modules do not invent overlapping status semantics.

## Tenant

```text
PROVISIONING
→ ACTIVE
→ EXPIRING
→ GRACE
→ READ_ONLY
→ SUSPENDED
→ ARCHIVED
```

Recovery:
```text
EXPIRING / GRACE / READ_ONLY / SUSPENDED → ACTIVE
```

## Subscription

```text
DRAFT
→ ACTIVE
→ EXPIRING
→ GRACE
→ EXPIRED
```

Additional:
```text
ACTIVE → SUSPENDED
ACTIVE → CANCELLED
```

## Sport / Format / Ruleset Version

```text
DRAFT
→ PUBLISHED
→ DEPRECATED
→ ARCHIVED
```

Published versions are immutable.

## Person

```text
ACTIVE
INACTIVE
DECEASED
MERGED
ARCHIVED
```

## Participant / Staff / Official

```text
ACTIVE
INACTIVE
SUSPENDED
RETIRED
ARCHIVED
```

## Organisation

```text
ACTIVE
INACTIVE
SUSPENDED
DISSOLVED
ARCHIVED
```

## Team

```text
ACTIVE
INACTIVE
SUSPENDED
DISBANDED
ARCHIVED
```

## Venue

```text
ACTIVE
TEMPORARILY_UNAVAILABLE
INACTIVE
CLOSED
ARCHIVED
```

## Team Membership

```text
PENDING
→ ACTIVE
→ INACTIVE / SUSPENDED / TRANSFERRED_OUT / RELEASED / EXPIRED
```

## Transfer

```text
DRAFT
→ SUBMITTED
→ UNDER_REVIEW
→ APPROVED
→ COMPLETED
```

Alternate terminal:
```text
REJECTED
CANCELLED
```

## Competition

```text
DRAFT
→ ACTIVE
→ INACTIVE / SUSPENDED
→ ARCHIVED
```

## Competition Edition

```text
DRAFT
→ REGISTRATION_OPEN
→ REGISTRATION_CLOSED
→ SCHEDULED
→ ACTIVE
→ COMPLETED
→ UNDER_REVIEW
→ CLOSED
→ ARCHIVED
```

Alternate:
```text
CANCELLED
```

## Registration

```text
DRAFT
→ SUBMITTED
→ UNDER_REVIEW
→ APPROVED / REJECTED / WAITLISTED
```

Post decision:
```text
WITHDRAWN
DISQUALIFIED
```

## Eligibility

```text
PENDING
→ ELIGIBLE / INELIGIBLE / CONDITIONAL
→ EXPIRED
```

Override:
```text
OVERRIDDEN
```

## Competition Roster

```text
DRAFT
→ SUBMITTED
→ UNDER_REVIEW
→ APPROVED
→ LOCKED
→ CLOSED
```

Exception:
```text
LOCKED → REOPENED → APPROVED/LOCKED
```

## Roster Member

```text
PENDING
→ ELIGIBLE / INELIGIBLE / SUSPENDED / REMOVED / TRANSFERRED_OUT
```

## Lineup

```text
DRAFT
→ SUBMITTED
→ APPROVED
→ LOCKED
→ CLOSED
```

Exception:
```text
LOCKED → LATE_CHANGE
```

## Schedule

```text
DRAFT
→ PUBLISHED
→ RESCHEDULED / POSTPONED / CANCELLED
```

This is distinct from contest lifecycle.

## Contest

```text
NOT_STARTED
→ STARTING_SOON
→ LIVE
→ ENDED
→ CLOSED
```

Exceptional:
```text
NOT_STARTED → DELAYED / POSTPONED / CANCELLED
LIVE → INTERRUPTED / SUSPENDED / ABANDONED
INTERRUPTED → LIVE / SUSPENDED / ABANDONED
SUSPENDED → LIVE / POSTPONED / ABANDONED
```

## Result

```text
NONE
→ DRAFT
→ SUBMITTED
→ UNDER_REVIEW
→ APPROVED
```

Alternate:
```text
REJECTED
DISPUTED
```

After approval:
```text
APPROVED → CORRECTED
```

## Timeline Event

```text
ACTIVE
PENDING_REVIEW
CONFIRMED
CORRECTED
REVERSED
VOID
```

## Import Job

```text
CREATED
→ MAPPED
→ VALIDATED
→ DRY_RUN_COMPLETE
→ RUNNING
→ COMPLETED
```

Alternate:
```text
PARTIAL_FAILURE
FAILED
CANCELLED
```

## Report Run

```text
QUEUED
→ RUNNING
→ COMPLETED
```

Alternate:
```text
FAILED
EXPIRED
```


---

# File: 07-database-schema-ownership-map.md

# Database Schema Ownership Map

Initial deployment uses **one PostgreSQL database** with logical module boundaries. Do not create a database per module.

Recommended PostgreSQL schemas:

| Schema | Primary Modules |
|---|---|
| `foundation` | reference data, shared infrastructure metadata |
| `config` | sports definitions, formats, rulesets |
| `tenancy` | tenants, subscriptions, entitlements |
| `registry` | people, participants, organisations, teams, staff, officials, venues |
| `teamops` | memberships, rosters, transfers |
| `competition` | competitions, editions, registrations, eligibility, stages/groups |
| `operations` | schedules, contests, periods, lineups, scores, timelines, results |
| `progression` | standings, brackets, series, qualification, leaderboards |
| `insights` | sport statistics, historical projections |
| `presentation` | read models/search projection metadata |
| `integration` | imports, providers, external mappings, notification delivery |
| `commercial` | branding, sponsorship |
| `audit` | audit entries |
| `analytics` | product analytics |
| `seed` | optional seed metadata/version records only |

## Table Ownership Guidelines

Examples:

```text
config.sports
config.sport_variants
config.sport_definition_versions
config.competition_formats
config.competition_format_versions
config.rulesets
config.ruleset_versions

tenancy.tenants
tenancy.tenant_domains
tenancy.entitlement_definitions
tenancy.plans
tenancy.tenant_subscriptions
tenancy.tenant_entitlement_overrides

registry.persons
registry.person_aliases
registry.participants
registry.athlete_profiles
registry.organisations
registry.teams
registry.staff_profiles
registry.official_profiles
registry.venues

teamops.team_memberships
teamops.competition_rosters
teamops.roster_members
teamops.transfer_requests

competition.competitions
competition.competition_editions
competition.registrations
competition.eligibility_evaluations
competition.stages
competition.groups
competition.rounds
competition.stage_participants

operations.scheduled_contests
operations.schedule_history
operations.contests
operations.contest_status_history
operations.contest_periods
operations.contest_squads
operations.lineups
operations.score_states
operations.timeline_events
operations.timeline_event_actors
operations.results
operations.result_versions

progression.standings
progression.standing_rows
progression.brackets
progression.bracket_slots
progression.series
progression.qualification_decisions
progression.leaderboards

insights.contest_statistics
insights.participant_statistics
insights.team_statistics
insights.historical_snapshots

presentation.public_contest_cards
presentation.competition_views
presentation.match_views

integration.import_jobs
integration.import_issues
integration.providers
integration.external_identifiers
integration.provider_sync_runs
integration.notification_deliveries

commercial.tenant_brands
commercial.sponsors
commercial.sponsor_campaigns

audit.audit_entries
analytics.analytics_events
```

## Cross-Schema Rules

1. Foreign keys are allowed where they preserve integrity, but ownership remains explicit.
2. No module may use EF/repository code to modify another module's owned table.
3. Read-only reporting/query projections may join across schemas through approved query layer.
4. Migrations should be grouped by owning module.
5. Public/API DTOs must never be database entities.
6. Every tenant-owned table must have direct or provably unambiguous tenant ownership.
7. Published version tables should be immutable at application level.
8. Timeline/result/history tables must favor corrections/versioning over destructive updates.


---

# File: 08-api-contract-standards.md

# API Contract Standards

## 1. API Families

```text
/api/public/v1/...      Public read APIs
/api/admin/v1/...       Tenant/admin operations
/api/platform/v1/...    Platform-owner operations
/api/internal/v1/...    Internal application/service contracts if HTTP is ever required
```

Inside the modular monolith, prefer in-process application contracts over self-HTTP calls.

## 2. Resource Rules

- URLs use plural nouns.
- State changes with business meaning use explicit action endpoints where PATCH would hide workflow intent.
- Never expose persistence entities directly.
- Every tenant-scoped request derives tenant context from trusted authentication/domain context, not only request body.

Examples:

```text
POST /api/admin/v1/competitions
POST /api/admin/v1/editions/{id}/publish
POST /api/admin/v1/contests/{id}/start
POST /api/admin/v1/results/{id}/approve
```

## 3. IDs

Canonical IDs are opaque strings/UUID/ULID-style values selected by implementation standards.

Provider IDs appear only in external-identifier fields.

## 4. Pagination

Default cursor or stable page pagination contract:

```json
{
  "items": [],
  "page": {
    "nextCursor": null,
    "hasMore": false
  }
}
```

Use consistent filters/sorts across APIs.

## 5. Error Contract

```json
{
  "type": "https://errors.sportsplatform.local/CONTEST_NOT_READY",
  "code": "CONTEST_NOT_READY",
  "title": "Contest is not ready to start",
  "status": 409,
  "detail": "Required lineup approval is missing.",
  "correlationId": "...",
  "errors": []
}
```

Use RFC 7807/9457-style Problem Details semantics.

## 6. Concurrency

Mutable admin resources expose version/ETag or equivalent optimistic-concurrency token.

Conflict returns `409`.

## 7. Idempotency

Required for:
- live-scoring commands
- timeline creation
- import execution
- provider event ingestion
- externally retried commands

Header:

```text
Idempotency-Key
```

or canonical request field for offline devices.

## 8. Dates / Times

- API instants use ISO 8601 UTC offsets.
- Local display timezone comes from tenant/venue context.
- Date-only values are explicit date types.

## 9. Realtime

SignalR channels/groups should use stable scopes:

```text
tenant:{tenantId}
competition:{competitionId}
contest:{contestId}
participant:{participantId}
user:{userId}
```

Realtime messages include:
```text
type
entityId
version
occurredAt
payload
```

## 10. Versioning

Breaking public contract changes require API version strategy.
Internal event versions are independent from HTTP API versions.


---

# File: 09-error-code-standards.md

# Error Code Standards

## Format

```text
<DOMAIN>_<CONDITION>
```

Examples:

```text
TENANT_READ_ONLY
PERSON_DUPLICATE_POSSIBLE
ROSTER_LOCKED
CONTEST_NOT_READY
RESULT_ALREADY_APPROVED
```

## Prefixes

```text
REF_   Reference/Foundation
SPT_   Sports
FMT_   Competition Format
RLS_   Ruleset
TNT_   Tenant
ENT_   Entitlements
PER_   Person
PRT_   Participant
ORG_   Organisation
TEAM_  Team
STF_   Staff
OFF_   Official
VEN_   Venue
MEM_   Membership
ROS_   Roster
TRN_   Transfer
CMP_   Competition
EDN_   Edition
REG_   Registration
ELG_   Eligibility
STG_   Stage
SCH_   Scheduling
LIN_   Lineup
CONTEST_ Contest Operations
SCORE_ Live Scoring
TIMELINE_ Timeline
RESULT_ Results
PROGRESSION_ Progression
STAT_  Sports Statistics
SEARCH_ Search
REPORT_ Reporting
IMPORT_ Import
PROVIDER_ Provider
NOTIFICATION_ Notification
BRAND_ Branding
SPONSOR_ Sponsorship
AUDIT_ Audit
INTEGRITY_ Governance/Integrity
```

## HTTP Mapping Guidance

```text
400 validation / malformed command
401 unauthenticated
403 permission/entitlement denied
404 canonical entity not found
409 business state / concurrency conflict
422 semantically invalid domain request (optional consistent policy)
429 rate/quota limit where appropriate
500 unexpected internal failure
503 external provider temporarily unavailable
```

## Rules

1. Error code is stable; wording may improve.
2. UI logic may branch on code, never on message text.
3. Errors include correlation ID.
4. Sensitive internal details stay in logs, not public detail.
5. Domain errors are defined by owning module.
6. Never reuse one code for unrelated conditions.


---

# File: 10-architecture-rules.md

# Architecture Rules

These rules are mandatory for the greenfield repository.

## AR-001 — Modular Monolith First

The initial backend is one deployable ASP.NET Core modular monolith. Module boundaries are business boundaries, not microservices.

## AR-002 — No Cross-Module Table Mutation

A module cannot directly update another module's owned data.

## AR-003 — Source-of-Truth Ownership

Every entity has exactly one owning module.

## AR-004 — Configuration Over Sport Hard-Coding

Generic modules must not scatter conditions such as:

```csharp
if (sport == Football)
```

Sport Definition + Ruleset + renderer/adapter contracts provide specialization.

## AR-005 — Universal Contest

Generic backend code models `Contest`, not always `Match`.

## AR-006 — Version Published Configuration

Sport definitions, competition formats, and rulesets become immutable after publish.

## AR-007 — Preserve History

Use versioning, effective dates, corrections, reversals, merge records, and status history instead of destructive overwrite.

## AR-008 — Tenant Isolation Is Structural

Tenant filtering is enforced below controllers/UI and tested continuously.

## AR-009 — Permission ≠ Entitlement ≠ Domain Rule

All three are independently evaluated.

## AR-010 — Person Is Canonical Human Identity

Athletes, staff, and officials reference Person.

## AR-011 — Organisation ≠ Team

Organisations may operate many teams.

## AR-012 — Membership ≠ Roster ≠ Squad

These relationships remain separate.

## AR-013 — Schedule Status ≠ Contest Status ≠ Sport Phase

Never combine these into one enum.

## AR-014 — Live Score ≠ Official Result

Approved results own official truth; live score remains operational/provisional.

## AR-015 — Progression Uses Approved Results

Standings/brackets/qualification do not consume provisional scores as official state.

## AR-016 — Timeline Is Structured

Timeline events have codes, actors, sequence, correction semantics, and optional sport metadata.

## AR-017 — Imports Use Domain Commands

No direct-table historical import.

## AR-018 — Provider IDs Are Secondary

Internal canonical IDs remain stable regardless of provider.

## AR-019 — Read Models Are Rebuildable

Public/admin projections are not source of truth.

## AR-020 — Statistics Are Derived

Statistics consume authoritative sport data and can be recalculated.

## AR-021 — Audit Is Append-Only

Audit is not equivalent to application logging.

## AR-022 — No Generic Constants Dumping Ground

Domain-owned statuses/rules remain in their domains.

## AR-023 — Postgres One Database Initially

Use logical schemas/module ownership, not one database per module.

## AR-024 — Infrastructure Complexity Must Be Earned

Do not introduce Kubernetes, RabbitMQ, Elasticsearch, or microservices without measured need and ADR.

## AR-025 — Shared Contracts Are Minimal

Do not create a giant common/shared project containing business logic.

## AR-026 — Tests Enforce Architecture

Architecture tests must prevent forbidden dependencies and tenant leaks.

## AR-027 — API Contracts Are Stable DTOs

Persistence entities do not cross API boundary.

## AR-028 — Offline Scoring Is Idempotent

Client-generated event IDs and sequence/version conflict logic are mandatory.

## AR-029 — UI Uses Presentation Metadata

Public/admin clients consume generic renderer/presentation contracts rather than hard-coded sport navigation behavior.

## AR-030 — ADR for Boundary Changes

Any change to module ownership or major technology choice requires an Architecture Decision Record.


# Implementation Documents


---

# File: 01-repository-structure.md

# Greenfield Repository Structure

Recommended monorepo:

```text
sports-platform/
├── README.md
├── LICENSE
├── .editorconfig
├── .gitattributes
├── .gitignore
├── global.json
├── Directory.Build.props
├── docker-compose.yml
│
├── docs/
│   ├── architecture/
│   ├── specifications/
│   ├── api/
│   ├── adr/
│   └── ui/
│
├── backend/
│   ├── SportsPlatform.sln
│   ├── src/
│   │   ├── Host/
│   │   │   └── SportsPlatform.Api/
│   │   ├── BuildingBlocks/
│   │   │   ├── Domain/
│   │   │   ├── Application/
│   │   │   ├── Infrastructure/
│   │   │   └── Observability/
│   │   └── Modules/
│   │       ├── Sports/
│   │       ├── CompetitionFormats/
│   │       ├── Rulesets/
│   │       ├── Tenancy/
│   │       ├── Entitlements/
│   │       ├── People/
│   │       ├── Participants/
│   │       ├── Organisations/
│   │       ├── Teams/
│   │       ├── Staff/
│   │       ├── Officials/
│   │       ├── Venues/
│   │       ├── Memberships/
│   │       ├── Rosters/
│   │       ├── Transfers/
│   │       ├── Competitions/
│   │       ├── Registrations/
│   │       ├── Eligibility/
│   │       ├── Stages/
│   │       ├── Scheduling/
│   │       ├── Lineups/
│   │       ├── Contests/
│   │       ├── Scoring/
│   │       ├── Timeline/
│   │       ├── Results/
│   │       ├── Progression/
│   │       ├── Statistics/
│   │       ├── History/
│   │       ├── ReadModels/
│   │       ├── Search/
│   │       ├── Reporting/
│   │       ├── Imports/
│   │       ├── Providers/
│   │       ├── Notifications/
│   │       ├── Branding/
│   │       ├── Sponsorship/
│   │       ├── Audit/
│   │       └── Analytics/
│   └── tests/
│       ├── Unit/
│       ├── Integration/
│       ├── Architecture/
│       └── Contract/
│
├── web/
│   ├── package.json
│   ├── apps/
│   │   ├── public-web/
│   │   └── admin-web/
│   └── packages/
│       ├── ui/
│       ├── sports-ui/
│       ├── api-client/
│       ├── design-tokens/
│       └── config/
│
├── mobile/
│   └── sports_platform/
│       ├── lib/
│       ├── test/
│       └── integration_test/
│
├── contracts/
│   ├── openapi/
│   ├── events/
│   └── schemas/
│
├── tools/
│   ├── seed/
│   ├── import/
│   ├── scripts/
│   └── codegen/
│
└── infra/
    ├── docker/
    ├── postgres/
    ├── redis/
    ├── observability/
    └── deployment/
```

## Backend Module Internal Shape

Use a consistent module structure:

```text
Modules/People/
├── People.Domain/
├── People.Application/
├── People.Infrastructure/
├── People.Contracts/
└── People.Tests/
```

For smaller modules, physical projects may be combined if architecture boundaries remain enforceable. Do not create dozens of assemblies merely for aesthetics.

## Web

Recommended:
- Next.js
- TypeScript
- Tailwind
- TanStack Query
- SignalR client
- Playwright
- Storybook

The public and admin web apps may share UI packages but should not share route-specific business state indiscriminately.

## Mobile

Recommended:
- Flutter
- Riverpod or Bloc
- Drift/SQLite
- FCM
- Offline scorer queue

## Initial Infrastructure

Required initially:
- PostgreSQL
- Redis
- Docker Compose
- structured logging
- health checks
- OpenTelemetry-compatible tracing/metrics

Deferred:
- RabbitMQ
- Kubernetes
- Elasticsearch/OpenSearch
- multi-database decomposition


---

# File: 02-greenfield-implementation-roadmap.md

# Greenfield Implementation Roadmap

The repository starts empty. The goal is to produce a working vertical slice early while preserving the final architecture.

# Phase 0 — Repository & Architecture Control

Deliver:
- monorepo structure
- docs copied into repository
- architecture rules
- ADR template
- action registry
- error standards
- CI
- formatting/linting
- branch/build conventions

Exit criteria:
- CI runs on empty skeleton
- architecture docs versioned
- solution/apps compile

# Phase 1 — Backend Platform Foundation

Deliver:
- ASP.NET Core host
- module registration pattern
- PostgreSQL
- EF Core
- Redis
- Docker Compose
- configuration/secrets conventions
- Problem Details
- correlation IDs
- structured logging
- health/readiness endpoints
- OpenTelemetry baseline
- auth abstraction
- tenant context
- permission/entitlement evaluation interfaces
- audit interface
- idempotency building block
- outbox/domain-event baseline if selected

Exit criteria:
- API boots from Docker/local
- DB migrates
- Redis reachable
- health checks pass
- tenant context test passes
- integration-test harness established

# Phase 2 — Configuration Foundation

Implement:
1. Sports Definition
2. Competition Format
3. Ruleset Registry

Seed:
- Football
- Basketball
- Rugby 15s
- Rugby 7s
- Cricket T20
- baseline format templates
- demo rulesets

Exit criteria:
- configuration versions publish immutably
- compatibility validation works
- admin API can retrieve effective configuration

# Phase 3 — Tenancy & Identity

Implement:
- Tenant
- Subscription & Entitlements
- Person
- Participant/Athlete
- Organisation
- Team
- Staff
- Official
- Venue

Exit criteria:
- two tenants isolated
- same person can be athlete + staff + official
- duplicate-person warning works
- organisation/team separation demonstrated

# Phase 4 — Team Operations

Implement:
- Team Membership
- Roster
- Transfer capability

Exit criteria:
- membership history preserved
- competition roster distinct from membership
- transfer updates memberships transactionally
- historical roster unaffected

# Phase 5 — Competition Setup

Implement:
- Competition
- Edition
- Registration
- Eligibility
- Stage/Group instantiation

Exit criteria:
- tenant admin can create football league edition
- approve teams
- build competition rosters
- instantiate league/group structure

# Phase 6 — Scheduling & Pre-Match

Implement:
- Scheduling
- Official assignment
- Match squad/lineup
- readiness checks

Exit criteria:
- fixtures generated/manual
- venues assigned
- reschedule history works
- lineup validation works

# Phase 7 — First Full Vertical Slice: Football

Implement:
- Contest Operations
- Live Scoring
- Timeline
- Result Approval
- Progression
- basic Statistics
- Read Models

Acceptance:
```text
Create league
→ register teams
→ roster players
→ generate fixture
→ assign officials
→ submit lineup
→ start match
→ score goal
→ add assist/card/substitution
→ end match
→ submit result
→ approve result
→ standings update
→ public UI shows result/timeline/table
```

This is the first production-shaped milestone.

# Phase 8 — Public & Admin UI Foundations

Admin:
- navigation
- dashboards
- entity directories
- forms
- competition wizard
- match operations/scorer

Public:
- home/live
- competition detail
- match detail
- team/player pages
- standings

Shared:
- design tokens
- renderer registry
- loading/error/empty states
- responsive tablet/mobile/desktop

# Phase 9 — Multi-Sport Architecture Test

Add:
- Basketball
- Rugby 15s
- Rugby 7s
- Cricket T20

The objective is not only features; it is proving generic abstractions.

No core redesign should be required to support:
- quarters/overtime
- rugby variants/bonus points
- pools+knockout
- complex cricket score/innings/ball-by-ball

# Phase 10 — Extended Sports

Add:
- Golf
- Athletics
- MMA
- Boxing
- Volleyball
- Netball
- Hockey
- Tennis

# Phase 11 — Information & Data Operations

Implement:
- Statistics expansion
- Historical Records
- Search
- Reporting
- Historical Backfill
- Provider Integration
- Notifications

# Phase 12 — Commercial Productization

Implement:
- Branding/White Label
- Sponsorship
- plan/entitlement UI
- quotas/usage
- custom domain workflows

# Phase 13 — Mobile/Offline Scorer Hardening

- local database
- pending event queue
- sync/retry
- conflict UX
- score reconciliation
- reconnect replay
- offline E2E

# Phase 14 — Production Hardening

- load tests
- security review
- tenant isolation audit
- backup/restore
- disaster recovery runbook
- monitoring/alerts
- retention
- privacy/export/delete policies
- performance tuning

# Deferred Until Proven Necessary

Do not introduce prematurely:
- microservices
- Kubernetes
- RabbitMQ
- Elasticsearch/OpenSearch
- separate databases per module
- event sourcing for the entire platform


---

# File: 03-codex-bootstrap-execution-plan.md

# Codex Bootstrap Execution Plan

This is the first implementation sequence for an empty repository.

## Commit 001 — Repository Skeleton

Create:
- root README
- .editorconfig
- .gitattributes
- .gitignore
- global.json
- Directory.Build.props
- backend solution
- web workspace
- Flutter app placeholder
- docs directories
- CI placeholder

Do not implement business modules yet.

Acceptance:
- backend builds
- web builds
- Flutter analyzes
- CI succeeds

## Commit 002 — Local Infrastructure

Create:
- docker-compose.yml
- PostgreSQL
- Redis
- environment sample files
- local setup docs

Acceptance:
- one command starts dependencies
- health connectivity tests pass

## Commit 003 — ASP.NET Core Host

Create:
- API host
- configuration
- dependency injection conventions
- Problem Details
- correlation middleware
- structured logging
- `/health/live`
- `/health/ready`

Acceptance:
- integration tests boot real host

## Commit 004 — Persistence Building Blocks

Create:
- EF Core baseline
- migration conventions
- PostgreSQL schemas
- optimistic-concurrency convention
- tenant-aware entity interfaces
- created/updated audit fields

Do not create domain tables not yet required.

## Commit 005 — Module Architecture

Create:
- module interface
- module registration
- architecture tests
- forbidden-reference tests
- module-owned migration registration pattern

Acceptance:
- sample module proves boundary enforcement

## Commit 006 — Identity/Tenant Context Interfaces

Create:
- authenticated actor abstraction
- tenant context
- tenant resolver
- platform vs tenant actor distinction
- test authentication scheme

Acceptance:
- cross-tenant integration test fails correctly

## Commit 007 — Authorization Building Blocks

Create:
- ActionCode
- Permission check interface
- Entitlement check interface
- Domain authorization result
- policy evaluation pipeline

Do not hard-code plan logic into controllers.

## Commit 008 — Audit

Implement append-only Audit module and shared recording interface.

Acceptance:
- sample action writes audit with correlation ID
- sensitive metadata redaction test

## Commit 009 — Domain Event Infrastructure

Implement:
- event envelope
- in-process dispatcher
- idempotent consumer conventions
- optional outbox table only if needed for reliability boundary

Do not introduce RabbitMQ.

## Commit 010 — Seed Framework

Implement:
- seed categories
- stable seed keys
- environment guard
- idempotent runner
- destructive reset guard

## Commit 011+ — Configuration Modules

Then implement, in order:
1. Sports Definition
2. Competition Format
3. Ruleset Registry

Every module task must include:
- domain model
- migration
- command handlers
- queries
- permissions
- audit
- tests
- seed
- API
- docs update

## Codex Working Rule

Never ask Codex to "build the sports platform" in one task.

Give bounded tasks such as:

```text
Implement Sports Definition System: version publication lifecycle.

Read:
- docs/specifications/03-sports-definition-system-spec.md
- docs/architecture/architecture-rules.md
- docs/architecture/entity-ownership-matrix.md
- docs/architecture/action-registry.md

Implement only:
- SportDefinitionVersion lifecycle
- publish command
- immutability rule
- audit
- tests

Do not implement Competition Format or Rulesets in this task.
```

This keeps changes reviewable and architecture-safe.


---

# File: 04-first-football-vertical-slice-acceptance.md

# First Vertical Slice Acceptance — Football League

The first end-to-end implementation target is a small football league because it exercises the largest useful portion of the architecture without requiring complex cricket-style scoring on day one.

## Scenario

Tenant:
```text
Uganda Sports Demo
```

Competition:
```text
Kampala Premier League
```

Edition:
```text
2027 Demo Season
```

Configuration:
```text
Sport Definition: Football Association published version
Format: Single Round Robin published version
Ruleset: Football League Demo published version
```

## Required Flow

1. Provision tenant.
2. Enable Football entitlement.
3. Create canonical people.
4. Create athlete profiles.
5. Create organisations/teams.
6. Create team memberships.
7. Create competition.
8. Create edition.
9. Open registration.
10. Register 4 teams.
11. Approve registrations.
12. Create/approve rosters.
13. Instantiate league stage.
14. Generate fixtures.
15. Assign venues.
16. Assign officials.
17. Publish schedule.
18. Create match squads.
19. Submit/approve lineups.
20. Pass readiness.
21. Start contest.
22. Start first half.
23. Record goal + scorer + assist.
24. Record yellow card.
25. Record substitution.
26. End first half.
27. Start second half.
28. Record second goal.
29. End contest.
30. Submit result.
31. Reconcile score/timeline.
32. Approve result.
33. Close contest.
34. Recalculate standings.
35. Public read model updates.
36. Public web shows:
    - fixture/result
    - score
    - timeline
    - lineups
    - competition table
37. Audit explorer can trace major admin/scorer actions.
38. Statistics show player goals/assists and team totals.

## Correction Scenario

39. Reopen approved result with authorized reason.
40. Correct an event/result.
41. Preserve old result version.
42. Re-approve.
43. Recalculate standings.
44. Public view reflects corrected official truth.
45. Audit and timeline history retain evidence of correction.

## Pass Condition

The vertical slice passes only if no step requires bypassing module boundaries or editing the database manually.


---

# File: 05-definition-of-ready-done.md

# Definition of Ready / Definition of Done

# Definition of Ready — Module Feature

A task is ready when:

- Owning module identified
- Specification section identified
- Source-of-truth entity identified
- Upstream dependencies identified
- Action code identified
- Permission identified
- Entitlement identified if applicable
- State transition/business rule defined
- Audit requirement known
- API contract direction known
- Error codes defined
- Acceptance criteria defined
- No unresolved ownership ambiguity

If these are missing, refine the specification before implementation.

# Definition of Done — Backend Feature

- Domain rule implemented
- Unit tests pass
- Integration tests pass
- Tenant isolation covered
- Permission/entitlement covered
- Migration added if needed
- Concurrency handled
- Audit emitted if required
- Domain event emitted if required
- API documented
- Error contract follows standard
- Seed/demo scenario updated if needed
- Architecture tests pass
- No cross-module data mutation
- Docs updated

# Definition of Done — UI Feature

- Uses canonical API contract
- Loading/empty/error states
- Permission/entitlement state
- Responsive mobile/tablet/desktop where applicable
- Accessibility labels/focus/touch targets
- Long-name/missing-image state
- Dark/light themes where applicable
- Tenant branding tested
- Realtime update behavior tested if applicable
- E2E path covered

# Definition of Done — Full Module

- Specification implemented or remaining gaps explicitly listed
- All commands/queries covered
- Lifecycle/state machine tested
- Audit/analytics integrated
- Imports supported where specified
- Seed data exists
- Public/admin projections exist where specified
- Observability exists
- Acceptance walkthrough passes
