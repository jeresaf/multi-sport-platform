# Multi-Sport Competition Platform — Consolidated Project Background

## 1. Product Vision

The system is not intended to be a football-only LiveScore clone.

The goal is to build a **configurable, multi-tenant, multi-sport competition management, live-scoring, results, and publishing platform** that can be leased to:

- National sports federations
- Leagues
- Tournament organisers
- Schools and universities
- Clubs
- Corporate competitions
- Golf tournaments
- Athletics meets
- Combat-sports promoters
- Short-term events
- Long-running competitions

The same platform should support public web, Android, iOS, administration, scorer/official workflows, and white-label deployments.

A new tenant should ideally be able to:

```text
Create organisation
→ Enable sport
→ Create competition
→ Configure format/rules
→ Register participants
→ Generate fixtures/stages
→ Assign officials
→ Run live scoring
→ Approve results
→ Publish standings/brackets/leaderboards
→ Archive or renew competition/lease
```

The core system must support new sports with **minimal code changes**.

---

# 2. Initial Flagship Sports

The initial flagship sports are:

- Football
- Basketball
- Rugby 15s
- Rugby 7s
- Cricket T20

The prototype should additionally support:

- Golf
- Athletics
- MMA
- Boxing
- Volleyball
- Netball
- Hockey
- Tennis

Optional later extensions:

- Motorsport
- Cycling
- Swimming
- Badminton
- Handball
- Other configured sports

---

# 3. Core Multi-Tenant Model

The commercial hierarchy is:

```text
PLATFORM
  └── TENANT
       └── COMPETITION
            └── SEASON / EDITION
                 └── STAGE
                      └── GROUP / ROUND
                           └── CONTEST
```

Examples:

```text
Tenant:
National Basketball Federation

Competition:
National Basketball League

Season:
2026
```

or:

```text
Tenant:
Kampala Fight Promotions

Competition:
Kampala Fight Night

Edition:
Fight Night 12
```

Each tenant needs:

- Branding
- Administrators
- Enabled sports
- Competitions
- Participants
- Sponsors
- Subscription/lease
- Reports
- Public presentation
- Strict data isolation

Every tenant-owned entity should be tenant-scoped.

---

# 4. Universal Contest Model

Do not model the entire platform around:

```text
Team A vs Team B
```

The core concept is a generic:

```text
Contest
```

A contest may contain:

- Two teams
- Two athletes
- Several athletes
- Several teams
- A race field
- A golf field
- A fight
- A heat
- A cricket match

Supported contest shapes include:

```text
HEAD_TO_HEAD_TEAM
HEAD_TO_HEAD_INDIVIDUAL
MULTI_PARTICIPANT_RANKED
RACE
HEAT
SERIES
FIGHT_CARD_BOUT
COMPLEX_SCORE
```

Examples:

```text
Arsenal vs Manchester City
Lakers vs Pistons
McGregor vs Holloway
24 golfers in a tournament round
8 athletes in a 100m heat
A cricket match
```

The core system must never assume:

- Exactly two teams
- A single integer score
- Higher score always wins
- A league table always exists
- Minutes are always used
- A competition lasts a season

---

# 5. Universal Competition Hierarchy

Use:

```text
Competition
  └── Edition / Season
       └── Stage
            ├── Group
            ├── Round
            ├── Bracket
            └── Contest
```

Competition lifecycle:

```text
DRAFT
REGISTRATION_OPEN
REGISTRATION_CLOSED
SCHEDULED
ACTIVE
COMPLETED
UNDER_REVIEW
CLOSED
ARCHIVED
CANCELLED
```

This lifecycle is separate from individual contest/game status.

---

# 6. Competition Formats

The platform should support reusable competition-format engines.

Required:

```text
Single round robin
Double round robin
Multi-round round robin
Single elimination
Two-leg knockout
Group + knockout
Best-of-3
Best-of-5
Best-of-7
Multi-round leaderboard
Heat → semifinal → final
Pool + knockout
Fight card
```

Optional future:

```text
Swiss system
Conference/division
Stage race
Classification events
```

A sport and a competition format are not the same thing.

Example:

```text
Football + league
Football + knockout
Football + group/knockout

Basketball + league/playoffs

Rugby 7s + pools/knockout

Golf + multi-round leaderboard
```

