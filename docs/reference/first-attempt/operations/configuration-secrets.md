# Configuration and secrets

## Configuration sources

ASP.NET Core loads non-secret defaults from `apps/api/appsettings.json`, then
environment-specific JSON, development user secrets, environment variables, and
command-line arguments in that order. Environment variables use double
underscores for nested keys, for example `Jwt__Secret`.

Source-controlled settings contain no database password or JWT signing value.
The committed inventories are:

- `.env.example` for the root development Docker stack;
- `.env.testing.example` for a standalone test host (the maintained integration
  factory normally injects isolated values itself);
- `infrastructure/deployment/staging/.env.example` for staging Compose;
- `.env.production.example` for a production secret/configuration provider.

Examples are inventories, not deployable credentials. Files without the
`.example` suffix are ignored and must never be committed.

## Windows development

For the root Docker stack, copy `.env.example` to `.env`, replace every
`replace-with-*` value, then start Compose. Compose stops before creating
containers when required values are absent.

For a host-run API, keep secrets outside the workspace with .NET user secrets:

```powershell
dotnet user-secrets set "Database:ConnectionString" "Host=localhost;Port=5432;Database=sportsplatform;Username=postgres;Password=<local-secret>" --project apps/api
dotnet user-secrets set "Tenant:DefaultConnectionString" "Host=localhost;Port=5432;Database=sportsplatform;Username=postgres;Password=<local-secret>" --project apps/api
dotnet user-secrets set "Jwt:Secret" "<at-least-32-random-utf8-bytes>" --project apps/api
dotnet run --project apps/api/SportsPlatform.Api.csproj
```

User secrets are for development only; they are not encrypted production secret
storage.

## Staging

Copy `infrastructure/deployment/staging/.env.example` to `.env.staging` and
provision at least `DB_PASSWORD` and `JWT_SECRET`. Both deployment scripts
validate the file before build/start without printing values. MinIO and Grafana
currently support a database-password fallback for upgrade compatibility, but
emit a warning until distinct credentials are provisioned.
An upgraded local staging file with a legacy example-style JWT value also emits
a rotation warning; production rejects the same value outright.

Validate without rebuilding or restarting services:

```powershell
.\infrastructure\deployment\staging\deploy-staging.ps1 validate
```

The API performs a second startup validation. Staging requires database and
tenant connections, JWT issuer/audience/signing material, explicit allowed web
origins, a persistent Data Protection key path, complete object-storage
configuration, and `Email:Host`, `Email:FromAddress`, and
`Email:AdminWebBaseUrl`. Sensitive database logging is rejected. The bundled
staging stack routes identity mail to Mailpit for local inspection; production
must use an approved SMTP provider and an HTTPS admin-web base URL.

`SeedData__EnableDemoData=true` is explicitly set only by the development,
staging, and integration-test hosts. Those fixtures contain public test-only
credentials and must never be exposed as production identities.

## Production

Use a managed secret provider or files mounted read-only by the orchestrator;
do not deploy a copied `.env.production.example`. Production additionally
rejects placeholder/default database passwords, a storage secret reused as the
database password, wildcard or non-HTTPS origins, and placeholder JWT/storage
secrets. Production also rejects `SeedData:EnableDemoData=true`, so known demo
accounts cannot be installed accidentally. Startup errors contain configuration
key names but never secret values.

Recommended ownership:

| Secret | Owner | Rotation |
|---|---|---|
| Database application password | Database/operator | On exposure or scheduled policy |
| JWT current/previous signing keys | Security/operator | Planned overlap using `Jwt__PreviousSecrets__0` |
| MinIO access and secret keys | Storage/operator | Independently from the database |
| Grafana administrator password | Operations | On staff/access change |
| SMTP credential | Messaging/operator | Provider policy |
| Firebase service account | Mobile/notification operator | Google IAM policy |

For JWT rotation, deploy the old key as a previous secret and the new key as the
current secret, wait beyond the maximum access-token lifetime, then remove the
old key. Never log configuration dumps, connection strings, bearer tokens, or
service-account JSON. Password-reset and verification links contain credentials:
do not log their query strings, and rotate the SMTP password through the same
external secret provider used for other service credentials.

## Failure diagnosis

An invalid API configuration exits before database connections, migrations, or
background workers start. Read the listed key names, provision them through the
environment's secret source, and restart. Do not paste secret values into issue
trackers, terminal transcripts, or source-controlled JSON.
