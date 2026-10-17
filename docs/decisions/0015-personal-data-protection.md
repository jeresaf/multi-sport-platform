# ADR-0015 — Personal data protection by design (classification, minors' consent, data location)

**Status:** Accepted
**Date:** 2026-10-17 (data location settled before first merge by ADR-0016)
**Deciders:** Owner, Reviewer

## Context

The platform stores personal data about athletes, staff and officials, including **children** (schools,
academies, youth leagues). Uganda's Data Protection and Privacy Act, 2019 includes, among other things:
consent of a parent/guardian for processing children's personal data (s.8); restrictions on special personal
data such as health status and medical records (s.9); conditions for processing or storing personal data
outside Uganda (s.19); breach notification (s.23); and the regulator's data protection register (s.29), with
registration required by the Data Protection and Privacy Regulations 2021 (reg. 13). (The Computer Misuse
(Amendment) Act 2022 also made sharing a child's information online without parental consent an offence, but
the Constitutional Court struck that Act down in March 2026; s.8 of the DPPA still applies.) Customers in other
countries will bring their own laws. This ADR is an engineering stance, **not legal advice** — the owner
should confirm obligations with a lawyer before pilots.

## Decision

- **Field classification** for every personal-data field: `PUBLIC`, `INTERNAL`, `PRIVATE`, `SENSITIVE`
  (docs/architecture/data-conventions.md). Public APIs and read models may only contain `PUBLIC` fields.
- **Registry is the PII boundary.** Other modules reference people by ID and copy only display names into
  read models.
- **Minors:** a person under 18 at the reference date is a minor. For minors:
  - public profile is **off by default**; when enabled, it shows only display name, team and age group
    (no date of birth; photo only if the consent covers photos);
  - a **guardian consent record** (guardian name, relationship, private contact, scopes such as
    DATA_PROCESSING / PUBLIC_PROFILE / PHOTO, date, evidence asset, recorded_by) is required before public
    exposure and — configurable per tenant — before competition registration.
- **No medical records** are stored. Injury events in a timeline record only that an injury stoppage
  happened, never diagnosis details.
- National ID numbers, if ever collected, are `SENSITIVE`: encrypted at rest at column level, never in logs,
  analytics or exports without explicit permission.
- Logs, analytics events and audit metadata never contain `PRIVATE`/`SENSITIVE` values (redaction tests).
- **Data location:** decided in ADR-0016 — primary data (database, backups, files, logs) in Azure South Africa
  North. Legal basis for storage outside Uganda: s.19(a) (South Africa has POPIA and a regulator) **and**
  s.19(b) consent. The `DATA_PROCESSING` scope of a guardian consent, and the privacy notice adults accept at
  registration, therefore state where data is stored and name the sub-processors. ADR-0016 lists the
  compliance tasks that gate the first pilot (PDPO registration, processing agreement, transfer register,
  breach runbook).
- Data-subject requests (export, correction) are supported by Registry tooling in the hardening phase.

## Consequences

- Positive: privacy rules are testable; national-registry evolution starts from a defensible base.
- Negative: extra fields and checks in Registry (consent, classification) from S06.

## Review trigger

Legal advice, a regulator requirement, or a customer contract with different obligations.
