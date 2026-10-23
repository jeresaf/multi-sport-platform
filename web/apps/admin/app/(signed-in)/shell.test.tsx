// The signed-in shell: the guard, the tenant workspace's navigation from the capabilities, blocked and unknown
// tenants, the switcher and the user menu.
import { cleanup, fireEvent, screen, waitFor, within } from "@testing-library/react";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { fakeApi, problem, renderWithQuery, type Answer } from "@/test/render";
import SignedInLayout from "./layout";
import PlatformLayout from "./platform/layout";
import TenantWorkspaceLayout from "./t/[tenantSlug]/layout";

const navigation = vi.hoisted(() => ({ params: {} as Record<string, string>, pathname: "/", replace: vi.fn() }));
const leaveTo = vi.hoisted(() => vi.fn());
vi.mock("@/lib/leave", () => ({ leaveTo }));
vi.mock("next/navigation", () => ({
  useParams: () => navigation.params,
  usePathname: () => navigation.pathname,
  useRouter: () => ({ replace: navigation.replace, push: vi.fn() }),
}));

const kmsa = {
  tenantId: "0199a3f0-0000-7000-8000-00000000000a",
  slug: "kmsa",
  displayName: "Kampala Metro Sports Association",
  status: "ACTIVE",
  roles: ["TENANT_OWNER"],
};
const elssc = {
  tenantId: "0199a3f0-0000-7000-8000-00000000000b",
  slug: "elssc",
  displayName: "Entebbe Lakeside Schools Sports Council",
  status: "SUSPENDED",
  roles: ["TENANT_OWNER"],
};
const me = (platformRoles: string[] = []): Answer => ({
  status: 200,
  body: { id: "0199a3f0-0000-7000-8000-000000000001", email: "grace.nakato@kmsa.example", platformRoles, tenants: [elssc, kmsa] },
});
const capabilities = (actions: string[]): Answer => ({ status: 200, body: { actions, scope: null } });

function openTenant(slug: string, pathname = `/t/${slug}`) {
  navigation.params = { tenantSlug: slug };
  navigation.pathname = pathname;
  renderWithQuery(
    <TenantWorkspaceLayout>
      <p>Workspace page</p>
    </TenantWorkspaceLayout>,
  );
}

const workspaceNav = () => screen.getAllByRole("navigation", { name: "Workspace" })[0]!;

beforeEach(() => {
  navigation.replace.mockReset();
  leaveTo.mockReset();
});
afterEach(() => {
  cleanup();
  vi.unstubAllGlobals();
  localStorage.clear();
  window.history.replaceState(null, "", "/");
});

describe("signed-in guard", () => {
  it("sends someone without a session to sign in, coming back to the page they asked for", async () => {
    fakeApi({ "GET /api/account/v1/me": { status: 401, body: problem("AUTH_UNAUTHENTICATED", 401) } });
    window.history.replaceState(null, "", "/t/kmsa/settings/members?tab=invitations");

    renderWithQuery(
      <SignedInLayout>
        <p>Workspace page</p>
      </SignedInLayout>,
    );

    await waitFor(() =>
      expect(navigation.replace).toHaveBeenCalledWith(
        "/sign-in?returnTo=%2Ft%2Fkmsa%2Fsettings%2Fmembers%3Ftab%3Dinvitations",
      ),
    );
    expect(screen.queryByText("Workspace page")).toBeNull();
  });
});

