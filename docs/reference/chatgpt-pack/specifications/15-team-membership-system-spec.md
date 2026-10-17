# Team Membership System — Full Specification

## 1. Purpose

Own the effective-dated relationship between an athlete/person participant and a team independently of competition-specific rosters.

### Core Relationship

```text
Person/Athlete
   ↓
Team Membership
   ↓
Competition Roster
   ↓
Match Squad
```

---

# 2. Scope

In scope:
- Team membership
- Effective dates
- Membership status
- Playing position
- Shirt number defaults
- Loan/temporary membership flag
- Historical membership
- Team staff assignment may use a parallel assignment structure but is not player membership

Out of scope:
- Competition eligibility
- Competition roster
- Transfers workflow itself
- Match squad
- Person biodata
- Team identity

---

# 3. Dependencies

- Participant/Athlete Registry
- Team Registry
- Sports Definition
- Tenant Management
- Audit

Downstream:
- Roster Management
- Transfer Management
- Competition Registration
- Statistics/Records
- Public team/player history

---

# 4. Core Entity

```text
TeamMembership
```

Fields:

```text
id
tenant_id
team_id
participant_id
start_date
end_date
status
membership_type
primary_position_code
default_shirt_number
is_loan
source_transfer_id
created_at
created_by
updated_at
version
```

---

# 5. Membership Status

```text
PENDING
ACTIVE
INACTIVE
SUSPENDED
TRANSFERRED_OUT
RELEASED
EXPIRED
```

---

# 6. Business Rules

## RULE-MEM-001
Membership dates may not be inverted.

## RULE-MEM-002
Participant and team sport must be compatible unless explicitly supported by rules.

## RULE-MEM-003
A participant may have multiple historical memberships.

## RULE-MEM-004
Concurrent active memberships are governed by sport/tenant policy, not universally forbidden.

## RULE-MEM-005
Changing teams must not erase prior membership.

## RULE-MEM-006
Membership does not automatically put a participant on a competition roster.

## RULE-MEM-007
Shirt number on membership is a default and may be overridden per competition/contest.

---

# 7. Commands

```text
membership.create
membership.activate
membership.update
membership.suspend
membership.release
membership.expire
membership.transfer_out
```

---

# 8. Queries

```text
Get current memberships
Get team members
Get participant membership history
Check membership on date
Get eligible membership candidates for roster
```

---

# 9. Events

```text
TeamMembershipCreated
TeamMembershipActivated
TeamMembershipSuspended
TeamMembershipReleased
TeamMembershipExpired
TeamMembershipTransferredOut
```

---

# 10. Permissions

```text
membership.read
membership.create
membership.update
membership.lifecycle.manage
```

---

# 11. Integrity

- Tenant scoped
- Valid team/participant refs
- Effective-date indexes
- No destructive deletion after roster/result use
- Optimistic concurrency

---

# 12. UI

Team → Players  
Player → Team History  
Membership Create/Edit  
Membership Timeline

---

# 13. Errors

```text
MEMBERSHIP_NOT_FOUND
MEMBERSHIP_DATE_INVALID
MEMBERSHIP_SPORT_MISMATCH
MEMBERSHIP_ALREADY_ACTIVE
MEMBERSHIP_CONFLICT
```

---

# 14. Tests

- Current membership
- Historical membership
- Concurrent membership policy
- Release
- Transfer-out
- Shirt-number override behavior
- Tenant isolation

---

# 15. Definition of Done

Complete when athlete/team relationships are historically accurate and rosters can select from valid memberships without owning membership history themselves.