---

# 7. Rules Must Be Configuration-Driven

Avoid shared-core logic such as:

```text
if sport == football
    winner gets 3 points
```

Instead use versioned rulesets.

Rulesets may configure:

- Win points
- Draw points
- Loss points
- Bonus points
- Tie breakers
- Qualification rules
- Aggregate scoring
- Extra time
- Penalties
- Overtime
- Cut rules
- Ranking direction
- Walkovers
- Forfeits
- Disqualifications
- Match/period durations

Every active competition references an **immutable ruleset version**.

Updating a sport template must not silently change an active or historical competition.

---

# 8. Recommended Backend Architecture

The recommended main backend is:

```text
ASP.NET Core
C#
PostgreSQL
Entity Framework Core
Redis
SignalR
Hangfire
RabbitMQ only when genuinely required
OpenTelemetry
Serilog
Docker
```

Architecture:

```text
Modular Monolith
```

Do not start with microservices or Kubernetes.

Suggested modules:

```text
Identity
Tenancy
Subscriptions
Branding
Sports
Participants
Registrations
Competitions
Scheduling
Contests
LiveScoring
Results
Standings
Brackets
Leaderboards
Officials
Notifications
Sponsorship
Reporting
Audit
```

Specialised services can later be extracted if actual scale requires it.

Go/Elixir/etc. may later be used for specialised high-concurrency services, but the main product should remain cohesive initially.

---

# 9. Recommended Frontend Stack

## Web

```text
Next.js
TypeScript
Tailwind CSS
TanStack Query
SignalR client
Playwright
```

## Mobile

```text
Flutter
Dart
Riverpod or Bloc
Drift / SQLite
Firebase Cloud Messaging
```

The Flutter application should support both:

```text
Supporter mode
Scorer / Official mode
```

Tablet and mobile layouts must be deliberately designed rather than simply scaled versions of desktop.

---

# 10. Core Domain Entities

The approximate domain/database model is:

```text
sports
sport_variants
sport_rulesets

tenants
tenant_branding
tenant_subscriptions
tenant_features
tenant_users

competitions
competition_editions
competition_stages
competition_groups
competition_rounds

participants
teams
athletes
clubs

team_memberships
competition_registrations
competition_rosters
roster_members
contest_squads
contest_lineups

contests
contest_participants
contest_periods
contest_events
contest_statistics
contest_results

officials
contest_officials

venues
contest_schedule_history

standings
standing_rows
brackets
series
leaderboards

external_identifiers
entity_aliases
audit_logs
```

---

# 11. Clubs, Teams, Players and Participants

A universal:

```text
Participant
```

model is required.

Participant types may include:

```text
TEAM
ATHLETE
PAIR
CREW
RELAY_TEAM
CLUB
SCHOOL
COUNTRY
COMPOSITE_TEAM
```

A club and a team should be separate concepts.

Example:

```text
Kampala Sports Club
├── Senior Men's Football
├── Women's Football
├── U19 Football
├── Rugby 15s
└── Rugby 7s
```

Hierarchy:

```text
Club / Organisation
  ↓
Team
  ↓
Competition Registration
```

---

# 12. Player/Athlete History

Do not simply store:

```text
current_team_id
```

and overwrite it.

Use:

```text
Athlete
  ↓
Team Membership
  ↓
Competition Registration
  ↓
Competition Roster
  ↓
Match Squad
```

Team membership should preserve:

```text
start_date
end_date
membership_type
status
shirt_number
position
```

Possible membership states:

```text
ACTIVE
INACTIVE
LOAN
TRANSFERRED
SUSPENDED
RELEASED
```

---

# 13. Competition Registration

Existing in the database does not automatically mean participating in a competition.

Use:

```text
competition_registration
```

Workflow:

```text
DRAFT
SUBMITTED
UNDER_REVIEW
APPROVED
REJECTED
WAITLISTED
WITHDRAWN
DISQUALIFIED
```

Registration may validate:

- Minimum roster
- Maximum roster
- Age
- Weight category
- Required documents
- Registration deadline
- Federation approval
- Fee status
- Eligibility

---

# 14. Rosters and Match Squads

Use separate levels:

```text
Team Membership
  ↓
Competition Roster
  ↓
Match Squad
  ↓
Starting Lineup
  ↓
Bench / Replacements
```

