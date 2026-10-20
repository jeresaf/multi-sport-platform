// Platform console → Tenants: the list and its filter, creating a tenant, and a tenant's lifecycle and owner invitation,
// with the controls following the platform capabilities and the tenant state machine.
import { cleanup, fireEvent, screen, waitFor, within } from "@testing-library/react";
import { errorMessage } from "@msp/api-client/errors";
import type { ReactElement } from "react";
import { afterEach, describe, expect, it, vi } from "vitest";
import { fakeApi, problem, renderWithQuery, type Answer } from "@/test/render";
import PlatformLayout from "../layout";
import TenantPage from "./[tenantId]/page";
import TenantsPage from "./page";

const navigation = vi.hoisted(() => ({ params: {} as Record<string, string>, pathname: "/platform/tenants", push: vi.fn() }));
vi.mock("next/navigation", () => ({
  useParams: () => navigation.params,
  usePathname: () => navigation.pathname,
  useRouter: () => ({ replace: vi.fn(), push: navigation.push }),
}));

const me = (platformRoles: string[]): Answer => ({
  status: 200,
  body: { id: "0199a3f0-0000-7000-8000-000000000001", email: "support@sportsplatform.localhost", platformRoles, tenants: [] },
});
const adminActions = [
  "tenant.read",
  "tenant.create",
  "tenant.activate",
  "tenant.suspend",
  "tenant.restore",
  "tenant.archive",
  "tenant.unarchive",
  "tenant.owner.invite",
];
const supportActions = ["tenant.read"];

const kmsa = {
  id: "0199a3f0-0000-7000-8000-00000000000a",
  slug: "kmsa",
  legalName: "Kampala Metro Sports Association",
  displayName: "Kampala Metro Sports Association",
  countryCode: "UG",
  timeZone: "Africa/Kampala",
  defaultLocale: "en-UG",
  currencyCode: "UGX",
  status: "ACTIVE",
  publicEnabled: true,
  // 22:30 UTC is the next day in Kampala (UTC+3).
  createdAt: "2026-10-17T22:30:00Z",
  updatedAt: "2026-10-19T09:00:00Z",
  version: 4,
};
const elssc = {
  ...kmsa,
  id: "0199a3f0-0000-7000-8000-00000000000b",
  slug: "elssc",
  legalName: "Eastern Lakes Schools Sports Council",
  displayName: "Eastern Lakes Schools Sports Council",
  status: "PROVISIONING",
  publicEnabled: false,
  version: 1,
};
// The list's item: the tenant without its settings.
const listItem = (tenant: typeof kmsa) => {
  const { id, slug, legalName, displayName, countryCode, status, createdAt, updatedAt, version } = tenant;
  return { id, slug, legalName, displayName, countryCode, status, createdAt, updatedAt, version };
};
const page = (items: unknown[], nextCursor: string | null = null): Answer => ({
  status: 200,
  body: { items, page: { nextCursor, hasMore: nextCursor !== null } },
});
const reference = {
  "GET /api/public/v1/reference/countries": page([{ code: "UG", alpha3Code: "UGA", numericCode: "800", name: "Uganda" }]),
  "GET /api/public/v1/reference/time-zones": page([{ code: "Africa/Kampala", countryCode: "UG" }]),
  "GET /api/public/v1/reference/currencies": page([
    { code: "UGX", numericCode: "800", name: "Ugandan Shilling", minorUnits: 0 },
  ]),
};

function render(ui: ReactElement, actions: string[], answers: Record<string, Answer | (() => Answer)>, roles = ["PLATFORM_ADMIN"]) {
  const requests = fakeApi({
    "GET /api/account/v1/me": me(roles),
    "GET /api/platform/v1/me/capabilities": { status: 200, body: { actions, scope: null } },
    ...reference,
    ...answers,
  });
  renderWithQuery(<PlatformLayout>{ui}</PlatformLayout>);
  return requests;
}

function openList(actions: string[] = adminActions, answers: Record<string, Answer | (() => Answer)> = {}) {
  navigation.params = {};
  navigation.pathname = "/platform/tenants";
  return render(<TenantsPage />, actions, {
    "GET /api/platform/v1/tenants": page([listItem(elssc), listItem(kmsa)]),
    ...answers,
  });
}

