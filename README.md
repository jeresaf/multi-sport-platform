# Multi-Sport Platform

A comprehensive, multi-tenant sports competition management platform built with ASP.NET Core 9, supporting multiple sports, competition formats, and real-time scoring.

## Architecture

```
sports-platform/
├── apps/                    # Application hosts
│   └── api/                 # ASP.NET Core Web API
├── backend/
│   ├── BuildingBlocks/      # Shared domain primitives
│   ├── Shared/              # Shared infrastructure
│   └── Modules/             # Domain modules (21 modules)
├── infrastructure/          # Docker, CI/CD, configs
└── docs/                    # Documentation
```

## Key Features

- **Multi-Tenant**: Full tenant isolation with header/claim-based resolution
- **Universal Contest Model**: Single contest model adaptable to any sport
- **Sport Definition Versions**: Versioned sport configurations (segments, events, metrics)
- **Competition Hierarchy**: Competition → Edition → Stage → Group/Round → Contest
- **Live Scoring**: Real-time event submission with idempotency and corrections
- **Format Engines**: Round-robin, knockout, group+knockout, leaderboard, heat, fight-card
- **Sport Packages**: Football, Basketball, Golf, MMA/Boxing, Athletics, T20 Cricket

## Tech Stack

- **Backend**: ASP.NET Core 9, C# 12
- **Database**: PostgreSQL (primary), Redis (cache)
- **Real-time**: SignalR
- **Observability**: OpenTelemetry, Serilog
- **Containers**: Docker, Docker Compose

The code-aligned [architecture record](docs/architecture.md) and
[architecture decisions](docs/adr/README.md) describe system boundaries. The API
exposes separate liveness/readiness probes and propagates structured correlation
context; see [observability operations](docs/operations/observability.md).

## Getting Started

### Prerequisites

- .NET 9 SDK
- Docker Desktop
- Node.js 20.9+ (for frontend apps)

### Run with Docker

On Windows 10 with Docker Desktop:

```powershell
.\infrastructure\deployment\staging\deploy-staging.ps1 start
```

The staging stack exposes the API at `http://localhost:8080`, admin web at
`http://localhost:3000`, public web at `http://localhost:3001`, MinIO's S3 API
and console at ports `9000` and `9001`, and Mailpit at
`http://localhost:8025`. PostgreSQL uses host port `5434` by default.

Use `status`, `logs`, `restart`, or `stop` as the script action. See
`docs/operations/backup-recovery.md` for Windows backup and recovery commands.
Configuration templates and safe secret provisioning are documented in
`docs/operations/configuration-secrets.md`. Database migration creation,
deployment, adoption, and rollback are documented in
`docs/operations/database-migrations.md`.

### Run locally

```powershell
dotnet restore SportsPlatform.sln
dotnet run --project apps/api/SportsPlatform.Api.csproj
```

The web applications use Node.js 20 and the mobile application uses Flutter
3.27.2. Install and start them independently when developing outside Docker:

```powershell
npm --prefix apps/admin-web ci
npm --prefix apps/admin-web run dev

npm --prefix apps/public-web ci
npm --prefix apps/public-web run dev

Push-Location apps/mobile
flutter pub get
flutter run
Pop-Location
```

### Formatting and quality checks

Repository-wide editor rules are defined in `.editorconfig`. Run the same
analyzer, lint, type, and formatting gates used by CI from Windows PowerShell:

```powershell
.\scripts\format.ps1 -Mode Check
```

Apply safe formatter and lint fixes with:

```powershell
.\scripts\format.ps1 -Mode Apply
```

Generated outputs, local IDE state, dependency folders, test reports, logs,
and environment secrets are excluded by `.gitignore`; Docker build contexts
exclude the same classes of files through `.dockerignore`.

### API Documentation

Once running, visit `http://localhost:8080/swagger` for Swagger UI.

## Project Structure

### Domain Modules

| Module | Description |
|--------|-------------|
| Tenancy | Tenant management and isolation |
| Sports | Sport catalogue and definition versions |
| Competitions | Competition hierarchy management |
| Contests | Universal contest model and live scoring |
| Participants | Athlete, team, and roster management |
| Registration | Configurable registration forms |
| Branding | Tenant branding and customization |
| Subscriptions | Plan management and feature entitlements |
| Audit | Full audit trail for all operations |
| Identity | User authentication and authorization |
| Reporting | Reports and exports |
| Sponsorship | Commercial and sponsorship features |

### Competition Format Engines

- Round-robin
- Knockout (single/double elimination)
- Group + Knockout
- Best-of-series
- Multi-round leaderboard
- Heat qualification
- Fight-card

## License

Proprietary - All rights reserved
