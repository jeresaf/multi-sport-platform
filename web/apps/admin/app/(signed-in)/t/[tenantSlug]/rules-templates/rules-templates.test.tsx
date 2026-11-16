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
    expect(cells("Coverage of a bout")).toEqual(["One of a list", "Detailed", "One of: Standard, Detailed"]);
    expect(cells("First spell (seconds)")).toEqual(["Whole number", "600", "300 to 900"]);
    expect(cells("Head-to-head again among crews still level")).toEqual(["Yes or no", "No", "Either"]);
  });

  it("shows every rule in the pinned definition's words, marking those a competition may change", async () => {
    openTemplate();

    await screen.findByRole("heading", { level: 1, name: "Ring league" });
    const changeable = " (default; a competition may change it)";
    expect(items("Period lengths")).toEqual([`First spell: 10 min${changeable}`, "Interval: 1 min 30 s", "Second spell: 10 min 30 s"]);
    const contest = section("Contest");
    expect(fact("When the scores are level", contest)).toBe("Extra periods sudden death");
    expect(fact("Extra periods", contest)).toBe("Golden spell, then Ring-off");
    expect(fact("Default coverage", contest)).toBe(`Detailed${changeable}`);

    const lineup = section("Lineup");
    expect(fact("Starters", lineup)).toBe("7");
    expect(fact("Substitutes on the bench", lineup)).toBe("At most 5");
    expect(fact("Lineups need approval", lineup)).toBe("Yes");
    expect(items("Positions")).toEqual(["Hoop guard: Only 1", "Any of: Wing ringer, Centre ringer: 2 to 6"]);

    const substitutions = section("Substitutions");
    expect(fact("Substitutions", substitutions)).toBe("At most 6");
    expect(fact("Substitution windows", substitutions)).toBe("4");
    expect(fact("Extra in extra periods", substitutions)).toBe("1");

    const roster = section("Roster");
    expect(fact("Roster size", roster)).toBe("10 to 14");
    expect(fact("Rosters need approval", roster)).toBe("No");
    expect(fact("Roster locks", roster)).toBe("First contest");

    const standings = section("Standings");
    expect(fact("Points for a win", standings)).toBe(`3${changeable}`);
    expect(fact("Points for no result", standings)).toBe("1");
    expect(fact("Points for a forfeit win", standings)).toBe("2");
    expect(fact("Points for a forfeit loss", standings)).toBe("0");
    expect(fact("Score of a forfeit", standings)).toBe("5–0");
    expect(items("Tiebreakers in order")).toEqual(["Points", "Ring difference", "Toss of a coin"]);
    expect(fact("Tiebreakers", standings)).toMatch(/Toss of a coin \(default; a competition may change it\)$/);
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

  // Every rule a template may make a parameter, by its path as the files write it, is marked where it is shown; one
  // template per path, so a misspelt path leaves its rule unmarked (review #91 R3). The seeded templates' paths first.
  // Each parameter is one the backend's rules accept on `ringLeague` (review #91 N8): a whole number or a yes or no as
  // the value at its path is, that value as its default and inside its bounds; a choice only where a tenant may choose,
  // among codes the sport supports and the extra periods fit.
  const base = { label: "A changeable rule", description: null, min: null, max: null, values: [] } as const;
  const int = (value: number) => ({ ...base, type: "NUMBER", min: 0, max: 3600, default: value }) as const;
  const flag = (value: boolean) => ({ ...base, type: "FLAG", default: value }) as const;
  const choice = (values: string[], value: string) => ({ ...base, type: "CHOICE", values, default: value }) as const;
  it.each([
    ["roster.min", int(10), "Roster size"],
    ["roster.max", int(14), "Roster size"],
    ["lineup.bench_max", int(5), "Substitutes on the bench"],
    ["substitutions.max", int(6), "Substitutions"],
    ["contest.period_seconds.SECOND_SPELL", int(630), "Second spell", "Period lengths"],
    // The only tie resolution the template's extra periods (a golden spell, then a ring-off) fit is its own.
    ["contest.tie_resolution", choice(["EXTRA_PERIODS_SUDDEN_DEATH"], "EXTRA_PERIODS_SUDDEN_DEATH"), "When the scores are level"],
    ["contest.coverage_default", choice(["BASIC", "STANDARD", "DETAILED"], "DETAILED"), "Default coverage"],
    ["standings.points.win", int(3), "Points for a win"],
    ["standings.points.draw", int(1), "Points for a draw"],
    ["standings.points.loss", int(0), "Points for a loss"],
    ["standings.points.no_result", int(1), "Points for no result"],
    ["standings.points.forfeit_win", int(2), "Points for a forfeit win"],
    ["standings.points.forfeit_loss", int(0), "Points for a forfeit loss"],
    ["standings.forfeit_score.for", int(5), "Score of a forfeit"],
    ["standings.forfeit_score.against", int(0), "Score of a forfeit"],
    ["standings.h2h_reapply", flag(false), "Head-to-head applied again to those still level"],
    ["lineup.starters", int(7), "Starters"],
    ["lineup.requires_approval", flag(true), "Lineups need approval"],
    ["lineup.positions.HOOP_GUARD.min", int(1), "Hoop guard", "Positions"],
    ["lineup.positions.HOOP_GUARD.max", int(1), "Hoop guard", "Positions"],
    ["substitutions.windows", int(4), "Substitution windows"],
    ["substitutions.extra_in_extra_time", int(1), "Extra in extra periods"],
    ["roster.requires_approval", flag(false), "Rosters need approval"],
    ["operations.officials_minimum.RING_JUDGE", int(2), "Ring judge", "Officials at least"],
    ["operations.scorer_correction_window_minutes", int(45), "Scorer corrections"],
    ["operations.starting_soon_minutes", int(20), "Starting soon"],
    ["eligibility.membership_required", flag(false), "Membership required"],
    ["eligibility.min_age", int(14), "Age"],
    ["eligibility.max_age", int(17), "Age"],
  ] as const)("marks %s as a default a competition may change", async (path, parameter, label, list?: string) => {
    openTemplate("RING_LEAGUE", "", reads, {
      "GET /api/admin/v1/ruleset-templates/RING_LEAGUE": { status: 200, body: { ...ringLeague, parameters: { [path]: parameter } } },
    });

    await screen.findByRole("heading", { level: 1, name: "Ring league" });
    const marked = screen.getAllByText("(default; a competition may change it)");
    expect(marked).toHaveLength(1);
    const where = list ? items(list).find((item) => item.startsWith(`${label}:`)) : fact(label, section("Rules"));
    expect(where).toMatch(/\(default; a competition may change it\)$/);
    // The parameters table lists it with its type.
    expect(cells("A changeable rule")[0]).toBe({ NUMBER: "Whole number", FLAG: "Yes or no", CHOICE: "One of a list" }[parameter.type]);
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

  it("says None for a template that changes nothing and lists no positions or officials", async () => {
    openTemplate("QUICK_BOUT");

    await screen.findByRole("heading", { level: 1, name: "Quick bout" });
    expect(fact("Description")).toBe("None");
    expect(screen.queryByRole("table")).toBeNull();
    expect(fact("Extra periods")).toBe("None");
    expect(fact("Positions")).toBe("None");
    expect(fact("Ready to start when")).toBe("None");
    expect(fact("Officials at least")).toBe("None");
    expect(fact("Tiebreakers")).toBe("Points");
    expect(fact("Age")).toBe("No limit");
    expect(fact("Age worked out on")).toBe("Not set");
    expect(fact("Scorer corrections")).toBe("Not after the end");
    expect(screen.queryByText(/a competition may change it/)).toBeNull();
  });

  // Defensive: the template schema requires a tiebreaker, so no published template has none; the page still says so.
  it("says None for no tiebreakers", async () => {
    const none = { ...quickBout, values: { ...quickBout.values, standings: { ...quickBout.values.standings, tiebreakers: [] } }, tiebreakers: [] };
    openTemplate("QUICK_BOUT", "", reads, { "GET /api/admin/v1/ruleset-templates/QUICK_BOUT": { status: 200, body: none } });

    await screen.findByRole("heading", { level: 1, name: "Quick bout" });
    expect(fact("Tiebreakers", section("Rules"))).toBe("None");
    const catalogue = screen.getByRole("heading", { level: 2, name: "Tiebreakers" }).closest("section")!;
    expect(within(catalogue).getByText("None")).toBeTruthy();
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
