// Tenant workspace → Sports: the published variants, and a variant's definition in tabs (S05 D17), all read from the
// API and shown in the definition's own words; only for whoever holds `sport.read`. The definition is a test-only one
// (test/sport-definitions.ts), so every sport word on screen must have come from it.
import { act, cleanup, fireEvent, screen, waitFor, within } from "@testing-library/react";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { tenantCapabilities } from "@/test/capabilities";
import { fakeApi, problem, renderWithQuery, type Answer, type Answers } from "@/test/render";
import { ringball, ringballVariant } from "@/test/sport-definitions";
import TenantWorkspaceLayout from "../layout";
import SportVariantPage from "./[variant]/page";
import SportsPage from "./page";

const navigation = vi.hoisted(() => ({
  pathname: "/t/kmsa/sports",
  search: "",
  variant: undefined as string | undefined,
  replace: vi.fn(),
}));

vi.mock("next/navigation", () => ({
  useParams: () => ({ tenantSlug: "kmsa", variant: navigation.variant }),
  usePathname: () => navigation.pathname,
  useSearchParams: () => new URLSearchParams(navigation.search),
  useRouter: () => ({ replace: navigation.replace, push: vi.fn() }),
}));

const tenantId = "0199a3f0-0000-7000-8000-00000000000a";
const me: Answer = {
  status: 200,
  body: {
    id: "0199a3f0-0000-7000-8000-000000000001",
    email: "grace.namusoke@kmsa.example",
    platformRoles: [],
    tenants: [
      { tenantId, slug: "kmsa", displayName: "Kampala Metro Sports Association", status: "ACTIVE", roles: ["SCORER"] },
    ],
  },
};

const reads = ["sport.read", "tenant.settings.read"];
const settings: Answer = {
  status: 200,
  body: {
    timeZone: "Africa/Kampala",
    currency: "UGX",
    languageCode: "en",
    startingSoonMinutes: 30,
    requireGuardianConsentForMinors: true,
    publicEnabled: true,
    version: "1",
  },
};

// The last render's query client, for a test that waits on a query's state.
let queryClient: ReturnType<typeof renderWithQuery>["queryClient"];

function open(page: "list" | "variant", actions = reads, answers: Answers = {}) {
  const requests = fakeApi({
    "GET /api/account/v1/me": me,
    "GET /api/admin/v1/me/capabilities": { status: 200, body: tenantCapabilities(actions) },
    "GET /api/admin/v1/tenant/settings": settings,
    "GET /api/admin/v1/sports": { status: 200, body: { items: [ringballVariant] } },
    "GET /api/admin/v1/sports/RINGBALL_SEVENS": { status: 200, body: ringball },
    ...answers,
  });
  ({ queryClient } = renderWithQuery(
    <TenantWorkspaceLayout>{page === "list" ? <SportsPage /> : <SportVariantPage />}</TenantWorkspaceLayout>,
  ));
  return requests;
}

function openVariant(search = "", actions = reads, answers: Answers = {}) {
  navigation.pathname = "/t/kmsa/sports/RINGBALL_SEVENS";
  navigation.variant = "RINGBALL_SEVENS";
  navigation.search = search;
  return open("variant", actions, answers);
}

// A row header holds the row's name, then (for a phone) the values of its wide columns: find it by its name.
const rowOf = (name: string) =>
  screen.getAllByRole("rowheader").find((header) => (header.firstElementChild ?? header).textContent === name)!.closest("tr")!;
const cells = (name: string) =>
  within(rowOf(name))
    .getAllByRole("cell")
    .map((cell) => cell.textContent);

beforeEach(() => {
  navigation.pathname = "/t/kmsa/sports";
  navigation.search = "";
  navigation.variant = undefined;
  navigation.replace.mockReset();
});

afterEach(() => {
  cleanup();
  vi.unstubAllGlobals();
});

