# ADR-0016 — Hosting: Azure South Africa North (Johannesburg) behind Cloudflare; primary data stays in South Africa

**Status:** Accepted
**Date:** 2026-10-17
**Deciders:** Owner (chose "outside Uganda", open question Q4), Reviewer (provider and region)

## Context

- **Owner decision (Q4):** host outside Uganda.
- **Users.** Fans on phones over mobile data; volunteer scorers at grounds with patchy connectivity; admins on
  laptops. Live scoring needs low, steady latency. Public pages must load fast on mid-range phones.
- **Team.** One owner plus Claude Code, no operations staff. Managed services (database backups, patching, TLS)
  matter more than the lowest price.
- **Law (Uganda).** This is an engineering record, not legal advice.
  - Data Protection and Privacy Act 2019, s.19: a Uganda-based controller or processor may process or store
    personal data outside Uganda if the destination country has protection at least equivalent to the Act, **or**
    the data subject has consented.
  - Data Protection and Privacy Regulations 2021: reg. 30(2) — a further transfer from that country to a third
    country needs the data subject's consent; reg. 30(4) — the regulator (PDPO) will list countries with adequate
    protection (not published as of October 2026); reg. 13 — every data collector, processor and controller
    registers with the PDPO, including foreign entities processing Ugandans' data; s.23 and reg. 33 — breaches
    are reported to the PDPO immediately.
  - PDPO decision of 18 July 2025 (complaint against Google LLC): no prior approval is needed for each transfer;
    transfers are lawful with consent or equivalent protection; organisations must keep records of the legal
    basis, safeguards and justification for transfers; an equivalent country must at least have data protection
    legislation and a regulator. Registration is renewed annually.
- **Where the clouds are.** No hyperscale cloud region was operating in East Africa in October 2026: Microsoft
  and G42's Kenya region stalled over payment guarantees and power (reported May 2026); Oracle's Nairobi region
  with iXAfrica was announced in January 2026 with no launch date found. The nearest full regions are in
  Johannesburg (Azure South Africa North, Google Cloud africa-south1) and Cape Town (AWS af-south-1; Azure South
  Africa West, restricted access).
- **Latency.** A hosting provider's own best-case measurements (April–June 2026) put Johannesburg to Kampala at
  about 60–65 ms and Lisbon to Kampala at about 150–170 ms; real-world figures run 10–30% higher.
- **South Africa** has the Protection of Personal Information Act (POPIA) and an Information Regulator, so it
  meets the PDPO's stated minimum for equivalence.
- **Workload.** One .NET 10 API container (REST + SignalR + background services), two Next.js containers,
  PostgreSQL 17, object storage for media, Hangfire on PostgreSQL when needed (ADR-0009, ADR-0013, ADR-0014).

## Decision

1. **Region.** Production and staging run in **Azure South Africa North (Johannesburg)**. All primary personal
   data — database, backups, files and logs — stays in South Africa.
2. **Services at launch.**
   - **Compute:** Azure Container Apps (consumption plan): `api`, `web-public`, `web-admin`. `api` runs **one
     replica** until an ADR adds a SignalR backplane (ADR-0013). Images are built by GitHub Actions; the registry
     (GitHub Container Registry or Azure Container Registry Basic) is chosen in the infrastructure task.
   - **Database:** Azure Database for PostgreSQL Flexible Server, **PostgreSQL 17**. Production starts on
     Burstable B2s (2 vCores, 4 GiB) with point-in-time-restore backups kept in the region; move to General
     Purpose when CPU credits run low, and add a zone-redundant standby when a paying customer depends on
     match-day uptime. The server admin creates the roles `sp_app`, `sp_migrator` and `sp_platform` (BYPASSRLS)
     at provisioning — the service supports creating BYPASSRLS roles on PostgreSQL 16 and later
     (docs/architecture/multi-tenancy.md).
   - **Files:** Azure Blob Storage through an `IFileStorage` adapter (local disk in development and tests);
     private files served by short-lived signed (SAS) URLs after a permission check (ADR-0014).
   - **Secrets:** Azure Key Vault and managed identities. No secrets in images, the repository or logs.
   - **Observability:** OpenTelemetry exported to Azure Monitor (Application Insights / Log Analytics) in the
     same region. Logs never contain `PRIVATE` or `SENSITIVE` values (ADR-0015).
   - **Email:** a transactional email provider, chosen in the first slice that sends email (S03 invitations).
     It is a sub-processor (point 6).
3. **Edge.** Cloudflare sits in front of every public host: DNS, TLS, web application firewall, rate limiting and
   caching of public pages and assets. Cloudflare has a data centre in Kampala, so cached public content is
   served from inside Uganda. Tenant custom domains (PRO, S22) use Cloudflare for SaaS custom hostnames (100
   included, then US$0.10 per hostname per month). Only `PUBLIC` content is cached. Admin pages and admin or
   platform API responses carry `Cache-Control: private, no-store`; public API responses use short shared
   caching only where a slice spec says so. SignalR WebSockets pass through the proxy.
