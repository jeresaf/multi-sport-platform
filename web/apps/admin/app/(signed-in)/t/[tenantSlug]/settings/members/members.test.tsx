// Tenant settings → Members & roles: controls follow the capabilities and the membership state machine, reasons
// are asked for, role changes are chained with If-Match, owner-only choices are offered to owners only.
import { cleanup, fireEvent, screen, waitFor, within } from "@testing-library/react";
import { errorMessage } from "@msp/api-client/errors";
import { afterEach, describe, expect, it, vi } from "vitest";
import { fakeApi, problem, renderWithQuery, type Answer } from "@/test/render";
import TenantWorkspaceLayout from "../../layout";
import MembersPage from "./page";
import { roleChanges } from "./roles-dialog";

vi.mock("next/navigation", () => ({
  useParams: () => ({ tenantSlug: "kmsa" }),
  usePathname: () => "/t/kmsa/settings/members",
  useRouter: () => ({ replace: vi.fn(), push: vi.fn() }),
}));

const tenantId = "0199a3f0-0000-7000-8000-00000000000a";
const me = (roles: string[]): Answer => ({
  status: 200,
  body: {
    id: "0199a3f0-0000-7000-8000-000000000001",
    email: "grace.nakato@kmsa.example",
    platformRoles: [],
    tenants: [{ tenantId, slug: "kmsa", displayName: "Kampala Metro Sports Association", status: "ACTIVE", roles }],
  },
});
const role = (roleCode: string, n: number) => ({
  assignmentId: `0199a3f0-0000-7000-8000-0000000002${n}0`,
  roleCode,
  scopeType: "TENANT",
  scopeId: null,
  grantedAt: "2026-10-18T09:00:00Z",
});
const brian = {
  membershipId: "0199a3f0-0000-7000-8000-000000000101",
  userId: "0199a3f0-0000-7000-8000-000000000011",
  email: "brian.ssemakula@kmsa.example",
  status: "ACTIVE",
  joinedAt: "2026-10-18T09:00:00Z",
  invitedBy: null,
  statusReason: null,
  statusChangedAt: null,
  roles: [role("REGISTRAR", 1)],
  version: 5,
};
const members = (items: unknown[]): Answer => ({ status: 200, body: { items, page: { nextCursor: null, hasMore: false } } });
const ownerInvitation = {
  invitationId: "0199a3f0-0000-7000-8000-000000000301",
  email: "moses.okello@kmsa.example",
  roleCodes: ["TENANT_OWNER"],
  status: "PENDING",
  invitedBy: "0199a3f0-0000-7000-8000-000000000001",
  invitedAt: "2026-10-19T09:00:00Z",
  expiresAt: "2026-10-26T09:00:00Z",
  emailSentAt: "2026-10-19T09:00:05Z",
  revokedAt: null,
  version: 1,
};
const viewerInvitation = {
  ...ownerInvitation,
  invitationId: "0199a3f0-0000-7000-8000-000000000302",
  email: "esther.nabirye@kmsa.example",
  roleCodes: ["VIEWER"],
};

const ownerActions = [
  "membership.read",
  "membership.invite",
  "membership.suspend",
  "membership.restore",
  "membership.remove",
  "role.assign",
  "role.revoke",
  "invitation.revoke",
];

function open(roles: string[], actions: string[], answers: Record<string, Answer | (() => Answer)> = {}) {
  const requests = fakeApi({
    "GET /api/account/v1/me": me(roles),
    "GET /api/admin/v1/me/capabilities": { status: 200, body: { actions, scope: null } },
    "GET /api/admin/v1/members": members([brian]),
    "GET /api/admin/v1/invitations": members([ownerInvitation, viewerInvitation]),
    ...answers,
  });
  renderWithQuery(
    <TenantWorkspaceLayout>
      <MembersPage />
    </TenantWorkspaceLayout>,
  );
  return requests;
}

const openMenu = (name: string) =>
  fireEvent.pointerDown(screen.getByRole("button", { name }), { button: 0, ctrlKey: false, pointerType: "mouse" });

afterEach(() => {
  cleanup();
  vi.unstubAllGlobals();
  localStorage.clear();
});

