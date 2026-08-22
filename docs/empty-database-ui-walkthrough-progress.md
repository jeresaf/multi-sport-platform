# Empty-database UI walkthrough progress

Use this log with `empty-database-ui-use-case-catalog.md`. The catalogue owns
the steps and expected outcomes; this file records only executed cases and
findings. Any case not listed here is `NOT RUN`.

Environment reset: 2026-08-22  
Docker project: `sports-platform-walkthrough`  
Database mode: bootstrap definitions only

## Completed cases

| Case | Status | Surface | Evidence or note |
| --- | --- | --- | --- |
| `ENV-001` | `PASS` | Docker/operations | Isolated volumes recreated; API, admin, and public web returned HTTP 200; business tenant, competition, participant, contest, registration, registration-form, and sponsor counts are zero. |

## Findings to fix

Add one row per finding before changing code, then update the same row after
the fix and regression run.

| Case | Status | Surface/device | Step | Expected | Actual | Evidence/request | Retest |
| --- | --- | --- | --- | --- | --- | --- | --- |

## Current checkpoint

Next case: `AUTH-001` — valid and invalid admin sign-in.

