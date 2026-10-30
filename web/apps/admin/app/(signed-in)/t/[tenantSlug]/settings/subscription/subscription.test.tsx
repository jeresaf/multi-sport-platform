// Tenant settings → Subscription: the plan, its dates, what it includes and the usage, as members see it; read-only
// without a period in effect; only for whoever may read it.
import type { components } from "@msp/api-client";
import { cleanup, screen, within } from "@testing-library/react";
import { afterEach, describe, expect, it, vi } from "vitest";
import { fullAccess, tenantCapabilities } from "@/test/capabilities";
import { fakeApi, problem, renderWithQuery, type Answer } from "@/test/render";
import TenantWorkspaceLayout from "../../layout";
import SubscriptionPage from "./page";

vi.mock("next/navigation", () => ({
  useParams: () => ({ tenantSlug: "kmsa" }),
  usePathname: () => "/t/kmsa/settings/subscription",
  useRouter: () => ({ replace: vi.fn(), push: vi.fn() }),
}));

type Subscription = components["schemas"]["TenantSubscriptionResponse"];
type Period = components["schemas"]["TenantSubscriptionPeriod"];

const tenantId = "0199a3f0-0000-7000-8000-00000000000a";
const me: Answer = {
  status: 200,
  body: {
    id: "0199a3f0-0000-7000-8000-000000000001",
    email: "daniel.kato@kmsa.example",
    platformRoles: [],
    tenants: [
      { tenantId, slug: "kmsa", displayName: "Kampala Metro Sports Association", status: "ACTIVE", roles: ["VIEWER"] },
    ],
  },
};

// KMSA's pilot period (pilot-customer.md) and the slice demo's staff-user override.
const pilot: Period = {
  planCode: "STANDARD",
  planName: "Standard",
  kind: "COMPLIMENTARY",
  status: "ACTIVE",
  startsOn: "2026-10-01",
  endsOn: "2027-03-31",
  expiringFrom: "2027-03-02",
  graceEndsOn: "2027-04-14",
};
const subscription = (overrides: Partial<Subscription> = {}): Subscription => ({
  today: "2026-10-29",
  timeZone: "Africa/Kampala",
  current: pilot,
  next: null,
  entitlements: [
    {
      code: "CUSTOM_DOMAIN",
      name: "Custom domain",
      description: "The public site on the tenant's own domain.",
      type: "BOOLEAN",
      unit: null,
      levels: [],
      value: { enabled: false },
      source: "PLAN",
    },
    {
      code: "MAX_ENABLED_SPORTS",
      name: "Enabled sports",
      description: "Sports the tenant can enable at once.",
      type: "QUOTA",
      unit: "sports",
      levels: [],
      value: { limit: 3 },
      source: "PLAN",
    },
    {
      code: "MAX_STAFF_USERS",
      name: "Staff users",
      description: "Members and pending invitations.",
      type: "QUOTA",
      unit: "users",
      levels: [],
      value: { limit: 5 },
      source: "OVERRIDE",
    },
  ],
  usage: [
    { code: "MAX_ENABLED_SPORTS", measured: false, used: null, limit: 3, warning: false },
    { code: "MAX_STAFF_USERS", measured: true, used: 4, limit: 5, warning: true },
  ],
  ...overrides,
});

const readOnly = { ...fullAccess, mode: "READ_ONLY" } as const;

function open(answer: Answer, actions = ["tenant.subscription.read"], access = fullAccess) {
  const requests = fakeApi({
    "GET /api/account/v1/me": me,
    "GET /api/admin/v1/me/capabilities": { status: 200, body: tenantCapabilities(actions, { access }) },
    "GET /api/admin/v1/tenant/subscription": answer,
  });
  renderWithQuery(
    <TenantWorkspaceLayout>
      <SubscriptionPage />
    </TenantWorkspaceLayout>,
  );
  return requests;
}

const entitlement = (name: string) => screen.getByText(name, { selector: "p" }).closest("li")!;

afterEach(() => {
  cleanup();
  vi.unstubAllGlobals();
});

