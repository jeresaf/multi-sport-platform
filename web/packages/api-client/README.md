# @msp/api-client

The typed API client (ADR-0011): [openapi-fetch](https://openapi-ts.dev/openapi-fetch/) over types that
[openapi-typescript](https://openapi-ts.dev/) generates from the backend's OpenAPI document.

```text
backend endpoints ──(CLI export-openapi)──▶ contracts/openapi.json ──(pnpm generate)──▶ src/generated/schema.ts
error catalogue ──(CLI export-catalogues)──▶ contracts/errors.json ──(pnpm generate)──▶ src/generated/errors.json
```

## Regenerate

After changing an endpoint, run from the repository root:

```bash
dotnet run --project backend/tools/SportsPlatform.Cli -- export-openapi
dotnet run --project backend/tools/SportsPlatform.Cli -- export-catalogues
cd web && pnpm --filter @msp/api-client generate
```

Commit the regenerated files. The `Contracts` CI job regenerates them and fails if they differ. Never hand-edit
`src/generated/`, and never hand-write request or response types the backend defines.

## Use

```ts
import { createApiClient, isProblemDetails } from "@msp/api-client";

const api = createApiClient({ baseUrl: "http://localhost:5080" });
const { data, error } = await api.GET("/api/public/v1/...");
if (isProblemDetails(error)) {
  // Branch on error.code; show error.correlationId to the user.
}
```

Every error body is Problem Details with a stable `code` (see `contracts/errors.json`) and a `correlationId`.

## The admin app

The admin app calls the API on its own origin: Next.js rewrites `/api/*` to the API, so the session cookie is
first-party. `createAdminClient` adds `X-CSRF: 1` to every request that isn't `GET`/`HEAD`/`OPTIONS` (S03 D7) and,
in a tenant workspace, `X-Tenant-Id` to every request:

```ts
import { createAdminClient } from "@msp/api-client/admin";
import { describeProblem } from "@msp/api-client/errors";

const api = createAdminClient({ tenantId }); // omit tenantId for /api/account and /api/platform
const { error } = await api.POST("/api/admin/v1/members/{membershipId}/suspend", { ... });
if (error) {
  const { message, correlationId } = describeProblem(error);
}
```

`describeProblem` gives the catalogue's message for a known `code`; for an unknown code or a server failure it gives
the server's message and the correlation ID, which the screen shows so support can find the request.
