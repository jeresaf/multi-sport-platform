# Open questions (owner decisions)

Each question has a **recommended default** so work can continue. The owner confirms or changes it; the
builder must not decide these alone. When a question is answered, move it to **Answered** with the decision,
the date and where the decision is recorded.

| # | Question | Why it matters | Default (used until decided) | Needed by |
|---|---|---|---|---|
| Q3 | Do fans get accounts in v1? | Identity scope, privacy | No — favourites stored on device; accounts with notifications in S21 | S14 |
| Q6 | Re-confirm Flutter for mobile when Phase 8 starts? | Third codebase choice | Flutter (ADR-0012) | S26 |
| Q7 | UI languages? | i18n framework cost | English only in v1; add i18n before the first non-English tenant | Phase 7 |
| Q8 | Who approves results by default, and may an edition auto-approve on submit? | Workflow friction vs. trust | Approval required by a RESULTS_OFFICER (or TENANT_ADMIN); per-edition setting `auto_approve_results` (default off, useful for friendlies) | S13 |
| Q9 | Who reviews sport definitions and ruleset templates against real regulations? | Correctness of rules | Owner arranges a review with the relevant national federations' competition regulations before a pilot | Before pilot |
| Q10 | Product name and platform domain? | Subdomains, emails, branding, TLS | Code name `SportsPlatform`; domain TBD (use `localhost` subdomains in dev) | Infrastructure task after M1 |
| Q11 | Minors: is guardian consent required before competition registration, or only before public exposure? | Registration friction vs. compliance | Required before public exposure; for registration, tenant setting (default "recommended"). The persona's tenants set it to "required" for under-17/under-16 competitions | S06 |
| Q12 | Build in the existing GitHub repository or a new one? | History, reviewer access, CI | The existing `jeresaf/multi-sport-platform`: preserve the prototype at tag `prototype-v0` and branch `prototype/v0`, then replace `main` with the kit (README-FIRST.md). Keep the repository private; CI on pull requests | S00 |
| Q13 | Which legal entity signs customer contracts and registers with Uganda's PDPO? | Required before real personal data (ADR-0016 pilot gate) | Owner's company registers with the PDPO; a lawyer reviews the processing agreement template, privacy notice and transfer register | Before pilot |
| Q14 | Do the indicative prices survive conversations with about five prospects? | Revenue, plan limits | Use the prices in docs/product/pricing-and-plans.md; revisit after the first three paying customers | Before the first invoice |
| Q16 | Should the country list include Kosovo (`XK`) or other codes that aren't ISO 3166-1 but that sports bodies use (e.g. England, Scotland, Wales for FIFA)? | Nationality and team country fields, eligibility | ISO 3166-1 only (S01-T7b-1); sporting "nations" become a separate list if a sport definition needs them | S06 (people) |
| Q18 | Bearer tokens for mobile and API clients in S03, or later? | One authentication scheme to secure in S03 vs. ADR-0005's bearer flow | Deferred: cookies only in S03; bearer tokens with the first non-browser client (Flutter scorer, Phase 8, or tenant API keys, PRO) | S03-T2 |
| Q19 | When is multi-factor sign-in required? | Platform admins can change every tenant | Not in S03; required for platform admins and offered to tenant owners before the first pilot (ADR-0016 compliance gate) | Before the first pilot |

## Answered

| # | Question | Decision | Date | Recorded in |
|---|---|---|---|---|
| Q1 | Per-tenant public sites, a platform-wide aggregator, or both? | Each customer gets its own public site; a cross-tenant hub may come later | 2026-10-17 (owner) | ADR-0010 |
| Q2 | What exactly is in STANDARD vs PRO? | Owner delegated to the reviewer's research. Two plans in code (STANDARD, PRO); event pass = short STANDARD period; Enterprise = PRO + overrides; no free plan at launch; indicative UGX/USD prices | 2026-10-17 | docs/architecture/authorization.md (values), docs/product/pricing-and-plans.md (prices and rationale) |
| Q4 | Hosting provider and region? | Owner: outside Uganda. Reviewer: Azure South Africa North (Johannesburg) behind Cloudflare; primary data stays in South Africa; compliance tasks gate the first pilot | 2026-10-17 | ADR-0016, ADR-0015 |
| Q5 | First pilot customer and sport? | No real customer yet: use the fictional Kampala Metro Sports Association. Sport order: football → rugby → basketball → others (roadmap Phase 5 reordered) | 2026-10-17 (owner) | docs/product/pilot-customer.md, docs/build/roadmap.md |
| Q15 | When RLS arrives (S02), should outbox/inbox tables get RLS, and should the outbox processor (and T5b's diagnostics) run as `sp_platform`? | No RLS on `outbox_messages`/`inbox_messages`; the processor stays on the `App` connection and never gets the superuser. Payloads carry IDs and `PUBLIC` display fields only (ADR-0015). S02's tenant resolver must keep `ITenantContext` resolving to a context the processor can set (reviewer recommendation on #20, accepted by the owner) | 2026-10-18 (owner) | docs/architecture/events-and-jobs.md |
| Q17 | S02-T3's public resolution reads `tenancy.tenant_domains`, which the spec created in T4; and T3 exceeds ~600 lines | `tenant_domains` moves into T3 (T3b); T4 keeps settings only. T3 is two PRs: T3a admin/platform resolution, actor, access mode, AR-21; T3b domains, public resolution and cache, dev override, `/site` | 2026-10-19 (owner) | docs/build/slices/S02-tenancy-core.md (T3, T4) |
