// The admin app's API access: one client for account and platform calls (no tenant), and the helpers that turn an
// openapi-fetch result into data or an ApiProblem that screens and TanStack Query can handle.
import { createAdminClient } from "@msp/api-client/admin";
import { isProblemDetails, type ProblemDetails } from "@msp/api-client";

/** A refusal or failure from the API, carrying its Problem Details (docs/architecture/api-conventions.md). */
export class ApiProblem extends Error {
  constructor(
    readonly problem: ProblemDetails,
    readonly status: number,
  ) {
    super(problem.code);
    this.name = "ApiProblem";
  }
}

// Fetch is looked up per request (not captured when the module loads), so tests can replace it.
export const api = createAdminClient({ fetch: (request: Request) => globalThis.fetch(request) });

type FetchResult<T> = { data?: T; error?: unknown; response: Response };

/** The response's data, or an ApiProblem. A body that isn't Problem Details (a proxy's error page) gets a stand-in. */
export async function call<T>(request: Promise<FetchResult<T>>): Promise<T> {
  const { data, error, response } = await request;
  if (response.ok) {
    return data as T;
  }

  const problem: ProblemDetails = isProblemDetails(error)
    ? error
    : {
        // Client-only code (never sent by the API): it isn't in the catalogue, so the screen shows the reference.
        code: "WEB_UNEXPECTED_RESPONSE",
        status: response.status,
        correlationId: response.headers.get("X-Correlation-Id") ?? "",
      };
  throw new ApiProblem(problem, response.status);
}

export function isApiProblem(error: unknown, code?: string): error is ApiProblem {
  return error instanceof ApiProblem && (code === undefined || error.problem.code === code);
}

/** True when the API answered 401: the caller has no session (or it expired). */
export function isUnauthenticated(error: unknown): boolean {
  return error instanceof ApiProblem && error.status === 401;
}

/** A client for one tenant workspace: every request carries the tenant's `X-Tenant-Id`. */
export function tenantApi(tenantId: string) {
  return createAdminClient({ tenantId, fetch: (request: Request) => globalThis.fetch(request) });
}
