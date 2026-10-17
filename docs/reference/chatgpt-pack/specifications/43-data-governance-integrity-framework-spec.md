# Data Governance & Integrity Framework — Full Specification

## 1. Purpose

Define cross-cutting rules that keep the platform's data consistent, tenant-safe, historically correct, and maintainable as it grows.

This is a framework enforced through schema conventions, shared libraries, migrations, CI, tests, and architecture review—not a generic "manager" that owns all data.

---

# 2. Governance Areas

```text
Identifiers
Tenant Scope
Naming
Foreign Keys
Uniqueness
Soft Delete / Archive
Temporal History
Versioning
Optimistic Concurrency
PII Classification
Public/Private Classification
Retention
Duplicate Prevention
External IDs
Audit Fields
Migration Rules
Import Provenance
Time Zones
Money/Currency
Units
```

---

# 3. Identifier Policy

Prefer stable opaque IDs.

Each domain may additionally have human-readable codes.

External/provider IDs never replace canonical IDs.

---

# 4. Tenant Policy

Every tenant-owned aggregate must have unambiguous tenant ownership.

Cross-tenant query/write requires explicit platform-level capability.

---

# 5. Delete Policy

Default:

```text
Never hard-delete historically referenced business entities.
```

Use:
- inactive
- archived
- merged
- reversed
- corrected

Hard delete limited to safe drafts/transient data by explicit policy.

---

# 6. Historical Policy

Preserve:
- membership periods
- roster history
- ruleset versions
- competition editions
- schedule changes
- contest status history
- timeline corrections
- result versions
- transfer history

---

# 7. Naming Policy

Canonical code vocabulary should match ubiquitous language.

Avoid ambiguous duplicates such as:

```text
match/game/event
player/athlete/person
league/competition/season
```

unless each has formally distinct meaning.

---

# 8. Time Policy

Store instants timezone-safely.

Keep venue/tenant timezone for presentation.

Do not store ambiguous local timestamps without timezone context.

---

# 9. PII Policy

Classify fields:

```text
PUBLIC
INTERNAL
PRIVATE
SENSITIVE
```

Person Registry is primary PII boundary.

Do not duplicate sensitive biodata into unrelated modules.

---

# 10. Duplicate Policy

Canonical registries should implement:
- normalized names
- aliases
- external IDs
- duplicate candidate workflow
- merge history

No automatic destructive merging.

---

# 11. Concurrency Policy

Use optimistic concurrency/version fields for mutable aggregates.

Live operational systems additionally use idempotency/version sequencing.

---

# 12. Migration Policy

Every schema migration must:
- be version controlled
- be reversible where practical
- preserve tenant scope
- preserve historical data
- include data migration when semantics change
- update tests/seeds

---

# 13. CI/Architecture Checks

Automate where possible:
- tenant-key presence
- forbidden cross-module references
- naming rules
- migration checks
- public/private DTO checks
- hard-delete restrictions
- duplicate action codes
- enum/code registry collisions

---

# 14. Errors / Violations

Potential architecture diagnostics:

```text
INTEGRITY_TENANT_SCOPE_MISSING
INTEGRITY_HARD_DELETE_FORBIDDEN
INTEGRITY_EXTERNAL_ID_COLLISION
INTEGRITY_HISTORY_BREAK
INTEGRITY_PUBLIC_FIELD_VIOLATION
INTEGRITY_MODULE_OWNERSHIP_VIOLATION
```

---

# 15. Definition of Done

Complete when data-integrity expectations are enforceable through shared policy, schema, tests, and CI rather than tribal knowledge.