const detailPath = (tenant: { id: string }) => `/api/platform/v1/tenants/${tenant.id}`;

function openDetail(
  tenant: typeof kmsa,
  actions: string[] = adminActions,
  answers: Record<string, Answer | (() => Answer)> = {},
  roles?: string[],
) {
  navigation.params = { tenantId: tenant.id };
  navigation.pathname = `/platform/tenants/${tenant.id}`;
  return render(<TenantPage />, actions, { [`GET ${detailPath(tenant)}`]: { status: 200, body: tenant }, ...answers }, roles);
}

afterEach(() => {
  cleanup();
  vi.unstubAllGlobals();
  navigation.push.mockReset();
  localStorage.clear();
});

describe("tenants list", () => {
  it("lists every tenant with its status, linking to its detail", async () => {
    openList();

    const link = await screen.findByRole("link", { name: kmsa.displayName });
    expect(link.getAttribute("href")).toBe(`/platform/tenants/${kmsa.id}`);
    expect(screen.getByText("elssc")).toBeTruthy();
    expect(screen.getAllByText("Provisioning").length).toBeGreaterThan(0);
    // The list has no tenant time zone: its dates are UTC, and the heading says so.
    expect(screen.getByRole("columnheader", { name: "Created (UTC)" })).toBeTruthy();
    expect(screen.getAllByText("17 Oct 2026").length).toBeGreaterThan(0);
  });

  it("filters by status and pages with Show more", async () => {
    const requests = openList(adminActions, {
      "GET /api/platform/v1/tenants": () => {
        const last = requests.at(-1)!;
        const query = new URLSearchParams(last.search);
        if (query.get("cursor") === "next") {
          return page([listItem(kmsa)]);
        }
        return query.get("status") === "PROVISIONING" ? page([listItem(elssc)], "next") : page([listItem(kmsa)]);
      },
    });

    await screen.findByRole("link", { name: kmsa.displayName });
    expect(new URLSearchParams(requests.find((request) => request.path === "/api/platform/v1/tenants")!.search).has("status")).toBe(false);

    fireEvent.change(screen.getByLabelText("Tenants to show"), { target: { value: "PROVISIONING" } });
    await screen.findByRole("link", { name: elssc.displayName });
    fireEvent.click(screen.getByRole("button", { name: "Show more" }));

    await screen.findByRole("link", { name: kmsa.displayName });
    const paged = requests.filter((request) => request.path === "/api/platform/v1/tenants").at(-1)!;
    expect(Object.fromEntries(new URLSearchParams(paged.search))).toEqual({ status: "PROVISIONING", cursor: "next" });
    expect(screen.queryByRole("button", { name: "Show more" })).toBeNull();
  });

  it("says when a status has no tenants", async () => {
    openList(adminActions, { "GET /api/platform/v1/tenants": page([]) });

    expect(await screen.findByText("No tenants yet")).toBeTruthy();
  });

  it("offers support staff no New tenant", async () => {
    openList(supportActions);

    await screen.findByRole("link", { name: kmsa.displayName });
    expect(screen.queryByRole("button", { name: "New tenant" })).toBeNull();
  });
});

