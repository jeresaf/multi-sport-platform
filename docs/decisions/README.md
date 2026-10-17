# Architecture Decision Records

ADRs are the highest source of truth in this repository. If any other document, spec or code disagrees with
an **Accepted** ADR, the ADR wins until a new ADR supersedes it.

## Process

1. Propose: `/new-adr <title>` creates the next number from `template.md` with status **Proposed**.
2. The owner (human) accepts or rejects. The reviewer (Claude) comments on the PR.
3. Never rewrite an Accepted ADR. To change a decision, write a new ADR and change only the old one's status
   line to `Superseded by ADR-XXXX`.

## Index

| ADR | Decision | Status |
|---|---|---|
| [0001](0001-modular-monolith-13-modules.md) | Modular monolith with 13 modules | Accepted |
| [0002](0002-timeline-is-source-of-truth.md) | Timeline is the source of truth; score is derived | Accepted |
| [0003](0003-sport-definitions-data-plus-engines.md) | Sport definitions as data + scoring-family engines | Accepted |
| [0004](0004-multi-tenancy-shared-schema.md) | Multi-tenancy: shared schema, `tenant_id`, EF filters + RLS | Accepted |
| [0005](0005-identity-and-access.md) | ASP.NET Core Identity; permission = action code; scoped roles | Accepted |
| [0006](0006-configuration-in-repo-v1.md) | Configuration authored in repo for v1; editions snapshot rules | Accepted |
| [0007](0007-simplified-status-model.md) | Simplified, non-overlapping status model | Accepted |
| [0008](0008-rosters-owned-by-competitions.md) | Rosters owned by Competitions; UI on team page | Accepted |
| [0009](0009-events-outbox-jobs.md) | Domain events + outbox; Hangfire for scheduled jobs | Accepted |
| [0010](0010-per-tenant-public-sites-first.md) | Per-tenant public sites first; hub later | Accepted |
| [0011](0011-web-stack.md) | Web stack: pnpm/Turborepo, Next.js 16, generated client | Accepted |
| [0012](0012-mobile-flutter-deferred.md) | Flutter mobile, deferred to Phase 8 | Accepted |
| [0013](0013-deferred-infrastructure-and-licenses.md) | Deferred infrastructure; dependency licence policy | Accepted |
| [0014](0014-media-storage.md) | Media storage abstraction (Azure Blob Storage in production) | Accepted |
| [0015](0015-personal-data-protection.md) | Personal data protection by design | Accepted |
| [0016](0016-hosting-azure-south-africa-north.md) | Hosting: Azure South Africa North behind Cloudflare; data stays in South Africa | Accepted |
