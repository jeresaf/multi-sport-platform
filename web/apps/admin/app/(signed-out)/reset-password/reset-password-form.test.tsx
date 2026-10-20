import { cleanup, fireEvent, screen, waitFor } from "@testing-library/react";
import { afterEach, describe, expect, it, vi } from "vitest";
import { fakeApi, problem, renderWithQuery } from "@/test/render";
import { ResetPasswordForm } from "./reset-password-form";

afterEach(() => {
  cleanup();
  vi.unstubAllGlobals();
  window.history.replaceState(null, "", "/");
});

function submit(password: string, again = password) {
  fireEvent.change(screen.getByLabelText("New password"), { target: { value: password } });
  fireEvent.change(screen.getByLabelText("Type it again"), { target: { value: again } });
  fireEvent.click(screen.getByRole("button", { name: "Save the new password" }));
}

describe("ResetPasswordForm", () => {
  it("sends the link's token with the new password, after removing it from the address bar", async () => {
    window.history.replaceState(null, "", "/reset-password?token=0199-user.secret");
    const requests = fakeApi({ "POST /api/account/v1/password/reset": { status: 204 } });
    renderWithQuery(<ResetPasswordForm />);

    await screen.findByLabelText("New password");
    expect(window.location.search).toBe("");
    submit("a long enough passphrase");

    expect(await screen.findByRole("heading", { name: "Password changed" })).toBeTruthy();
    expect(requests[0]!.body).toEqual({ token: "0199-user.secret", newPassword: "a long enough passphrase" });
  });

  it("shows the API's password rule under the field", async () => {
    window.history.replaceState(null, "", "/reset-password?token=0199-user.secret");
    fakeApi({
      "POST /api/account/v1/password/reset": {
        status: 400,
        body: problem("AUTH_PASSWORD_REJECTED", 400, {
          detail: "Passwords must use at least 4 different characters.",
          errors: [{ field: "newPassword", code: "AUTH_PASSWORD_REJECTED" }],
        }),
      },
    });
    renderWithQuery(<ResetPasswordForm />);

    await screen.findByLabelText("New password");
    submit("aaaaaaaaaaaaaaaa");

    expect(await screen.findByText("Passwords must use at least 4 different characters.")).toBeTruthy();
    expect(screen.queryByRole("alert")).toBeNull();
  });

  it("offers a new link when the token is refused", async () => {
    window.history.replaceState(null, "", "/reset-password?token=0199-user.used");
    fakeApi({ "POST /api/account/v1/password/reset": { status: 400, body: problem("AUTH_RESET_TOKEN_INVALID", 400) } });
    renderWithQuery(<ResetPasswordForm />);

    await screen.findByLabelText("New password");
    submit("a long enough passphrase");

    expect(await screen.findByText("The reset link is invalid or has expired. Ask for a new one.")).toBeTruthy();
    expect(screen.getByRole("link", { name: "Ask for a new link" }).getAttribute("href")).toBe("/forgot-password");
  });

  it("checks the two passwords match before calling the API", async () => {
    window.history.replaceState(null, "", "/reset-password?token=0199-user.secret");
    const requests = fakeApi({});
    renderWithQuery(<ResetPasswordForm />);

    await screen.findByLabelText("New password");
    submit("a long enough passphrase", "a different passphrase");

    expect(await screen.findByText("The two passwords don't match.")).toBeTruthy();
    await waitFor(() => expect(requests).toHaveLength(0));
  });

  it("says the link is incomplete without a token", async () => {
    fakeApi({});
    renderWithQuery(<ResetPasswordForm />);

    expect(await screen.findByRole("heading", { name: "This link is incomplete" })).toBeTruthy();
  });
});
