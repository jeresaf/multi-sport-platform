# Staff Registry — Full Specification

## 1. Purpose

Represent sport/team staff roles as profiles linked to canonical Person records without duplicating human identity.

Staff examples:

```text
HEAD_COACH
ASSISTANT_COACH
TEAM_MANAGER
PHYSIO
DOCTOR
ANALYST
EQUIPMENT_MANAGER
```

---

# 2. Scope

In scope:
- Staff profile
- Sport/role capability
- Qualifications/certifications metadata
- Staff lifecycle
- Public profile fields
- Staff aliases/external IDs

Out of scope:
- Canonical biodata
- Team assignment
- Employment/payroll
- Match official roles
- User authentication

---

# 3. Dependencies

- Person Registry
- Sports Definition
- Tenant Management
- Audit

Downstream:
- Team Staff Assignment
- Competition Operations
- Public Team pages
- Historical Records

---

# 4. Core Model

```text
StaffProfile
StaffQualification
StaffExternalIdentifier
StaffLifecycleHistory
```

Fields:

```text
id
tenant_id
person_id
primary_role_code
sport_id
sport_variant_id
status
public_enabled
created_at
updated_at
```

---

# 5. Lifecycle

```text
ACTIVE
INACTIVE
SUSPENDED
RETIRED
ARCHIVED
```

---

# 6. Business Rules

## RULE-STF-001
Every staff profile references one canonical Person.

## RULE-STF-002
Role code should be valid for the sport when a sport-specific definition applies.

## RULE-STF-003
Staff profile does not imply team assignment.

## RULE-STF-004
One person may hold multiple staff roles over time.

## RULE-STF-005
Historical assignments remain after retirement/archive.

---

# 7. Commands

```text
staff.create
staff.update
staff.activate
staff.suspend
staff.retire
staff.qualification.add
staff.external_id.add
```

---

# 8. Queries

```text
Search staff
Get staff profile
Get qualifications
Get current assignments
Get history
```

---

# 9. Events

```text
StaffProfileCreated
StaffProfileUpdated
StaffSuspended
StaffRetired
StaffQualificationAdded
```

---

# 10. Permissions

```text
staff.read
staff.create
staff.update
staff.lifecycle.manage
staff.qualification.manage
```

---

# 11. Integrity

- Tenant scoped
- Person required
- No duplicate active profile for same person+sport+role unless explicitly allowed
- No hard-delete of historical profile

---

# 12. UI

Staff Directory  
Staff Detail  
Qualifications  
Assignments (read projection)  
History

---

# 13. Seed/Test

Include:
- coach
- physio
- same person with multiple staff roles
- retired staff
- staff later acting as official via separate Official Profile

---

# 14. Definition of Done

Complete when staff roles can reference one canonical person identity and can be assigned to teams/competitions without duplicating biodata.
