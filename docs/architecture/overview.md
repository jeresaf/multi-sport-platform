# Architecture overview

## System context

```text
                    ┌──────────────────────── Public users (fans, media, parents)
                    │                          phone / tablet / desktop browsers; later Flutter apps
                    ▼
┌───────────────────────────────┐        ┌──────────────────────────────┐
│ Public web (Next.js, per      │        │ Admin web (Next.js)          │◄── Platform staff, tenant admins,
│ tenant host, SSR + live)      │        │ incl. tablet scorer UI       │    competition managers, team
└──────────────┬────────────────┘        └──────────────┬───────────────┘    managers, scorers, results officers
               │ HTTPS (REST + SignalR)                 │
               ▼                                        ▼
        ┌──────────────────────────────────────────────────────────┐
        │ SportsPlatform.Host  (ASP.NET Core, .NET 10)             │
        │  /api/public/v1  /api/admin/v1  /api/platform/v1  /hubs  │
        │  13 modules (docs/architecture/modules.md)               │
        │  Outbox processor · Hangfire jobs · SignalR hub          │
        └───────────────┬──────────────────────┬───────────────────┘
                        │                      │
                ┌───────▼────────┐     ┌───────▼────────┐
                │ PostgreSQL 17  │     │ File storage   │  local disk (dev) / Azure Blob Storage (prod)
                │ module schemas │     └────────────────┘
                │ + RLS          │     Email (SMTP; Mailpit in dev) · Push (later)
                └────────────────┘
```

## Technology (pinned majors — keep current within the major)

| Area | Choice |
|---|---|
| Backend runtime | .NET 10 (LTS, supported to Nov 2028), C#, ASP.NET Core minimal APIs |
| Data | PostgreSQL 17, EF Core 10 + Npgsql, snake_case naming, one DbContext per module |
| Realtime | SignalR (single instance; Redis backplane only via ADR when scaling out) |
| Jobs | Hosted services (outbox); Hangfire + PostgreSQL storage for scheduled jobs |
| Auth | ASP.NET Core Identity (cookies for web, bearer tokens for mobile/API) |
| Observability | Serilog (structured JSON), OpenTelemetry traces/metrics (OTLP), health checks |
| API docs | Built-in OpenAPI generation; Scalar UI in development |
| Tests | xUnit, Shouldly, Testcontainers (PostgreSQL), Respawn, NetArchTest, WebApplicationFactory |
| Web | pnpm + Turborepo, Next.js 16 (App Router), React 19, TypeScript strict, Tailwind, shadcn/ui, TanStack Query, generated OpenAPI client, `@microsoft/signalr`, Vitest, Playwright |
| Mobile (Phase 8) | Flutter, Riverpod, Drift, generated Dart client, flavours for white-label |
| Local infra | Docker Compose: PostgreSQL, Mailpit; Azurite (Azure Storage emulator) only for testing the Blob adapter |
| CI | GitHub Actions |

## Repository layout

```text
/
├── CLAUDE.md                     instructions for Claude Code
├── README-FIRST.md               human guide to this kit (can be replaced by README.md later)
├── .claude/                      Claude Code settings, rules, skills, agents, hooks
├── .github/                      CI workflows, PR template
├── docs/                         product, architecture, domain, decisions, build plan, reference
├── backend/
│   ├── SportsPlatform.sln(x)
│   ├── Directory.Build.props, Directory.Packages.props
│   ├── src/
│   │   ├── SportsPlatform.Host/              composition root, middleware, endpoint mapping
│   │   ├── SportsPlatform.BuildingBlocks/    minimal technical kernel (AR-26)
│   │   └── Modules/<Name>/
│   │       ├── SportsPlatform.Modules.<Name>/            Domain/ Features/ Infrastructure/ Seed/
│   │       └── SportsPlatform.Modules.<Name>.Contracts/  queries, approved commands, DTOs, integration events
│   ├── tests/
│   │   ├── SportsPlatform.UnitTests/          folders per module (+ engine scenario tests)
│   │   ├── SportsPlatform.IntegrationTests/   real PostgreSQL via Testcontainers; isolation harness
│   │   └── SportsPlatform.ArchitectureTests/
│   └── tools/SportsPlatform.Cli/              migrate, seed, create-platform-admin, export catalogues
├── web/
│   ├── apps/admin, apps/public
│   └── packages/ui, tokens, api-client, config
├── mobile/                       (Phase 8)
├── contracts/                    generated: openapi.json, actions.json, errors.json, events.json
└── docker-compose.yml
```

## Inside a module

```text
SportsPlatform.Modules.Registry/
├── RegistryModule.cs           implements IModule: services, DbContext, endpoints, seeders
├── Domain/                     entities, value objects, domain events, invariants (no EF/ASP.NET references)
├── Features/                   one folder per use case: request, handler, validator, endpoint, response
│   └── People/CreatePerson/
├── Infrastructure/             RegistryDbContext, EF configurations, Migrations/, outbox/inbox mapping
└── Seed/
```

- Use cases are plain handler classes resolved from DI. No MediatR/AutoMapper (ADR-0013).
- Handlers use the module's DbContext directly as the unit of work; add repositories only where they remove
  real duplication.
- Domain methods enforce invariants and return `Result`/`Error` for expected failures; exceptions are for bugs.

## Request flow (state-changing)

1. Middleware: correlation ID → authentication → tenant resolution (admin: membership-validated header;
   public: host) → access mode check.
2. Endpoint metadata declares the **action code**; the authorization filter evaluates
   entitlement + scoped permission (docs/architecture/authorization.md).
3. Request validation (shape) → handler → aggregate method (domain rules) → `SaveChanges`.
4. In the same transaction: audit entry (automatic for audit-required actions), integration events to the outbox,
   score snapshot (for timeline events).
5. After commit: realtime push for live contest changes; outbox processor delivers integration events to
   consumers (projections, standings, notifications).
6. Response: DTO + `version` for optimistic concurrency, or Problem Details with a stable error `code`.

## Environments

| Env | Purpose | Data |
|---|---|---|
| Local | developer machine (Docker Compose) | REFERENCE + CONFIGURATION + DEMO seeds |
| CI | automated tests | Testcontainers, per-test reset |
| Staging | pilot rehearsal, owner demos | DEMO seed + anonymised samples; never real minors' data |
| Production | customers | REFERENCE + CONFIGURATION only; tenants created by platform admins |

Hosting: Azure South Africa North (Johannesburg) behind Cloudflare (ADR-0016). The backend ships as one
container image and each web app as a Node container, all on Azure Container Apps; PostgreSQL 17 on Azure
Database for PostgreSQL Flexible Server; files on Azure Blob Storage. Primary data stays in South Africa.

## Further reading

docs/architecture/modules.md · rules.md · multi-tenancy.md · authorization.md · sport-engine.md ·
events-and-jobs.md · api-conventions.md · data-conventions.md · national-registry.md
