// Puts an API refusal on a form: validation failures for fields the form shows go under those fields; the form's
// alert shows everything else.
import { errorMessage } from "@msp/api-client/errors";
import type { FieldValues, Path, UseFormSetError } from "react-hook-form";
import { isApiProblem } from "./api";

export const invalidValueMessage = "This value isn't valid.";

/** The form's fields by the API's name for them (`problem.errors[].field`), e.g. `{ newPassword: "password" }`. */
export type ApiFields<T extends FieldValues> = Readonly<Record<string, Path<T>>>;

function failures(error: unknown) {
  return isApiProblem(error) ? (error.problem.errors ?? []) : [];
}

/** Sets an error on each shown field the API refused. */
export function applyFieldErrors<T extends FieldValues>(
  error: unknown,
  fields: ApiFields<T>,
  setError: UseFormSetError<T>,
): void {
  if (!isApiProblem(error)) {
    return;
  }

  const { problem } = error;
  for (const failure of failures(error)) {
    const field = Object.hasOwn(fields, failure.field) ? fields[failure.field] : undefined;
    if (field) {
      setError(field, { type: "server", message: failureMessage(problem, failure.code) });
    }
  }
}

// The problem's own code carries its detail (e.g. the password rules); a finer code has its catalogue message.
function failureMessage(problem: { code: string; detail?: string | null }, code: string): string {
  return code === problem.code && problem.detail ? problem.detail : (errorMessage(code) ?? invalidValueMessage);
}

/** The API's refusal of one field kept outside a form library (a file input), or undefined. */
export function fieldError(error: unknown, field: string): string | undefined {
  if (!isApiProblem(error)) {
    return undefined;
  }
  const failure = failures(error).find((item) => item.field === field);
  return failure ? failureMessage(error.problem, failure.code) : undefined;
}

/** False when every failure is shown under one of the form's fields, so the alert would only repeat them. */
export function needsAlert(error: unknown, fields: Readonly<Record<string, string>>): boolean {
  const list = failures(error);
  return list.length === 0 || list.some((failure) => !Object.hasOwn(fields, failure.field));
}
