# Audit System — Full Specification

## 1. Purpose

Provide immutable, searchable records of meaningful user and system actions across the platform.

---

# 2. Scope

In scope:
- Action audit
- Entity references
- Actor
- Tenant
- Source
- Timestamp
- Correlation ID
- Reason
- Before/after metadata where safe
- System/background actions
- Export/search
- Retention policy

Out of scope:
- Operational application logs
- Analytics
- Sport timeline
- Full database change-data-capture unless separately introduced

---

# 3. Core Model

```text
AuditEntry
AuditEntityReference
AuditMetadata
AuditExport
```

Fields:

```text
id
tenant_id
actor_type
actor_id
action_code
entity_type
entity_id
occurred_at
source
correlation_id
reason
outcome
metadata_json
```

---

# 4. Business Rules

## RULE-AUD-001
Audit entries are append-only.

## RULE-AUD-002
Audit must not store secrets/passwords.

## RULE-AUD-003
Sensitive before/after values may be redacted.

## RULE-AUD-004
Tenant users can only view permitted tenant audit data.

## RULE-AUD-005
Platform-level actions may have no tenant or may reference target tenant.

## RULE-AUD-006
Critical domain actions define mandatory audit requirements in their own specs.

---

# 5. Commands

Audit is mainly append/query:

```text
audit.record
audit.export
```

Domain modules should use a shared audit interface.

---

# 6. UI

Audit Explorer filters:

```text
Tenant
Actor
Action
Module
Entity
Outcome
Date range
Correlation ID
```

---

# 7. Performance

Audit can grow very large.

Use:
- partitioning strategy where needed
- date indexes
- tenant/action indexes
- retention/archive
- asynchronous export

---

# 8. Tests

- append-only
- sensitive redaction
- tenant isolation
- correlation
- background job actor
- export

---

# 9. Definition of Done

Complete when every critical domain action can be traced without depending on mutable business tables or general application logs.
