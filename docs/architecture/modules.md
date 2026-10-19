# Modules — ownership, boundaries and dependencies

This is the map of the backend. Every entity has exactly **one owning module**. Other modules may hold its ID,
consume its integration events, or call its **Contracts** (query interfaces, and a few approved command
interfaces used by imports and seeding) — nothing else. Decision record: ADR-0001.

Each module owns one PostgreSQL schema (Platform also owns `audit`). Modules are created **only when the first
slice that needs them begins** (see "First slice" below).

---

## Dependency order

Arrows point from a module to what it may depend on (upstream). Upstream modules never call downstream ones;
they publish integration events instead.

```text
Platform ◄── everything

Tenancy & Commercial ──► Platform, Sports Configuration (read: sport variants for enablement)
Identity & Access    ──► Platform, Tenancy
Sports Configuration ──► Platform
Registry             ──► Platform, Tenancy, Sports Configuration
Team Operations      ──► Registry, Sports Configuration
Competitions         ──► Registry, Team Operations, Sports Configuration, Tenancy
Match Operations     ──► Competitions, Team Operations, Registry, Sports Configuration
Standings & Progression ──► Competitions, Match Operations, Sports Configuration
Insights             ──► (reads) all modules above via Contracts + events; writes only its own projections
Data Exchange        ──► approved command/query Contracts of Registry, Team Ops, Competitions, Match Ops
Engagement           ──► events from any module; Identity (users, preferences); Branding (templates)
Branding & Sponsorship ──► Platform (media), Tenancy (entitlements), Competitions (sponsor associations)
```

The **authorization evaluator** (docs/architecture/authorization.md) is implemented in Identity & Access and
injected everywhere through an interface defined in BuildingBlocks, so modules never reference Identity
directly.

---

## 1. Platform

**Purpose:** cross-cutting foundations that are not business domains.
**Owns:** reference data (countries, currencies, languages, IANA time zones, units); `audit.audit_entries`
(append-only, tenant RLS; written and read across tenants by `PlatformAuditDbContext` on `sp_platform`); media asset metadata (ADR-0014); product analytics events; seed runner bookkeeping.
**Provides (Contracts):** `IReferenceData`, `IAuditLog` (also used automatically by AuditScope and the audit middleware),
`IMediaAssets` (URLs, upload validation), analytics recording.
**Also lives here (code, not data):** action catalogue export, error catalogue export, outbox processor host,
seed framework.
**Does not own:** domain statuses, rules, permissions — those stay in their modules (no "constants" bucket).
**First slice:** S01.
**Absorbs:** pack specs 40 (Audit), 41 (Analytics), 42 (Seed), 43 (Data Governance → enforced as
conventions/tests), Reference Data. Owner's managers: **Constant, Seed, Audit, Data Integrity, Analytics**.

## 2. Identity & Access

