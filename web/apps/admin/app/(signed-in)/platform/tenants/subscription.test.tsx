// Platform console → a tenant's Subscription panel (S04 D19): the current period, periods, effective entitlements with
// usage, overrides with reasons; the period and override commands send what the API takes and show its answer; the
// activation warns when the tenant has no period (D9).
import { cleanup, fireEvent, screen, waitFor, within } from "@testing-library/react";
import { errorMessage } from "@msp/api-client/errors";
import { afterEach, describe, expect, it, vi } from "vitest";
import { fakeApi, problem, renderWithQuery, type Answers } from "@/test/render";
import {
  kmsaId,
  noSubscription,
  pilotPeriod,
  plansAnswer,
  staffOverride,
  subscription,
} from "@/test/subscriptions";
import PlatformLayout from "../layout";
import { renewalStart } from "./[tenantId]/subscription-panel";
import TenantPage from "./[tenantId]/page";

vi.mock("next/navigation", () => ({
  useParams: () => ({ tenantId: kmsaId }),
  usePathname: () => `/platform/tenants/${kmsaId}`,
  useRouter: () => ({ replace: vi.fn(), push: vi.fn() }),
}));

const kmsa = {
  id: kmsaId,
  slug: "kmsa",
  legalName: "Kampala Metro Sports Association",
  displayName: "Kampala Metro Sports Association",
  countryCode: "UG",
  timeZone: "Africa/Kampala",
  defaultLocale: "en-UG",
  currencyCode: "UGX",
  status: "ACTIVE",
  publicEnabled: true,
  createdAt: "2026-09-30T07:00:00Z",
  updatedAt: "2026-10-19T09:00:00Z",
  version: 4,
};

const adminActions = [
  "tenant.read",
  "tenant.activate",
  "tenant.suspend",
  "tenant.archive",
  "plan.read",
  "subscription.read",
  "subscription.assign",
  "subscription.renew",
  "subscription.change_plan",
  "subscription.cancel",
  "entitlement.override",
  "entitlement.override.remove",
];
const supportActions = ["tenant.read", "plan.read", "subscription.read"];

const base = `/api/platform/v1/tenants/${kmsaId}`;

function open(answers: Answers = {}, actions = adminActions, tenant = kmsa) {
  const requests = fakeApi({
    "GET /api/account/v1/me": {
      status: 200,
      body: { id: "0199a3f0-0000-7000-8000-000000000001", email: "admin@sportsplatform.localhost", platformRoles: ["PLATFORM_ADMIN"], tenants: [] },
    },
    "GET /api/platform/v1/me/capabilities": { status: 200, body: { actions, scope: null } },
    "GET /api/public/v1/reference/countries": { status: 200, body: { items: [], page: { nextCursor: null, hasMore: false } } },
    "GET /api/platform/v1/plans": plansAnswer,
    [`GET ${base}`]: { status: 200, body: tenant },
    [`GET ${base}/subscription`]: { status: 200, body: subscription() },
    ...answers,
  });
  renderWithQuery(
    <PlatformLayout>
      <TenantPage />
    </PlatformLayout>,
  );
  return requests;
}

const subscriptionReads = (requests: { method: string; path: string }[]) =>
  requests.filter((request) => request.method === "GET" && request.path === `${base}/subscription`).length;

const confirm = (dialog: ReturnType<typeof within>, name: string) =>
  dialog.getByRole("button", { name }) as HTMLButtonElement;

const panel = async () => within((await screen.findByRole("heading", { name: "Subscription" })).closest("section")!);
const commandsOffered = async () =>
  (await panel())
    .getAllByRole("button")
    .map((button) => button.textContent)
    .filter((label) => ["Assign a period", "Renew", "Change plan", "Cancel subscription"].includes(label ?? ""));

afterEach(() => {
  cleanup();
  vi.unstubAllGlobals();
});