describe("members & roles", () => {
  it("shows a viewer the members and invitations with nothing to change", async () => {
    open(["VIEWER"], ["membership.read"]);

    expect(await screen.findByText(brian.email)).toBeTruthy();
    expect(screen.getAllByText("Registrar").length).toBeGreaterThan(0);
    expect(await screen.findByText(viewerInvitation.email)).toBeTruthy();
    expect(screen.queryByRole("button", { name: `Change ${brian.email}` })).toBeNull();
    expect(screen.queryByRole("button", { name: "Invite someone" })).toBeNull();
    expect(screen.queryByRole("button", { name: /^Revoke the invitation/ })).toBeNull();
  });

  it("asks for a reason before suspending, then sends it with the member's version", async () => {
    const requests = open(["TENANT_OWNER"], ownerActions, {
      "POST /api/admin/v1/members/0199a3f0-0000-7000-8000-000000000101/suspend": {
        status: 200,
        body: { ...brian, status: "SUSPENDED", version: 6 },
      },
    });

    await screen.findByText(brian.email);
    openMenu(`Change ${brian.email}`);
    expect(screen.queryByRole("menuitem", { name: "Restore" })).toBeNull();
    fireEvent.click(await screen.findByRole("menuitem", { name: "Suspend" }));

    const dialog = within(await screen.findByRole("dialog"));
    fireEvent.click(dialog.getByRole("button", { name: "Suspend" }));
    expect(await dialog.findByText(/Give a reason/)).toBeTruthy();
    expect(requests.some((request) => request.path.endsWith("/suspend"))).toBe(false);

    fireEvent.change(dialog.getByLabelText("Reason"), { target: { value: "  Left the association  " } });
    fireEvent.click(dialog.getByRole("button", { name: "Suspend" }));

    await waitFor(() => expect(screen.queryByRole("dialog")).toBeNull());
    const suspend = requests.find((request) => request.path.endsWith("/suspend"))!;
    expect(suspend.body).toEqual({ reason: "Left the association" });
    expect(suspend.headers.get("If-Match")).toBe('"5"');
    expect(suspend.headers.get("X-Tenant-Id")).toBe(tenantId);
    // The member left the list with their menu button: focus goes to the section's heading.
    await waitFor(() => expect(document.activeElement?.id).toBe("members-heading"));
  });

  it("keeps the last-owner refusal in the dialog", async () => {
    open(["TENANT_OWNER"], ownerActions, {
      "POST /api/admin/v1/members/0199a3f0-0000-7000-8000-000000000101/remove": {
        status: 409,
        body: problem("AUTH_LAST_OWNER", 409),
      },
    });

    await screen.findByText(brian.email);
    openMenu(`Change ${brian.email}`);
    fireEvent.click(await screen.findByRole("menuitem", { name: "Remove" }));
    const dialog = within(await screen.findByRole("dialog"));
    fireEvent.change(dialog.getByLabelText("Reason"), { target: { value: "Duplicate account" } });
    fireEvent.click(dialog.getByRole("button", { name: "Remove" }));

    expect(await dialog.findByText(errorMessage("AUTH_LAST_OWNER")!)).toBeTruthy();
    expect(screen.getByRole("dialog")).toBeTruthy();
  });

  it("offers a suspended member restore, not suspend", async () => {
    open(["TENANT_OWNER"], ownerActions, {
      "GET /api/admin/v1/members": members([{ ...brian, status: "SUSPENDED", statusReason: "Unpaid fees" }]),
    });

    expect((await screen.findAllByText("Unpaid fees")).length).toBeGreaterThan(0);
    openMenu(`Change ${brian.email}`);
    expect(await screen.findByRole("menuitem", { name: "Restore" })).toBeTruthy();
    expect(screen.queryByRole("menuitem", { name: "Suspend" })).toBeNull();
  });

  it("grants and takes away roles one request at a time, each with the version the last one returned", async () => {
    const requests = open(["TENANT_OWNER"], ownerActions, {
      "POST /api/admin/v1/members/0199a3f0-0000-7000-8000-000000000101/roles": {
        status: 201,
        body: { ...brian, roles: [role("REGISTRAR", 1), role("VIEWER", 2)], version: 6 },
      },
      [`POST /api/admin/v1/members/0199a3f0-0000-7000-8000-000000000101/roles/${role("REGISTRAR", 1).assignmentId}/revoke`]:
        { status: 200, body: { ...brian, roles: [role("VIEWER", 2)], version: 7 } },
    });

    await screen.findByText(brian.email);
    openMenu(`Change ${brian.email}`);
    fireEvent.click(await screen.findByRole("menuitem", { name: "Change roles" }));
    const dialog = within(await screen.findByRole("dialog"));
    fireEvent.click(dialog.getByRole("checkbox", { name: "Viewer" }));
    fireEvent.click(dialog.getByRole("checkbox", { name: "Registrar" }));
    fireEvent.click(dialog.getByRole("button", { name: "Save roles" }));

    await waitFor(() => expect(screen.queryByRole("dialog")).toBeNull());
    const changes = requests.filter((request) => request.method === "POST");
    expect(changes.map((request) => request.headers.get("If-Match"))).toEqual(['"5"', '"6"']);
    expect(changes[0]!.body).toEqual({ roleCode: "VIEWER" });
    expect(changes[1]!.path.endsWith("/revoke")).toBe(true);
  });

  it("says which roles were already saved when a later one is refused", async () => {
    open(["TENANT_OWNER"], ownerActions, {
      "POST /api/admin/v1/members/0199a3f0-0000-7000-8000-000000000101/roles": {
        status: 201,
        body: { ...brian, roles: [role("REGISTRAR", 1), role("VIEWER", 2)], version: 6 },
      },
      [`POST /api/admin/v1/members/0199a3f0-0000-7000-8000-000000000101/roles/${role("REGISTRAR", 1).assignmentId}/revoke`]:
        { status: 409, body: problem("AUTH_CONCURRENCY_CONFLICT", 409) },
    });

    await screen.findByText(brian.email);
    openMenu(`Change ${brian.email}`);
    fireEvent.click(await screen.findByRole("menuitem", { name: "Change roles" }));
    const dialog = within(await screen.findByRole("dialog"));
    fireEvent.click(dialog.getByRole("checkbox", { name: "Viewer" }));
    fireEvent.click(dialog.getByRole("checkbox", { name: "Registrar" }));
    fireEvent.click(dialog.getByRole("button", { name: "Save roles" }));

    expect(await dialog.findByText("This record changed since you opened it")).toBeTruthy();
    expect(dialog.getByText("Already saved: Viewer granted.")).toBeTruthy();
  });

  it("doesn't offer an administrator the changes only an owner can make", async () => {
    const owner = {
      ...brian,
      membershipId: "0199a3f0-0000-7000-8000-000000000102",
      email: "daniel.kato@kmsa.example",
      roles: [role("TENANT_OWNER", 4)],
    };
    open(["TENANT_ADMIN"], ownerActions, { "GET /api/admin/v1/members": members([brian, owner]) });

    // An owner's membership: roles can still be looked at and changed (bar the owner role), nothing else.
    await screen.findByText(owner.email);
    openMenu(`Change ${owner.email}`);
    expect(await screen.findByRole("menuitem", { name: "Change roles" })).toBeTruthy();
    expect(screen.queryByRole("menuitem", { name: "Suspend" })).toBeNull();
    expect(screen.queryByRole("menuitem", { name: "Remove" })).toBeNull();
    fireEvent.keyDown(screen.getByRole("menu"), { key: "Escape" });
    await waitFor(() => expect(screen.queryByRole("menu")).toBeNull());

    await screen.findByText(ownerInvitation.email);
    expect(screen.queryByRole("button", { name: `Revoke the invitation to ${ownerInvitation.email}` })).toBeNull();
    expect(screen.getByRole("button", { name: `Revoke the invitation to ${viewerInvitation.email}` })).toBeTruthy();

    openMenu(`Change ${brian.email}`);
    fireEvent.click(await screen.findByRole("menuitem", { name: "Change roles" }));
    const roles = within(await screen.findByRole("dialog"));
    expect(roles.getByRole("checkbox", { name: "Tenant owner" }).hasAttribute("disabled")).toBe(true);
    fireEvent.click(roles.getByRole("button", { name: "Cancel" }));

    fireEvent.click(screen.getByRole("button", { name: "Invite someone" }));
    const invite = within(await screen.findByRole("dialog"));
    expect(invite.queryByRole("checkbox", { name: "Tenant owner" })).toBeNull();
  });

  it("invites with the chosen roles, and puts 'already a member' under the email", async () => {
    const requests = open(["TENANT_OWNER"], ownerActions, {
      "POST /api/admin/v1/invitations": { status: 409, body: problem("AUTH_ALREADY_MEMBER", 409) },
    });

    fireEvent.click(await screen.findByRole("button", { name: "Invite someone" }));
    const dialog = within(await screen.findByRole("dialog"));
    fireEvent.change(dialog.getByLabelText("Email"), { target: { value: brian.email } });
    fireEvent.click(dialog.getByRole("checkbox", { name: "Viewer" }));
    fireEvent.click(dialog.getByRole("button", { name: "Send invitation" }));

    await waitFor(() => expect(dialog.getByLabelText("Email").getAttribute("aria-invalid")).toBe("true"));
    expect(requests.find((request) => request.method === "POST")!.body).toEqual({
      email: brian.email,
      roleCodes: ["VIEWER"],
    });
    expect(dialog.queryByText("That didn't work")).toBeNull();
  });
});

