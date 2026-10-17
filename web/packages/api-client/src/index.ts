// The typed API client (ADR-0011). Types come from src/generated/schema.ts, which `pnpm generate` writes from
// contracts/openapi.json; never hand-write request or response types the backend defines.
import createClient, { type ClientOptions } from "openapi-fetch";
import type { components, paths } from "./generated/schema";

export type { components, paths } from "./generated/schema";

/** Every API error body (docs/architecture/api-conventions.md): branch on `code`, show `correlationId`. */
export type ProblemDetails = components["schemas"]["ProblemDetails"];

export type ApiClientOptions = Omit<ClientOptions, "baseUrl"> & {
  /** The API origin, e.g. `http://localhost:5080`; paths in the client already start with `/api/...`. */
  baseUrl: string;
};

export type ApiClient = ReturnType<typeof createApiClient>;

export function createApiClient(options: ApiClientOptions) {
  return createClient<paths>(options);
}

/** True when an error body follows the error contract, so `code` and `correlationId` can be read. */
export function isProblemDetails(value: unknown): value is ProblemDetails {
  if (typeof value !== "object" || value === null) {
    return false;
  }

  const { code, correlationId } = value as Record<string, unknown>;
  return typeof code === "string" && typeof correlationId === "string";
}
