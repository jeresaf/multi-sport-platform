// Tenant settings → Enabled sports (S05 D17): every variant with its state, Enable at once, Disable with a reason, the
// sport quota's usage and its limit (two variants of one sport count once, D13), read-only, and the API's refusals.
// The variants are invented ("Ringball", "Hoopstick"): the page names nothing but what the API sends.
import { cleanup, fireEvent, screen, waitFor, within } from "@testing-library/react";
import { errorMessage } from "@msp/api-client/errors";
import { afterEach, describe, expect, it, vi } from "vitest";
import { readOnlyCapabilities, tenantCapabilities } from "@/test/capabilities";
import { fakeApi, problem, renderWithQuery, type Answer, type Answers } from "@/test/render";
import TenantWorkspaceLayout from "../../layout";
import EnabledSportsPage from "./page";
import { sportLimitReason, type TenantSport, type TenantSports } from "./sports-settings-data";

vi.mock("next/navigation", () => ({
  useParams: () => ({ tenantSlug: "kmsa" }),
  usePathname: () => "/t/kmsa/settings/sports",
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

const sevens: TenantSport = {
  variant: "RINGBALL_SEVENS",
  sport: "RINGBALL",
  name: "Ringball sevens",
  scoringFamily: "ADDITIVE",
  enabled: true,
  // 01:00 on 12 Nov in Kampala, still 11 Nov in UTC: the day shown is the tenant's.
  enabledSince: "2026-11-11T22:00:00Z",
};
const fives: TenantSport = { ...sevens, variant: "RINGBALL_FIVES", name: "Ringball fives", enabled: false, enabledSince: null };
const hoopstick: TenantSport = {
  variant: "HOOPSTICK",
  sport: "HOOPSTICK",
  name: "Hoopstick",
  scoringFamily: "ADDITIVE",
  enabled: false,
  enabledSince: null,
};
const quota = (used: number, limit: number | null) => ({
  code: "MAX_ENABLED_SPORTS",
  measured: true,
  used,
  limit,
  warning: limit !== null && used >= limit * 0.8,
});
const list = (variants: TenantSport[], sportQuota: TenantSports["sportQuota"] = quota(1, 3)): Answer => ({
  status: 200,
  body: { variants, sportQuota },
});

const ownerActions = [
  "tenant.settings.read",
  "tenant.sport.read", "tenant.sport.enable", "tenant.sport.disable", "sport.read", "tenant.subscription.read"];

function open(actions: string[], answers: Answers = {}, capabilities = tenantCapabilities(actions)) {
  const requests = fakeApi({
    "GET /api/account/v1/me": me(["TENANT_OWNER"]),
    "GET /api/admin/v1/me/capabilities": { status: 200, body: capabilities },
    "GET /api/admin/v1/tenant/settings": {
      status: 200,
      body: { timeZone: "Africa/Kampala", defaultLocale: "en-UG", currencyCode: "UGX" },
    },
    "GET /api/admin/v1/tenant/sports": list([sevens, fives, hoopstick]),
    ...answers,
  });
  const { queryClient } = renderWithQuery(
    <TenantWorkspaceLayout>
      <EnabledSportsPage />
    </TenantWorkspaceLayout>,
  );
  // The Subscription page's read, as if it had been opened: a change must mark its usage stale.
  queryClient.setQueryData(subscriptionKey, { usage: [] });
  return Object.assign(requests, { queryClient });
}

const subscriptionKey = ["tenant-subscription", tenantId];
const subscriptionStale = (requests: ReturnType<typeof open>) =>
  requests.queryClient.getQueryState(subscriptionKey)?.isInvalidated === true;

const row = (name: string) => screen.getByRole("row", { name: new RegExp(`^${name}`) });

afterEach(() => {
  cleanup();
  vi.unstubAllGlobals();
  localStorage.clear();
});

describe("enabled sports", () => {
  it("lists every variant with its state, since when in the tenant's time zone, and the plan's usage", async () => {
    open(ownerActions);

    expect(await screen.findByRole("heading", { level: 1, name: "Enabled sports" })).toBeTruthy();
    expect(await screen.findByRole("link", { name: "Ringball sevens" })).toBeTruthy();
    expect(within(row("Ringball sevens")).getAllByText("since 12 Nov 2026").length).toBeGreaterThan(0);
    expect(within(row("Hoopstick")).getAllByText("Not enabled").length).toBeGreaterThan(0);
    expect(screen.getByText(/1 of 3 sports/)).toBeTruthy();
    expect(screen.getByRole("button", { name: "Disable Ringball sevens" })).toBeTruthy();
    expect((screen.getByRole("button", { name: "Enable Hoopstick" }) as HTMLButtonElement).disabled).toBe(false);
  });

  it("shows a viewer the list without controls, usage or links it can't use", async () => {
    open(["tenant.sport.read"], { "GET /api/admin/v1/tenant/sports": list([sevens, hoopstick], null) });

    expect(await screen.findByText("Ringball sevens")).toBeTruthy();
    expect(screen.queryByRole("link", { name: "Ringball sevens" })).toBeNull();
    expect(screen.queryByRole("button", { name: /^(Enable|Disable) / })).toBeNull();
    expect(screen.queryByText(/Used:/)).toBeNull();
  });

  it("enables a variant at once and shows the list the API answers", async () => {
    const requests = open(ownerActions, {
      "POST /api/admin/v1/tenant/sports/HOOPSTICK/enable": list(
        [sevens, fives, { ...hoopstick, enabled: true, enabledSince: "2026-11-16T07:00:00Z" }],
        quota(2, 3),
      ),
    });

    fireEvent.click(await screen.findByRole("button", { name: "Enable Hoopstick" }));

    expect(await screen.findByText("Hoopstick is enabled.")).toBeTruthy();
    expect(screen.getByRole("button", { name: "Disable Hoopstick" })).toBeTruthy();
    expect(screen.getByText(/2 of 3 sports/)).toBeTruthy();
    expect(requests.filter((request) => request.method === "POST").map((request) => request.path)).toEqual([
      "/api/admin/v1/tenant/sports/HOOPSTICK/enable",
    ]);
    // The Enable button is gone: focus is on the row's Disable, not lost (#92 R1); the subscription's usage is stale.
    await waitFor(() => expect(document.activeElement).toBe(screen.getByRole("button", { name: "Disable Hoopstick" })));
    expect(subscriptionStale(requests)).toBe(true);
  });

  it("asks for a reason before disabling, then sends it", async () => {
    const requests = open(ownerActions, {
      "POST /api/admin/v1/tenant/sports/RINGBALL_SEVENS/disable": list(
        [{ ...sevens, enabled: false, enabledSince: null }, fives, hoopstick],
        quota(0, 3),
      ),
    });

    fireEvent.click(await screen.findByRole("button", { name: "Disable Ringball sevens" }));
    const dialog = await screen.findByRole("dialog", { name: "Disable Ringball sevens?" });
    fireEvent.click(within(dialog).getByRole("button", { name: "Disable" }));
    expect(await within(dialog).findByText("Give a reason: it is kept in the audit trail.")).toBeTruthy();

    fireEvent.change(within(dialog).getByLabelText("Reason"), { target: { value: "  The league moved to the fives  " } });
    fireEvent.click(within(dialog).getByRole("button", { name: "Disable" }));

    expect(await screen.findByText("Ringball sevens is disabled.")).toBeTruthy();
    await waitFor(() => expect(screen.queryByRole("dialog")).toBeNull());
    const enableAgain = screen.getByRole("button", { name: "Enable Ringball sevens" });
    // The Disable button that opened the dialog is gone: focus is on the row's Enable (#92 R1).
    await waitFor(() => expect(document.activeElement).toBe(enableAgain));
    const sent = requests.find((request) => request.method === "POST");
    expect(sent?.body).toEqual({ reason: "The league moved to the fives" });
    expect(subscriptionStale(requests)).toBe(true);
  });

  it("keeps a disable refused because it was disabled meanwhile in the dialog, read again, and confirming it no more", async () => {
    let reads = 0;
    const requests = open(ownerActions, {
      "GET /api/admin/v1/tenant/sports": () => {
        reads += 1;
        return list(reads === 1 ? [sevens, fives, hoopstick] : [{ ...sevens, enabled: false, enabledSince: null }, fives, hoopstick]);
      },
      "POST /api/admin/v1/tenant/sports/RINGBALL_SEVENS/disable": { status: 409, body: problem("TENANT_SPORT_NOT_ENABLED", 409) },
    });

    fireEvent.click(await screen.findByRole("button", { name: "Disable Ringball sevens" }));
    const dialog = await screen.findByRole("dialog", { name: "Disable Ringball sevens?" });
    fireEvent.change(within(dialog).getByLabelText("Reason"), { target: { value: "The league moved to the fives" } });
    fireEvent.click(within(dialog).getByRole("button", { name: "Disable" }));

    expect(await within(dialog).findByText(errorMessage("TENANT_SPORT_NOT_ENABLED")!)).toBeTruthy();
    await waitFor(() => expect(reads).toBe(2));
    await waitFor(() =>
      expect((within(dialog).getByRole("button", { name: "Disable" }) as HTMLButtonElement).disabled).toBe(true),
    );
    expect(requests.filter((request) => request.method === "POST")).toHaveLength(1);
    expect(subscriptionStale(requests)).toBe(true);
  });

  it("at the limit, disables Enable for a new sport with the plan's limit, but not for a variant of a counted sport", async () => {
    open(ownerActions, { "GET /api/admin/v1/tenant/sports": list([sevens, fives, hoopstick], quota(1, 1)) });

    const enableHoopstick = (await screen.findByRole("button", { name: "Enable Hoopstick" })) as HTMLButtonElement;
    expect(enableHoopstick.disabled).toBe(true);
    expect(enableHoopstick.getAttribute("aria-describedby")).toBe("sports-unavailable");
    expect(screen.getByText("Your plan allows 1 sport.")).toBeTruthy();
    expect(screen.getByText("At the limit")).toBeTruthy();
    expect((screen.getByRole("button", { name: "Enable Ringball fives" }) as HTMLButtonElement).disabled).toBe(false);
  });

  it("warns near the limit and says nothing about it below", async () => {
    open(ownerActions, { "GET /api/admin/v1/tenant/sports": list([sevens, fives, hoopstick], quota(4, 5)) });
    expect(await screen.findByText("Near the limit")).toBeTruthy();
    expect(screen.queryByText(/Your plan allows/)).toBeNull();
  });

  it("with a limit of none, refuses every new sport", async () => {
    open(ownerActions, {
      "GET /api/admin/v1/tenant/sports": list([{ ...sevens, enabled: false, enabledSince: null }, hoopstick], quota(0, 0)),
    });
    expect(await screen.findByText("Your plan allows 0 sports.")).toBeTruthy();
    expect((screen.getByRole("button", { name: "Enable Hoopstick" }) as HTMLButtonElement).disabled).toBe(true);
    expect((screen.getByRole("button", { name: "Enable Ringball sevens" }) as HTMLButtonElement).disabled).toBe(true);
  });

  it("after the API's quota refusal, reads the list again and says the limit once, with Enable disabled", async () => {
    let reads = 0;
    const requests = open(ownerActions, {
      "GET /api/admin/v1/tenant/sports": () => {
        reads += 1;
        return list([sevens, fives, hoopstick], reads === 1 ? quota(1, 3) : quota(3, 3));
      },
      "POST /api/admin/v1/tenant/sports/HOOPSTICK/enable": {
        status: 403,
        body: problem("ENTITLEMENT_QUOTA_EXCEEDED", 403, { detail: "Your plan allows 3 sports." }),
      },
    });

    fireEvent.click(await screen.findByRole("button", { name: "Enable Hoopstick" }));

    await waitFor(() => expect(reads).toBe(2));
    const enable = screen.getByRole("button", { name: "Enable Hoopstick" }) as HTMLButtonElement;
    await waitFor(() => expect(enable.disabled).toBe(true));
    expect(enable.getAttribute("aria-describedby")).toBe("sports-unavailable");
    expect(screen.getByText("Used: 3 of 3 sports")).toBeTruthy();
    // The note says the limit; the refusal's alert would only repeat it (#92 R4).
    expect(screen.getAllByText("Your plan allows 3 sports.")).toHaveLength(1);
    expect(screen.queryByRole("alert")).toBeNull();
    // Enable can't be used now: focus goes to the heading rather than the page's body (#92 R1).
    await waitFor(() => expect(document.activeElement).toBe(screen.getByRole("heading", { level: 1, name: "Enabled sports" })));
    expect(subscriptionStale(requests)).toBe(true);
  });

  it("reads the list again when it was out of date", async () => {
    let reads = 0;
    open(ownerActions, {
      "GET /api/admin/v1/tenant/sports": () => {
        reads += 1;
        return list(reads === 1 ? [sevens, fives, hoopstick] : [sevens, fives, { ...hoopstick, enabled: true }]);
      },
      "POST /api/admin/v1/tenant/sports/HOOPSTICK/enable": {
        status: 409,
        body: problem("TENANT_SPORT_ALREADY_ENABLED", 409),
      },
    });

    fireEvent.click(await screen.findByRole("button", { name: "Enable Hoopstick" }));

    expect(await screen.findByText(errorMessage("TENANT_SPORT_ALREADY_ENABLED")!)).toBeTruthy();
    expect(await screen.findByRole("button", { name: "Disable Hoopstick" })).toBeTruthy();
  });

  it("shows the controls disabled with the reason when the workspace is read-only", async () => {
    open(
      ["tenant.sport.read"],
      {},
      readOnlyCapabilities(["tenant.sport.read", "sport.read"], ["tenant.sport.enable", "tenant.sport.disable"]),
    );

    const enable = (await screen.findByRole("button", { name: "Enable Hoopstick" })) as HTMLButtonElement;
    expect(enable.disabled).toBe(true);
    expect((screen.getByRole("button", { name: "Disable Ringball sevens" }) as HTMLButtonElement).disabled).toBe(true);
    expect(screen.getByText("Read-only: the subscription was cancelled")).toBeTruthy();
  });

  it("refuses someone without tenant.sport.read", async () => {
    const requests = open(["tenant.settings.read"]);
    expect(await screen.findByRole("heading", { name: "You can't open this page" })).toBeTruthy();
    expect(requests.some((request) => request.path === "/api/admin/v1/tenant/sports")).toBe(false);
  });
});

// The limit's edge cases (#92 R5): unlimited, a limit lowered below the usage, a usage not known.
describe("sportLimitReason", () => {
  const variants = [sevens, fives, hoopstick];
  it.each([
    ["unlimited", quota(5, null), null],
    ["below the limit", quota(2, 3), null],
    ["at the limit", quota(3, 3), "Your plan allows 3 sports."],
    ["over a lowered limit", quota(2, 1), "Your plan allows 1 sport."],
    ["usage not read (no tenant.subscription.read)", null, null],
    ["not measured", { ...quota(0, 1), measured: false, used: null }, null],
  ])("a new sport, %s", (_, usage, expected) => {
    expect(sportLimitReason(hoopstick, variants, usage)).toBe(expected);
  });

  it("never refuses a variant of a sport already enabled, even over the limit", () => {
    expect(sportLimitReason(fives, variants, quota(2, 1))).toBeNull();
  });
});