Examples:

Football:

```text
25 registered
20 match squad
11 starters
9 bench
```

Basketball:

```text
15 registered
12 active
5 starting
```

Rugby 15s:

```text
Competition roster
Matchday squad
Starting XV
Replacements
```

Rugby 7s:

```text
Tournament roster
Match squad
Starting VII
Replacements
```

All limits come from rulesets.

---

# 15. Contest/Game Status Model

Separate three concepts:

```text
Contest Status
Sport Phase
Result Status
```

## Universal Contest Status

```text
NOT_STARTED
STARTING_SOON
LIVE
DELAYED
INTERRUPTED
SUSPENDED
POSTPONED
ENDED
CLOSED
CANCELLED
ABANDONED
```

Meanings:

```text
NOT_STARTED
scheduled but not started

STARTING_SOON
about to start

LIVE
actively being played

DELAYED
start delayed

INTERRUPTED
temporary stoppage

SUSPENDED
longer stoppage / future resumption

POSTPONED
moved to future date

ENDED
sporting action finished but result may not yet be approved

CLOSED
official result confirmed

CANCELLED
will not take place

ABANDONED
started but permanently stopped
```

---

# 16. Result Status

Keep result workflow separate:

```text
NONE
DRAFT
SUBMITTED
UNDER_REVIEW
APPROVED
CORRECTED
DISPUTED
REJECTED
```

Example:

```text
Contest = ENDED
Result = SUBMITTED
```

then:

```text
Contest = CLOSED
Result = APPROVED
```

---

# 17. Sport Phase Model

Phase is sport-specific.

Example:

```json
{
  "contestStatus": "LIVE",
  "phase": "SECOND_HALF",
  "clock": "67:23"
}
```

Do not create statuses such as:

```text
LIVE_SECOND_HALF
LIVE_Q3
```

---

# 18. Football Phases

```text
NOT_STARTED
FIRST_HALF
HALFTIME
SECOND_HALF
AWAITING_EXTRA_TIME
EXTRA_TIME_FIRST_HALF
EXTRA_TIME_BREAK
EXTRA_TIME_SECOND_HALF
AWAITING_PENALTIES
PENALTIES
FINISHED
```

---

# 19. Basketball Phases

```text
NOT_STARTED
FIRST_QUARTER
FIRST_BREAK
SECOND_QUARTER
HALFTIME
THIRD_QUARTER
THIRD_BREAK
FOURTH_QUARTER
REGULATION_END
OVERTIME_1
OVERTIME_2
OVERTIME_3...
FINISHED
```

Also allow competitions using halves.

---

# 20. Rugby 15s Phases

```text
NOT_STARTED
FIRST_HALF
HALFTIME
SECOND_HALF
AWAITING_EXTRA_TIME
EXTRA_TIME_FIRST_PERIOD
EXTRA_TIME_BREAK
EXTRA_TIME_SECOND_PERIOD
FINISHED
```

Do not hard-code 40-minute halves into the UI.

---

# 21. Rugby 7s Phases

```text
NOT_STARTED
FIRST_HALF
HALFTIME
SECOND_HALF
AWAITING_EXTRA_TIME
EXTRA_TIME
FINISHED
```

Match duration comes from competition rules.

Pool/cup/final is **competition stage**, not game phase.

---

# 22. Cricket T20 Phases

```text
NOT_STARTED
FIRST_INNINGS
INNINGS_BREAK
SECOND_INNINGS
SUPER_OVER
FINISHED
```

Architecture should later extend to multi-innings formats.

---

# 23. Contest Periods

A core:

```text
contest_periods
```

model is required.

Suggested fields:

```text
id
contest_id
period_number
period_type
status
home_score
away_score
started_at
ended_at
clock
is_overtime
```

This avoids fixed columns such as:

```text
quarter1_home
quarter2_home
```

---

# 24. Status History

Use:

```text
contest_status_history
```

including:

```text
from_status
to_status
phase_before
phase_after
reason
changed_by
changed_at
source
```

Exceptional status transitions should require reasons.

---

# 25. Timeline / Play-by-Play

Timeline is a first-class subsystem.

Do not store only free-text commentary.

Use:

