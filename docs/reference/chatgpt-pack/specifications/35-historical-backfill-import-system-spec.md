# Historical Backfill & Import System — Full Specification

## 1. Purpose

Provide controlled ingestion of historical or bulk data through canonical domain commands rather than direct table writes.

---

# 2. Supported Inputs

```text
CSV
Excel
JSON
External API
Legacy database migration
Manual historical-entry workflow
```

---

# 3. Core Flow

```text
Upload / Source
   ↓
Parse
   ↓
Map
   ↓
Normalize
   ↓
Validate
   ↓
Duplicate Resolution
   ↓
Dry Run
   ↓
Domain Commands
   ↓
Audit
   ↓
Import Report
```

---

# 4. Scope

In scope:
- templates
- field mapping
- validation
- reference resolution
- duplicate matching
- dry run
- batch execution
- error report
- resume/retry
- import history
- provenance

Out of scope:
- domain-rule ownership
- arbitrary SQL import
- bypassing permissions
- source-of-truth entity ownership

---

# 5. Core Model

```text
ImportJob
ImportSource
ImportMapping
ImportRow
ImportIssue
ImportResolution
ImportBatch
ImportProvenance
```

---

# 6. Business Rules

## RULE-IMP-001
Imports must invoke approved application/domain operations.

## RULE-IMP-002
Dry-run must not mutate authoritative data.

## RULE-IMP-003
Duplicate conflicts require explicit resolution where confidence insufficient.

## RULE-IMP-004
Each created/updated entity should retain import provenance.

## RULE-IMP-005
Partial imports require clear batch/result semantics.

## RULE-IMP-006
Re-running same import should support idempotency where feasible.

---

# 7. Commands

```text
import.create
import.upload
import.map
import.validate
import.dry_run
import.resolve_issue
import.execute
import.pause
import.resume
import.cancel
```

---

# 8. UI

Import Center  
Template Download  
Upload  
Column Mapping  
Validation Summary  
Duplicate Resolution  
Dry Run Preview  
Execution Progress  
Error Report  
Import History

---

# 9. Errors

```text
IMPORT_FORMAT_INVALID
IMPORT_MAPPING_INVALID
IMPORT_REFERENCE_NOT_FOUND
IMPORT_DUPLICATE_UNRESOLVED
IMPORT_DOMAIN_VALIDATION_FAILED
IMPORT_PARTIAL_FAILURE
```

---

# 10. Tests

- person import
- team import
- competition import
- roster import
- historical results
- duplicate resolution
- dry run
- retry
- idempotency
- tenant isolation

---

# 11. Definition of Done

Complete when old/bulk data can enter the platform without bypassing the same rules used by normal UI/API operations.