4. **Environments.** Local (Docker Compose) · CI (Testcontainers) · staging (Azure South Africa North, smallest
   sizes, scale to zero, database stopped when idle, **demo data only**) · production.
5. **Portability.** Domain and application code stay cloud-neutral: containers, plain PostgreSQL, `IFileStorage`,
   OpenTelemetry. Provider SDKs appear only in infrastructure adapters (AR-33). Changing provider means new
   infrastructure scripts and one storage adapter, nothing else.
6. **Pilot gate — compliance before real personal data.** Owner tasks, with a lawyer's review:
   1. Register the company with the PDPO and renew every year. Ask tenants to confirm their own registration in
      the contract.
   2. A data processing agreement template for tenants (the tenant is the controller; the platform company is
      the processor) listing sub-processors and locations — Microsoft Azure (South Africa), Cloudflare (global
      edge: public content and request metadata), the email provider — plus security measures, breach notice,
      and return or deletion at the end of the contract.
   3. A **transfer register** recording the legal basis for storage abroad: s.19(a) — South Africa has POPIA and
      a regulator; plus s.19(b) — consent in privacy notices, registration forms and guardian consents (ADR-0015).
      Further transfers (reg. 30(2)) go only to the listed sub-processors and are covered by that consent.
   4. A breach-response runbook: report to the PDPO immediately and tell affected tenants.
7. **Indicative monthly cost** (verify in the Azure pricing calculator for South Africa North before
   provisioning):

   | Item | Size at launch | US$ per month |
   |---|---|---|
   | Container Apps: `api`, `web-public`, `web-admin` | 0.5 vCPU / 1 GiB each; `api` and `web-public` always on | 40–110 |
   | PostgreSQL production | Burstable B2s, 64 GB storage, in-region backups | 70–100 |
   | PostgreSQL staging | Burstable B1ms, stopped when idle | 10–25 |
   | Blob Storage, Key Vault, container registry | low volumes | 5–15 |
   | Azure Monitor | sampled telemetry; free allowance first | 0–30 |
   | Cloudflare | Free plan, Pro when needed | 0–25 |
   | Transactional email | low volume | 0–15 |
   | **Total** | | **about 125–300; plan for 250** |

   Microsoft for Startups offers self-service Azure credits (reported in 2026 as up to US$1,000 without a
   company and up to US$5,000 for a registered business, each with an expiry) — apply before provisioning.

## Alternatives considered

- **Google Cloud africa-south1 (Johannesburg)** — same latency; Cloud Run and Cloud SQL fit the workload well.
  A sound second choice. Azure is preferred for its .NET tooling, its documented support for creating BYPASSRLS
  roles (our role design), a paired region in the same country (South Africa West) for later disaster recovery,
  and startup credits.
- **AWS af-south-1 (Cape Town)** — mature services, but farther from Kampala than Johannesburg and an opt-in
  region; no advantage for this stack.
- **Europe (Azure/AWS/GCP regions, or budget providers such as DigitalOcean or Hetzner)** — GDPR makes the
  equivalence argument easiest, and budget providers cost less. But latency to Kampala is two to three times
  higher, and a plain virtual machine means running PostgreSQL ourselves. Kept as the fallback if costs bite
  before revenue.
- **Data centres in Kenya or Uganda (colocation or local VPS)** — lowest latency, but no managed PostgreSQL or
  container platform at the level we need, and more operations work. Revisit when a hyperscale region opens in
  East Africa.

## Consequences

- Positive: about 60–80 ms from Kampala; managed backups, patching and TLS; a jurisdiction with comparable data
  protection; public pages cached in Kampala; nothing provider-specific in the domain.
- Negative / costs: more expensive than budget hosting in Europe; Azure-specific infrastructure scripts; one API
  replica limits scale until a backplane ADR; South Africa West is restricted-access, so cross-region disaster
  recovery needs a request or another approach.
- Follow-up work:
  - Infrastructure task right after M1 (docs/build/roadmap.md): infrastructure as code (Bicep), staging
    environment, deployment workflow, secrets, telemetry.
  - Production environment and the compliance pilot gate (point 6) before the first pilot stores real data.
  - S14: a per-tenant privacy page (template naming the storage location and sub-processors; the tenant can
    edit it).
  - ADR-0014: `IFileStorage` production adapter = Azure Blob Storage.

## Rules affected

- New **AR-33 Cloud-neutral application code** (docs/architecture/rules.md).

## Review trigger

- A hyperscale region with managed PostgreSQL and containers opens in East Africa.
- Hosting costs more than 30% of revenue after the first year, or credits run out before revenue starts.
- A customer in a country that restricts storage abroad (for example, Rwanda's Law 058/2021 requires a
  certificate from its supervisory authority to store personal data outside Rwanda; Kenya and Tanzania have
  their own transfer rules).
- The PDPO publishes its list of adequate countries and South Africa is not on it.
- Measured p95 API latency from Kampala exceeds 250 ms.