```text
Contest
  └── Timeline
      ├── Period events
      ├── Scoring
      ├── Discipline
      ├── Personnel
      ├── Reviews
      ├── Plays
      ├── Stoppages
      ├── Commentary
      └── Corrections
```

---

# 26. Timeline Event Categories

```text
SYSTEM
PERIOD
SCORING
DISCIPLINE
PERSONNEL
PLAY
REVIEW
STOPPAGE
STATISTIC
COMMENTARY
CORRECTION
```

Timeline importance:

```text
KEY
STANDARD
DETAILED
TECHNICAL
```

This supports:

```text
Key Events
All Events
```

---

# 27. Timeline Event Model

Recommended fields:

```text
id
tenant_id
contest_id

category
event_type
event_subtype

sport_code
sport_variant

period_id
period_number
period_type

game_clock
elapsed_seconds
stoppage_time

primary_participant_id
secondary_participant_id

primary_player_id
secondary_player_id

points_value
metric_value

home_score_after
away_score_after

description

sequence_number
play_id

occurred_at
recorded_at

source
provider_name
provider_event_id

status
importance

supersedes_event_id
reversal_of_event_id

x
y

metadata_json
```

`sequence_number` should be authoritative for ordering.

---

# 28. Timeline Corrections

Never silently delete or overwrite published events.

Example:

```text
GOAL
ACTIVE
```

VAR overturn:

```text
GOAL
REVERSED
```

plus:

```text
VAR_DECISION
GOAL_OVERTURNED
reversal_of_event_id = original goal
```

Timeline should preserve history.

---

# 29. Football Timeline Events

Required:

```text
MATCH_START
PERIOD_START
PERIOD_END
GOAL
OWN_GOAL
PENALTY_GOAL
PENALTY_MISSED
ASSIST
YELLOW_CARD
SECOND_YELLOW_CARD
RED_CARD
SUBSTITUTION
VAR_REVIEW
VAR_DECISION
INJURY
PENALTY_SHOOTOUT_START
PENALTY_SHOOTOUT_SCORE
PENALTY_SHOOTOUT_MISS
MATCH_END
```

Optional detailed:

```text
SHOT
SHOT_ON_TARGET
CORNER
OFFSIDE
FOUL
FREE_KICK
```

---

# 30. Basketball Timeline Events

```text
GAME_START
PERIOD_START
PERIOD_END
FREE_THROW_MADE
FREE_THROW_MISSED
TWO_POINT_MADE
TWO_POINT_MISSED
THREE_POINT_MADE
THREE_POINT_MISSED
ASSIST
REBOUND_OFFENSIVE
REBOUND_DEFENSIVE
STEAL
BLOCK
TURNOVER
FOUL_PERSONAL
FOUL_TECHNICAL
FOUL_UNSPORTSMANLIKE
SUBSTITUTION
TIMEOUT
JUMP_BALL
GAME_END
```

Related events use:

```text
play_id
```

Example:

```text
THREE_POINT_MADE
ASSIST
SCORE_CHANGE
```

same play.

---

# 31. Rugby Timeline Events

Shared for 7s and 15s:

```text
MATCH_START
PERIOD_START
PERIOD_END
TRY
PENALTY_TRY
CONVERSION_MADE
CONVERSION_MISSED
PENALTY_GOAL
PENALTY_MISSED
DROP_GOAL
YELLOW_CARD
RED_CARD
SIN_BIN_START
SIN_BIN_END
SUBSTITUTION
TEMPORARY_REPLACEMENT
TMO_REVIEW
TMO_DECISION
MATCH_END
```

Optional:

```text
SCRUM
LINEOUT
TURNOVER
PENALTY
FREE_KICK
```

---

# 32. Cricket Timeline Events

```text
MATCH_START
INNINGS_START
OVER_START
BALL
RUN
FOUR
SIX
WIDE
NO_BALL
BYE
LEG_BYE
WICKET
BOWLED
CAUGHT
LBW
RUN_OUT
STUMPED
HIT_WICKET
OVER_END
DRINKS_BREAK
INNINGS_END
RAIN_INTERRUPTION
PLAY_RESUMED
SUPER_OVER_START
SUPER_OVER_END
MATCH_END
```

Important first-class cricket fields:

```text
innings_number
over_number
ball_number
batter_id
bowler_id
fielder_id
runs_batter
runs_extras
total_runs
wicket_type
```

