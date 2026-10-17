# Sports Platform — Master Domain & Context Map

## 1. Purpose

This document defines the business-domain architecture for the Sports Platform. It is the high-level constitution for the system and should be consulted before creating schemas, APIs, background jobs, UI screens, or integrations.

The platform is intended to grow from competition-management and live-scoring software into a broader sports information platform that can support federation, league, tournament, club, school, promoter, and national-registry use cases.

The architecture should remain a **modular monolith initially**. The systems below are logical/business boundaries, not an instruction to create microservices.

---

## 2. Platform Product Surfaces

The platform has two principal user-interface shells:

### 2.1 Admin Application

The Admin Application contains permission-driven workspaces for:

- Platform Administration
- Tenant Administration
- Competition Administration
- Team/Participant Administration
- Match/Contest Operations
- Scoring/Officials
- Reporting and Data Operations

### 2.2 Public Application

The Public Application serves:

- Web desktop
- Web tablet
- Web mobile
- Flutter mobile
- Flutter tablet

It exposes only approved public data.

---

## 3. Domain Architecture

```text
SPORTS PLATFORM
│
├── FOUNDATION
│   ├── Reference Data Registry
│   ├── Seed & Bootstrap Tooling
│   ├── Audit
│   ├── Data Governance & Integrity
│   └── Platform Analytics
│
├── CONFIGURATION
│   ├── Sports Definition System
│   ├── Competition Format System
│   └── Ruleset Registry
│
├── TENANCY & COMMERCIAL
│   ├── Tenant Management
│   ├── Subscription & Entitlements
│   ├── Branding / White Label
│   └── Sponsorship
│
├── REGISTRIES
│   ├── Person Registry
│   ├── Participant / Athlete Registry
│   ├── Club / Organisation Registry
│   ├── Team Registry
│   ├── Staff Registry
│   ├── Official Registry
│   └── Venue Registry
│
├── TEAM OPERATIONS
│   ├── Team Memberships
│   ├── Rosters
│   └── Transfers
│
├── COMPETITION MANAGEMENT
│   ├── Competition
│   ├── Competition Edition / Season
│   ├── Registration
│   ├── Eligibility
│   ├── Stages & Groups
│   └── Competition Participants
│
├── COMPETITION OPERATIONS
│   ├── Scheduling
│   ├── Contest Operations
│   ├── Live Scoring
│   ├── Timeline / Play-by-Play
│   ├── Results & Approval
│   └── Progression
│
├── INFORMATION & INSIGHTS
│   ├── Sports Statistics
│   ├── Historical Records
│   ├── Query / Presentation Read Models
│   ├── Search
│   └── Reporting
│
└── DATA INGESTION
    ├── Historical Backfill
    ├── CSV / Excel
    ├── JSON
    ├── External Sports APIs
    └── Migration Tools
```

---

## 4. Architectural Principles

### 4.1 A system is a business boundary, not automatically a microservice

Each system should own:

- Domain entities
- Business rules
- Commands
- Queries
- Permissions
- Audit requirements
- Domain events
- Tests
- Internal interfaces

Cross-system access should happen through explicit application services, contracts, domain events, or approved read models.

### 4.2 No direct cross-module table ownership

A module may reference another module's identifiers, but it should not treat another module's tables as its own aggregate.

Example:

```text
Competition Registration
may reference person_id/team_id
but must not update Person Registry biodata directly.
```

### 4.3 Historical truth should be preserved

The platform should prefer:

- Versioning
- Membership periods
- Roster history
- Ruleset versions
- Status history
- Corrections/reversals
- Append-only audit records

over destructive overwrites.

### 4.4 Configuration replaces sport hard-coding

Sport-specific behavior should come from:

```text
Sport Definition
+
Competition Format
+
Ruleset
```

rather than scattered `if football`, `if rugby`, or `if basketball` logic.

### 4.5 Imports must use domain services

CSV, Excel, JSON, API ingestion, and historical backfill must not bypass business rules by writing arbitrary tables directly.

---

# 5. Foundation Systems

## 5.1 Reference Data Registry

### Owns

Stable shared reference data such as:

- Countries
- Currencies
- Languages
- Time zones
- Measurement units
- Shared classification values where genuinely cross-domain

### Does Not Own

- Contest status
- Registration status
- Sport event types
- Competition formats
- Permissions
- Rules

Those belong to their respective domains.

---

## 5.2 Seed & Bootstrap Tooling

### Purpose

Provide deterministic initialization for:

- Empty development database
- Test database
- Demo tenants
- Reference data
- Sports definitions
- Competition-format templates
- Ruleset templates
- End-to-end scenarios

### Classification

Engineering/data tooling rather than a business domain.

---