describe("tenant workspace", () => {
  it("sends someone whose session ended to sign in when any call answers 401, not only `me`", async () => {
    let meAnswers = 0;
    fakeApi({
      "GET /api/account/v1/me": () =>
        ++meAnswers === 1 ? me() : { status: 401, body: problem("AUTH_UNAUTHENTICATED", 401) },
      "GET /api/admin/v1/me/capabilities": { status: 401, body: problem("AUTH_UNAUTHENTICATED", 401) },
    });
    window.history.replaceState(null, "", "/t/kmsa");
    navigation.params = { tenantSlug: "kmsa" };

    renderWithQuery(
      <SignedInLayout>
        <TenantWorkspaceLayout>
          <p>Workspace page</p>
        </TenantWorkspaceLayout>
      </SignedInLayout>,
    );

    await waitFor(() => expect(navigation.replace).toHaveBeenCalledWith("/sign-in?returnTo=%2Ft%2Fkmsa"));
  });

  it("shows an owner every entry, asking for the capabilities in that tenant", async () => {
    const requests = fakeApi({
      "GET /api/account/v1/me": me(),
      "GET /api/admin/v1/me/capabilities": capabilities(["tenant.settings.read", "membership.read"]),
    });

    openTenant("kmsa");

    await screen.findByText("Workspace page");
    const nav = within(workspaceNav());
    expect(nav.getByRole("link", { name: "Overview" }).getAttribute("aria-current")).toBe("page");
    expect(nav.getByRole("link", { name: "Organisation" }).getAttribute("href")).toBe("/t/kmsa/settings/organisation");
    expect(nav.getByRole("link", { name: "Members & roles" })).toBeTruthy();
    const capabilitiesRequest = requests.find((request) => request.path === "/api/admin/v1/me/capabilities");
    expect(capabilitiesRequest?.headers.get("X-Tenant-Id")).toBe(kmsa.tenantId);
    expect(localStorage.getItem("msp.admin.lastWorkspace")).toBe("/t/kmsa");
  });

  it("hides the entries a viewer's capabilities don't allow", async () => {
    fakeApi({
      "GET /api/account/v1/me": me(),
      "GET /api/admin/v1/me/capabilities": capabilities(["tenant.settings.read"]),
    });

    openTenant("kmsa");

    await screen.findByText("Workspace page");
    expect(within(workspaceNav()).queryByRole("link", { name: "Members & roles" })).toBeNull();
  });

  it("answers an unknown tenant with a 404", async () => {
    fakeApi({ "GET /api/account/v1/me": me() });

    openTenant("nowhere");

    expect(await screen.findByRole("heading", { name: "Page not found" })).toBeTruthy();
    expect(screen.queryByText("Workspace page")).toBeNull();
  });

  it("says why a suspended tenant is closed, without asking for its capabilities", async () => {
    const requests = fakeApi({
      "GET /api/account/v1/me": me(),
      "GET /api/admin/v1/tenant/access": {
        status: 200,
        body: {
          status: "SUSPENDED",
          accessMode: "BLOCKED",
          reason: "Subscription unpaid",
          since: "2026-10-12T08:00:00Z",
          timeZone: "Africa/Kampala",
          subscription: { status: "ACTIVE", startsOn: "2026-04-23", endsOn: "2027-04-22", graceEndsOn: "2027-05-06" },
        },
      },
    });

    openTenant("elssc");

    expect(await screen.findByRole("heading", { name: `${elssc.displayName} is suspended` })).toBeTruthy();
    expect(await screen.findByText("Subscription unpaid")).toBeTruthy();
    expect(screen.getByText("12 October 2026")).toBeTruthy();
    expect(screen.queryByText("Workspace page")).toBeNull();
    expect(requests.some((request) => request.path === "/api/admin/v1/me/capabilities")).toBe(false);
  });

  it("shows a failure to load why a tenant is suspended, with its reference", async () => {
    fakeApi({
      "GET /api/account/v1/me": me(),
      "GET /api/admin/v1/tenant/access": { status: 500, body: problem("PLATFORM_UNEXPECTED_ERROR", 500) },
    });

    openTenant("elssc");

    expect(await screen.findByText("Reference: kmsa-admin.test")).toBeTruthy();
  });

  it("tells a member who may not read why a tenant is suspended whom to ask", async () => {
    fakeApi({
      "GET /api/account/v1/me": me(),
      "GET /api/admin/v1/tenant/access": { status: 403, body: problem("AUTH_FORBIDDEN", 403) },
    });

    openTenant("elssc");

    expect(await screen.findByText(/Ask the organisation's owner why/)).toBeTruthy();
  });

  it("shows the server's reason when it refuses the workspace (a tenant still being set up)", async () => {
    localStorage.setItem("msp.admin.lastWorkspace", "/t/kmsa");
    fakeApi({
      "GET /api/account/v1/me": me(),
      "GET /api/admin/v1/me/capabilities": { status: 403, body: problem("TENANT_PROVISIONING", 403) },
    });

    openTenant("kmsa");

    expect(await screen.findByRole("heading", { name: "You can't open this page" })).toBeTruthy();
    expect(screen.getByText(/still being set up/)).toBeTruthy();
    // `/` must not lead back here; the menu, not a link home, leads elsewhere.
    await waitFor(() => expect(localStorage.getItem("msp.admin.lastWorkspace")).toBeNull());
    expect(screen.getByText(/workspace menu at the top/)).toBeTruthy();
  });

  it("asks `me` again when a tenant was blocked since it was read, and says why it's closed", async () => {
    let meAnswers = 0;
    fakeApi({
      "GET /api/account/v1/me": () =>
        ++meAnswers === 1
          ? me()
          : { status: 200, body: { ...(me().body as object), tenants: [elssc, { ...kmsa, status: "SUSPENDED" }] } },
      "GET /api/admin/v1/me/capabilities": { status: 403, body: problem("TENANT_BLOCKED", 403) },
      "GET /api/admin/v1/tenant/access": {
        status: 200,
        body: {
          status: "SUSPENDED",
          accessMode: "BLOCKED",
          reason: "Subscription unpaid",
          since: "2026-10-20T08:00:00Z",
          timeZone: "Africa/Kampala",
          subscription: { status: "ACTIVE", startsOn: "2026-04-23", endsOn: "2027-04-22", graceEndsOn: "2027-05-06" },
        },
      },
    });

    openTenant("kmsa");

    expect(await screen.findByRole("heading", { name: `${kmsa.displayName} is suspended` })).toBeTruthy();
    expect(screen.queryByRole("heading", { name: "You can't open this page" })).toBeNull();
  });

  it("waits for the guard on a 401 instead of showing it", async () => {
    const requests = fakeApi({
      "GET /api/account/v1/me": me(),
      "GET /api/admin/v1/me/capabilities": { status: 401, body: problem("AUTH_UNAUTHENTICATED", 401) },
    });

    openTenant("kmsa");

    await waitFor(() => expect(requests.some((request) => request.path === "/api/admin/v1/me/capabilities")).toBe(true));
    await waitFor(() => expect(requests.filter((request) => request.path === "/api/account/v1/me")).toHaveLength(2));
    expect(screen.getByRole("status").textContent).toBe("Loading…");
    expect(screen.queryByText("That didn't work")).toBeNull();
  });
});

describe("platform console", () => {
  it("refuses someone without a platform role, without asking the API", async () => {
    const requests = fakeApi({ "GET /api/account/v1/me": me() });
    navigation.pathname = "/platform";

    renderWithQuery(
      <PlatformLayout>
        <p>Console page</p>
      </PlatformLayout>,
    );

    expect(await screen.findByRole("heading", { name: "You can't open this page" })).toBeTruthy();
    expect(screen.queryByText("Console page")).toBeNull();
    expect(requests.some((request) => request.path.startsWith("/api/platform"))).toBe(false);
  });

  it("shows platform staff the entries their platform capabilities allow", async () => {
    fakeApi({
      "GET /api/account/v1/me": me(["PLATFORM_SUPPORT"]),
      "GET /api/platform/v1/me/capabilities": capabilities(["tenant.read"]),
    });
    navigation.pathname = "/platform";

    renderWithQuery(
      <PlatformLayout>
        <p>Console page</p>
      </PlatformLayout>,
    );

    await screen.findByText("Console page");
    expect(localStorage.getItem("msp.admin.lastWorkspace")).toBe("/platform");
    const nav = within(workspaceNav());
    expect(nav.getByRole("link", { name: "Tenants" })).toBeTruthy();
    expect(nav.queryByRole("link", { name: "Diagnostics" })).toBeNull();
  });
});

describe("switcher and user menu", () => {
  const openMenu = (name: string | RegExp) =>
    fireEvent.pointerDown(screen.getByRole("button", { name }), { button: 0, ctrlKey: false, pointerType: "mouse" });

  async function openOwnerWorkspace(platformRoles: string[] = []) {
    fakeApi({
      "GET /api/account/v1/me": me(platformRoles),
      "GET /api/admin/v1/me/capabilities": capabilities([]),
      "POST /api/account/v1/logout": { status: 204 },
    });
    openTenant("kmsa");
    await screen.findByText("Workspace page");
  }

  it("lists the user's tenants, marking blocked ones, and the console for platform staff", async () => {
    await openOwnerWorkspace(["PLATFORM_ADMIN"]);

    openMenu(`Workspace: ${kmsa.displayName}`);

    const menu = within(await screen.findByRole("menu"));
    const current = menu.getByRole("menuitem", { name: kmsa.displayName });
    expect(current.getAttribute("href")).toBe("/t/kmsa");
    expect(current.getAttribute("aria-current")).toBe("page");
    expect(menu.getByRole("menuitem", { name: new RegExp(`^${elssc.displayName}\\s*Suspended$`) })).toBeTruthy();
    expect(menu.getByRole("menuitem", { name: "Platform console" }).getAttribute("href")).toBe("/platform");
  });

  it("offers no console to someone without a platform role", async () => {
    await openOwnerWorkspace();

    openMenu(`Workspace: ${kmsa.displayName}`);

    const menu = within(await screen.findByRole("menu"));
    expect(menu.queryByRole("menuitem", { name: "Platform console" })).toBeNull();
  });

  it("signs out from the user menu with a fresh load of the sign-in page (no return path)", async () => {
    await openOwnerWorkspace();

    openMenu(/^Account menu: /);
    fireEvent.click(await screen.findByRole("menuitem", { name: "Sign out" }));

    await waitFor(() => expect(leaveTo).toHaveBeenCalledWith("/sign-in"));
    expect(navigation.replace).not.toHaveBeenCalled();
    expect(localStorage.getItem("msp.admin.lastWorkspace")).toBeNull();
  });
});
