// Platform console → Plans: every entitlement against every plan, its value shown by type, and a value the seed hasn't
// written shown as such, never as unlimited or none.
import { act, cleanup, screen, within } from "@testing-library/react";
import { afterEach, describe, expect, it, vi } from "vitest";
import { fakeApi, renderWithQuery, type Answers } from "@/test/render";
import { plansAnswer, pro, standard } from "@/test/subscriptions";
import PlatformLayout from "../layout";
import PlansPage from "./page";

vi.mock("next/navigation", () => ({
  useParams: () => ({}),
  usePathname: () => "/platform/plans",
  useRouter: () => ({ replace: vi.fn(), push: vi.fn() }),
}));

function open(actions: string[], answers: Answers = {}) {
  const requests = fakeApi({
    "GET /api/account/v1/me": {
      status: 200,
      body: { id: "0199a3f0-0000-7000-8000-000000000001", email: "support@sportsplatform.localhost", platformRoles: ["PLATFORM_SUPPORT"], tenants: [] },
    },
    "GET /api/platform/v1/me/capabilities": { status: 200, body: { actions, scope: null } },
    "GET /api/platform/v1/plans": plansAnswer,
    ...answers,
  });
  renderWithQuery(
    <PlatformLayout>
      <PlansPage />
    </PlatformLayout>,
  );
  return requests;
}

const cells = (entitlement: string) =>
  within(screen.getByRole("rowheader", { name: new RegExp(`^${entitlement}`) }).closest("tr")!)
    .getAllByRole("cell")
    .map((cell) => cell.textContent);

afterEach(() => {
  cleanup();
  vi.unstubAllGlobals();
});

describe("plans", () => {
  it("shows each entitlement's value on each plan, by its type", async () => {
    open(["plan.read"]);

    await screen.findByRole("columnheader", { name: "Pro" });
    expect(screen.getAllByRole("columnheader").map((header) => header.textContent)).toEqual([
      "Entitlement",
      "Standard",
      "Pro",
    ]);
    expect(cells("Branding")).toEqual(["Basic", "Full"]);
    expect(cells("Custom domain")).toEqual(["Not included", "Included"]);
    expect(cells("Enabled sports")).toEqual(["3 sports", "Unlimited"]);
    expect(cells("Staff users")).toEqual(["100 users", "1,000 users"]);
    expect(screen.getByText("Enforced by Identity & Access")).toBeTruthy();
  });

  it("says a value the configuration seed hasn't written is not seeded", async () => {
    const unseeded = {
      ...pro,
      entitlements: pro.entitlements.map((item) => (item.code === "MAX_STAFF_USERS" ? { ...item, value: null } : item)),
    };
    open(["plan.read"], { "GET /api/platform/v1/plans": { status: 200, body: { items: [standard, unseeded] } } });

    await screen.findByRole("columnheader", { name: "Pro" });
    expect(cells("Staff users")).toEqual(["100 users", "Not seeded"]);
  });

  it("says so when there are no plans", async () => {
    open(["plan.read"], { "GET /api/platform/v1/plans": { status: 200, body: { items: [] } } });

    expect(await screen.findByText("No plans yet")).toBeTruthy();
  });

  it("doesn't ask for plans without plan.read", async () => {
    const requests = open(["tenant.read"]);

    expect(await screen.findByRole("heading", { name: "You can't open this page" })).toBeTruthy();
    await act(async () => {});
    expect(requests.some((request) => request.path === "/api/platform/v1/plans")).toBe(false);
  });
});
