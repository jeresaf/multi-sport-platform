import { describe, expect, it, vi } from "vitest";
import { ApiProblem } from "./api";
import { applyFieldErrors, invalidValueMessage, needsAlert } from "./form-errors";

const rejected = new ApiProblem(
  {
    code: "AUTH_PASSWORD_REJECTED",
    correlationId: "kmsa-admin.9",
    detail: "Passwords must be at least 12 characters.",
    errors: [{ field: "newPassword", code: "AUTH_PASSWORD_REJECTED" }],
  },
  400,
);

describe("applyFieldErrors", () => {
  it("puts the problem's detail under the field the API names, by the form's name for it", () => {
    const setError = vi.fn();

    applyFieldErrors(rejected, { newPassword: "password" }, setError);

    expect(setError).toHaveBeenCalledWith("password", {
      type: "server",
      message: "Passwords must be at least 12 characters.",
    });
  });

  it("uses the catalogue message for a finer code, and a plain message for an unknown one", () => {
    const setError = vi.fn();
    const invalid = new ApiProblem(
      {
        code: "VALIDATION_INVALID_REQUEST",
        correlationId: "kmsa-admin.10",
        errors: [
          { field: "email", code: "AUTH_USER_EXISTS" },
          { field: "password", code: "FIXTURE_UNKNOWN" },
        ],
      },
      400,
    );

    applyFieldErrors(invalid, { email: "email", password: "password" }, setError);

    expect(setError).toHaveBeenCalledWith("email", { type: "server", message: "A user with this email already exists." });
    expect(setError).toHaveBeenCalledWith("password", { type: "server", message: invalidValueMessage });
  });

  it("ignores errors that aren't API problems", () => {
    const setError = vi.fn();
    applyFieldErrors(new Error("offline"), { email: "email" }, setError);
    expect(setError).not.toHaveBeenCalled();
  });
});

describe("needsAlert", () => {
  it("is false only when every failure is shown under a field", () => {
    expect(needsAlert(rejected, { newPassword: "password" })).toBe(false);
    expect(needsAlert(rejected, { email: "email" })).toBe(true);
    expect(needsAlert(new Error("offline"), { email: "email" })).toBe(true);
  });
});