describe("creating a tenant", () => {
  async function fillIn() {
    fireEvent.click(await screen.findByRole("button", { name: "New tenant" }));
    const dialog = within(await screen.findByRole("dialog"));
    fireEvent.change(dialog.getByLabelText("Slug"), { target: { value: "elssc" } });
    fireEvent.change(dialog.getByLabelText("Name"), { target: { value: elssc.displayName } });
    fireEvent.change(dialog.getByLabelText("Legal name"), { target: { value: elssc.legalName } });
    // The choices come from the platform's reference data.
    await dialog.findByRole("option", { name: "Uganda" });
    await dialog.findByRole("option", { name: "UGX — Ugandan Shilling" });
    await dialog.findByRole("option", { name: "Africa/Kampala" });
    fireEvent.change(dialog.getByLabelText("Country"), { target: { value: "UG" } });
    fireEvent.change(dialog.getByLabelText("Time zone"), { target: { value: "Africa/Kampala" } });
    fireEvent.change(dialog.getByLabelText("Language"), { target: { value: "en-UG" } });
    fireEvent.change(dialog.getByLabelText("Currency"), { target: { value: "UGX" } });
    return dialog;
  }

  it("checks the form, sends it, and opens the new tenant", async () => {
    const requests = openList(adminActions, {
      "POST /api/platform/v1/tenants": { status: 201, body: elssc },
    });

    fireEvent.click(await screen.findByRole("button", { name: "New tenant" }));
    let dialog = within(await screen.findByRole("dialog"));
    fireEvent.change(dialog.getByLabelText("Slug"), { target: { value: "Not a slug" } });
    fireEvent.click(dialog.getByRole("button", { name: "Create tenant" }));
    expect(await dialog.findByText(/Use 3 to 40 lower-case letters/)).toBeTruthy();
    expect(dialog.getByText("Choose a country.")).toBeTruthy();
    expect(requests.some((request) => request.method === "POST")).toBe(false);
    fireEvent.click(dialog.getByRole("button", { name: "Cancel" }));
    await waitFor(() => expect(screen.queryByRole("dialog")).toBeNull());

    dialog = await fillIn();
    fireEvent.click(dialog.getByRole("button", { name: "Create tenant" }));

    await waitFor(() => expect(navigation.push).toHaveBeenCalledWith(`/platform/tenants/${elssc.id}`));
    const create = requests.find((request) => request.method === "POST")!;
    expect(create.body).toEqual({
      slug: "elssc",
      displayName: elssc.displayName,
      legalName: elssc.legalName,
      countryCode: "UG",
      timeZone: "Africa/Kampala",
      defaultLocale: "en-UG",
      currencyCode: "UGX",
      publicEnabled: false,
    });
    expect(create.headers.get("X-Tenant-Id")).toBeNull();
  });

  it("puts a taken slug and the server's field errors under their fields", async () => {
    let answer: Answer = { status: 409, body: problem("TENANT_SLUG_TAKEN", 409) };
    openList(adminActions, { "POST /api/platform/v1/tenants": () => answer });

    const dialog = await fillIn();
    fireEvent.click(dialog.getByRole("button", { name: "Create tenant" }));
    expect(await dialog.findByText(errorMessage("TENANT_SLUG_TAKEN")!)).toBeTruthy();
    expect(dialog.queryByRole("alert")).toBeNull();

    answer = {
      status: 400,
      body: problem("VALIDATION_INVALID_REQUEST", 400, {
        errors: [{ field: "defaultLocale", code: "VALIDATION_INVALID_REQUEST" }],
      }),
    };
    fireEvent.change(dialog.getByLabelText("Slug"), { target: { value: "eastern-lakes" } });
    fireEvent.click(dialog.getByRole("button", { name: "Create tenant" }));
    expect(await dialog.findByText(errorMessage("VALIDATION_INVALID_REQUEST")!)).toBeTruthy();
    expect(dialog.getByLabelText("Language").getAttribute("aria-invalid")).toBe("true");
    expect(navigation.push).not.toHaveBeenCalled();
  });
});

