# Evolving into national registers

The owner's goal: "each separate system should expand into a national register wherever possible." This
document explains what we do **now** (cheap, already in the design) and what comes **later** (explicit,
governed features). Nothing here permits sharing data between tenants implicitly.

## What "national register" means here

A federation or national body (e.g. a football association, a schools sports council) operates an
authoritative register — licensed athletes, accredited officials, certified coaches, affiliated clubs, sanctioned
competitions, official results and records — that member organisations' records can be **linked** to.

## Candidate registers by module

| Module | National register it can become |
|---|---|
| Registry — people/athletes | National athlete register (licences, eligibility, clearance) |
| Registry — officials | National officials register (licences, grades, assignment history) |
| Registry — staff | Coach/medical-staff certification register |
| Registry — organisations, teams | Affiliated clubs/schools register |
| Registry — venues | Certified venues register |
| Competitions | Sanctioned competitions register |
| Match Operations + Insights | National results archive and records |
| Team Operations | Inter-club transfer clearance |

## Built in from the start (no extra cost later)

- Stable UUIDv7 identifiers that never change; external identifiers with an `authority` (FUFA, FIFA, school
  numbers …).
- Person separated from roles (one person can be athlete, coach and referee).
- Aliases, normalised names, duplicate detection and merge history in every registry.
- Provenance on imported/linked records (`source`, `import_job_id`).
- Effective-dated memberships and immutable, versioned results.
- Personal-data classification and minors' consent (ADR-0015).
- Public read models that contain only public data — the base for a national public archive.

## Later, as explicit features (each needs an ADR and legal review)

1. **Tenant types and hierarchy** — `tenant_type` (`FEDERATION`, `LEAGUE`, `CLUB`, `SCHOOL`, `ORGANISER`,
   `COMPANY`) and an optional `parent_tenant_id` (federation → member organisations).
2. **Federation registry tenant** — the federation's own Registry data (licensed players, officials…).
3. **Linking, not copying** — a member tenant proposes a link between its local record and a federation
   record (`registry_links`: local entity, federation tenant, federation entity, status
   `PROPOSED | CONFIRMED | REJECTED | REVOKED`, evidence, consent reference). A federation steward confirms.
   Only fields listed in the federation's **data-sharing agreement** flow, in the agreed direction.
4. **Stewardship** — cross-tenant duplicate resolution and merges only by federation stewards, fully audited,
   with dispute handling.
5. **National services** — licence lookups, eligibility checks against national suspensions, transfer clearance
   between member tenants, publishing approved results to the national archive, national records, officials'
   grading.
6. **Platform hub** — optional public aggregation across opted-in tenants (ADR-0010).

## Prerequisites before any cross-tenant sharing

- Legal basis and agreements (Uganda's Data Protection and Privacy Act, 2019 — consent, children, special data,
  cross-border storage, registration with the regulator; other countries' laws for other customers).
- A consent model that records who agreed to what sharing, revocable.
- Stewardship roles and procedures.
- Security review and audit coverage of every cross-tenant path.
