# Person Registry System — Full Specification

## 1. System Identity

**Name:** Person Registry System  
**Short Code:** `PERSON`

### Purpose

Provide a canonical tenant-aware identity record for human beings who may participate in sports as athletes, staff, officials, administrators, or other roles.

### Business Outcome

The same real person is not recreated independently as a player, coach, referee, or staff member, enabling accurate history and future federation/national-registry evolution.

---

# 2. Scope

## In Scope

- Person identity
- Core biodata
- Public/private field separation
- Identity aliases
- Duplicate detection
- Duplicate review
- Merge workflow
- Person status
- Identity documents metadata where permitted
- Tenant ownership/stewardship metadata
- Person history
- Public profile basics
- External identifiers

## Out of Scope

- Athlete sport profile
- Team membership
- Staff assignment
- Official assignment
- Competition registration
- User authentication credentials
- Medical record management
- Performance statistics

## Future Scope

- Federation-wide canonical identity
- National person/athlete registry
- Cross-tenant identity federation
- Identity verification providers

---

# 3. Ubiquitous Language

| Term | Definition |
|---|---|
| Person | Canonical record representing a human being |
| Alias | Alternative name or known identity label |
| Duplicate Candidate | Person record potentially representing the same human |
| Merge | Controlled consolidation of duplicate person identities |
| Steward | Authorized actor responsible for identity resolution |
| Public Profile | Approved public subset of a person's information |
| External Identifier | Identifier assigned by federation/provider/import source |

---

# 4. Actors

## Tenant Administrator
- Create/update people within tenant scope
- Review duplicates where permitted

## Registry Steward
- Resolve duplicate candidates
- Merge records
- Restore/clarify identity history

## Team Administrator
- Search/select existing people
- Propose new person if not found

## Import System
- Resolve or create people through controlled matching

## Public User
- Read approved public profile only

---

# 5. Dependencies

## Upstream

- Tenant Management
- Reference Data
- Audit
- Data Governance

## Downstream

- Participant/Athlete Registry
- Staff Registry
- Official Registry
- Team Operations
- Competition Registration
- Statistics/Records
- Search
- Historical Backfill

---

# 6. Ownership

## Source-of-Truth Entities

```text
Person
PersonName
PersonAlias
PersonExternalIdentifier
PersonDuplicateCandidate
PersonMergeRecord
PersonPublicProfileSettings
PersonLifecycleHistory
```

## Referenced External IDs

```text
tenant_id
country/nationality reference
```

## Must Not Mutate

- Athlete profiles
- Team memberships
- Staff assignments
- Official assignments
- User authentication accounts

---

# 7. Person Entity

Suggested fields:

```text
id
tenant_id
first_name
middle_name
last_name
other_names
preferred_name
gender_code
date_of_birth
nationality_code
secondary_nationality_code
height_cm
weight_kg
photo_asset_id
status
public_profile_enabled
created_at
created_by
updated_at
version
```

Not all fields should be mandatory.

Avoid requiring sport-specific fields.

---

# 8. Person Status

```text
ACTIVE
INACTIVE
DECEASED
MERGED
ARCHIVED
```

`MERGED` must reference surviving person ID.

---

# 9. Duplicate Prevention

Duplicate prevention is mandatory but must not rely on one field only.

Candidate matching inputs may include:

```text
normalized full name
date of birth
nationality
gender/classification
external identifiers
team history
phone/email only where permitted/private
```

Do not auto-merge based solely on similar names.

Use confidence/rules to produce:

```text
NO_MATCH
POSSIBLE_MATCH
LIKELY_MATCH
EXACT_IDENTIFIER_MATCH
```

Creation UI must show possible matches before final creation.

---

# 10. Merge Workflow

Suggested:

```text
Duplicate Candidate
→ Review
→ Confirm Same Person
→ Choose Survivor
→ Repoint permitted references
→ Preserve aliases/external IDs
→ Mark duplicate MERGED
→ Audit
```

Merge must be transactional and reversible only through specialized administrative repair, not normal UI.

---

# 11. Public vs Private Fields

Public candidate fields:

```text
display_name
photo
nationality
public age/year where policy allows
height/weight where sport/public policy allows
```

Private/admin:

```text
full date of birth
identity documents
private contacts
verification notes
duplicate-resolution notes
```

Visibility must be explicit.

---

# 12. Business Rules

## RULE-PER-001
A person belongs to exactly one tenant scope initially unless federation/national identity mode is explicitly introduced.

## RULE-PER-002
Duplicate checking runs before normal creation.

## RULE-PER-003
Possible duplicate warning cannot be silently ignored without recording an override reason where policy requires.

## RULE-PER-004
Merged persons cannot receive new operational relationships.

## RULE-PER-005
Public APIs expose only approved public fields.

## RULE-PER-006
Deleting a historically referenced person is prohibited; use status/archive.

## RULE-PER-007
External identifiers must be unique per provider/authority within applicable scope.

## RULE-PER-008
A person record must not encode team membership or competition registration directly.

---

# 13. Commands

```text
person.create
person.update
person.archive
person.restore
person.alias.add
person.external_id.add
person.duplicate.scan
person.duplicate.resolve
person.merge
person.public_profile.update
```

---

# 14. Queries

