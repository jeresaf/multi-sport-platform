# Search System — Full Specification

## 1. Purpose

Provide tenant-aware universal search across public and administrative entities using approved projections.

---

# 2. Searchable Entities

```text
Competition
Edition
Team
Organisation
Person/Athlete (public/admin scope differs)
Participant
Contest
Venue
Official (admin)
Sport
```

---

# 3. Dependencies

- Read Models
- Person Registry
- Team Registry
- Competition
- Venue
- Tenant
- Data Governance

---

# 4. Core Model

```text
SearchDocument
SearchAlias
SearchIndexVersion
SearchRebuildRun
```

---

# 5. Business Rules

## RULE-SRCH-001
Search respects tenant scope.

## RULE-SRCH-002
Public search indexes only public-approved fields/entities.

## RULE-SRCH-003
Aliases and external names may improve matching but must not alter canonical identity.

## RULE-SRCH-004
Merged/archive entities should resolve appropriately without producing confusing duplicates.

## RULE-SRCH-005
Search ranking must not leak private metadata.

---

# 6. Queries

```text
Universal search
Search by entity type
Autocomplete
Admin advanced search
Alias search
```

---

# 7. UI

Grouped results:

```text
Competitions
Teams
Athletes
Matches
Venues
Sports
```

Admin may include additional entity groups.

---

# 8. Performance

- debounce
- autocomplete cache
- normalized text
- typo tolerance where supported
- pagination
- search index rebuild capability

---

# 9. Tests

- aliases
- long names
- tenant isolation
- public/private separation
- merged person
- archived team
- competition/edition distinction

---

# 10. Definition of Done

Complete when users can reliably find domain entities without bypassing privacy or tenant boundaries.