describe("the subscription panel", () => {
  it("shows the current period, the periods, the entitlements with usage, and the overrides with their reasons", async () => {
    open();

    const section = await panel();
    expect(await section.findByRole("heading", { name: "Current period" })).toBeTruthy();
    expect(section.getAllByText("Standard").length).toBeGreaterThan(0);
    expect(section.getAllByText("Complimentary").length).toBeGreaterThan(0);
    expect(section.getAllByText("1 Oct 2026 – 31 Mar 2027").length).toBe(2);
    expect(section.getByText("14 Apr 2027")).toBeTruthy();
    expect(section.getByText(/today is 29 Oct 2026 there/)).toBeTruthy();

    // Each entitlement with its value and where it comes from; the staff users with their usage and the warning.
    const staff = section.getByText("Staff users", { selector: "p" }).closest("li")!;
    expect(staff.textContent).toContain("5 users");
    expect(staff.textContent).toContain("Override");
    expect(staff.textContent).toContain("Used: 4 of 5 users");
    expect(staff.textContent).toContain("Near the limit");
    const sports = section.getByText("Enabled sports", { selector: "p" }).closest("li")!;
    expect(sports.textContent).toContain("3 sports · Standard plan");
    expect(sports.textContent).toContain("Usage: not measured yet");

    // The override in place with its reason; the replaced one in the history.
    expect(section.getByText(staffOverride.reason)).toBeTruthy();
    expect(section.getByText(/until 31 Mar 2027\./)).toBeTruthy();
    expect(section.getByText("Earlier overrides (1)")).toBeTruthy();
    expect(section.getByText(/Replaced by a new override/)).toBeTruthy();

    expect(await commandsOffered()).toEqual(["Assign a period", "Renew", "Change plan", "Cancel subscription"]);
  });

  it("offers support staff no change", async () => {
    open({}, supportActions);

    const section = await panel();
    expect(await section.findByRole("heading", { name: "Current period" })).toBeTruthy();
    expect(section.queryByRole("button")).toBeNull();
    expect(section.getByText("Changing the subscription or its overrides needs a platform administrator.")).toBeTruthy();
  });

  it("says a tenant without a period is read-only, and offers only to assign one", async () => {
    open({ [`GET ${base}/subscription`]: { status: 200, body: noSubscription } });

    const section = await panel();
    expect(await section.findByText("No subscription")).toBeTruthy();
    expect(section.getByText("The tenant is read-only until a period is assigned.")).toBeTruthy();
    expect(await commandsOffered()).toEqual(["Assign a period"]);
  });

  it("says an active tenant whose period has expired is read-only", async () => {
    const expired = { ...pilotPeriod, status: "EXPIRED" as const };
    open({ [`GET ${base}/subscription`]: { status: 200, body: subscription({ periods: [expired] }) } });

    const section = await panel();
    expect(await section.findByRole("heading", { name: "Current period" })).toBeTruthy();
    expect(section.getByText("No period is in effect today: the tenant is read-only until one starts or is assigned.")).toBeTruthy();
    // Nothing left to cancel or move to another plan.
    expect(await commandsOffered()).toEqual(["Assign a period", "Renew"]);
  });

  it("can't send a plan change again once the subscription shows it no longer applies", async () => {
    // Another administrator cancelled meanwhile: no current period to move to another plan.
    const cancelled = subscription({
      periods: [{ ...pilotPeriod, status: "CANCELLED", cancelledAt: "2026-10-29T08:00:00Z", cancelReason: "Fees not paid" }],
    });
    let reads = 0;
    open({
      [`GET ${base}/subscription`]: () => {
        reads += 1;
        return { status: 200, body: reads === 1 ? subscription() : cancelled };
      },
      [`POST ${base}/subscription/change-plan`]: { status: 409, body: problem("TENANT_SUBSCRIPTION_NOT_FOUND", 409) },
    });

    fireEvent.click(await (await panel()).findByRole("button", { name: "Change plan" }));
    const dialog = within(await screen.findByRole("dialog"));
    fireEvent.click(confirm(dialog, "Change plan"));

    expect(await dialog.findByText(errorMessage("TENANT_SUBSCRIPTION_NOT_FOUND")!)).toBeTruthy();
    await waitFor(() => expect(confirm(dialog, "Change plan").disabled).toBe(true));
  });

  it("keeps the dialog and its refusal when reading the subscription again fails", async () => {
    let reads = 0;
    open({
      [`GET ${base}/subscription`]: () => {
        reads += 1;
        return reads === 1
          ? { status: 200, body: subscription() }
          : { status: 500, body: problem("PLATFORM_UNEXPECTED_ERROR", 500) };
      },
      [`POST ${base}/subscription/periods`]: { status: 409, body: problem("TENANT_SUBSCRIPTION_OVERLAP", 409) },
    });

    fireEvent.click(await (await panel()).findByRole("button", { name: "Assign a period" }));
    const dialog = within(await screen.findByRole("dialog"));
    fireEvent.change(dialog.getByLabelText("Last day"), { target: { value: "2027-01-31" } });
    fireEvent.click(confirm(dialog, "Assign period"));

    expect(await dialog.findByText(errorMessage("TENANT_SUBSCRIPTION_OVERLAP")!)).toBeTruthy();
    // The failed read shows its alert in the panel (#71 R10); the dialog and its refusal stay.
    await waitFor(() => expect(reads).toBe(2));
    // The page behind the modal dialog is hidden from the accessibility tree: found by its text.
    expect((await screen.findAllByText(errorMessage("PLATFORM_UNEXPECTED_ERROR")!)).length).toBeGreaterThan(0);
    expect(screen.getByRole("dialog")).toBeTruthy();
    expect(dialog.getByText(errorMessage("TENANT_SUBSCRIPTION_OVERLAP")!)).toBeTruthy();
  });

  it("says when a suspended tenant without a period will be read-only", async () => {
    open({ [`GET ${base}/subscription`]: { status: 200, body: noSubscription } }, adminActions, {
      ...kmsa,
      status: "SUSPENDED",
    });

    expect(
      await (await panel()).findByText("Once restored, the tenant is read-only until a period is assigned."),
    ).toBeTruthy();
  });

  it("offers no plan change on the highest plan", async () => {
    const onPro = { ...pilotPeriod, planCode: "PRO" };
    open({ [`GET ${base}/subscription`]: { status: 200, body: subscription({ periods: [onPro] }) } });

    await (await panel()).findByRole("heading", { name: "Current period" });
    expect(await commandsOffered()).toEqual(["Assign a period", "Renew", "Cancel subscription"]);
  });

  it("assigns a period with the chosen dates and default windows, and shows the answer", async () => {
    const assigned = subscription({ nextPeriodId: "0199a3f0-0000-7000-8000-000000000b02" });
    assigned.periods = [
      { ...pilotPeriod, id: "0199a3f0-0000-7000-8000-000000000b02", kind: "PAID", status: "SCHEDULED", startsOn: "2027-04-01", endsOn: "2028-03-31" },
      pilotPeriod,
    ];
    const requests = open({ [`POST ${base}/subscription/periods`]: { status: 201, body: assigned } });

    fireEvent.click(await (await panel()).findByRole("button", { name: "Assign a period" }));
    const dialog = within(await screen.findByRole("dialog"));
    expect((dialog.getByLabelText("First day") as HTMLInputElement).value).toBe("2026-10-29");
    fireEvent.click(dialog.getByRole("button", { name: "Assign period" }));
    expect(await dialog.findByText("Choose the last day.")).toBeTruthy();

    fireEvent.change(dialog.getByLabelText("First day"), { target: { value: "2027-04-01" } });
    fireEvent.change(dialog.getByLabelText("Last day"), { target: { value: "2027-03-31" } });
    fireEvent.click(dialog.getByRole("button", { name: "Assign period" }));
    expect(await dialog.findByText("The last day can't be before the first day.")).toBeTruthy();

    fireEvent.change(dialog.getByLabelText("Last day"), { target: { value: "2028-03-31" } });
    fireEvent.change(dialog.getByLabelText("Kind"), { target: { value: "PAID" } });
    fireEvent.click(dialog.getByRole("button", { name: "Assign period" }));

    await waitFor(() => expect(screen.queryByRole("dialog")).toBeNull());
    expect(requests.find((request) => request.path === `${base}/subscription/periods`)!.body).toEqual({
      planCode: "STANDARD",
      kind: "PAID",
      startsOn: "2027-04-01",
      endsOn: "2028-03-31",
      expiringWindowDays: null,
      graceDays: null,
    });
    expect(await screen.findByText(/Next: Standard, paid, from 1 Apr 2027 to 31 Mar 2028\./)).toBeTruthy();
    expect(screen.getAllByRole("status").map((status) => status.textContent)).toContain("Period assigned.");
  });

  it("keeps an overlap refusal in the dialog, and reads the subscription again", async () => {
    const requests = open({
      [`POST ${base}/subscription/periods`]: { status: 409, body: problem("TENANT_SUBSCRIPTION_OVERLAP", 409) },
    });

    fireEvent.click(await (await panel()).findByRole("button", { name: "Assign a period" }));
    const dialog = within(await screen.findByRole("dialog"));
    fireEvent.change(dialog.getByLabelText("Last day"), { target: { value: "2027-01-31" } });
    fireEvent.change(dialog.getByLabelText("Grace (days)"), { target: { value: "7" } });
    fireEvent.click(dialog.getByRole("button", { name: "Assign period" }));

    expect(await dialog.findByText(errorMessage("TENANT_SUBSCRIPTION_OVERLAP")!)).toBeTruthy();
    expect(screen.getByRole("dialog")).toBeTruthy();
    // A conflict means the panel was out of date (#71 R1).
    await waitFor(() => expect(subscriptionReads(requests)).toBe(2));
  });

  it("reads the subscription again when a cancel finds nothing left to cancel", async () => {
    // Another administrator cancelled meanwhile: the second read shows it.
    const cancelled = subscription({
      periods: [{ ...pilotPeriod, status: "CANCELLED", cancelledAt: "2026-10-29T08:00:00Z", cancelReason: "Fees not paid" }],
    });
    let reads = 0;
    const requests = open({
      [`GET ${base}/subscription`]: () => {
        reads += 1;
        return { status: 200, body: reads === 1 ? subscription() : cancelled };
      },
      [`POST ${base}/subscription/cancel`]: { status: 409, body: problem("TENANT_SUBSCRIPTION_NOT_FOUND", 409) },
    });

    fireEvent.click(await (await panel()).findByRole("button", { name: "Cancel subscription" }));
    const dialog = within(await screen.findByRole("dialog"));
    fireEvent.change(dialog.getByLabelText("Reason"), { target: { value: "Club wound up" } });
    fireEvent.click(dialog.getByRole("button", { name: "Cancel subscription" }));

    expect(await dialog.findByText(errorMessage("TENANT_SUBSCRIPTION_NOT_FOUND")!)).toBeTruthy();
    await waitFor(() => expect(subscriptionReads(requests)).toBe(2));
    // Nothing left to cancel: the refusal stays, sending again doesn't (#71 R9).
    await waitFor(() => expect(confirm(dialog, "Cancel subscription").disabled).toBe(true));
    fireEvent.click(dialog.getByRole("button", { name: "Cancel" }));
    await waitFor(() => expect(screen.queryByRole("dialog")).toBeNull());
    expect(await commandsOffered()).toEqual(["Assign a period", "Renew"]);
  });

  it("puts the API's field errors under their fields", async () => {
    open({
      [`POST ${base}/subscription/periods`]: {
        status: 400,
        body: problem("VALIDATION_INVALID_REQUEST", 400, {
          errors: [{ field: "expiringWindowDays", code: "VALIDATION_INVALID_REQUEST", message: "0 to 365." }],
        }),
      },
    });

    fireEvent.click(await (await panel()).findByRole("button", { name: "Assign a period" }));
    const dialog = within(await screen.findByRole("dialog"));
    fireEvent.change(dialog.getByLabelText("Last day"), { target: { value: "2027-01-31" } });
    fireEvent.change(dialog.getByLabelText("Expiring warning (days)"), { target: { value: "400" } });
    fireEvent.click(dialog.getByRole("button", { name: "Assign period" }));
    // Checked here first: the API isn't asked.
    expect(await dialog.findByText("Enter a whole number of days from 0 to 365.")).toBeTruthy();

    fireEvent.change(dialog.getByLabelText("Expiring warning (days)"), { target: { value: "300" } });
    fireEvent.click(dialog.getByRole("button", { name: "Assign period" }));
    await waitFor(() =>
      expect(dialog.getByLabelText("Expiring warning (days)").getAttribute("aria-invalid")).toBe("true"),
    );
    expect(dialog.queryByRole("alert")).toBeNull();
  });

  it("renews from the day after the latest period, keeping what isn't chosen", async () => {
    const requests = open({ [`POST ${base}/subscription/renew`]: { status: 200, body: subscription() } });

    fireEvent.click(await (await panel()).findByRole("button", { name: "Renew" }));
    const dialog = within(await screen.findByRole("dialog"));
    expect(dialog.getByText(/starts on 1 Apr 2027/)).toBeTruthy();
    // Checked here first: the API would refuse it without naming the field.
    fireEvent.change(dialog.getByLabelText("Last day"), { target: { value: "2027-03-31" } });
    fireEvent.click(dialog.getByRole("button", { name: "Renew" }));
    expect(await dialog.findByText("The last day can't be before 1 Apr 2027.")).toBeTruthy();
    fireEvent.change(dialog.getByLabelText("Last day"), { target: { value: "2028-03-31" } });
    fireEvent.click(dialog.getByRole("button", { name: "Renew" }));

    await waitFor(() => expect(screen.queryByRole("dialog")).toBeNull());
    expect(requests.find((request) => request.path === `${base}/subscription/renew`)!.body).toEqual({
      endsOn: "2028-03-31",
      planCode: null,
      kind: null,
      expiringWindowDays: null,
      graceDays: null,
    });
  });

  it("changes only to a higher plan", async () => {
    const requests = open({ [`POST ${base}/subscription/change-plan`]: { status: 200, body: subscription() } });

    fireEvent.click(await (await panel()).findByRole("button", { name: "Change plan" }));
    const dialog = within(await screen.findByRole("dialog"));
    expect(within(dialog.getByLabelText("New plan")).getAllByRole("option").map((option) => option.textContent)).toEqual([
      "Pro",
    ]);
    fireEvent.click(dialog.getByRole("button", { name: "Change plan" }));

    await waitFor(() => expect(screen.queryByRole("dialog")).toBeNull());
    expect(requests.find((request) => request.path === `${base}/subscription/change-plan`)!.body).toEqual({
      planCode: "PRO",
    });
  });

  it("cancels with a reason", async () => {
    const cancelled = subscription({ periods: [{ ...pilotPeriod, status: "CANCELLED", cancelledAt: "2026-10-29T08:00:00Z", cancelReason: "Fees not paid" }] });
    const requests = open({ [`POST ${base}/subscription/cancel`]: { status: 200, body: cancelled } });

    fireEvent.click(await (await panel()).findByRole("button", { name: "Cancel subscription" }));
    const dialog = within(await screen.findByRole("dialog"));
    expect(dialog.getByText(/becomes read-only/)).toBeTruthy();
    fireEvent.click(dialog.getByRole("button", { name: "Cancel subscription" }));
    expect(await dialog.findByText(/Give a reason/)).toBeTruthy();
    fireEvent.change(dialog.getByLabelText("Reason"), { target: { value: "Fees not paid" } });
    fireEvent.click(dialog.getByRole("button", { name: "Cancel subscription" }));

    await waitFor(() => expect(screen.queryByRole("dialog")).toBeNull());
    expect(requests.find((request) => request.path === `${base}/subscription/cancel`)!.body).toEqual({
      reason: "Fees not paid",
    });
    expect((await screen.findAllByText(/Cancelled 29 Oct 2026: Fees not paid/)).length).toBeGreaterThan(0);
    expect(await commandsOffered()).toEqual(["Assign a period", "Renew"]);
  });
});

