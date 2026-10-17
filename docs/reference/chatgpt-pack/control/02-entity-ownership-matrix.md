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
