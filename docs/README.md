# Documentation map

| Folder | Read when | Key files |
|---|---|---|
| `decisions/` | Before changing anything structural — ADRs are the highest source of truth | `README.md` (index) |
| `architecture/` | Designing or reviewing any backend/web change | `modules.md`, `rules.md`, `overview.md`, `multi-tenancy.md`, `authorization.md`, `sport-engine.md`, `events-and-jobs.md`, `api-conventions.md`, `data-conventions.md`, `national-registry.md` |
| `domain/` | Working on competitions, contests, scoring, standings | `state-machines.md`, `formats.md`, `rulesets.md`, `sports/*.yaml` |
| `product/` | Building UI, naming things, writing seeds | `vision.md`, `glossary.md`, `pilot-customer.md`, `pricing-and-plans.md`, `ux/public-app.md`, `ux/admin-app.md`, `ux/design-system.md` |
| `build/` | Every task | `workflow.md`, `roadmap.md`, `slices/`, `PROGRESS.md`, `review-protocol.md`, `definition-of-done.md`, `open-questions.md`, `KICKOFF.md` |
| `generated/` | Looking up actions, errors, events, entitlements | written by tools only |
| `background/` | Understanding why the plan looks like this | `review-of-previous-plans.md`, `first-attempt-code-review.md` |
| `reference/` | Looking for detail from earlier plans or the prototype | frozen; ADRs win on conflict; `first-attempt/README.md` |

Source-of-truth order: ADRs → architecture/domain → slice spec → reference → code.