describe("overrides", () => {
  it("sets an unlimited quota with a reason and a last day", async () => {
    const requests = open({ [`POST ${base}/entitlement-overrides`]: { status: 201, body: subscription() } });

    fireEvent.click(await (await panel()).findByRole("button", { name: "Set an override" }));
    const dialog = within(await screen.findByRole("dialog"));
    fireEvent.change(dialog.getByLabelText("Entitlement"), { target: { value: "MAX_ENABLED_SPORTS" } });
    expect(dialog.getByText(/Now: 3 sports\./)).toBeTruthy();
    fireEvent.click(dialog.getByRole("button", { name: "Set override" }));
    expect(await dialog.findByText("Enter a whole number, or choose Unlimited.")).toBeTruthy();
    expect(await dialog.findByText(/Give a reason/)).toBeTruthy();

    fireEvent.click(dialog.getByRole("checkbox", { name: "Unlimited" }));
    expect((dialog.getByLabelText("Limit (sports)") as HTMLInputElement).disabled).toBe(true);
    fireEvent.change(dialog.getByLabelText("Last day (optional)"), { target: { value: "2026-12-15" } });
    fireEvent.change(dialog.getByLabelText("Reason"), { target: { value: "Schools festival needs every sport" } });
    fireEvent.click(dialog.getByRole("button", { name: "Set override" }));

    await waitFor(() => expect(screen.queryByRole("dialog")).toBeNull());
    expect(requests.find((request) => request.path === `${base}/entitlement-overrides`)!.body).toEqual({
      entitlementCode: "MAX_ENABLED_SPORTS",
      value: { limit: null },
      reason: "Schools festival needs every sport",
      effectiveUntil: "2026-12-15",
    });
    expect(screen.getAllByRole("status").map((status) => status.textContent)).toContain("Override set.");
  });

  it("takes a value by the entitlement's type", async () => {
    const requests = open({ [`POST ${base}/entitlement-overrides`]: { status: 201, body: subscription() } });

    fireEvent.click(await (await panel()).findByRole("button", { name: "Set an override" }));
    let dialog = within(await screen.findByRole("dialog"));
    // The first entitlement (by code) is a level.
    fireEvent.change(dialog.getByLabelText("Level"), { target: { value: "FULL" } });
    fireEvent.change(dialog.getByLabelText("Reason"), { target: { value: "Sponsor launch" } });
    fireEvent.click(dialog.getByRole("button", { name: "Set override" }));
    await waitFor(() => expect(screen.queryByRole("dialog")).toBeNull());

    fireEvent.click(await (await panel()).findByRole("button", { name: "Set an override" }));
    dialog = within(await screen.findByRole("dialog"));
    fireEvent.change(dialog.getByLabelText("Entitlement"), { target: { value: "CUSTOM_DOMAIN" } });
    fireEvent.change(dialog.getByLabelText("Value"), { target: { value: "true" } });
    fireEvent.change(dialog.getByLabelText("Reason"), { target: { value: "Pilot offer" } });
    fireEvent.click(dialog.getByRole("button", { name: "Set override" }));
    await waitFor(() => expect(screen.queryByRole("dialog")).toBeNull());

    const bodies = requests
      .filter((request) => request.path === `${base}/entitlement-overrides`)
      .map((request) => request.body as { entitlementCode: string; value: unknown });
    expect(bodies.map(({ entitlementCode, value }) => ({ entitlementCode, value }))).toEqual([
      { entitlementCode: "BRANDING", value: { level: "FULL" } },
      { entitlementCode: "CUSTOM_DOMAIN", value: { enabled: true } },
    ]);
  });

  it("puts a refused value under the value's field", async () => {
    open({
      [`POST ${base}/entitlement-overrides`]: {
        status: 400,
        body: problem("VALIDATION_INVALID_REQUEST", 400, {
          errors: [{ field: "value", code: "TENANT_OVERRIDE_INVALID_VALUE", message: "Not a value of the entitlement." }],
        }),
      },
    });

    fireEvent.click(await (await panel()).findByRole("button", { name: "Set an override" }));
    const dialog = within(await screen.findByRole("dialog"));
    fireEvent.change(dialog.getByLabelText("Entitlement"), { target: { value: "MAX_STAFF_USERS" } });
    fireEvent.change(dialog.getByLabelText("Limit (users)"), { target: { value: "6" } });
    fireEvent.change(dialog.getByLabelText("Reason"), { target: { value: "One more seat" } });
    fireEvent.click(dialog.getByRole("button", { name: "Set override" }));

    expect(await dialog.findByText(errorMessage("TENANT_OVERRIDE_INVALID_VALUE")!)).toBeTruthy();
    expect(dialog.getByLabelText("Limit (users)").getAttribute("aria-invalid")).toBe("true");
  });

  it("removes an override with a reason", async () => {
    const removed = subscription({
      overrides: [{ ...staffOverride, inEffect: false, removedAt: "2026-10-29T08:00:00Z", removedBy: staffOverride.grantedBy, removeReason: "Demo finished" }],
    });
    const requests = open({ [`POST ${base}/entitlement-overrides/${staffOverride.id}/remove`]: { status: 200, body: removed } });

    fireEvent.click(await (await panel()).findByRole("button", { name: "Remove the override of Staff users" }));
    const dialog = within(await screen.findByRole("dialog"));
    expect(dialog.getByRole("heading", { name: "Remove the override of Staff users?" })).toBeTruthy();
    fireEvent.change(dialog.getByLabelText("Reason"), { target: { value: "Demo finished" } });
    fireEvent.click(dialog.getByRole("button", { name: "Remove override" }));

    await waitFor(() => expect(screen.queryByRole("dialog")).toBeNull());
    expect(requests.find((request) => request.path.endsWith("/remove"))!.body).toEqual({ reason: "Demo finished" });
    expect(await screen.findByText("No overrides.")).toBeTruthy();
    expect(screen.getByText(/Demo finished/)).toBeTruthy();
  });
});

