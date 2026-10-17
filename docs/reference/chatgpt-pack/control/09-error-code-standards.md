# Error Code Standards

## Format

```text
<DOMAIN>_<CONDITION>
```

Examples:

```text
TENANT_READ_ONLY
PERSON_DUPLICATE_POSSIBLE
ROSTER_LOCKED
CONTEST_NOT_READY
RESULT_ALREADY_APPROVED
```

## Prefixes

```text
REF_   Reference/Foundation
SPT_   Sports
FMT_   Competition Format
RLS_   Ruleset
TNT_   Tenant
ENT_   Entitlements
PER_   Person
PRT_   Participant
ORG_   Organisation
TEAM_  Team
STF_   Staff
OFF_   Official
VEN_   Venue
MEM_   Membership
ROS_   Roster
TRN_   Transfer
CMP_   Competition
EDN_   Edition
REG_   Registration
ELG_   Eligibility
STG_   Stage
SCH_   Scheduling
LIN_   Lineup
CONTEST_ Contest Operations
SCORE_ Live Scoring
TIMELINE_ Timeline
RESULT_ Results
PROGRESSION_ Progression
STAT_  Sports Statistics
SEARCH_ Search
REPORT_ Reporting
IMPORT_ Import
PROVIDER_ Provider
NOTIFICATION_ Notification
BRAND_ Branding
SPONSOR_ Sponsorship
AUDIT_ Audit
INTEGRITY_ Governance/Integrity
```

## HTTP Mapping Guidance

```text
400 validation / malformed command
401 unauthenticated
403 permission/entitlement denied
404 canonical entity not found
409 business state / concurrency conflict
422 semantically invalid domain request (optional consistent policy)
429 rate/quota limit where appropriate
500 unexpected internal failure
503 external provider temporarily unavailable
```

## Rules

1. Error code is stable; wording may improve.
2. UI logic may branch on code, never on message text.
3. Errors include correlation ID.
4. Sensitive internal details stay in logs, not public detail.
5. Domain errors are defined by owning module.
6. Never reuse one code for unrelated conditions.