describe("sports list", () => {
  it("lists the published variants, each linking to its definition", async () => {
    open("list");

    const link = await screen.findByRole("link", { name: "Ringball sevens" });
    expect(link.getAttribute("href")).toBe("/t/kmsa/sports/RINGBALL_SEVENS");
    expect(cells("Ringball sevens")).toEqual(["Ringball", "Additive", "Head to head", "2"]);
    // The navigation's entry for the screen.
    expect(screen.getAllByRole("link", { name: "Sports" }).length).toBeGreaterThan(0);
  });

  it("says when nothing is published yet", async () => {
    open("list", reads, { "GET /api/admin/v1/sports": { status: 200, body: { items: [] } } });

    expect(await screen.findByText("No sports yet")).toBeTruthy();
    expect(screen.getByText("No sports are published yet. Ask the platform team.")).toBeTruthy();
  });

  it("is refused without sport.read, and doesn't ask the API", async () => {
    const requests = open("list", ["tenant.settings.read"]);

    expect(await screen.findByRole("heading", { name: "You can't open this page" })).toBeTruthy();
    expect(requests.some((request) => request.path.startsWith("/api/admin/v1/sports"))).toBe(false);
  });
});

describe("sport variant", () => {
  it("shows the header and the overview in the definition's words, published in the tenant's time zone", async () => {
    const requests = openVariant();

    expect(await screen.findByRole("heading", { level: 1, name: "Ringball sevens" })).toBeTruthy();
    // 21:30 UTC on 1 Nov is 2 Nov in Kampala.
    expect(screen.getByText(/Ringball · version 2 · published 2 Nov 2026/)).toBeTruthy();
    expect(screen.getByText(ringball.contentHash)).toBeTruthy();
    expect(screen.getByRole("tab", { name: "Overview", selected: true })).toBeTruthy();
    expect(screen.getByText("Bout (Bouts)")).toBeTruthy();
    expect(screen.getByText("Ringer")).toBeTruthy();
    expect(screen.getByText("Guard: 1, Ringers: at least 2")).toBeTruthy();
    expect(screen.getByText(/Defaults: a competition's rules can change the numbers/)).toBeTruthy();
    const header = screen.getByRole("heading", { level: 1, name: "Ringball sevens" }).closest("header")!;
    expect(within(header).getByRole("link", { name: "Sports" }).getAttribute("href")).toBe("/t/kmsa/sports");
    expect(requests.find((request) => request.path === "/api/admin/v1/sports/RINGBALL_SEVENS")?.search).toBe("");
  });

  it("waits for the tenant's settings, so the date is never shown in UTC first (review #93 Q1)", async () => {
    openVariant();
    // Hold the settings back until the definition has arrived.
    const answer = globalThis.fetch;
    let release = () => {};
    let settingsAsked = false;
    const settingsRead = new Promise<void>((resolve) => (release = resolve));
    vi.stubGlobal("fetch", async (request: Request) => {
      if (new URL(request.url).pathname === "/api/admin/v1/tenant/settings") {
        settingsAsked = true;
        await settingsRead;
      }
      return answer(request);
    });

    // The definition has arrived and the page has rendered with it (re-review N5), the settings still held back.
    await waitFor(() =>
      expect(queryClient.getQueryState(["sports", "definition", "RINGBALL_SEVENS", "latest"])?.status).toBe("success"),
    );
    await act(async () => {});
    expect(settingsAsked).toBe(true);
    expect(screen.queryByRole("heading", { level: 1, name: "Ringball sevens" })).toBeNull();
    expect(screen.queryByText(/\(UTC\)/)).toBeNull();
    expect(screen.getByRole("status").textContent).toContain("Loading");

    release();
    expect(await screen.findByRole("heading", { level: 1, name: "Ringball sevens" })).toBeTruthy();
    expect(screen.getByText(/published 2 Nov 2026$/)).toBeTruthy();
  });

  it("shows the publication date in UTC, and says so, when the tenant's settings can't be read (re-review N4)", async () => {
    openVariant("", reads, {
      "GET /api/admin/v1/tenant/settings": { status: 500, body: problem("PLATFORM_UNEXPECTED_ERROR", 500) },
    });

    expect(await screen.findByRole("heading", { level: 1, name: "Ringball sevens" })).toBeTruthy();
    expect(screen.getByText(/Ringball · version 2 · published 1 Nov 2026 \(UTC\)/)).toBeTruthy();
  });

  it("shows the publication date in UTC, and says so, to someone who can't read the tenant's settings", async () => {
    const requests = openVariant("", ["sport.read"]);

    expect(await screen.findByRole("heading", { level: 1, name: "Ringball sevens" })).toBeTruthy();
    // 21:30 UTC on 1 Nov: 2 Nov in Kampala, but the zone can't be read.
    expect(screen.getByText(/Ringball · version 2 · published 1 Nov 2026 \(UTC\)/)).toBeTruthy();
    expect(requests.some((request) => request.path === "/api/admin/v1/tenant/settings")).toBe(false);
  });

  it("lists the six tabs and puts the chosen one in the address", async () => {
    openVariant("version=2");

    await screen.findByRole("heading", { level: 1, name: "Ringball sevens" });
    expect(screen.getAllByRole("tab").map((tab) => tab.textContent)).toEqual([
      "Overview",
      "Periods",
      "Events",
      "Positions & roles",
      "Statistics",
      "Scorer pad",
    ]);

    fireEvent.mouseDown(screen.getByRole("tab", { name: "Statistics" }));
    expect(navigation.replace).toHaveBeenCalledWith(
      "/t/kmsa/sports/RINGBALL_SEVENS?version=2&tab=statistics",
      { scroll: false },
    );
  });

  it("opens the version the address names", async () => {
    const requests = openVariant("version=1");

    await screen.findByRole("heading", { level: 1, name: "Ringball sevens" });
    expect(requests.find((request) => request.path === "/api/admin/v1/sports/RINGBALL_SEVENS")?.search).toBe("?version=1");
  });

  it("shows the periods", async () => {
    openVariant("tab=periods");

    await screen.findByRole("tab", { name: "Periods", selected: true });
    expect(cells("First spell")).toEqual(["S1", "Play", "Counts up", "10 min", "—"]);
    expect(cells("Interval")).toEqual(["INT", "Break", "No clock", "1 min 30 s", "—"]);
    expect(cells("Second spell")).toEqual(["S2", "Play", "Counts up", "10 min", "Added time, Clock starts at 10 min"]);
    expect(cells("Golden spell")).toEqual(["GS", "Play", "Counts up", "5 min", "Overtime, Sudden death"]);
    expect(cells("Ring-off")).toEqual(["RO", "Shootout", "No clock", "—", "Overtime"]);
    // On a phone the row's name carries the wide columns' values (they are hidden there).
    expect(rowOf("Second spell").querySelector("th dl")?.textContent).toBe(
      "Short: S2Kind: PlayClock: Counts upNotes: Added time, Clock starts at 10 min",
    );
  });

  it("shows each event with who takes part, when, its points and follow-up, then the generic events", async () => {
    openVariant("tab=events");

    const ring = (await screen.findByRole("heading", { level: 3, name: "Ring" })).closest("[data-slot=card]")!;
    const facts = within(ring as HTMLElement);
    expect(facts.getByText("Thrower (required from standard coverage); Feeder (optional, not the thrower)")).toBeTruthy();
    expect(facts.getByText("2 to the event's side")).toBeTruthy();
    expect(facts.getByText("First spell, Second spell and Golden spell")).toBeTruthy();
    expect(facts.getByText("Reach: one of Near, Far (optional); Clean: yes or no (optional)")).toBeTruthy();
    expect(screen.getByText("Points are defaults: a competition's rules can change them.")).toBeTruthy();
    expect(facts.getByText("Ring voided")).toBeTruthy();

    const voided = within(screen.getByRole("heading", { level: 3, name: "Ring voided" }).closest("[data-slot=card]") as HTMLElement);
    expect(
      voided.getByText("Needs an earlier Ring or Long ring for the same side, in the same period, not already followed up, which it uses up"),
    ).toBeTruthy();
    const hit = within(screen.getByRole("heading", { level: 3, name: "Ring-off hit" }).closest("[data-slot=card]") as HTMLElement);
    expect(hit.getByText("1 to the event's side (shootout score)")).toBeTruthy();
    const miss = within(screen.getByRole("heading", { level: 3, name: "Ring-off miss" }).closest("[data-slot=card]") as HTMLElement);
    expect(miss.getByText("No points")).toBeTruthy();
    const warning = within(screen.getByRole("heading", { level: 3, name: "Warning card" }).closest("[data-slot=card]") as HTMLElement);
    expect(warning.getByText("Thrower (required)")).toBeTruthy();
    const swap = within(screen.getByRole("heading", { level: 3, name: "Ringer swap" }).closest("[data-slot=card]") as HTMLElement);
    expect(swap.getByText("Substitutions: max")).toBeTruthy();
    expect(swap.getByText("First spell, Interval, Second spell and Golden spell")).toBeTruthy();

    expect(cells("Period start")).toEqual(["Period"]);
  });

  it("shows positions, roles in events, staff and officials", async () => {
    openVariant("tab=positions");

    await screen.findByRole("tab", { name: "Positions & roles", selected: true });
    expect(screen.getByText("Wing ringer, Centre ringer")).toBeTruthy();
    expect(cells("Ringer on")).toEqual(["On the bench", "Own side"]);
    expect(screen.getByText("Head coach, Kit keeper")).toBeTruthy();
    expect(cells("Ring judge")).toEqual(["1", "2"]);
    expect(cells("Timekeeper")).toEqual(["0", "No limit"]);
    expect(rowOf("Timekeeper").querySelector("th dl")?.textContent).toBe("Most: No limit");
  });

  it("shows how each statistic is worked out", async () => {
    openVariant("tab=statistics");

    await screen.findByRole("tab", { name: "Statistics", selected: true });
    expect(cells("Rings")).toEqual(["Counts Ring or Long ring events by Thrower", "Player, Team", "Sum", "Higher", "Standard"]);
    expect(cells("Warnings")).toEqual(["Entered by hand", "Player", "Sum", "Lower", "Basic"]);
    expect(cells("Ring rate")).toEqual(["Rings divided by Far rings", "Team", "Ratio", "Higher", "Detailed"]);
  });

  it("previews the scorer pad from the definition", async () => {
    openVariant("tab=scorer-pad");

    await screen.findByRole("tab", { name: "Scorer pad", selected: true });
    expect(screen.getByRole("region", { name: "Ring-off" })).toBeTruthy();
    expect(screen.getByText("Long ring", { selector: "li" })).toBeTruthy();
  });

  it("opens the overview for an unknown tab", async () => {
    openVariant("tab=history");

    expect(await screen.findByRole("tab", { name: "Overview", selected: true })).toBeTruthy();
  });

  it("is not found for a version that isn't one, without asking the API", async () => {
    const requests = openVariant("version=latest");

    expect(await screen.findByRole("heading", { name: "Page not found" })).toBeTruthy();
    expect(requests.some((request) => request.path.startsWith("/api/admin/v1/sports/"))).toBe(false);
  });

  it("is not found for an unknown variant", async () => {
    openVariant("", reads, {
      "GET /api/admin/v1/sports/RINGBALL_SEVENS": { status: 404, body: problem("SPORT_DEFINITION_NOT_FOUND", 404) },
    });

    expect(await screen.findByRole("heading", { name: "Page not found" })).toBeTruthy();
  });

  it("shows any other failure with its reference", async () => {
    openVariant("", reads, {
      "GET /api/admin/v1/sports/RINGBALL_SEVENS": { status: 500, body: problem("PLATFORM_UNEXPECTED_ERROR", 500) },
    });

    expect(await screen.findByText("Reference: kmsa-admin.test")).toBeTruthy();
    // The way back stays while the page can't show the definition.
    expect(within(screen.getByRole("main")).getByRole("link", { name: "Sports" }).getAttribute("href")).toBe("/t/kmsa/sports");
  });

  it("is refused without sport.read, and doesn't ask the API", async () => {
    const requests = openVariant("", ["tenant.settings.read"]);

    expect(await screen.findByRole("heading", { name: "You can't open this page" })).toBeTruthy();
    expect(requests.some((request) => request.path.startsWith("/api/admin/v1/sports"))).toBe(false);
  });
});
