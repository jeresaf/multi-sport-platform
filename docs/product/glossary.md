# Glossary (ubiquitous language)

Use these words in code, APIs, database objects, events and documentation. UI labels may use sport terminology
from definitions (Match, Game, Bout, Innings…). New terms are added here first.

## Commercial and access

| Term | Meaning | Not the same as |
|---|---|---|
| Platform | The whole product operated by us | A tenant |
| Tenant | A customer organisation leasing the platform; isolated data | A club or team record |
| Tenant domain | Host name that routes to a tenant's public site | Competition URL |
| Access mode | Computed `FULL` / `READ_ONLY` / `BLOCKED` from tenant + subscription | Tenant status |
| Plan | Commercial package (`STANDARD`, `PRO`) | Subscription |
| Subscription | A tenant's dated assignment to a plan (periods) | Entitlement |
| Entitlement | A capability or limit a tenant has paid for | Permission |
| Action code | Name of a business operation (`contest.start`); also the permission | UI button label |
| Role | Named set of action codes | Job title |
| Role assignment | Tenant membership + role + scope (`TENANT`/`COMPETITION`/`TEAM`/`CONTEST`) | Team membership |
| Tenant membership | A user's access to a tenant | Player membership |
| User | A login account | Person |

## Registry

| Term | Meaning | Not the same as |
|---|---|---|
| Person | Canonical human identity and biodata | User, player record |
| Athlete profile | A person's participation identity in one sport variant | Person biodata |
| Staff profile | A person's staff capability (coach, physio…) | Staff assignment to a team |
| Official profile | A person's officiating capability, grades, licences | Contest assignment |
| Guardian consent | Recorded consent for a minor's data/public exposure | Registration approval |
| Organisation | Long-lived institution: club, school, academy, federation, company | Team, tenant |
| Team | Persistent playing identity (e.g. Ntinda Swifts FC Senior Men) | Roster, organisation |
| Venue | Persistent place where contests happen | Fixture |

## Team operations

| Term | Meaning | Not the same as |
|---|---|---|
| Player membership | Effective-dated relationship athlete ↔ team | Roster entry |
| Staff assignment | Effective-dated relationship staff profile ↔ team, with role | Staff profile |
| Transfer / loan | Workflow moving a player between teams | Roster change |

## Configuration

| Term | Meaning | Not the same as |
|---|---|---|
| Sport / sport variant | Discipline / playable form (`RUGBY_UNION_7`) | Competition |
| Sport definition | Versioned data describing a variant | Ruleset |
| Scoring family | Engine computing score/outcome (`ADDITIVE`, `INNINGS`…) | Sport |
| Contest shape | `HEAD_TO_HEAD` or `FIELD` | Format |
| Coverage level | Detail recorded: `RESULT_ONLY`, `BASIC`, `STANDARD`, `DETAILED` | Entitlement |
| Format | How a competition is organised (round robin, knockout…) | Sport rules |
| Ruleset template | Sport + format + policy defaults with editable parameters | Effective rules |
| Effective rules snapshot | Frozen, hashed rules an edition runs on | Template |

## Competitions

| Term | Meaning | Not the same as |
|---|---|---|
| Competition | Persistent property (Metro Football League) | Edition |
| Edition | One occurrence/season (2027) with frozen rules | Competition |
| Registration | Application to enter an edition | Roster |
| Entrant | An approved registration — the team or athlete that appears in fixtures and standings | Team, person |
| Roster | An entrant team's competition-specific player list | Player membership, lineup |
| Eligibility evaluation | Contextual check against the edition rules | Person status |
| Stage / group (pool) / round | Edition structure from the format | Sport period |
| Slot | Placeholder in structure (`Winner QF1`) filled by progression | Contest participant |
| Draw | Recorded seeding/placement with inputs and random seed | Fixture generation |

## Match operations

| Term | Meaning | Not the same as |
|---|---|---|
| Contest | Universal unit of competition (match, game, bout, heat) | Always "match" |
| Fixture | A contest viewed as planned (time, venue, publication) | Live state |
| Schedule version | One time/venue/reason record of a contest | Contest status |
| Contest status | Operational lifecycle (`NOT_STARTED`, `LIVE`, `ENDED`…) | Sport phase |
| Sport phase / period | Segment of play from the definition (`SECOND_HALF`) | Status, round |
| Lineup | Contest-specific starters and bench from the roster | Roster |
| Readiness | Configured checks before a contest can start | Eligibility |
| Timeline event | Append-only record of something that happened in a contest | Audit entry |
| Correction / reversal | New event superseding / cancelling an earlier one | Edit / delete |
| Score snapshot | Cached result of folding the timeline | Official result |
| Scorer lease | Which device may record events for a contest right now | Permission |
| Result / result version | Official outcome workflow; versions are immutable once approved | Live score |
| Result method | How it was decided (`REGULATION`, `SHOOTOUT`, `WALKOVER`…) | Contest status |

## Downstream

| Term | Meaning | Not the same as |
|---|---|---|
| Standings / progression | Tables, brackets, series, qualification derived from approved results | Results |
| Tiebreaker | Ordered criterion that separates level entrants | Ruleset |
| Statistic | Sports performance measure | Platform analytics |
| Historical record | Durable projection (champions, careers, records) | Operational data |
| Read model / projection | Rebuildable view for fast reads | Source of truth |
| Import job / dry run | Controlled ingestion through commands / validation without writes | Seed |
| Backfill | Entering past competitions and results | Software backlog |
| Provider | External data source mapped into canonical entities | Tenant |
| External identifier | Federation/provider ID attached to an entity | Primary key |
| Follow | A user's subscription to updates about a team/competition/contest | Subscription (commercial) |
| Brand version | A tenant's published look | Sponsor |
| Sponsor / campaign / placement | Commercial partner content and where it appears | Branding |
| Audit entry | Append-only record of a meaningful action | Application log |
| Analytics event | Product-usage telemetry | Statistic |

## Naming rules for code

1. `contest` in generic code; sport words only in definitions, engines and renderers.
2. `person` for identity; `athlete` for sport participation; `player` only in UI labels.
3. `edition` (not `season`) in code; `season_label` is a display field.
4. `organisation` for institutions; `team` for playing entities.
5. `roster` only for competition lists; `lineup` for contest selections; `membership` for team relationships.
6. `result` only for the official outcome workflow; `score` for derived live state.
7. Never `analytics` for sports statistics; never a generic `constants` bucket for domain values.
8. British spelling in identifiers is fine (`organisation`) — be consistent.
