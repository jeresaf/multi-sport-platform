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
