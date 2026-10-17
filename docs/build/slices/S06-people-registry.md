# S06 — People registry (outline)

> **Status: outline.** The reviewer writes the detailed task breakdown and acceptance tests before this slice
> starts, based on what S01–S05 actually built. Do not start from this outline alone.

**Goal:** canonical people with strong duplicate prevention, role profiles (athlete, staff, official), minors'
consent and photos — the owner's "Player Manager", done so it can grow into a national register.
**Modules:** Registry (new), Platform (media), Tenancy (settings).
**Reference:** pack specs 08, 09, 12, 13 (fields, edge cases); ADR-0014, ADR-0015; data-conventions.md.

## Scope

- **Person:** names (first, middle, last, other names, preferred/display name), gender, date of birth,
  nationality (+ secondary), height, weight, photo, status, public-profile flag — each field classified
  `PUBLIC/INTERNAL/PRIVATE`.
- **Duplicate prevention ("avoid at all costs"):**
  - normalised name fields (trimmed, lower-case, accents removed), a **token-sorted name key** so
    "Ssekandi John" and "John Ssekandi" match, and a `pg_trgm` index;
  - `POST …/people/duplicate-check` returns candidates with confidence and evidence:
    `EXACT_IDENTIFIER` (same external ID authority + value — blocks creation),
    `LIKELY` (same date of birth and same name key or name similarity ≥ 0.8),
    `POSSIBLE` (similarity ≥ 0.6 with missing/±1-year birth year or same nationality; or same name key with a
    different date of birth);
  - creating a person requires a recent duplicate-check token; proceeding past `LIKELY`/`POSSIBLE` requires an
    override reason (audited); "not the same person" decisions are remembered so warnings don't repeat.
- Aliases and external identifiers (authority + value, unique per tenant).
- **Athlete profiles** (per sport variant: primary position, dominant side, public display name), **staff
  profiles** (role capabilities from sport definitions), **official profiles** (sport-role capabilities, grade,
  licence number + expiry).
- **Minors:** computed from date of birth; guardian consent records (scopes, evidence upload); public profile
  off by default and restricted (ADR-0015); registration policy from tenant setting.
- Photos through Platform media (variants, public/private).
- Public person endpoint (PUBLIC fields only); admin screens: directory, search-first create with duplicate
  review, detail tabs (Overview, Roles, Consent, External IDs, Audit).
- Seed (DEMO): realistic fictional Ugandan names, including near-duplicates, twins with the same surname and
  birth date, a person who is athlete + referee + coach, a minor with consent, an archived person.

## Key acceptance (to be expanded)

- Duplicate scenarios behave as specified; override reasons are audited.
- Public API never returns `INTERNAL/PRIVATE` fields; minors follow consent rules.
- Isolation harness covers all new tenant entities.

## Not in this slice

Merges (S25), memberships (S08), statistics tabs (later).
