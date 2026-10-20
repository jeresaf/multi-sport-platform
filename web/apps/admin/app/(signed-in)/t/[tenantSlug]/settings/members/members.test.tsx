// Tenant settings → Members & roles: controls follow the capabilities and the membership state machine, reasons
// are asked for, role changes are chained with If-Match, owner-only choices are offered to owners only.
import { cleanup, fireEvent, screen, waitFor, within } from "@testing-library/react";
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
    expect(screen.getByText("Registrar")).toBeTruthy();
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

    expect(await dialog.findByText("That didn't work")).toBeTruthy();
    expect(screen.getByRole("dialog")).toBeTruthy();
  });

  it("offers a suspended member restore, not suspend", async () => {
    open(["TENANT_OWNER"], ownerActions, {
      "GET /api/admin/v1/members": members([{ ...brian, status: "SUSPENDED", statusReason: "Unpaid fees" }]),
    });

    expect(await screen.findByText("Unpaid fees")).toBeTruthy();
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

  it("doesn't let an administrator give the owner role or revoke an owner's invitation", async () => {
    open(["TENANT_ADMIN"], ownerActions);

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