describe("members & roles: refusals and the owner's own rules", () => {
  const suspendPath = "POST /api/admin/v1/members/0199a3f0-0000-7000-8000-000000000101/suspend";

  async function suspendBrian() {
    await screen.findByText(brian.email);
    openMenu(`Change ${brian.email}`);
    fireEvent.click(await screen.findByRole("menuitem", { name: "Suspend" }));
    const dialog = within(await screen.findByRole("dialog"));
    fireEvent.change(dialog.getByLabelText("Reason"), { target: { value: "Season break" } });
    fireEvent.click(dialog.getByRole("button", { name: "Suspend" }));
    return dialog;
  }

  it("says the member changed since the list was read, and Reload reads it again", async () => {
    const requests = open(["TENANT_OWNER"], ownerActions, {
      [suspendPath]: { status: 409, body: problem("AUTH_CONCURRENCY_CONFLICT", 409) },
    });

    const dialog = await suspendBrian();
    fireEvent.click(await dialog.findByRole("button", { name: "Reload" }));

    await waitFor(() => expect(screen.queryByRole("dialog")).toBeNull());
    await waitFor(() =>
      expect(requests.filter((request) => request.path === "/api/admin/v1/members").length).toBe(2),
    );
  });

  it("reads the list again when the member's status changed meanwhile", async () => {
    const requests = open(["TENANT_OWNER"], ownerActions, {
      [suspendPath]: { status: 409, body: problem("AUTH_MEMBERSHIP_TRANSITION_INVALID", 409) },
    });

    const dialog = await suspendBrian();

    expect(await dialog.findByText(errorMessage("AUTH_MEMBERSHIP_TRANSITION_INVALID")!)).toBeTruthy();
    await waitFor(() =>
      expect(requests.filter((request) => request.path === "/api/admin/v1/members").length).toBe(2),
    );
  });

  it("offers an owner every change on another owner, the owner role included", async () => {
    const owner = { ...brian, roles: [role("TENANT_OWNER", 4)] };
    open(["TENANT_OWNER"], ownerActions, { "GET /api/admin/v1/members": members([owner]) });

    await screen.findByText(brian.email);
    openMenu(`Change ${brian.email}`);
    expect(await screen.findByRole("menuitem", { name: "Suspend" })).toBeTruthy();
    expect(screen.getByRole("menuitem", { name: "Remove" })).toBeTruthy();
    fireEvent.click(screen.getByRole("menuitem", { name: "Change roles" }));
    const roles = within(await screen.findByRole("dialog"));
    expect(roles.getByRole("checkbox", { name: "Tenant owner" }).hasAttribute("disabled")).toBe(false);
  });

  it("marks the user's own row and says so before they change their own access", async () => {
    const self = { ...brian, userId: "0199a3f0-0000-7000-8000-000000000001", email: "grace.nakato@kmsa.example" };
    open(["TENANT_OWNER"], ownerActions, { "GET /api/admin/v1/members": members([self]) });

    const row = (await screen.findByText("You")).closest("tr")!;
    expect(within(row).getByText(self.email)).toBeTruthy();
    openMenu(`Change ${self.email}`);
    fireEvent.click(await screen.findByRole("menuitem", { name: "Remove" }));
    expect(within(await screen.findByRole("dialog")).getByText("This is you:")).toBeTruthy();
    fireEvent.click(within(screen.getByRole("dialog")).getByRole("button", { name: "Cancel" }));
    await waitFor(() => expect(screen.queryByRole("dialog")).toBeNull());

    openMenu(`Change ${self.email}`);
    fireEvent.click(await screen.findByRole("menuitem", { name: "Change roles" }));
    expect(within(await screen.findByRole("dialog")).getByText("These are your own roles:")).toBeTruthy();
  });

  it("says when an invitation's email hasn't gone out yet, and that it never will once it isn't pending", async () => {
    const revoked = { ...ownerInvitation, status: "REVOKED", emailSentAt: null, revokedAt: "2026-10-19T09:00:01Z" };
    open(["TENANT_OWNER"], ownerActions, {
      "GET /api/admin/v1/invitations": members([{ ...viewerInvitation, emailSentAt: null }, revoked]),
    });

    expect((await screen.findAllByText("Not sent yet")).length).toBe(1);
    expect(within(screen.getByText(revoked.email).closest("tr")!).getByText("Not sent")).toBeTruthy();
  });

  it("lists expired invitations on their own, with when they expired", async () => {
    const expired = { ...viewerInvitation, status: "EXPIRED", expiresAt: "2026-10-12T09:00:00Z" };
    const requests = open(["TENANT_OWNER"], ownerActions, { "GET /api/admin/v1/invitations": members([expired]) });

    fireEvent.change(await screen.findByLabelText("Invitations to show"), { target: { value: "EXPIRED" } });

    await waitFor(() =>
      expect(requests.some((r) => r.path === "/api/admin/v1/invitations" && r.search === "?status=EXPIRED")).toBe(true),
    );
    const row = (await screen.findByText(expired.email)).closest("tr")!;
    expect(within(row).getByText(/^Expired /)).toBeTruthy();
    expect(within(row).queryByText("—")).toBeNull();
  });
});