## 5.3 Audit

### Owns

Immutable records of:

- User actions
- System actions
- Before/after references where permitted
- Actor
- Tenant
- Action code
- Entity
- Time
- Source
- Correlation ID
- Reason

### Requirement

Business modules emit auditable actions but remain owners of their domain data.

---

## 5.4 Data Governance & Integrity Framework

Defines shared policy for:

- Identifier strategy
- Tenant scoping
- Foreign-key rules
- Unique constraints
- Soft-delete policy
- Archival policy
- Temporal/history policy
- PII classification
- Duplicate prevention
- Naming conventions
- Concurrency/versioning
- Data retention
- Migration conventions

This is a framework enforced through schemas, shared infrastructure, tests, analyzers, and CI—not necessarily a standalone runtime screen.

---

## 5.5 Platform Analytics

Tracks product/platform usage, not sports performance.

Examples:

- Active tenants
- Feature usage
- Admin activity
- API volume
- Imports
- Storage
- Live-scoring sessions
- Public traffic
- Error rates

Sports performance belongs to Sports Statistics.

---

# 6. Configuration Systems

## 6.1 Sports Definition System

Defines what a sport is structurally.

Owns:

- Sport
- Sport variant
- Participant roles
- Period definitions
- Scoring-event definitions
- Timeline-event definitions
- Statistic definitions
- Position/role definitions
- Official-role definitions
- Default presentation metadata
- Sport-definition versions

Does not own:

- Competition participants
- Fixtures
- Results
- Competition-specific points/tiebreakers
- Actual player/team data

---

## 6.2 Competition Format System

Defines how a competition is structurally organised.

Examples:

- Single round robin
- Double round robin
- Single elimination
- Group + knockout
- Best-of series
- Pool + knockout
- Heat to final
- Multi-round leaderboard
- Fight card

Owns:

- Format templates
- Stage templates
- Progression structure
- Generator capability declarations
- Format-specific configurable parameters
- Format versions

Does not own sport scoring rules.

---

## 6.3 Ruleset Registry

Bridges:

```text
Sport Definition
+
Competition Format
+
Competition-specific policy
```

Owns versioned rules such as:

- Period duration
- Roster limits
- Substitution rules
- Points for win/draw/loss
- Bonus points
- Tie-break order
- Qualification count
- Extra time
- Penalties
- Overtime
- Transfer windows
- Eligibility constraints
- Matchday squad limits
- Result methods
- Competition-specific overrides

Rulesets become immutable once used by an active/published competition edition.

---

# 7. Tenancy & Commercial Systems

## 7.1 Tenant Management

Owns:

- Tenant identity
- Tenant lifecycle
- Tenant ownership/admin relationship
- Enabled sports
- Locale
- Time zone
- Currency
- Domains
- Data-isolation metadata
- Tenant status

Lifecycle:

```text
PROVISIONING
ACTIVE
EXPIRING
GRACE
READ_ONLY
SUSPENDED
ARCHIVED
```

---

## 7.2 Subscription & Entitlements

Owns commercial capability allocation.

Distinguish:

```text
ENTITLEMENT
```

from:

```text
PERMISSION
```

Examples of entitlements:

- LIVE_SCORING enabled
- MAX_COMPETITIONS = 20
- ENABLED_SPORTS = football,basketball,rugby
- CUSTOM_DOMAIN enabled
- ADVANCED_REPORTS disabled
- STORAGE_QUOTA
- API_ACCESS

Final authorization should evaluate:

```text
Tenant Entitlement
+
User Permission
+
Domain Rule
=
Allowed Action
```

---

## 7.3 Branding / White Label

Owns:

- Logos
- Color tokens
- Typography selections
- App identity
- Domain presentation
- Email presentation
- Public theme
- Dedicated-app metadata

Semantic colors such as live/error/warning must remain accessible and should not be freely overridden by tenant branding.

---

## 7.4 Sponsorship

Owns:

- Sponsor profile
- Campaign
- Asset
- Placement
- Competition association
- Start/end date
- Impression/click analytics
- Visibility rules

Sponsorship is separate from tenant branding.

---

# 8. Registry Systems

## 8.1 Person Registry

The canonical identity for a human.

Owns common biodata such as:

- Legal/public name components
- Date of birth
- Gender/classification where relevant
- Nationality
- Photo
- Height
- Weight where appropriate
- Identity aliases
- Duplicate-resolution metadata

A person can later have multiple roles:

```text
Person
├── Athlete Profile
├── Staff Profile
├── Official Profile
└── User Relationship
```

This avoids creating the same individual separately as player, coach, and referee.

---

## 8.2 Participant / Athlete Registry

Represents a competition participant.

Participant types may include:

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