---

# 33. Provider/API Compatibility

The domain model should resemble how major sports-data APIs work conceptually:

```text
Sport
Category
Competition
Season
Stage/Round
Sport Event
Competitors
Status
Period Scores
Timeline
Statistics
Coverage
```

Do not expose provider-specific enums to web/Flutter.

Use:

```text
Provider
  ↓
Adapter
  ↓
Canonical platform model
```

Support:

```text
external_identifiers
```

for:

- Competition
- Season
- Stage
- Team
- Player
- Contest
- Venue
- Official

The system should be able to switch providers without changing internal IDs.

---

# 34. Coverage Capabilities

Competitions/events should declare available data.

Example:

```text
coverage_live
coverage_period_scores
coverage_timeline
coverage_full_play_by_play
coverage_lineups
coverage_player_stats
coverage_team_stats
coverage_commentary
coverage_coordinates
```

UI hides unsupported functions.

---

# 35. Real-Time Delivery

Use SignalR.

Groups:

```text
tenant:{tenantId}
competition:{competitionId}
contest:{contestId}
participant:{participantId}
user:{userId}
```

Updates include:

```text
contest.status.changed
contest.timeline.event.created
contest.timeline.event.corrected
contest.timeline.event.reversed
contest.result.approved
```

Do not refetch the entire daily feed after every scoring action.

---

# 36. Offline Scorer

Flutter scorer/official mode must work offline.

Local event states:

```text
PENDING
SYNCING
SYNCED
REJECTED
CONFLICT
SUPERSEDED
```

Every event includes:

```text
client_event_id
device_id
local_sequence
contest_id
payload
created_at
```

Do not use last-write-wins for conflicts.

---

# 37. Public UI Navigation

Primary navigation should be:

```text
Home
Live
Flagship Sports
Competitions
Favourites
Search
```

Do not have a global:

```text
Standings
```

navigation page.

Standings are competition context.

Example:

```text
Premier League
Overview | Fixtures | Results | Table | Statistics | Teams
```

Golf:

```text
Overview | Leaderboard | Rounds | Players
```

Rugby 7s:

```text
Overview | Schedule | Pools | Results | Knockout | Teams
```

---

# 38. Flagship Sports Navigation

Tenant configuration determines flagship sports.

Example:

```text
Football
Basketball
Rugby
Cricket
More
```

Rugby may expand to:

```text
Rugby 15s
Rugby 7s
```

All other enabled sports appear under More.

---

# 39. Homepage

Homepage is **date-centric**.

Past date:

```text
Results
```

Today:

```text
Live
Upcoming
Finished
```

Future:

```text
Fixtures
```

Suggested layout:

```text
Sports
Date navigator
All | Live | Upcoming | Finished | Favourites

Football
Competition
Contest
Contest

Basketball
Competition
Contest
```

---

# 40. Contest Renderers

Use:

```text
HEAD_TO_HEAD
MULTI_PARTICIPANT
RACE_HEAT
COMBAT
COMPLEX_SCORE
```

Examples:

Football/Basketball/Rugby:

```text
HeadToHeadContestCard
```

Golf:

```text
MultiParticipantContestCard
```

Athletics:

```text
RaceHeatContestCard
```

MMA/Boxing:

```text
CombatContestCard
```

Cricket:

```text
ComplexScoreContestCard
```

---

# 41. Responsive Targets

Explicitly support:

```text
Web desktop >= 1200
Web tablet 768–1199
Web mobile < 768
Flutter mobile
Flutter tablet
```

Flutter mobile should use bottom navigation.

Flutter tablet should use NavigationRail and master/detail where appropriate.

---

# 42. UI Design System

Shared conceptual design tokens:

```text
Colors
Typography
Spacing
Radius
Elevation
Motion
Breakpoints
Status Colors
```

The sample CSS already contains a useful starting token set for primary, live/danger, warning, surfaces, text, borders, radii, shadows and transitions .

It also has useful starting patterns for compact fixture rows and live states , but it should be treated as a reference and broken into reusable components rather than kept as one permanent global stylesheet.

Its current responsive design already collapses the desktop layout and reduces match/date density on smaller screens , and its dark-mode variables provide a useful conceptual starting point .

---

