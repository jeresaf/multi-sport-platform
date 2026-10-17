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