describe("an override removed meanwhile", () => {
  it("can't be removed again once the subscription shows it removed", async () => {
    const removed = subscription({
      overrides: [{ ...staffOverride, inEffect: false, removedAt: "2026-10-29T08:00:00Z", removedBy: staffOverride.grantedBy, removeReason: "Demo finished" }],
    });
    let reads = 0;
    open({
      [`GET ${base}/subscription`]: () => {
        reads += 1;
        return { status: 200, body: reads === 1 ? subscription() : removed };
      },
      [`POST ${base}/entitlement-overrides/${staffOverride.id}/remove`]: {
        status: 404,
        body: problem("TENANT_OVERRIDE_NOT_FOUND", 404),
      },
    });

    fireEvent.click(await (await panel()).findByRole("button", { name: "Remove the override of Staff users" }));
    const dialog = within(await screen.findByRole("dialog"));
    fireEvent.change(dialog.getByLabelText("Reason"), { target: { value: "Demo finished" } });
    fireEvent.click(confirm(dialog, "Remove override"));

    expect(await dialog.findByText(errorMessage("TENANT_OVERRIDE_NOT_FOUND")!)).toBeTruthy();
    await waitFor(() => expect(confirm(dialog, "Remove override").disabled).toBe(true));
  });
});

describe("activation", () => {
  it("warns that a tenant without a period will be read-only", async () => {
    open({ [`GET ${base}/subscription`]: { status: 200, body: noSubscription } }, adminActions, {
      ...kmsa,
      status: "PROVISIONING",
    });

    await (await panel()).findByText("No subscription");
    fireEvent.click(await screen.findByRole("button", { name: "Activate" }));
    const dialog = within(await screen.findByRole("dialog"));
    expect(dialog.getByText(/No subscription period is in effect today: it will be read-only until one starts or/)).toBeTruthy();
  });

  it("warns when the tenant's only period hasn't started yet", async () => {
    const scheduled = { ...pilotPeriod, status: "SCHEDULED" as const, startsOn: "2026-11-01" };
    open(
      { [`GET ${base}/subscription`]: { status: 200, body: subscription({ periods: [scheduled], nextPeriodId: scheduled.id }) } },
      adminActions,
      { ...kmsa, status: "PROVISIONING" },
    );

    expect(await (await panel()).findByText(/No period is in effect today: once activated/)).toBeTruthy();
    fireEvent.click(await screen.findByRole("button", { name: "Activate" }));
    const dialog = within(await screen.findByRole("dialog"));
    expect(dialog.getByText(/No subscription period is in effect today/)).toBeTruthy();
  });

  it("says when the subscription couldn't be checked", async () => {
    open({ [`GET ${base}/subscription`]: { status: 500, body: problem("PLATFORM_UNEXPECTED_ERROR", 500) } }, adminActions, {
      ...kmsa,
      status: "PROVISIONING",
    });

    await (await panel()).findByRole("alert");
    fireEvent.click(await screen.findByRole("button", { name: "Activate" }));
    const dialog = within(await screen.findByRole("dialog"));
    expect(dialog.getByText(/Its subscription couldn't be checked/)).toBeTruthy();
  });

  it("doesn't warn when the tenant has a period", async () => {
    open({}, adminActions, { ...kmsa, status: "PROVISIONING" });

    await (await panel()).findByRole("heading", { name: "Current period" });
    fireEvent.click(await screen.findByRole("button", { name: "Activate" }));
    const dialog = within(await screen.findByRole("dialog"));
    expect(dialog.queryByText(/No subscription period is in effect today/)).toBeNull();
  });
});

describe("renewalStart", () => {
  const at = (periods: { endsOn: string; status: "ACTIVE" | "CANCELLED" | "EXPIRED" }[], today = "2026-10-29") =>
    renewalStart(subscription({ today, periods: periods.map((period) => ({ ...pilotPeriod, ...period })) }));

  it("is the day after the latest period that isn't cancelled ends", () => {
    expect(at([{ endsOn: "2027-03-31", status: "ACTIVE" }, { endsOn: "2027-12-31", status: "CANCELLED" }])).toBe("2027-04-01");
  });

  it("is today when that day has passed, or every period is cancelled", () => {
    expect(at([{ endsOn: "2026-09-30", status: "EXPIRED" }])).toBe("2026-10-29");
    expect(at([{ endsOn: "2027-03-31", status: "CANCELLED" }])).toBe("2026-10-29");
  });
});