describe("tenant subscription", () => {
  it("shows the plan, its dates and what it includes, with the usage and its warning", async () => {
    open({ status: 200, body: subscription() });

    expect(await screen.findByRole("heading", { level: 2, name: "Standard plan" })).toBeTruthy();
    expect(screen.getByText("Complimentary")).toBeTruthy();
    expect(screen.getByText("1 Oct 2026 – 31 Mar 2027")).toBeTruthy();
    expect(screen.getByText("14 Apr 2027")).toBeTruthy();
    expect(screen.getByText(/contact the platform team/)).toBeTruthy();
    expect(screen.getByText("Days begin and end in Africa/Kampala time.")).toBeTruthy();

    const staff = within(entitlement("Staff users"));
    expect(staff.getByText(/^5 users/)).toBeTruthy();
    // An override shows its value, never its reason (the platform's).
    expect(staff.getByText(/Arranged for your organisation/)).toBeTruthy();
    expect(staff.getByText(/Used: 4 of 5 users/)).toBeTruthy();
    expect(staff.getByText("Near the limit")).toBeTruthy();

    expect(within(entitlement("Enabled sports")).getByText(/Standard plan/)).toBeTruthy();
    expect(within(entitlement("Enabled sports")).getByText("Usage: not measured yet")).toBeTruthy();
    expect(within(entitlement("Custom domain")).getByText(/Not included/)).toBeTruthy();
    // No self-service: nothing to press on the page (the shell has its own menus).
    expect(within(screen.getByRole("main")).queryByRole("button")).toBeNull();
  });

  it("says the workspace is read-only without a subscription", async () => {
    open({ status: 200, body: subscription({ current: null }) }, undefined, { ...readOnly, subscriptionStatus: null });

    expect(await screen.findByText("No subscription")).toBeTruthy();
    expect(screen.getByText(/read-only until the platform team assigns one/)).toBeTruthy();
  });

  it("doesn't call a tenant being set up read-only: it has full access without a subscription", async () => {
    open({ status: 200, body: subscription({ current: null }) }, undefined, { ...fullAccess, subscriptionStatus: null });

    expect(await screen.findByText("No subscription")).toBeTruthy();
    expect(screen.getByText("The platform team assigns your organisation's subscription.")).toBeTruthy();
    expect(screen.queryByText(/read-only/)).toBeNull();
  });

  it("says until when a workspace whose period ended is read-only, and shows the next period", async () => {
    const next: Period = { ...pilot, kind: "PAID", status: "SCHEDULED", startsOn: "2026-11-01", endsOn: "2027-10-31", graceEndsOn: "2027-11-14" };
    open({ status: 200, body: subscription({ current: { ...pilot, status: "EXPIRED" }, next }) }, undefined, readOnly);

    expect(await screen.findByText("The workspace is read-only until the period starts on 1 Nov 2026.")).toBeTruthy();
    // Its grace is over: no "full access until" a past day.
    expect(screen.queryByText("Full access until")).toBeNull();
    expect(screen.getByRole("heading", { level: 2, name: "Next period" })).toBeTruthy();
    expect(screen.getByText("1 Nov 2026 – 31 Oct 2027")).toBeTruthy();
  });

  it("doesn't offer grace for a cancelled period", async () => {
    open({ status: 200, body: subscription({ current: { ...pilot, status: "CANCELLED" } }) }, undefined, readOnly);

    expect(await screen.findByText(/read-only until the platform team renews/)).toBeTruthy();
    expect(screen.queryByText("Full access until")).toBeNull();
  });

  it("shows a failure to read the subscription", async () => {
    open({ status: 500, body: problem("PLATFORM_UNEXPECTED_ERROR", 500) });

    expect(await screen.findByText("Reference: kmsa-admin.test")).toBeTruthy();
  });

  it("refuses a member who may not read it, without asking", async () => {
    const requests = open({ status: 200, body: subscription() }, ["tenant.settings.read"]);

    expect(await screen.findByRole("heading", { name: "You can't open this page" })).toBeTruthy();
    expect(requests.some((request) => request.path === "/api/admin/v1/tenant/subscription")).toBe(false);
  });
});
