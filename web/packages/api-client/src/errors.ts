// Turns Problem Details into what a screen shows (docs/product/ux/admin-app.md): the catalogue's message for a known
// `code`, otherwise the server's own message with the correlation ID so support can find the request.
import catalogue from "./generated/errors.json";
import type { ProblemDetails } from "./index";

const messages: ReadonlyMap<string, string> = new Map(catalogue.errors.map((error) => [error.code, error.message]));

export const genericErrorMessage = "Something went wrong. Try again, and quote the reference if it keeps happening.";

export type ProblemDescription = {
  message: string;
  /** Shown with the message when the code is unknown or the server failed, so support can trace the request. */
  correlationId?: string;
};

/** The catalogue message for an error code, if the code is in contracts/errors.json. */
export function errorMessage(code: string): string | undefined {
  return messages.get(code);
}

// Codes whose `detail` is written for people and says more than the catalogue's message: a quota refusal names the
// limit ("Your plan allows 5 staff users.", S04 D11).
const detailedCodes: ReadonlySet<string> = new Set(["ENTITLEMENT_QUOTA_EXCEEDED"]);

export function describeProblem(problem: ProblemDetails): ProblemDescription {
  const known = errorMessage(problem.code);
  const serverFailed = (problem.status ?? 0) >= 500;
  if (known && !serverFailed) {
    return { message: detailedCodes.has(problem.code) && problem.detail ? problem.detail : known };
  }

  return {
    message: known ?? problem.detail ?? problem.title ?? genericErrorMessage,
    correlationId: problem.correlationId,
  };
}

/** The validation failures for one field (`errors[].field`, camelCase), as their codes. */
export function fieldErrorCodes(problem: ProblemDetails, field: string): string[] {
  return (problem.errors ?? []).filter((error) => error.field === field).map((error) => error.code);
}
