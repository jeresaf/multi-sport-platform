# First attempt — selected documents (frozen)

Copied on 2026-10-17 from the prototype in this repository (`jeresaf/multi-sport-platform`, commit `8e72a36`),
which is preserved at tag `prototype-v0` and branch `prototype/v0`. Why the project restarted:
docs/background/first-attempt-code-review.md.

**Background, not instructions.** These files describe the prototype, not the system being built. Where they
conflict with `docs/decisions/` or `docs/architecture/`, those win. Never port prototype code or patterns; take
ideas, then implement them the kit's way. Never edit files here.

| File | Why it is kept | Use it when |
|---|---|---|
| `empty-database-ui-use-case-catalog.md` | A thorough, dependency-ordered list of user journeys (about 1,080 lines) with expected outcomes | Writing detailed slice specs and E2E acceptance scenarios (S14 and later). Prototype passwords are redacted; test accounts in the new system come from environment configuration |
| `adr/` | The prototype's seven decisions (modular monolith, universal contest model, shared-schema tenancy, ordered offline replay, SignalR after persistence, versioned rulesets, append-only audit) | Comparing with the kit's ADRs; offline replay ideas for S27 |
| `operations/` | Runbooks: backup and recovery, configuration and secrets, migrations, monitoring, observability, security verification, support tooling | Phase 9 hardening and the infrastructure task after M1 (adapt to Azure, ADR-0016) |
| `architecture.md` | The prototype's own architecture record, including its admission that it had no tenant query filter and no row-level security | Understanding the review |
| `design-tokens.json` | Starter colour, spacing, radius, type and breakpoint values | S00/S14 if the owner has no brand palette yet (`web/packages/tokens`, docs/product/ux/design-system.md) |

Some links inside these files point to prototype files that were not copied (for example `SRS.md` and
`api-reference.md`); they are available at tag `prototype-v0`.

## Left at the tag on purpose

All code (backend, web apps, Flutter app, tests), migrations, deployment and monitoring scripts, the CSV seed pack
(`docs/public_multi_sport_seed_csv_pack/` — some names resemble real clubs, so do not reuse them; seeds come from
docs/product/pilot-customer.md), and the prototype's SRS, SDD, UAT plan and user manual (they describe the old
system).

To look at something there without switching branches: `git show prototype-v0:<path>`.
