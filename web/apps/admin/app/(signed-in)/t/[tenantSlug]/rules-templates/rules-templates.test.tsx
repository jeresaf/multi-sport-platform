// Tenant workspace → Rules templates: the published ruleset templates, and a template's parameters, rules and
// tiebreakers (S05 D17), read from the API and shown in the template's and its pinned sport definition's own words;
// only for whoever holds `ruleset.read`. The templates, definition and formats are test-only (test/ruleset-templates.ts,
// test/sport-definitions.ts, test/formats.ts), so every word on screen must have come from them.
import { cleanup, screen, within } from "@testing-library/react";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { tenantCapabilities } from "@/test/capabilities";
import { ladder, showdown } from "@/test/formats";
import { fakeApi, problem, renderWithQuery, type Answer, type Answers } from "@/test/render";
import { quickBout, ringLeague } from "@/test/ruleset-templates";
import { ringball, ringballVariant } from "@/test/sport-definitions";
import TenantWorkspaceLayout from "../layout";
import RulesTemplatePage from "./[code]/page";
import RulesTemplatesPage from "./page";

const navigation = vi.hoisted(() => ({
  pathname: "/t/kmsa/rules-templates",
  search: "",
  code: undefined as string | undefined,
}));

vi.mock("next/navigation", () => ({
  useParams: () => ({ tenantSlug: "kmsa", code: navigation.code }),
  usePathname: () => navigation.pathname,
  useSearchParams: () => new URLSearchParams(navigation.search),
  useRouter: () => ({ replace: vi.fn(), push: vi.fn() }),
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

const reads = ["ruleset.read", "sport.read", "format.read", "tenant.settings.read"];
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

function open(page: "list" | "template", actions = reads, answers: Answers = {}) {
  const requests = fakeApi({
    "GET /api/account/v1/me": me,
    "GET /api/admin/v1/me/capabilities": { status: 200, body: tenantCapabilities(actions) },
    "GET /api/admin/v1/tenant/settings": settings,
    "GET /api/admin/v1/ruleset-templates": { status: 200, body: { items: [quickBout, ringLeague] } },
    "GET /api/admin/v1/ruleset-templates/RING_LEAGUE": { status: 200, body: ringLeague },
    "GET /api/admin/v1/ruleset-templates/QUICK_BOUT": { status: 200, body: quickBout },
    // Names unlike their codes' words, so a test can tell they came from these reads.
    "GET /api/admin/v1/sports": { status: 200, body: { items: [{ ...ringballVariant, name: "Ringball 7s" }] } },
    "GET /api/admin/v1/sports/RINGBALL_SEVENS": { status: 200, body: { ...ringball, name: "Ringball 7s" } },
    "GET /api/admin/v1/formats": { status: 200, body: { items: [{ ...ladder, name: "Climbing ladder" }, showdown] } },
    ...answers,
  });
  renderWithQuery(
    <TenantWorkspaceLayout>{page === "list" ? <RulesTemplatesPage /> : <RulesTemplatePage />}</TenantWorkspaceLayout>,
  );
  return requests;
}

function openTemplate(code = "RING_LEAGUE", search = "", actions = reads, answers: Answers = {}) {
  navigation.pathname = `/t/kmsa/rules-templates/${code}`;
  navigation.code = code;
  navigation.search = search;
  return open("template", actions, answers);
}

// A row header holds the row's name, then (for a phone) the values of its wide columns: find it by its name.
const rowOf = (name: string) =>
  screen.getAllByRole("rowheader").find((header) => (header.firstElementChild ?? header).textContent === name)!.closest("tr")!;
const cells = (name: string) =>
  within(rowOf(name))
    .getAllByRole("cell")
    .map((cell) => cell.textContent);
// A fact's value, by its label (a description list's term).
const fact = (label: string, scope: HTMLElement = document.body) =>
  within(scope)
    .getAllByText(label, { selector: "dt" })
    .map((term) => term.nextElementSibling?.textContent)[0];
const section = (title: string) => screen.getByRole("heading", { name: title }).closest("section")!;
const items = (name: string) =>
  within(screen.getByRole("list", { name }))
    .getAllByRole("listitem")
    .map((item) => item.textContent);

beforeEach(() => {
  navigation.pathname = "/t/kmsa/rules-templates";
  navigation.search = "";
  navigation.code = undefined;
});

afterEach(() => {
  cleanup();
  vi.unstubAllGlobals();
});

describe("rules templates list", () => {
  it("lists the published templates with their sport and formats, each linking to its page", async () => {
    open("list");

    const link = await screen.findByRole("link", { name: "Ring league" });
    expect(link.getAttribute("href")).toBe("/t/kmsa/rules-templates/RING_LEAGUE");
    expect((await screen.findAllByText("Climbing ladder, Showdown")).length).toBeGreaterThan(0);
    expect(cells("Ring league")).toEqual(["Ringball 7s", "Climbing ladder, Showdown", "2"]);
    expect(within(rowOf("Ring league")).getByText(ringLeague.description!)).toBeTruthy();
    expect(cells("Quick bout")).toEqual(["Ringball 7s", "Showdown", "1"]);
    const entries = screen.getAllByRole("link", { name: "Rules templates" });
    expect(entries.length).toBeGreaterThan(0);
    expect(entries.every((entry) => entry.getAttribute("aria-current") === "page")).toBe(true);
  });

  it("writes the sport and formats as words from their codes without sport.read and format.read", async () => {
    const requests = open("list", ["ruleset.read", "tenant.settings.read"]);

    await screen.findByRole("link", { name: "Ring league" });
    expect(cells("Ring league")).toEqual(["Ringball sevens", "Ladder climb, Showdown", "2"]);
    expect(requests.some((request) => request.path.startsWith("/api/admin/v1/sports"))).toBe(false);
    expect(requests.some((request) => request.path.startsWith("/api/admin/v1/formats"))).toBe(false);
  });

  it("says when nothing is published yet", async () => {
    open("list", reads, { "GET /api/admin/v1/ruleset-templates": { status: 200, body: { items: [] } } });

    expect(await screen.findByText("No rules templates yet")).toBeTruthy();
    expect(screen.getByText("No rules templates are published yet. Ask the platform team.")).toBeTruthy();
  });

  it("shows a failure with its reference", async () => {
    open("list", reads, {
      "GET /api/admin/v1/ruleset-templates": { status: 500, body: problem("PLATFORM_UNEXPECTED_ERROR", 500) },
    });

    expect(await screen.findByText("Reference: kmsa-admin.test")).toBeTruthy();
  });

  it("is refused without ruleset.read, and doesn't ask the API", async () => {
    const requests = open("list", ["sport.read", "format.read", "tenant.settings.read"]);

    expect(await screen.findByRole("heading", { name: "You can't open this page" })).toBeTruthy();
    expect(requests.some((request) => request.path.startsWith("/api/admin/v1/ruleset-templates"))).toBe(false);
  });
});

describe("a rules template", () => {
  it("shows the header and the overview, linking to the pinned sport version and the formats", async () => {
    const requests = openTemplate();

    expect(await screen.findByRole("heading", { level: 1, name: "Ring league" })).toBeTruthy();
    expect(screen.getByText("Version 2 · published 2 Nov 2026")).toBeTruthy();
    expect(screen.getAllByRole("heading", { level: 2 }).map((heading) => heading.textContent)).toEqual([
      "Overview",
      "Parameters",
      "Rules",
      "Tiebreakers",
    ]);
    expect(screen.getByText(ringLeague.contentHash)).toBeTruthy();
    expect(screen.getByText(ringLeague.description!)).toBeTruthy();
    const sport = screen.getByRole("link", { name: "Ringball 7s, version 2" });
    expect(sport.getAttribute("href")).toBe("/t/kmsa/sports/RINGBALL_SEVENS?version=2");
    const formats = within(screen.getByRole("list", { name: "Formats" })).getAllByRole("link");
    expect(formats.map((format) => [format.textContent, format.getAttribute("href")])).toEqual([
      ["Climbing ladder", "/t/kmsa/formats/LADDER_CLIMB"],
      ["Showdown", "/t/kmsa/formats/SHOWDOWN"],
    ]);
    const header = screen.getByRole("heading", { level: 1, name: "Ring league" }).closest("header")!;
    expect(within(header).getByRole("link", { name: "Rules templates" }).getAttribute("href")).toBe("/t/kmsa/rules-templates");
    expect(requests.find((request) => request.path === "/api/admin/v1/ruleset-templates/RING_LEAGUE")?.search).toBe("");
    expect(requests.find((request) => request.path === "/api/admin/v1/sports/RINGBALL_SEVENS")?.search).toBe("?version=2");
  });

  it("shows each parameter, labelling tiebreakers from the catalogue", async () => {
    openTemplate();

    await screen.findByRole("heading", { level: 1, name: "Ring league" });
    expect(cells("Points for a bout won")).toEqual(["Whole number", "3", "2 to 4"]);
    expect(cells("Tiebreakers")).toEqual([
      "Ordered list",
      "Points, then Ring difference, then Toss of a coin",
      "Any of, each once, in the order chosen: Points, Ring difference, Far rings, Toss of a coin",
    ]);
    expect(within(rowOf("Tiebreakers")).getByText("Applied in this order when crews are level on points.")).toBeTruthy();
    expect(cells("When a bout is level")).toEqual([
      "One of a list",
      "Extra periods sudden death",
      "One of: Extra periods sudden death, Shootout",
    ]);
    expect(cells("First spell (seconds)")).toEqual(["Whole number", "600", "300 to 900"]);
    expect(cells("Head-to-head again among crews still level")).toEqual(["Yes or no", "No", "Either"]);
  });

  it("shows every rule in the pinned definition's words, marking those a competition may change", async () => {
    openTemplate();

    await screen.findByRole("heading", { level: 1, name: "Ring league" });
    const changeable = " (default; a competition may change it)";
    expect(items("Period lengths")).toEqual([`First spell: 10 min${changeable}`, "Interval: 1 min 30 s", "Second spell: 10 min 30 s"]);
    const contest = section("Contest");
    expect(fact("When the scores are level", contest)).toBe(`Extra periods sudden death${changeable}`);
    expect(fact("Extra periods", contest)).toBe("Golden spell, then Ring-off");
    expect(fact("Default coverage", contest)).toBe("Detailed");

    const lineup = section("Lineup");
    expect(fact("Starters", lineup)).toBe("7");
    expect(fact("Substitutes on the bench", lineup)).toBe("At most 5");
    expect(fact("Lineups need approval", lineup)).toBe("Yes");
    expect(items("Positions")).toEqual(["Hoop guard: Only 1", "Any of: Wing ringer, Centre ringer: 2 to 6"]);

    const substitutions = section("Substitutions");
    expect(fact("Substitutions", substitutions)).toBe("At most 6");
    expect(fact("Substitution windows", substitutions)).toBe("4");
    expect(fact("Extra in extra time", substitutions)).toBe("1");

    const roster = section("Roster");
    expect(fact("Roster size", roster)).toBe("10 to 14");
    expect(fact("Rosters need approval", roster)).toBe("No");
    expect(fact("Roster locks", roster)).toBe("First contest");

    const standings = section("Standings");
    expect(fact("Points for a win", standings)).toBe(`3${changeable}`);
    expect(fact("Points for no result", standings)).toBe("1");
    expect(fact("Points for a forfeit loss", standings)).toBe("-1");
    expect(fact("Score of a forfeit", standings)).toBe("5–0");
    expect(items("Tiebreakers in order")).toEqual(["Points", "Ring difference", "Toss of a coin"]);
    expect(within(standings).getByText("Default; a competition may change it.")).toBeTruthy();
    expect(fact("Head-to-head applied again to those still level", standings)).toBe(`No${changeable}`);

    expect(items("Readiness checks")).toEqual(["Lineups approved", "Officials minimum"]);
    expect(items("Officials at least")).toEqual(["Ring judge: 2"]);
    const operations = section("Contest operations");
    expect(fact("Scorer corrections", operations)).toBe("Up to 45 min after the end");
    expect(fact("Starting soon", operations)).toBe("20 min before the start");

    const eligibility = section("Eligibility");
    expect(fact("Membership required", eligibility)).toBe("No");
    expect(fact("Age", eligibility)).toBe("14 to 17");
    expect(fact("Age worked out on", eligibility)).toBe("1 Jan 2027");
    expect(fact("Consent for minors", eligibility)).toBe("As the organisation's settings say");
  });

  it("describes every tiebreaker the template lists or allows", async () => {
    openTemplate();

    await screen.findByRole("heading", { level: 1, name: "Ring league" });
    const tiebreakers = screen.getByRole("heading", { level: 2, name: "Tiebreakers" }).closest("section")!;
    expect(within(tiebreakers).getAllByRole("term").map((term) => term.textContent)).toEqual([
      "Points",
      "Ring difference",
      "Far rings",
      "Toss of a coin",
    ]);
    expect(fact("Far rings", tiebreakers)).toBe("More rings from beyond the far line.");
  });

  it("says None for a template that changes nothing and lists no positions, officials or tiebreakers", async () => {
    openTemplate("QUICK_BOUT");

    await screen.findByRole("heading", { level: 1, name: "Quick bout" });
    expect(fact("Description")).toBe("None");
    expect(screen.queryByRole("table")).toBeNull();
    expect(fact("Extra periods")).toBe("None");
    expect(fact("Positions")).toBe("None");
    expect(fact("Ready to start when")).toBe("None");
    expect(fact("Officials at least")).toBe("None");
    expect(fact("Tiebreakers")).toBe("None");
    expect(fact("Age")).toBe("No limit");
    expect(fact("Age worked out on")).toBe("Not set");
    expect(fact("Scorer corrections")).toBe("Not after the end");
    expect(screen.queryByText(/a competition may change it/)).toBeNull();
  });

  it("writes the definition's codes as words, without links, when the sport and formats can't be read", async () => {
    const requests = openTemplate("RING_LEAGUE", "", ["ruleset.read", "tenant.settings.read"]);

    await screen.findByRole("heading", { level: 1, name: "Ring league" });
    expect(screen.queryByRole("link", { name: /Ringball/ })).toBeNull();
    expect(fact("Sport")).toBe("Ringball sevens, version 2");
    expect(items("Formats")).toEqual(["Ladder climb", "Showdown"]);
    expect(items("Period lengths")[0]).toMatch(/^First spell: 10 min/);
    expect(items("Positions")).toEqual(["Hoop guard: Only 1", "Ringers: 2 to 6"]);
    expect(items("Officials at least")).toEqual(["Ring judge: 2"]);
    expect(requests.some((request) => request.path.startsWith("/api/admin/v1/sports"))).toBe(false);
  });

  it("still shows the template when its sport definition can't be read", async () => {
    openTemplate("RING_LEAGUE", "", reads, {
      "GET /api/admin/v1/sports/RINGBALL_SEVENS": { status: 500, body: problem("PLATFORM_UNEXPECTED_ERROR", 500) },
    });

    expect(await screen.findByRole("heading", { level: 1, name: "Ring league" })).toBeTruthy();
    expect(items("Positions")).toEqual(["Hoop guard: Only 1", "Ringers: 2 to 6"]);
  });

  it("opens the version the address names", async () => {
    const requests = openTemplate("RING_LEAGUE", "version=1", reads, {
      "GET /api/admin/v1/ruleset-templates/RING_LEAGUE": { status: 200, body: { ...ringLeague, version: 1 } },
    });

    await screen.findByRole("heading", { level: 1, name: "Ring league" });
    expect(requests.find((request) => request.path === "/api/admin/v1/ruleset-templates/RING_LEAGUE")?.search).toBe("?version=1");
    expect(screen.getByText("Version 1 · published 2 Nov 2026")).toBeTruthy();
  });

  it("is not found for a version that isn't one, without asking the API", async () => {
    const requests = openTemplate("RING_LEAGUE", "version=two");

    expect(await screen.findByRole("heading", { name: "Page not found" })).toBeTruthy();
    expect(requests.some((request) => request.path.startsWith("/api/admin/v1/ruleset-templates/"))).toBe(false);
  });

  it("is not found for an unknown template", async () => {
    openTemplate("RING_LEAGUE", "", reads, {
      "GET /api/admin/v1/ruleset-templates/RING_LEAGUE": { status: 404, body: problem("RULESET_TEMPLATE_NOT_FOUND", 404) },
    });

    expect(await screen.findByRole("heading", { name: "Page not found" })).toBeTruthy();
  });

  it("shows any other failure with its reference, keeping the way back", async () => {
    openTemplate("RING_LEAGUE", "", reads, {
      "GET /api/admin/v1/ruleset-templates/RING_LEAGUE": { status: 500, body: problem("PLATFORM_UNEXPECTED_ERROR", 500) },
    });

    expect(await screen.findByText("Reference: kmsa-admin.test")).toBeTruthy();
    expect(within(screen.getByRole("main")).getByRole("link", { name: "Rules templates" }).getAttribute("href")).toBe(
      "/t/kmsa/rules-templates",
    );
  });

  it("is refused without ruleset.read, and doesn't ask the API", async () => {
    const requests = openTemplate("RING_LEAGUE", "", ["sport.read", "tenant.settings.read"]);

    expect(await screen.findByRole("heading", { name: "You can't open this page" })).toBeTruthy();
    expect(requests.some((request) => request.path.startsWith("/api/admin/v1/ruleset-templates"))).toBe(false);
  });
});