describe("a tenant's detail", () => {
  const lifecycle = ["Activate", "Restore", "Suspend", "Unarchive", "Archive"];
  const offered = () => lifecycle.filter((name) => screen.queryByRole("button", { name }) !== null);

  it.each([
    ["PROVISIONING", ["Activate"]],
    ["ACTIVE", ["Suspend", "Archive"]],
    ["SUSPENDED", ["Restore", "Archive"]],
    ["ARCHIVED", ["Unarchive"]],
  ])("offers a %s tenant only the changes its status allows", async (status, expected) => {
    openDetail({ ...kmsa, status });

    await screen.findByRole("heading", { level: 1, name: kmsa.displayName });
    expect(offered()).toEqual(expected);
  });

  it("shows the profile with dates in the tenant's time zone", async () => {
    openDetail(kmsa);

    await screen.findByRole("heading", { level: 1, name: kmsa.displayName });
    expect(await screen.findByText("Uganda")).toBeTruthy();
    // Created 22:30 UTC on 17 October: the 18th in Kampala.
    expect(screen.getByText("18 Oct 2026")).toBeTruthy();
    expect(screen.getByText("On")).toBeTruthy();
    expect(screen.getByText(/status history and reasons, members, domains and subscription aren.t available here yet/)).toBeTruthy();
  });

  it("offers support staff no change and no invitation", async () => {
    openDetail(kmsa, supportActions, {}, ["PLATFORM_SUPPORT"]);

    await screen.findByRole("heading", { level: 1, name: kmsa.displayName });
    expect(offered()).toEqual([]);
    expect(screen.queryByRole("button", { name: "Invite an owner" })).toBeNull();
  });

  it("asks for a reason before suspending, then sends it with the tenant's version", async () => {
    const requests = openDetail(kmsa, adminActions, {
      [`POST ${detailPath(kmsa)}/suspend`]: { status: 200, body: { ...kmsa, status: "SUSPENDED", version: 5 } },
    });

    fireEvent.click(await screen.findByRole("button", { name: "Suspend" }));
    const dialog = within(await screen.findByRole("dialog"));
    expect(dialog.getByRole("heading", { name: `Suspend ${kmsa.displayName}?` })).toBeTruthy();
    fireEvent.click(dialog.getByRole("button", { name: "Suspend" }));
    expect(await dialog.findByText(/Give a reason/)).toBeTruthy();
    expect(requests.some((request) => request.path.endsWith("/suspend"))).toBe(false);

    fireEvent.change(dialog.getByLabelText("Reason"), { target: { value: "  Annual fee unpaid  " } });
    fireEvent.click(dialog.getByRole("button", { name: "Suspend" }));

    await waitFor(() => expect(screen.queryByRole("dialog")).toBeNull());
    const suspend = requests.find((request) => request.path.endsWith("/suspend"))!;
    expect(suspend.body).toEqual({ reason: "Annual fee unpaid" });
    expect(suspend.headers.get("If-Match")).toBe('"4"');
    expect(suspend.headers.get("X-Tenant-Id")).toBeNull();
    // The tenant is shown as the API returned it.
    expect(offered()).toEqual(["Restore", "Archive"]);
  });

  it("activates without a reason", async () => {
    const requests = openDetail(elssc, adminActions, {
      [`POST ${detailPath(elssc)}/activate`]: { status: 200, body: { ...elssc, status: "ACTIVE", version: 2 } },
    });

    fireEvent.click(await screen.findByRole("button", { name: "Activate" }));
    const dialog = within(await screen.findByRole("dialog"));
    expect(dialog.queryByLabelText("Reason")).toBeNull();
    fireEvent.click(dialog.getByRole("button", { name: "Activate" }));

    await waitFor(() => expect(screen.queryByRole("dialog")).toBeNull());
    const activate = requests.find((request) => request.path.endsWith("/activate"))!;
    expect(activate.body).toBeUndefined();
    expect(activate.headers.get("If-Match")).toBe('"1"');
    expect(offered()).toEqual(["Suspend", "Archive"]);
  });

  it("offers to reload after a concurrency conflict", async () => {
    let reads = 0;
    openDetail(kmsa, adminActions, {
      [`GET ${detailPath(kmsa)}`]: () => {
        reads += 1;
        return { status: 200, body: reads === 1 ? kmsa : { ...kmsa, status: "SUSPENDED", version: 5 } };
      },
      [`POST ${detailPath(kmsa)}/archive`]: { status: 409, body: problem("TENANT_CONCURRENCY_CONFLICT", 409) },
    });

    fireEvent.click(await screen.findByRole("button", { name: "Archive" }));
    const dialog = within(await screen.findByRole("dialog"));
    fireEvent.change(dialog.getByLabelText("Reason"), { target: { value: "Merged into another council" } });
    fireEvent.click(dialog.getByRole("button", { name: "Archive" }));

    expect(await dialog.findByText("This record changed since you opened it")).toBeTruthy();
    fireEvent.click(dialog.getByRole("button", { name: "Reload" }));

    await waitFor(() => expect(screen.queryByRole("dialog")).toBeNull());
    await waitFor(() => expect(offered()).toEqual(["Restore", "Archive"]));
  });

  it("reads the tenant again when its status no longer allows the change", async () => {
    let reads = 0;
    openDetail(kmsa, adminActions, {
      [`GET ${detailPath(kmsa)}`]: () => {
        reads += 1;
        return { status: 200, body: reads === 1 ? kmsa : { ...kmsa, status: "SUSPENDED", version: 5 } };
      },
      [`POST ${detailPath(kmsa)}/suspend`]: { status: 409, body: problem("TENANT_INVALID_TRANSITION", 409) },
    });

    fireEvent.click(await screen.findByRole("button", { name: "Suspend" }));
    const dialog = within(await screen.findByRole("dialog"));
    fireEvent.change(dialog.getByLabelText("Reason"), { target: { value: "Annual fee unpaid" } });
    fireEvent.click(dialog.getByRole("button", { name: "Suspend" }));

    expect(await dialog.findByText(errorMessage("TENANT_INVALID_TRANSITION")!)).toBeTruthy();
    await waitFor(() => expect(reads).toBe(2));
  });

  it("says an unknown tenant isn't there", async () => {
    openDetail(kmsa, adminActions, {
      [`GET ${detailPath(kmsa)}`]: { status: 404, body: problem("TENANT_NOT_FOUND", 404) },
    });

    expect(await screen.findByRole("heading", { name: "Page not found" })).toBeTruthy();
  });
});

