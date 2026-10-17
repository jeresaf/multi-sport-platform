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