# 43. Figma References

Several Figma community files were supplied.

They are visual references only.

Useful patterns identified:

- Compact scores
- Horizontal date navigation
- Horizontal sport navigation
- Strong score/match hero
- Tabs on match detail
- Bottom mobile navigation
- Favourites
- Search
- Dense cards
- Light/dark variants
- Competition/team/player screens

The final platform UI must be original.

Suggested master Figma organization:

```text
00 Foundations
01 Components
02 Public Mobile
03 Public Tablet
04 Public Desktop
05 Competition
06 Match Detail
07 Team & Player
08 Tenant Admin
09 Scorer
10 Platform Admin
11 Dark Mode
12 Responsive Examples
```

---

# 44. Public Screens Required

```text
Home
Live
Search
Favourites
Sports
Competition
Fixtures
Results
Standings
Groups
Bracket
Series
Leaderboard
Medals
Contest Detail
Timeline
Lineups
Statistics
Team
Player/Athlete
Venue
Authentication
Profile
Notification Settings
```

---

# 45. Competition Management UI

Use a guided competition wizard:

```text
1 General Information
2 Sport / Variant
3 Format
4 Rules
5 Registration
6 Participants
7 Stages
8 Schedule
9 Officials
10 Presentation
11 Review & Publish
```

Competition admin should support:

- Create
- Clone
- Edit
- Publish
- Archive
- Close
- Cancel

---

# 46. Team Management UI

Team page:

```text
Overview
Players
Competitions
Fixtures
Results
Statistics
Staff
Documents
History
```

Actions:

```text
Edit
Add player
Register competition
Manage roster
Transfer player
Deactivate
```

---

# 47. Player Management UI

Player admin:

```text
Overview
Memberships
Competitions
Matches
Statistics
Discipline
Registration
Documents
History
```

Keep public/private information separated.

---

# 48. Match Management UI

Admin match page:

```text
Score/Status Hero
Controls
Timeline
Lineups
Statistics
Officials
Audit
```

Match lifecycle:

```text
Scheduled
→ Check-in Open
→ Participants Check In
→ Lineups Submitted
→ Officials Confirm
→ Ready
→ Live
→ Ended
→ Result Submitted
→ Review
→ Approved
→ Closed
```

---

# 49. Scorer Workspace

Scorer interface must be fast and touch-friendly.

Football:

```text
Goal
Card
Substitution
VAR
Period
```

Basketball:

```text
+1
+2
+3
Miss
Foul
Timeout
Substitution
Period
```

Rugby:

```text
Try
Conversion
Penalty Goal
Drop Goal
Card
Substitution
Period
```

Cricket:

```text
Runs
Boundary
Extras
Wicket
Next Delivery
```

Always show sync state:

```text
Online
Offline
3 Pending
Syncing
Conflict
```

---

# 50. Officials

Support configurable officials:

```text
REFEREE
ASSISTANT_REFEREE
SCORER
TIMEKEEPER
JUDGE
COMMISSIONER
UMPIRE
```

Assignments should detect:

- Time conflict
- Venue conflict
- Multiple assignments
- Unsupported role

---

# 51. Match Readiness

Before Start Contest, validate configurable requirements.

Example:

```text
Home checked in
Away checked in
Lineups submitted
Officials assigned
Venue confirmed
```

Rules differ by sport.

---

# 52. Scheduling

Matches can be:

```text
Generated
Manually created
Imported
Provider-supplied
```

All become the same internal Contest entity.

Rescheduling must preserve:

```text
old date
new date
old venue
new venue
reason
changed by
changed at
```

---

# 53. Bulk Operations

Admin should support:

```text
Bulk team registration
Bulk player import
Bulk fixture import
Bulk venue assignment
Bulk official assignment
Bulk fixture publish
Bulk postpone
Bulk result import
CSV import/export
```

---

# 54. Platform Leasing

Commercial packages may include:

```text
Event
Seasonal
Annual
Federation
Enterprise / White-label
```

Lease states:

```text
DRAFT
ACTIVE
EXPIRING
GRACE_PERIOD
READ_ONLY
SUSPENDED
ARCHIVED
```

Expired tenants should normally become read-only rather than deleting data.

---

# 55. White Label

Support:

- Custom logo
- Colors
- Domain
- Sponsors
- Custom emails
- Dedicated Flutter builds
- Separate App Store/Play Store branding

One codebase should support multiple branded Flutter builds.

---

# 56. Seed Data Strategy

A deterministic public staging seed pack has already been designed.

Principles:

```text
Anchor date
Past
Today
Future
Live
Completed
Postponed
Suspended
Cancelled
Abandoned
Corrected
```

Use sensible fictional Uganda/East Africa-style names.

Avoid:

```text
Team A
Player 1
Lorem FC
```

Examples:

```text
Kampala City FC
Jinja Nile RFC
Kampala Titans
Lake Victoria Open
Kampala Fight Night
```

Seed should cover:

- League
- Knockout
- Group + knockout
- Best-of series
- Rugby 7s pools
- Golf leaderboard
- Athletics heats
- Fight card
- Cricket tournament

---

# 57. Seed Data Enhancements Required

The next seed version should include:

```text
sport_variants.csv
contest_periods.csv
timeline_event_definitions.csv
contest_events.csv
contest_event_actors.csv
contest_statistics.csv
contest_coverage.csv
external_identifiers.csv
status_history.csv
```

Derived standings/brackets should preferably be recalculated using production services rather than manually trusted.

---

# 58. Test Strategy

Required:

```text
Unit tests
Integration tests
Tenant isolation tests
Playwright web E2E
Flutter integration tests
Visual regression
Golden tests
Load tests
SignalR tests
Offline sync tests
Status transition tests
Timeline correction tests
```

Key scenarios:

```text
Create competition
Register teams
Register players
Create roster
Generate fixtures
Assign officials
Run contest
Enter timeline
Correct event
Submit result
Approve result
Update standings
Publish result
```

---

# 59. Product Development Philosophy

Use:

```text
Configuration over hard-coding
Modular monolith over early microservices
Canonical domain model over provider-specific models
Responsive components over duplicated screens
One design language across web and Flutter
Audit history over destructive edits
Offline-first scoring
Provider adapters
Versioned rules
Deterministic seed data
```

---

# 60. Immediate Recommended Implementation Sequence

The next implementation method/specification should proceed roughly in this order:

```text
1. Audit current codebase
2. Confirm domain schema
3. Confirm statuses/phases
4. Confirm timeline event model
5. Confirm competition/team/player model
6. Implement design system
7. Implement public app shell
8. Implement contest renderers
9. Implement competition detail
10. Implement match detail/timeline
11. Implement tenant admin
12. Implement competition wizard
13. Implement team/player/roster management
14. Implement fixture management
15. Implement scorer workflow
16. Implement result approval
17. Implement reporting
18. Implement platform tenant/lease management
19. Upgrade staging seed pack
20. Implement full E2E tests
21. Pilot real competitions
22. Production hardening
```

---

# 61. Important Instructions for the Next AI/Implementation Agent

Before making changes:

1. Inspect the actual repository.
2. Inspect existing database entities and migrations.
3. Inspect current sport definitions.
4. Inspect current UI components.
5. Inspect existing status enums.
6. Inspect current timeline/event implementation.
7. Inspect seed infrastructure.
8. Inspect API contracts.
9. Do not invent duplicate concepts when something equivalent already exists.
10. Map this specification to the existing implementation.

If a required feature is unsupported:

```text
DO NOT FAKE INVALID DATA.
```

Instead:

```text
Mark as unsupported
Document the gap
Implement the required capability deliberately
```

Do not rebuild working architecture merely because naming differs.

---

# 62. Overall Success Definition

The system succeeds when a tenant administrator can:

```text
Create tenant
→ Configure branding
→ Enable sports
→ Create competition
→ Configure rules/format
→ Register teams/athletes
→ Build rosters
→ Generate fixtures/stages
→ Assign officials
→ Run games live
→ Enter structured play-by-play
→ Handle corrections
→ Submit results
→ Approve results
→ Automatically update tables/brackets/leaderboards
→ Publish everything on web and mobile
→ Produce reports
→ Archive or renew competition/tenant
```

without requiring direct database access or routine developer intervention.

The same platform should support a football league, Rugby 7s tournament, Rugby 15s league, basketball playoffs, cricket T20 tournament, golf championship, athletics meet, combat-sport card, and other configured sports using the same core platform.
