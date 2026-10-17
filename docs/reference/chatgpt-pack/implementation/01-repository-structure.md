# Greenfield Repository Structure

Recommended monorepo:

```text
sports-platform/
├── README.md
├── LICENSE
├── .editorconfig
├── .gitattributes
├── .gitignore
├── global.json
├── Directory.Build.props
├── docker-compose.yml
│
├── docs/
│   ├── architecture/
│   ├── specifications/
│   ├── api/
│   ├── adr/
│   └── ui/
│
├── backend/
│   ├── SportsPlatform.sln
│   ├── src/
│   │   ├── Host/
│   │   │   └── SportsPlatform.Api/
│   │   ├── BuildingBlocks/
│   │   │   ├── Domain/
│   │   │   ├── Application/
│   │   │   ├── Infrastructure/
│   │   │   └── Observability/
│   │   └── Modules/
│   │       ├── Sports/
│   │       ├── CompetitionFormats/
│   │       ├── Rulesets/
│   │       ├── Tenancy/
│   │       ├── Entitlements/
│   │       ├── People/
│   │       ├── Participants/
│   │       ├── Organisations/
│   │       ├── Teams/
│   │       ├── Staff/
│   │       ├── Officials/
│   │       ├── Venues/
│   │       ├── Memberships/
│   │       ├── Rosters/
│   │       ├── Transfers/
│   │       ├── Competitions/
│   │       ├── Registrations/
│   │       ├── Eligibility/
│   │       ├── Stages/
│   │       ├── Scheduling/
│   │       ├── Lineups/
│   │       ├── Contests/
│   │       ├── Scoring/
│   │       ├── Timeline/
│   │       ├── Results/
│   │       ├── Progression/
│   │       ├── Statistics/
│   │       ├── History/
│   │       ├── ReadModels/
│   │       ├── Search/
│   │       ├── Reporting/
│   │       ├── Imports/
│   │       ├── Providers/
│   │       ├── Notifications/
│   │       ├── Branding/
│   │       ├── Sponsorship/
│   │       ├── Audit/
│   │       └── Analytics/
│   └── tests/
│       ├── Unit/
│       ├── Integration/
│       ├── Architecture/
│       └── Contract/
│
├── web/
│   ├── package.json
│   ├── apps/
│   │   ├── public-web/
│   │   └── admin-web/
│   └── packages/
│       ├── ui/
│       ├── sports-ui/
│       ├── api-client/
│       ├── design-tokens/
│       └── config/
│
├── mobile/
│   └── sports_platform/
│       ├── lib/
│       ├── test/
│       └── integration_test/
│
├── contracts/
│   ├── openapi/
│   ├── events/
│   └── schemas/
│
├── tools/
│   ├── seed/
│   ├── import/
│   ├── scripts/
│   └── codegen/
│
└── infra/
    ├── docker/
    ├── postgres/
    ├── redis/
    ├── observability/
    └── deployment/
```

## Backend Module Internal Shape

Use a consistent module structure:

```text
Modules/People/
├── People.Domain/
├── People.Application/
├── People.Infrastructure/
├── People.Contracts/
└── People.Tests/
```

For smaller modules, physical projects may be combined if architecture boundaries remain enforceable. Do not create dozens of assemblies merely for aesthetics.

## Web

Recommended:
- Next.js
- TypeScript
- Tailwind
- TanStack Query
- SignalR client
- Playwright
- Storybook

The public and admin web apps may share UI packages but should not share route-specific business state indiscriminately.

## Mobile

Recommended:
- Flutter
- Riverpod or Bloc
- Drift/SQLite
- FCM
- Offline scorer queue

## Initial Infrastructure

Required initially:
- PostgreSQL
- Redis
- Docker Compose
- structured logging
- health checks
- OpenTelemetry-compatible tracing/metrics

Deferred:
- RabbitMQ
- Kubernetes
- Elasticsearch/OpenSearch
- multi-database decomposition