describe("revoking an invitation", () => {
  it("sends no If-Match: sending the email changes the version the list read", async () => {
    const requests = open(["TENANT_OWNER"], ownerActions, {
      [`POST /api/admin/v1/invitations/${viewerInvitation.invitationId}/revoke`]: {
        status: 200,
        body: { ...viewerInvitation, status: "REVOKED", version: 3 },
      },
    });

    fireEvent.click(await screen.findByRole("button", { name: `Revoke the invitation to ${viewerInvitation.email}` }));
    fireEvent.click(within(await screen.findByRole("dialog")).getByRole("button", { name: "Revoke" }));

    await waitFor(() => expect(screen.queryByRole("dialog")).toBeNull());
    const revoke = requests.find((request) => request.path.endsWith("/revoke"))!;
    expect(revoke.headers.get("If-Match")).toBeNull();
    expect(revoke.headers.get("X-CSRF")).toBe("1");
  });
});

describe("roleChanges", () => {
  it("grants the new roles and takes away only tenant-wide ones no longer chosen", () => {
    const member = {
      ...brian,
      roles: [role("REGISTRAR", 1), { ...role("SCORER", 3), scopeType: "COMPETITION", scopeId: "x" }],
    } as Parameters<typeof roleChanges>[0];

    expect(roleChanges(member, new Set(["VIEWER"]))).toEqual({
      assign: ["VIEWER"],
      revoke: [{ roleCode: "REGISTRAR", assignmentId: role("REGISTRAR", 1).assignmentId }],
    });
  });
});
