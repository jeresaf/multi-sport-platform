# @msp/api-client

The typed API client (ADR-0011): [openapi-fetch](https://openapi-ts.dev/openapi-fetch/) over types that
[openapi-typescript](https://openapi-ts.dev/) generates from the backend's OpenAPI document.

```text
backend endpoints ──(CLI export-openapi)──▶ contracts/openapi.json ──(pnpm generate)──▶ src/generated/schema.ts
```

## Regenerate

After changing an endpoint, run from the repository root:

```bash
dotnet run --project backend/tools/SportsPlatform.Cli -- export-openapi
cd web && pnpm --filter @msp/api-client generate
```

Commit both files. The `Contracts` CI job regenerates them and fails if they differ. Never hand-edit
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
