# Security verification

## Automated dependency checks

The backend, admin web, and public web workflows fail on vulnerable NuGet or
high-severity npm dependencies. Repository-wide OSV workflows add two further
gates:

- pull requests and merge queues fail when they introduce a known vulnerable
  dependency;
- `main` and a Monday schedule scan every supported manifest and publish SARIF
  results to GitHub code scanning.

All external workflow references are pinned to immutable commit SHAs. Dependabot
or a deliberate maintenance change should update those SHAs after reviewing the
corresponding release.

Local equivalents from the repository root are:

```powershell
dotnet list SportsPlatform.sln package --vulnerable --include-transitive
npm --prefix apps/admin-web audit --audit-level=high
npm --prefix apps/public-web audit --audit-level=high
Push-Location apps/mobile
flutter pub outdated --dependency-overrides
Pop-Location
```

`flutter pub outdated` is an upgrade inventory, not a vulnerability verdict.
The OSV GitHub workflows are the authoritative known-vulnerability scan for the
Dart lockfile.

## Application security regression

Run the focused hosted backend controls with:

```powershell
dotnet test tests/IntegrationTests/IntegrationTests.csproj --filter "FullyQualifiedName~SecurityControlTests"
```

These tests exercise security headers and CORS, invalid authentication input,
account lockout, refresh-token replay rejection, high-risk throttling, hostile
uploads, and MFA. The Docker-backed Playwright suite additionally verifies that
the admin and public responses expose the required browser security policies and
that those policies do not break public rendering or administrator login.

## Staging penetration check

Only scan an explicitly authorized staging environment. Start the preserved
staging stack, confirm its URLs, and run the maintained security regression
before a passive DAST baseline. Never point an automated scanner at production
without written authorization and an agreed test window.

Record the scanner image/version, target URLs, timestamp, report, exclusions,
and disposition for every finding. A scanner image download timing out is not a
passing scan; rerun it from a host with registry access.

### Recorded passive baseline — 2026-08-11

OWASP ZAP 2.17.0 (`ghcr.io/zaproxy/zaproxy:stable`) was run from the isolated
`staging_sports-platform-staging` Docker network against these authorized
targets after the production web images were rebuilt:

- `http://admin-web:3000` — 20 URLs, 0 failures, 3 warning classes;
- `http://public-web:3000` — 37 URLs, 0 failures, 3 warning classes;
- `http://nginx/health` — 5 URLs, 0 failures, 2 warning classes.

The first public-web run found the default Next.js `X-Powered-By` disclosure.
Both web configurations now disable that header, and the shared runtime image
copies `next.config.js` so the setting is not lost at `next start`. Nginx
`server_tokens` are also disabled. Raw response checks and the final Admin and
Public ZAP reports confirm that the disclosure rule now passes.

The remaining warnings are accepted and recorded rather than hidden:

- non-storable dynamic/404 responses are intentional and contain no private
  cacheable data;
- `script-src 'unsafe-inline'` supports the current Next.js bootstrap and will
  require a per-request nonce migration before it can be removed safely;
- COEP is intentionally omitted because tenant branding and API assets may be
  cross-origin; COOP, frame denial, content-type, referrer, and permissions
  protections remain enabled;
- the health response omits CORP because it is a non-sensitive monitoring
  endpoint without browser-readable credentials.

HTML, JSON, and Markdown evidence is stored under `artifacts/zap/`. These were
passive baseline scans; no active attack scan was run.

## Current verification boundary

The project is published to the private GitHub repository
`jeresaf/multi-sport-platform`. A dedicated draft pull request is used to
observe the backend, web, Flutter, and OSV workflows on GitHub-hosted runners
before their exact successful check names are made required on `main`.

Workflow syntax, local equivalents, and the passive DAST baseline above are
validated. This private repository does not currently provide GitHub Code
Scanning ingestion, so OSV retains the fail-on-vulnerability gate, workflow
annotations, and downloadable JSON/SARIF artifacts while dashboard upload is
disabled. Hosted workflow results and merge-gate behavior must be recorded from
that pull request before branch protection is considered verified.
