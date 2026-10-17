# Official Registry — Full Specification

## 1. Purpose

Represent sports officials as profiles linked to canonical Person records, with sport-specific role capability and qualification metadata.

Examples:

```text
REFEREE
ASSISTANT_REFEREE
UMPIRE
JUDGE
SCORER
TIMEKEEPER
MATCH_COMMISSIONER
TMO
THIRD_UMPIRE
```

---

# 2. Scope

In scope:
- Official profile
- Sport/variant role capability
- Qualifications/licenses
- Grade/level
- Availability metadata
- Lifecycle
- Public profile fields
- External IDs

Out of scope:
- Canonical biodata
- Contest assignment
- Scheduling conflict ownership
- Payroll/payment
- User login

---

# 3. Dependencies

Upstream:
- Person Registry
- Sports Definition
- Tenant Management
- Audit

Downstream:
- Scheduling
- Contest Operations
- Reporting
- Historical Records

---

# 4. Core Model

```text
OfficialProfile
OfficialRoleCapability
OfficialQualification
OfficialExternalIdentifier
OfficialAvailability
OfficialLifecycleHistory
```

---

# 5. Business Rules

## RULE-OFF-001
Every official profile references one Person.

## RULE-OFF-002
Official role capability must be valid for the sport definition.

## RULE-OFF-003
Official profile does not equal contest assignment.

## RULE-OFF-004
One person may be staff and official simultaneously through separate role profiles.

## RULE-OFF-005
Availability does not guarantee assignability; Scheduling checks conflicts and competition rules.

---

# 6. Lifecycle

```text
ACTIVE
INACTIVE
SUSPENDED
RETIRED
ARCHIVED
```

---

# 7. Commands

```text
official.create
official.update
official.role.add
official.qualification.add
official.availability.set
official.suspend
official.retire
```

---

# 8. Queries

```text
Search officials
Find officials by sport/role
Get qualifications
Get availability
Get assignments (projection)
Get history
```

---

# 9. Events

```text
OfficialCreated
OfficialRoleCapabilityAdded
OfficialQualificationAdded
OfficialAvailabilityChanged
OfficialSuspended
OfficialRetired
```

---

# 10. Permissions

```text
official.read
official.create
official.update
official.lifecycle.manage
official.qualification.manage
official.availability.manage
```

---

# 11. Integrity

- Tenant scoped
- Person required
- Sport-role compatibility
- License/external identifiers unique where applicable
- Historical records preserved

---

# 12. UI

Official Directory  
Official Detail  
Roles  
Qualifications  
Availability Calendar  
Assignments  
History

---

# 13. Errors

```text
OFFICIAL_NOT_FOUND
OFFICIAL_ROLE_INVALID
OFFICIAL_SUSPENDED
OFFICIAL_QUALIFICATION_INVALID
OFFICIAL_EXTERNAL_ID_EXISTS
```

---

# 14. Tests

- Create
- Multi-role official
- Staff+official same person
- Availability
- Suspension
- Sport-role validation
- Tenant isolation

---

# 15. Definition of Done

Complete when Scheduling and Contest Operations can select qualified officials without duplicating person identity.
