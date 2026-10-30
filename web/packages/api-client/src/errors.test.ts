import { describeProblem, errorMessage, fieldErrorCodes, genericErrorMessage } from "./errors";
import type { ProblemDetails } from "./index";

const problem = (overrides: Partial<ProblemDetails>): ProblemDetails => ({
  code: "AUTH_INVALID_CREDENTIALS",
  correlationId: "kmsa-admin.42",
  status: 401,
  ...overrides,
});

describe("describeProblem", () => {
  it("shows the catalogue's message for a known code, without the correlation ID", () => {
    expect(describeProblem(problem({ title: "ignored" }))).toEqual({
      message: "The email or password is incorrect, or the account can't sign in right now.",
    });
  });

  it("shows the server's message and the correlation ID for an unknown code", () => {
    expect(describeProblem(problem({ code: "FIXTURE_UNKNOWN", title: "Title", detail: "Detail" }))).toEqual({
      message: "Detail",
      correlationId: "kmsa-admin.42",
    });
    expect(describeProblem(problem({ code: "FIXTURE_UNKNOWN", title: "Title" })).message).toBe("Title");
    expect(describeProblem(problem({ code: "FIXTURE_UNKNOWN" })).message).toBe(genericErrorMessage);
  });

  it("shows a quota refusal's detail, which names the limit", () => {
    const quota = { code: "ENTITLEMENT_QUOTA_EXCEEDED", status: 403 };
    expect(describeProblem(problem({ ...quota, detail: "Your plan allows 5 staff users." }))).toEqual({
      message: "Your plan allows 5 staff users.",
    });
    expect(describeProblem(problem(quota)).message).toBe(errorMessage("ENTITLEMENT_QUOTA_EXCEEDED"));
    // Other known codes keep the catalogue's message whatever the detail says.
    expect(describeProblem(problem({ detail: "ignored" })).message).toBe(errorMessage("AUTH_INVALID_CREDENTIALS"));
  });

  it("adds the correlation ID when the server failed", () => {
    expect(describeProblem(problem({ code: "PLATFORM_UNEXPECTED_ERROR", status: 500 })).correlationId).toBe(
      "kmsa-admin.42",
    );
  });
});

describe("errorMessage", () => {
  it("knows every code in contracts/errors.json and nothing else", () => {
    expect(errorMessage("AUTH_FORBIDDEN")).toBe("You are not allowed to perform this action.");
    expect(errorMessage("FIXTURE_UNKNOWN")).toBeUndefined();
  });
});

describe("fieldErrorCodes", () => {
  it("returns the codes for one field", () => {
    const rejected = problem({
      code: "AUTH_PASSWORD_REJECTED",
      errors: [
        { field: "password", code: "AUTH_PASSWORD_REJECTED" },
        { field: "email", code: "VALIDATION_REQUIRED" },
      ],
    });

    expect(fieldErrorCodes(rejected, "password")).toEqual(["AUTH_PASSWORD_REJECTED"]);
    expect(fieldErrorCodes(rejected, "token")).toEqual([]);
  });
});