Individual participants may reference a canonical Person.

---

## 8.3 Club / Organisation Registry

Owns persistent sports organisations.

Example:

```text
Kampala Sports Club
├── Men's Football
├── Women's Football
├── Rugby 15s
└── Rugby 7s
```

---

## 8.4 Team Registry

Owns persistent playing-team identity:

- Name
- Short name
- Logo/badge
- Motto
- Sport
- Parent organisation
- Home venue
- Public metadata
- Aliases
- External identifiers

Competition participation is not owned by Team Registry.

---

## 8.5 Staff Registry

Represents sport/team staff roles linked to Person.

Examples:

- Coach
- Assistant coach
- Team manager
- Physio
- Doctor
- Equipment manager

Team assignment and effective dates belong to team operations.

---

## 8.6 Official Registry

Represents match/competition officials linked to Person.

Examples:

- Referee
- Assistant referee
- Umpire
- Judge
- Scorer
- Timekeeper
- Commissioner

Contest assignment belongs to Competition Operations.

---

## 8.7 Venue Registry

Owns persistent venue information:

- Name
- Location
- Surface/type
- Capacity where appropriate
- Public information
- Availability metadata
- Aliases/external IDs

Scheduling references venue IDs.

---

# 9. Team Operations

## 9.1 Memberships

Owns effective-dated relationships:

```text
Person/Athlete
  ↓
Team Membership
```

Fields may include:

- start date
- end date
- membership status
- position
- shirt number
- loan/transfer status

---

## 9.2 Rosters

Competition roster:

```text
Team
+
Competition Edition
=
Roster
```

A player may be on multiple competition rosters for the same team.

Roster eligibility is distinct from permanent team membership.

---

## 9.3 Transfers

A capability-enabled subsystem, not mandatory for every sport/competition.

Supports:

- Request
- Approval
- Release
- Destination
- Effective date
- Window validation
- Loan
- Historical record

Ruleset decides whether transfer functionality applies.

---

# 10. Competition Management

## 10.1 Competition

The persistent identity:

```text
Uganda Premier League
Kampala Sevens
Lake Victoria Open
```

---

## 10.2 Competition Edition / Season

The operational occurrence:

```text
Uganda Premier League
  └── 2026/27

Kampala Sevens
  └── 2026 Edition
```

Owns:

- Dates
- Ruleset version
- Registration window
- Participants
- Stage configuration
- Scheduling policy
- Publication status

---

## 10.3 Registration

Owns competition-entry workflow:

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

---

## 10.4 Eligibility

Evaluates eligibility according to the bound ruleset.

Possible checks:

- Age
- Gender/classification
- Weight
- Registration fee status
- Roster size
- Transfer eligibility
- Required documents
- Suspension
- Qualification status

---

## 10.5 Stages & Groups

Owns competition structure instantiated from the selected format.

Examples:

```text
Regular Season
Group Stage
Pool A
Quarterfinal
Semifinal
Final
Classification Stage
```

---

# 11. Competition Operations

## 11.1 Scheduling

Owns:

- Fixture generation
- Date/time
- Venue
- Schedule publication
- Rescheduling
- Schedule history
- Round scheduling
- Conflict detection

It does not own the contest lifecycle once play begins.

---

## 11.2 Contest Operations

Owns live and operational contest state:

- Contest status
- Phase
- Clock
- Match readiness
- Check-in
- Match squad
- Lineup
- Start
- Interrupt
- Suspend
- End
- Abandon

Canonical contest statuses:

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

---

## 11.3 Live Scoring

Owns authoritative provisional scoring during active contests.

Scoring actions come from:

```text
Sport Definition
+
Ruleset
```

not Competition Format.

---

## 11.4 Timeline / Play-by-Play

Owns structured contest events.

Event categories:

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

Events are ordered by sequence and corrected through reversal/supersession, not destructive deletion.

---

## 11.5 Results & Approval

Owns result workflow:

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

Typical lifecycle:

```text
Contest = ENDED
Result = SUBMITTED

→ review →

Contest = CLOSED
Result = APPROVED
```

---

## 11.6 Progression

Consumes approved results to calculate:

- Standings
- Group qualification
- Bracket advancement
- Series status
- Leaderboards
- Heat qualification
- Competition completion

Progression must use the bound competition format and ruleset.

---

# 12. Information & Insights

## 12.1 Sports Statistics

Produces sport-performance data.

Examples:

- Goals
- Assists
- Tries
- Points
- Rebounds
- Wickets
- Wins/losses
- Player performance
- Team performance

It consumes domain events/results and does not own competition master data.

---

## 12.2 Historical Records

Provides durable historical views:

- Career totals
- Past seasons
- Competition history
- Records
- Champions
- Team history
- Player history