describe("inviting a tenant's owner", () => {
  const invitation = {
    invitationId: "0199a3f0-0000-7000-8000-000000000401",
    email: "agnes.namutebi@elssc.example",
    roleCodes: ["TENANT_OWNER"],
    status: "PENDING",
    invitedBy: "0199a3f0-0000-7000-8000-000000000001",
    invitedAt: "2026-10-20T09:00:00Z",
    expiresAt: "2026-10-27T09:00:00Z",
    emailSentAt: null,
    revokedAt: null,
    version: 1,
  };

  it("sends the owner's email and says until when the link works", async () => {
    const requests = openDetail(elssc, adminActions, {
      [`POST ${detailPath(elssc)}/owner-invitations`]: { status: 201, body: invitation },
    });

    fireEvent.click(await screen.findByRole("button", { name: "Invite an owner" }));
    const dialog = within(await screen.findByRole("dialog"));
    fireEvent.click(dialog.getByRole("button", { name: "Send invitation" }));
    expect(await dialog.findByText(/Enter an email address/)).toBeTruthy();
    fireEvent.change(dialog.getByLabelText("Email"), { target: { value: invitation.email } });
    fireEvent.click(dialog.getByRole("button", { name: "Send invitation" }));

    await waitFor(() => expect(screen.queryByRole("dialog")).toBeNull());
    expect(requests.find((request) => request.path.endsWith("/owner-invitations"))!.body).toEqual({
      email: invitation.email,
    });
    expect(screen.getByRole("status").textContent).toBe(
      `Invited ${invitation.email}. The email with the link goes out in a moment; the link works until 27 Oct 2026.`,
    );
  });

  it("puts 'already a member' under the address and other refusals in the dialog", async () => {
    let answer: Answer = { status: 409, body: problem("AUTH_ALREADY_MEMBER", 409) };
    openDetail(elssc, adminActions, { [`POST ${detailPath(elssc)}/owner-invitations`]: () => answer });

    fireEvent.click(await screen.findByRole("button", { name: "Invite an owner" }));
    const dialog = within(await screen.findByRole("dialog"));
    fireEvent.change(dialog.getByLabelText("Email"), { target: { value: invitation.email } });
    fireEvent.click(dialog.getByRole("button", { name: "Send invitation" }));
    expect(await dialog.findByText(errorMessage("AUTH_ALREADY_MEMBER")!)).toBeTruthy();
    expect(dialog.queryByRole("alert")).toBeNull();

    answer = { status: 409, body: problem("AUTH_TENANT_ARCHIVED", 409) };
    fireEvent.click(dialog.getByRole("button", { name: "Send invitation" }));
    expect(await dialog.findByText(errorMessage("AUTH_TENANT_ARCHIVED")!)).toBeTruthy();
  });

  it("offers no invitation for an archived tenant", async () => {
    openDetail({ ...kmsa, status: "ARCHIVED" });

    expect(await screen.findByText("An archived tenant can't be invited to.")).toBeTruthy();
    expect(screen.queryByRole("button", { name: "Invite an owner" })).toBeNull();
  });
});
