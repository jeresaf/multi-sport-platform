import { cleanup, fireEvent, screen, waitFor } from "@testing-library/react";
import { afterEach, describe, expect, it, vi } from "vitest";
import { fakeApi, problem, renderWithQuery, type Answer } from "@/test/render";
import { AcceptInvitation } from "./accept-invitation";

const invitation = (accountExists: boolean): Answer => ({
  status: 200,
  body: {
    tenantDisplayName: "Kampala Metro Sports Association",
    email: "brian.ssemakula@kmsa.example",
    roleCodes: ["REGISTRAR"],
    expiresAt: "2026-10-27T09:00:00Z",
    accountExists,
  },
});

const signedOut: Answer = { status: 401, body: problem("AUTH_UNAUTHENTICATED", 401) };
const signedInAs = (email: string): Answer => ({
  status: 200,
  body: { id: "0199a3f0-0000-7000-8000-000000000001", email, platformRoles: [], tenants: [] },
});

afterEach(() => {
  cleanup();
  vi.unstubAllGlobals();
  window.history.replaceState(null, "", "/");
});

function openLink() {
  window.history.replaceState(null, "", "/accept-invitation?token=invite-secret");
  renderWithQuery(<AcceptInvitation />);
}

describe("AcceptInvitation", () => {
  it("creates the account with the chosen password when the email has none", async () => {
    const requests = fakeApi({
      "POST /api/account/v1/invitations/lookup": invitation(false),
      "GET /api/account/v1/me": signedOut,
      "POST /api/account/v1/invitations/accept": {
        status: 200,
        body: { tenantId: "0199a3f0-0000-7000-8000-00000000000a", membershipId: "0199a3f0-0000-7000-8000-00000000000b" },
      },
    });
    openLink();

    expect(await screen.findByRole("heading", { name: "Join Kampala Metro Sports Association" })).toBeTruthy();
    expect(screen.getByText(/invited as Registrar/)).toBeTruthy();
    fireEvent.change(screen.getByLabelText("Choose a password"), { target: { value: "a long enough passphrase" } });
    fireEvent.change(screen.getByLabelText("Type it again"), { target: { value: "a long enough passphrase" } });
    fireEvent.click(screen.getByRole("button", { name: "Create account and accept" }));

    expect(await screen.findByRole("heading", { name: "You've joined Kampala Metro Sports Association" })).toBeTruthy();
    expect(requests.find((r) => r.path.endsWith("/lookup"))!.body).toEqual({ token: "invite-secret" });
    expect(requests.find((r) => r.path.endsWith("/accept"))!.body).toEqual({
      token: "invite-secret",
      password: "a long enough passphrase",
    });
    expect(window.location.search).toBe("");
  });

  it("asks an existing account to sign in on the page, then accepts as that user", async () => {
    let session: Answer = signedOut;
    const requests = fakeApi({
      "POST /api/account/v1/invitations/lookup": invitation(true),
      "GET /api/account/v1/me": () => session,
      "POST /api/account/v1/login": () => {
        session = signedInAs("brian.ssemakula@kmsa.example");
        return { status: 204 };
      },
      "POST /api/account/v1/invitations/accept": {
        status: 200,
        body: { tenantId: "0199a3f0-0000-7000-8000-00000000000a", membershipId: "0199a3f0-0000-7000-8000-00000000000b" },
      },
    });
    openLink();

    const email = (await screen.findByLabelText("Email")) as HTMLInputElement;
    expect(email.value).toBe("brian.ssemakula@kmsa.example");
    fireEvent.change(screen.getByLabelText("Password"), { target: { value: "his own passphrase" } });
    fireEvent.click(screen.getByRole("button", { name: "Sign in" }));
    fireEvent.click(await screen.findByRole("button", { name: "Accept invitation" }));

    expect(await screen.findByRole("link", { name: "Continue" })).toBeTruthy();
    expect(requests.find((r) => r.path.endsWith("/accept"))!.body).toEqual({ token: "invite-secret", password: null });
  });

  it("tells someone signed in as another user who the invitation is for (#50 N2)", async () => {
    fakeApi({
      "POST /api/account/v1/invitations/lookup": invitation(false),
      "GET /api/account/v1/me": signedInAs("grace.nakato@kmsa.example"),
    });
    openLink();

    expect(await screen.findByText("You're signed in as grace.nakato@kmsa.example")).toBeTruthy();
    expect(screen.getByText(/Accepting creates a new account for that address/)).toBeTruthy();
    expect(screen.getByRole("button", { name: "Sign out" })).toBeTruthy();
    expect(screen.getByLabelText("Choose a password")).toBeTruthy();
  });

  it("signing out of another account keeps the invitation open for a new account", async () => {
    let session: Answer = signedInAs("grace.nakato@kmsa.example");
    const requests = fakeApi({
      "POST /api/account/v1/invitations/lookup": invitation(false),
      "GET /api/account/v1/me": () => session,
      "POST /api/account/v1/logout": () => {
        session = signedOut;
        return { status: 204 };
      },
    });
    openLink();

    fireEvent.click(await screen.findByRole("button", { name: "Sign out" }));

    await waitFor(() => expect(screen.queryByText(/You're signed in as/)).toBeNull());
    expect(screen.getByLabelText("Choose a password")).toBeTruthy();
    const lookups = requests.filter((r) => r.path.endsWith("/lookup"));
    expect(lookups.length).toBeGreaterThan(1);
    expect(lookups.every((r) => (r.body as { token: string }).token === "invite-secret")).toBe(true);
  });

  it("shows the API's refusal of the chosen password under the field", async () => {
    fakeApi({
      "POST /api/account/v1/invitations/lookup": invitation(false),
      "GET /api/account/v1/me": signedOut,
      "POST /api/account/v1/invitations/accept": {
        status: 400,
        body: problem("AUTH_PASSWORD_REJECTED", 400, {
          detail: "Passwords must use at least 4 different characters.",
          errors: [{ field: "password", code: "AUTH_PASSWORD_REJECTED" }],
        }),
      },
    });
    openLink();

    fireEvent.change(await screen.findByLabelText("Choose a password"), { target: { value: "aaaaaaaaaaaaaaaa" } });
    fireEvent.change(screen.getByLabelText("Type it again"), { target: { value: "aaaaaaaaaaaaaaaa" } });
    fireEvent.click(screen.getByRole("button", { name: "Create account and accept" }));

    expect(await screen.findByText("Passwords must use at least 4 different characters.")).toBeTruthy();
    expect(screen.queryByRole("alert")).toBeNull();
  });

  it("offers no accept button to the wrong user when the invited email has an account", async () => {
    fakeApi({
      "POST /api/account/v1/invitations/lookup": invitation(true),
      "GET /api/account/v1/me": signedInAs("grace.nakato@kmsa.example"),
    });
    openLink();

    expect(await screen.findByText(/Sign out, then sign in as that account/)).toBeTruthy();
    expect(screen.queryByRole("button", { name: "Accept invitation" })).toBeNull();
  });

  it("explains an expired invitation", async () => {
    fakeApi({
      "POST /api/account/v1/invitations/lookup": { status: 410, body: problem("AUTH_INVITATION_EXPIRED", 410) },
      "GET /api/account/v1/me": signedOut,
    });
    openLink();

    expect(await screen.findByText("The invitation has expired. Ask for a new invitation.")).toBeTruthy();
    await waitFor(() => expect(screen.queryByRole("button", { name: /accept/i })).toBeNull());
  });
});
