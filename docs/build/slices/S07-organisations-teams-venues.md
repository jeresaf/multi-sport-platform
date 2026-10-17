# S07 — Organisations, teams and venues (outline)

> **Status: outline.** The reviewer writes the detailed spec before this slice starts.

**Goal:** persistent identities for clubs/schools/federations, their teams, and the venues where contests happen.
**Modules:** Registry, Platform (media), Tenancy (enabled sports, `MAX_TEAMS`).
**Reference:** pack specs 10, 11, 14.

## Scope

- **Organisations:** type (`CLUB, SCHOOL, UNIVERSITY, ACADEMY, FEDERATION, ASSOCIATION, COMPANY, COMMUNITY,
  GOVERNMENT, OTHER`), legal/display/short names, parent organisation (no cycles), logo, private contacts, public
  profile, aliases, external IDs, lifecycle.
- **Teams:** sport variant (must be enabled for the tenant), structured category (gender category + age group +
  level, e.g. Senior Men, U19 Women), optional organisation, name, short name, slug, 3-letter code for
  scoreboards, motto, colours, logo and badge, home venue, aliases with effective dates (renames keep history),
  lifecycle. Quota `MAX_TEAMS`.
- **Venues:** type, address, coordinates, **IANA time zone (required)**, capacity, surfaces, supported sport
  variants, lifecycle.
- Lighter duplicate warnings (normalised name + organisation + sport + city).
- Public endpoints and admin screens (directories, create/edit, detail).
- Seed (DEMO): the clubs, teams and venues in docs/product/pilot-customer.md ("Seed set") — at least the
  multi-sport clubs (Ntinda Swifts SC, Kisaasi Kestrels SC, Kololo Kudus Club) plus enough football clubs for a
  10-team league; venues Bukoto Community Stadium, Kisaasi Recreation Ground, Ggaba Lakeside Rugby Ground,
  Metro Indoor Arena and Seeta Hill Sports Field. ELSSC gets a few schools and one venue.

## Key acceptance

- An organisation can run teams in several sports; renaming a team keeps the old name as a dated alias.
- A team cannot be created for a sport the tenant hasn't enabled.
- Isolation and authorization tests for all new endpoints.