**Purpose:** who you are and what you may do.
**Owns:** users and credentials (ASP.NET Core Identity), tenant memberships, invitations, roles,
role → action-code sets, scoped role assignments (`TENANT | COMPETITION | TEAM | CONTEST`), platform staff roles,
the data protection key ring (`identity.data_protection_keys`: cookies and Identity's tokens, S03 D14).
Later: public fan accounts.
**Provides:** current actor, `IPermissionQueries`, the authorization evaluator implementation, membership
queries (e.g. "users with role X in scope Y" for notifications).
**Does not own:** Person biodata (a user *may* be linked to a Person in Registry by ID), entitlements, tenant
lifecycle.
**First slice:** S03.
**Absorbs:** gap in the pack (users/roles were unspecified); the pack's permission lists become action codes.

## 3. Tenancy & Commercial

**Purpose:** customers and what they've paid for.
**Owns:** tenants (lifecycle + history), tenant domains, tenant settings (time zone, locale, currency,
public visibility, policies such as "minor consent required for registration"), enabled sports per tenant,
entitlement definitions, plans, plan entitlements, subscriptions (periods), tenant entitlement overrides, usage
counters.
**Provides:** `ITenantDirectory` (resolve by host/slug/id), `ITenantAccess` (computed access mode:
`FULL | READ_ONLY | BLOCKED`), `IEntitlements` (`CanUse`, `GetLimit`, `CheckQuota`), enabled sports.
**Does not own:** branding (module 13), users (module 2), billing/payment processing (out of scope).
**First slice:** S02 (tenants), S04 (plans & entitlements), S05 (sport enablement).
**Absorbs:** pack specs 06, 07. Owner's managers: **Tenant, Subscription/Feature**.

## 4. Sports Configuration

**Purpose:** what sports and competition formats are, and the rules templates that combine them.
**Owns (platform-global, no tenant_id):** sport definitions (versioned, hashed; seeded from repo YAML),
scoring-family engine registry, format definitions + fixture generators, ruleset templates and their parameter
schemas, tiebreaker catalogue.
**Provides:** `ISportDefinitions` (resolved definition by variant + version), `IScoringEngines`,
`IFormatGenerators` (pure functions: participants + parameters → structure/pairings),
`IRulesetResolver` (template + overrides → validated effective rules snapshot).
**Does not own:** any tenant data, competitions, fixtures, results.
**First slice:** S05.
**Absorbs:** pack specs 03, 04, 05. Owner's systems: **Sports Management System, Competition Format
Management System**. Details: docs/architecture/sport-engine.md, docs/domain/formats.md,
docs/domain/rulesets.md.

## 5. Registry

**Purpose:** the canonical records of people, organisations, teams and venues — the base for future national
registers.
**Owns:**
- **People:** Person (biodata with field classification), aliases, external identifiers, duplicate candidates,
  merge records, guardian consent (minors), public-profile settings.
- **Role profiles:** athlete profiles (per sport variant), staff profiles (role capabilities), official profiles
  (role capabilities per sport, grades/licences, availability).
- **Organisations:** clubs, schools, academies, federations, companies (+ parent/child relationships).
- **Teams:** persistent playing identities (name, short name, slug, logo, badge, motto, colours, sport variant,
  category e.g. Senior Men/U19 Women, parent organisation, home venue, aliases).
- **Venues:** location, time zone, capacity, surfaces, supported sports.
**Provides:** summaries for other modules (`IPeopleDirectory`, `ITeamDirectory`, `IVenueDirectory`), duplicate
check, approved commands for imports (`IRegistryCommands`), external-ID lookup.
**Does not own:** who plays for whom (Team Operations), who is entered in a competition (Competitions).
**First slice:** S06 (people), S07 (organisations, teams, venues).
**Absorbs:** pack specs 08–14. Owner's systems: **Player Management System**, the identity half of
**Team Management System**. Owner's rule "duplicate players within the same tenant should be avoided at all
costs" is implemented here (docs/build/slices/S06-people-registry.md).

## 6. Team Operations

**Purpose:** the permanent, effective-dated relationships that make a team.
**Owns:** player memberships (start/end, status, position, default shirt number, loan flag), staff
assignments (role, start/end), transfers and loans (later: windows, approvals), team documents (later).
**Provides:** `ITeamMembers` (members on a date, membership history), `TeamMembershipChanged`,
`TransferCompleted` events, approved commands for imports.
**Does not own:** competition rosters (Competitions, ADR-0008), people/team identity (Registry).
**First slice:** S08 (memberships, staff), S24 (transfers).
**Absorbs:** pack specs 15, 17, and the missing staff-assignment piece. Owner's managers: **Staff Manager,
Transfer Manager** (Roster Manager's UI lives on the team page; its data is in Competitions).

## 7. Competitions

**Purpose:** competitions, their editions/seasons, who is entered, and the structure they play in.
**Owns:** competitions (persistent identity), editions (dates, bound configuration, frozen effective-rules
snapshot, lifecycle), registrations, **entrants** (approved registrations — the team or athlete that appears in
fixtures and standings), **rosters** and roster members, eligibility evaluations (+ overrides), stages,
groups/pools, rounds, bracket slots, seeding and draw records.
**Provides:** `ICompetitionStructure` (edition, effective rules snapshot, stages/groups/rounds/slots,
approved entrants), `IRosters` (eligible roster members for lineups), scope chain for authorization, events
(`EditionRulesFrozen`, `RegistrationApproved`, `RosterLocked`, `StageStructurePublished`).
**Does not own:** fixtures and results (Match Operations), standings (Progression).
**First slice:** S09.
**Absorbs:** pack specs 16, 18–22. Owner's system: **Competition Management System** (+ roster data).

## 8. Match Operations

**Purpose:** everything about individual contests, from fixture to official result. One module because every
part acts on the same Contest and must stay consistent (ADR-0001, ADR-0002).
**Owns:**
- **Fixtures:** contests, contest participants (each references an entrant, or a slot until it is resolved),
  schedule versions (time, venue, reason), publication state, fixture generation runs.
- **Match day:** official assignments, check-in/readiness, squads & lineups (from rosters), captaincy.
- **Live:** contest status + history, current phase, clock state, **timeline events** (append-only),
  score snapshots (cache), scorer device sessions/locks.
- **Results:** contest results and result versions (draft → submitted → approved), walkovers/forfeits,
  replays (link to replay contest).
**Provides:** `IContests` (fixtures by date/competition/team, contest detail), realtime hub (SignalR groups),
events (`FixturesPublished`, `ContestRescheduled`, `ContestStatusChanged`, `TimelineEventRecorded`,
`TimelineEventCorrected`, `ResultApproved`, `ResultSuperseded`), approved commands for backfill
(`contest.record_historical_result`).
**Does not own:** standings/brackets (Progression), statistics (Insights), sport definitions (Config).
**First slice:** S10 (fixtures), S11 (match day), S12 (live), S13 (results).
**Absorbs:** pack specs 23–28. Owner's systems: **Schedule Management System** and **Score (Live Score)
Management System** — answered: *one module, not two*.

## 9. Standings & Progression

**Purpose:** turn approved results into tables, brackets, series and qualification.
**Owns:** standings tables and rows (per stage/group), bracket slot resolutions (who fills "Winner QF1"),
series state, leaderboards (field sports, later), qualification decisions, progression runs (inputs, versions,
output hash), manual overrides (reason + audit).
**Provides:** `IStandings`, `IBrackets`, events (`StandingsRecalculated`, `SlotResolved`, `StageCompleted`).
Match Operations reacts to `SlotResolved` by filling TBD contest participants.
**Does not own:** results (Match Operations), competition structure templates (Competitions/Config).
**Rules:** consumes **approved** result versions only; recalculation is deterministic and idempotent
(same inputs → same output hash).
**First slice:** S13.
**Absorbs:** pack spec 29. Owner's need: "display the competition within its ruleset such as brackets or a
table".

## 10. Insights

**Purpose:** the read side — fast, rebuildable views and statistics. Never a source of truth.
**Owns:** sports statistics (contest/player/team/edition/career aggregates with coverage + provisional flags),
historical records (champions, past editions, career summaries, records), public read models (contest cards,
match detail, competition pages, team/player pages), search documents (PostgreSQL full-text + trigram),
report definitions/runs/artifacts, tenant dashboards.
**Provides:** public query API (`/api/public/v1/...`), admin dashboards/reports, rebuild commands.
**Does not own:** anything it displays; it only projects.
**First slice:** S12/S13 (match and competition read models), S14 (public web), S18 (statistics & history),
S20 (search, reports).
**Absorbs:** pack specs 30–34. Owner's manager: **History/Statistics Manager** — answered: *standalone module,
but read-only and rebuildable*.

## 11. Data Exchange

**Purpose:** getting data in and out safely.
**Owns:** import jobs (source file, template version, column mapping, rows, issues, resolutions, dry-run
results, execution batches, provenance), import templates (XLSX/JSON), export jobs, provider connections and
sync runs, provider entity mappings (later).
**Rules:** every write goes through the owning module's approved commands (same validation, authorization,
audit as the UI). Dry run never mutates. Re-running an import is idempotent via source row keys.
**First slice:** S19 (historical backfill & bulk import); providers later.
**Absorbs:** pack specs 35, 36. Owner's manager: **Backlog Manager** — answered: *standalone module that writes
only through other modules' commands*. Past matches are entered with Match Operations'
`contest.record_historical_result` (final score + optional key events, no live clock), then approved like any
result.

## 12. Engagement

**Purpose:** telling people what happened.
**Owns:** notification definitions and templates, user notification preferences, follows/favourites (teams,
competitions, contests), deliveries (dedup keys, status), reminder schedules.
**Rules:** never sends every detailed timeline event; respects preferences; tenant-branded templates;
deduplicates by event ID.
**First slice:** S21.
**Absorbs:** pack spec 37; the owner's Schedule Manager requirement "sending reminders and notifications of
game statuses".

## 13. Branding & Sponsorship

**Purpose:** each tenant's look and its commercial partners.
**Owns:** tenant brand versions (logo, colours mapped to tokens, typography choice, theme defaults, email
identity, white-label mobile config), sponsors, campaigns, assets (via media), placements (positions on public
pages), sponsor-free mode, ad-network slot configuration (e.g. Google AdSense) as a placement source type.
**Rules:** semantic colours (live, error, warning, success) cannot be overridden unsafely; contrast checks
before publish; removing sponsors never breaks layouts.
**First slice:** S22 (branding), S23 (sponsorship).
**Absorbs:** pack specs 38, 39. Owner's managers: **White label/Branding, Sponsor**.

---

## Owner's managers → modules (quick reference)

| Owner's manager / system | Where it lives now |
|---|---|
| Constant Manager | Split on purpose: reference data → Platform; statuses/rules → owning module's code; tunable values → Sports Configuration / tenant settings; app settings → environment config. Catalogues of actions, errors and events are **generated from code** (docs/generated/). |
| Seed Manager | Platform seed framework + per-module seeders (stable keys, environment guards) |
| Audit Manager | Platform (`audit` schema), recorded automatically by AuditScope and the audit middleware |
| Data Integrity Manager | Conventions + tests + DB constraints (docs/architecture/data-conventions.md, architecture tests) — not a runtime module |
| Analytics Manager | Platform (product analytics); never mixed with sports statistics |
| Sports Management System | Sports Configuration |
| Competition Format Management System | Sports Configuration |
| Tenant Manager | Tenancy & Commercial |
| Subscription/Feature Manager | Tenancy & Commercial (entitlements) + action catalogue |
| White label/Branding Manager | Branding & Sponsorship |
| Sponsor Manager | Branding & Sponsorship |
| Player Management System | Registry (Person + Athlete Profile) |
| Team Management System | Registry (team identity) + Team Operations (players, staff, transfers) + Competitions (rosters; UI on team page) |
| Competition Management System | Competitions |
| Schedule Management System | Match Operations (Fixtures) |
| Score / Live Score Management System | Match Operations (Live + Results) |
| History/Statistics Manager | Insights (+ Standings & Progression for tables/brackets) |
| Backlog Manager | Data Exchange (historical backfill via Match Operations commands) |
| (missing before) Users, roles, login | Identity & Access |
| (missing before) Files/photos/logos | Platform media (ADR-0014) |

## Pack specifications → modules (for looking up detail)

The 43 specs in docs/reference/chatgpt-pack/specifications/ remain useful for field lists, command names and
edge cases. Where they conflict with an ADR or this file, the ADR/this file wins.

| Pack spec | Module |
|---|---|
| 01 Master context map, 02 template | (superseded by this file) |
| 03 Sports Definition, 04 Competition Format, 05 Ruleset Registry | Sports Configuration |
| 06 Tenant, 07 Subscription & Entitlements | Tenancy & Commercial |
| 08 Person, 09 Participant/Athlete, 10 Organisation, 11 Team, 12 Staff, 13 Official, 14 Venue | Registry |
| 15 Team Membership, 17 Transfer | Team Operations |
| 16 Roster, 18 Competition, 19 Edition, 20 Registration, 21 Eligibility, 22 Stage/Group | Competitions |
| 23 Scheduling, 24 Contest Ops, 25 Lineup, 26 Live Scoring, 27 Timeline, 28 Results | Match Operations |
| 29 Progression | Standings & Progression |
| 30 Statistics, 31 Historical Records, 32 Read Models, 33 Search, 34 Reporting | Insights |
| 35 Historical Backfill, 36 Provider Integration | Data Exchange |
| 37 Notifications | Engagement |
| 38 Sponsorship, 39 Branding | Branding & Sponsorship |
| 40 Audit, 41 Analytics, 42 Seed, 43 Data Governance | Platform (+ conventions) |

**Known deviations from the pack** (intentional): the pack's polymorphic "Participant Registry" (spec 09) is
replaced by athlete profiles and teams in Registry plus **entrants** in Competitions — pairs, crews and relay
teams are added later as needed; Live Scoring has no separate score store (ADR-0002);
schedule status reduced to publication state + versions (ADR-0007); tenant/subscription states separated
(ADR-0007); rosters moved to Competitions (ADR-0008); configuration editors deferred (ADR-0006); contest shapes
reduced to `HEAD_TO_HEAD`/`FIELD` (ADR-0003); error-code prefixes use readable names (docs/architecture/api-conventions.md).
