// Optimistic concurrency (api-conventions.md): a change sends the version it was based on as `If-Match`; the API
// answers `409 *_CONCURRENCY_CONFLICT` when the record changed since, and the screen offers to reload it.
import { isApiProblem } from "./api";

/** The `If-Match` header for a resource's `version` (the API's ETag is the quoted version). */
export function ifMatch(version: number) {
  return { "If-Match": `"${version}"` };
}

export function isConcurrencyConflict(error: unknown): boolean {
  return isApiProblem(error) && error.problem.code.endsWith("_CONCURRENCY_CONFLICT");
}