```text
Search people
Get person
Get public person
Find duplicate candidates
Get merge history
Get aliases
Get external IDs
Get role summary
```

Role summary may be a read projection from downstream systems.

---

# 15. Domain Events

```text
PersonCreated
PersonUpdated
PersonArchived
PersonRestored
PersonAliasAdded
PersonExternalIdentifierAdded
PersonDuplicateCandidateDetected
PersonMerged
PersonPublicProfileChanged
```

---

# 16. Permissions

```text
person.read
person.read_private
person.create
person.update
person.archive
person.duplicate.review
person.merge
person.public_profile.manage
```

---

# 17. Entitlements

Potential commercial entitlements:

```text
PERSON_REGISTRY
ADVANCED_DUPLICATE_DETECTION
FEDERATION_REGISTRY
BULK_PERSON_IMPORT
```

---

# 18. Audit

Audit:

- Create/update
- Private-field change
- Archive/restore
- Alias/external ID change
- Duplicate override
- Merge
- Public-profile visibility changes

Sensitive values may require redaction.

---

# 19. Data Integrity

- Tenant scoped
- Normalized name search fields
- Unique provider+external ID
- Merge survivor cannot equal duplicate
- Merged chain must resolve to one active survivor
- Optimistic concurrency
- No hard delete once referenced

---

# 20. Security & Privacy

Person Registry contains PII.

Requirements:

- Field-level public/private classification
- Least-privilege private access
- Audit private reads if policy requires
- Avoid public exposure of exact DOB by default
- Export restrictions
- Tenant isolation
- Future national-registry governance before cross-tenant sharing

---

# 21. Import / Export

Import supports:

```text
CSV
Excel
JSON
Migration/API
```

Flow:

```text
Upload
→ map columns
→ normalize
→ duplicate scan
→ preview matches/new records
→ resolve
→ import through commands
→ report
```

Exports must respect privacy/permission.

---

# 22. API Contracts

Admin:

```text
GET  /api/admin/people
POST /api/admin/people
GET  /api/admin/people/{id}
PATCH /api/admin/people/{id}
GET  /api/admin/people/duplicates
POST /api/admin/people/{id}/merge
```

Public:

```text
GET /api/public/people/{id}
```

only if public profile enabled.

---

# 23. UI Workflows

## Person Directory

Filters:

```text
Name
Status
Nationality
Role summary
Duplicate warning
```

## Create Person

Steps:

```text
1 Search existing
2 Biodata
3 Photo
4 External IDs
5 Duplicate Review
6 Save
```

## Duplicate Review

Side-by-side comparison:

```text
Record A
Record B
Matching evidence
Conflicts
Relationships summary
```

Actions:

```text
Not Same Person
Merge
Needs Investigation
```

---

# 24. Notifications

Usually minimal.

Potential:

- Duplicate review assigned
- Merge completed
- Import requires resolution

---

# 25. Analytics

Track:

```text
person_created
duplicate_candidate_detected
duplicate_avoided
person_merged
bulk_import_completed
```

Never send sensitive biodata into generic analytics payloads.

---

# 26. Seed Data

Seed realistic fictional people with:

- Similar names to test duplicates
- Same surname different DOB
- Exact external-ID duplicate
- Athlete/staff/official multi-role example
- Archived/merged example

---

# 27. Error Catalogue

```text
PERSON_NOT_FOUND
PERSON_DUPLICATE_POSSIBLE
PERSON_DUPLICATE_OVERRIDE_REQUIRED
PERSON_EXTERNAL_ID_EXISTS
PERSON_MERGED
PERSON_ARCHIVED
PERSON_MERGE_CONFLICT
PERSON_PRIVATE_FIELD_FORBIDDEN
```

---

# 28. Edge Cases

- Twins/same names/same DOB
- Name change
- Missing DOB
- Unknown nationality
- Incorrect imported DOB
- Person both athlete and official
- Duplicate discovered after years of history
- Conflicting external IDs

---

# 29. Background Jobs

- Duplicate candidate rescan
- Search normalization/index refresh
- Optional public-profile projection rebuild

---

# 30. Performance

Expect potentially national-scale person counts.

Requirements:

- Indexed normalized names
- DOB/nationality indexes where privacy policy permits
- External ID index
- Pagination
- Search projection/index
- Duplicate matching optimized asynchronously for bulk imports

---

# 31. Observability

Track:

- duplicate scan duration
- merge failures
- import conflict rate
- private access denials
- person-search latency

---

# 32. Tests

- Create
- Duplicate warning
- Exact identifier match
- Merge
- Merge history
- Public/private visibility
- Tenant isolation
- Import duplicate resolution
- Archived/merged behavior
- Multi-role person references

---

# 33. Acceptance Criteria

1. Search for person before create.
2. Create new person.
3. Create athlete profile downstream.
4. Reuse same person as staff profile downstream.
5. Attempt duplicate creation.
6. See duplicate warning.
7. Resolve as existing person.
8. Import duplicate via CSV.
9. Detect candidate.
10. Merge true duplicate.
11. Verify all supported references point to survivor.
12. Verify public API excludes private fields.
13. Verify tenant B cannot see tenant A person.

---

# 34. Definition of Done

Complete when all human-role systems can reference one canonical person identity and duplicate prevention/merge preserves history safely.