---

## 12.3 Query / Presentation Read Models

Optimized read-side projections for:

- Public home feed
- Competition detail
- Match detail
- Team pages
- Player pages
- Admin dashboards
- Search results

These are not the source of truth.

---

## 12.4 Search

Searches approved projections across:

- Competitions
- Editions
- Teams
- People/Athletes
- Venues
- Contests
- Sports

---

## 12.5 Reporting

Produces operational and historical exports:

- Competition
- Results
- Rosters
- Registrations
- Officials
- Audit
- Usage
- Statistics

---

# 13. Data Ingestion

## 13.1 Historical Backfill

Replaces the ambiguous term "Backlog Manager".

Purpose:

- Enter old competition data
- Import historic players/teams/results
- Reconstruct seasons
- Import play-by-play when available

---

## 13.2 Import Flow

All imports should follow:

```text
Input File/API
   ↓
Parse
   ↓
Map
   ↓
Validate
   ↓
Duplicate Resolution
   ↓
Dry Run
   ↓
Domain Commands
   ↓
Audit
   ↓
Import Report
```

Supported inputs may include:

- CSV
- Excel
- JSON
- External sports API
- Migration source

---

# 14. National Registry Evolution

The architecture should allow future federation/national registers such as:

```text
National Person Registry
National Athlete Registry
National Club Registry
National Team Registry
National Official Registry
National Venue Registry
National Competition Registry
National Results Archive
National Sports Records Registry
```

However:

- Tenant data must remain isolated by default.
- Cross-tenant identity sharing must not happen implicitly.
- Federation/national identity must have explicit ownership and governance.
- Duplicate merging must require authorized stewardship.
- Privacy and PII rules must be defined before national sharing.

Possible future model:

```text
Canonical National Person
        ↓
Federation Registration
        ↓
Team Membership
        ↓
Competition Registration
        ↓
Roster
```

---

# 15. Action / Capability Registry

Create a canonical action catalogue.

Examples:

```text
person.create
person.update

team.create
team.update
team.membership.manage
roster.manage
transfer.manage

competition.create
competition.update
competition.publish
competition.close

registration.review

fixture.generate
fixture.schedule
fixture.reschedule

contest.start
contest.interrupt
contest.score
contest.timeline.manage
contest.end

result.submit
result.approve
result.correct

official.assign

report.generate
```

Each action can be used by:

- Authorization
- Entitlements
- Audit
- Analytics
- UI action visibility

But permissions and subscription entitlements remain distinct.

---

# 16. High-Level Context Relationships

```text
Reference Data
      ↓

Sports Definition ─────┐
                       ├──→ Ruleset Registry
Competition Format ────┘
                              ↓
Tenant / Entitlements → Competition Edition
                              ↓
                    Registration / Eligibility
                              ↓
                   Teams / Athletes / Rosters
                              ↓
                           Scheduling
                              ↓
                       Contest Operations
                              ↓
                 Timeline / Scoring / Result
                              ↓
                          Progression
                              ↓
             Statistics / Records / Read Models
                              ↓
                          Public UI
```

Historical Backfill flows **into** domain commands.

Statistics/read models flow **out of** domain events/results.

---

# 17. Recommended Module Dependency Rule

Prefer:

```text
Foundation
    ↓
Configuration
    ↓
Tenancy / Registries
    ↓
Team Operations
    ↓
Competition Management
    ↓
Competition Operations
    ↓
Information & Insights
```

Data Ingestion may call approved application-layer commands across these systems but should not bypass them.

---

# 18. Implementation Order

Recommended implementation sequence:

1. Foundation conventions
2. Sports Definition System
3. Competition Format System
4. Ruleset Registry
5. Tenant Management + Entitlements
6. Person/Participant Registry
7. Club/Team Registry
8. Staff/Official/Venue registries
9. Team Memberships/Rosters
10. Competition + Edition
11. Registration/Eligibility
12. Stage instantiation
13. Scheduling
14. Contest Operations
15. Timeline/Scoring
16. Results/Approval
17. Progression
18. Read models/Search
19. Statistics/Records
20. Historical Backfill/Import
21. Reports
22. National-registry extensions

---

# 19. Architecture Review Rule

Before implementing a new feature, answer:

1. Which system owns this behavior?
2. Which system owns the source-of-truth data?
3. Is this a command, query, projection, event, or import?
4. Which ruleset/configuration applies?
5. Does it affect historical records?
6. Is it tenant-scoped?
7. What action/permission/entitlement controls it?
8. What audit entry is required?
9. What public/read-model projection changes?
10. What tests prove the boundary is preserved?

If ownership cannot be answered clearly, the feature is not ready for implementation.
