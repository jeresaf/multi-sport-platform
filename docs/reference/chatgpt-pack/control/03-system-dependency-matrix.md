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
