import { cleanup, fireEvent, screen, waitFor } from "@testing-library/react";
import { afterEach, describe, expect, it, vi } from "vitest";
import { fakeApi, problem, renderWithQuery } from "@/test/render";
import { SignInForm } from "./sign-in-form";

afterEach(() => {
  cleanup();
  vi.unstubAllGlobals();
});

function fillAndSubmit(email: string, password: string) {
  fireEvent.change(screen.getByLabelText("Email"), { target: { value: email } });
  fireEvent.change(screen.getByLabelText("Password"), { target: { value: password } });
  fireEvent.click(screen.getByRole("button", { name: "Sign in" }));
}

describe("SignInForm", () => {
  it("signs in with X-CSRF and reports success", async () => {
    const requests = fakeApi({ "POST /api/account/v1/login": { status: 204 } });
    const onSignedIn = vi.fn();
    renderWithQuery(<SignInForm onSignedIn={onSignedIn} />);

    fillAndSubmit("grace.nakato@kmsa.example", "correct horse battery");

    await waitFor(() => expect(onSignedIn).toHaveBeenCalledOnce());
    expect(requests[0]).toMatchObject({
      method: "POST",
      path: "/api/account/v1/login",
      body: { email: "grace.nakato@kmsa.example", password: "correct horse battery" },
    });
    expect(requests[0]!.headers.get("X-CSRF")).toBe("1");
  });

  it("shows the catalogue's message for wrong credentials and clears the password", async () => {
    fakeApi({ "POST /api/account/v1/login": { status: 401, body: problem("AUTH_INVALID_CREDENTIALS", 401) } });
    const onSignedIn = vi.fn();
    renderWithQuery(<SignInForm onSignedIn={onSignedIn} />);

    fillAndSubmit("grace.nakato@kmsa.example", "wrong password");

    expect(
      await screen.findByText("The email or password is incorrect, or the account can't sign in right now."),
    ).toBeTruthy();
    expect((screen.getByLabelText("Password") as HTMLInputElement).value).toBe("");
    expect(onSignedIn).not.toHaveBeenCalled();
  });

  it("asks for both fields before calling the API", async () => {
    const requests = fakeApi({});
    renderWithQuery(<SignInForm onSignedIn={vi.fn()} />);

    fireEvent.click(screen.getByRole("button", { name: "Sign in" }));

    expect(await screen.findByText("Enter your email address.")).toBeTruthy();
    expect(screen.getByText("Enter your password.")).toBeTruthy();
    expect(requests).toHaveLength(0);
  });

  it("fixes the email when given one", () => {
    fakeApi({});
    renderWithQuery(<SignInForm email="daniel.kato@kmsa.example" onSignedIn={vi.fn()} />);

    const email = screen.getByLabelText("Email") as HTMLInputElement;
    expect(email.value).toBe("daniel.kato@kmsa.example");
    expect(email.readOnly).toBe(true);
  });
});
