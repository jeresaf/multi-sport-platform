// Platform console → Sports, Formats and Rules templates (S05 D17): the workspace's tables and viewers over the platform
// reads (`platform.sport.read`), linking within the console, with dates in UTC (the console has no time zone). The
// definitions, formats and templates are test-only (test/sport-definitions.ts, test/formats.ts,
// test/ruleset-templates.ts). The views themselves are covered by the workspace's tests.
import { act, cleanup, fireEvent, screen, within } from "@testing-library/react";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { ladder, showdown } from "@/test/formats";
import { fakeApi, problem, renderWithQuery, type Answers } from "@/test/render";
import { quickBout, ringLeague } from "@/test/ruleset-templates";
import { ringball, ringballVariant } from "@/test/sport-definitions";
import FormatPage from "./formats/[code]/page";
import FormatsPage from "./formats/page";
import PlatformLayout from "./layout";
import RulesTemplatePage from "./rules-templates/[code]/page";
import RulesTemplatesPage from "./rules-templates/page";
import SportVariantPage from "./sports/[variant]/page";
import SportsPage from "./sports/page";

const navigation = vi.hoisted(() => ({
  pathname: "/platform/sports",
  search: "",
  params: {} as Record<string, string>,
  replace: (() => {}) as (href: string, options?: unknown) => void,
}));

vi.mock("next/navigation", () => ({
  useParams: () => navigation.params,
  usePathname: () => navigation.pathname,
  useSearchParams: () => new URLSearchParams(navigation.search),
  useRouter: () => ({ replace: navigation.replace, push: vi.fn() }),
}));

const pages = {
  sports: SportsPage,
  variant: SportVariantPage,
  formats: FormatsPage,
  format: FormatPage,
  templates: RulesTemplatesPage,
  template: RulesTemplatePage,
};

function open(
  page: keyof typeof pages,
  { path = "/platform/sports", params = {}, search = "", actions = ["platform.sport.read"], answers = {} }: {
    path?: string;
    params?: Record<string, string>;
    search?: string;
    actions?: string[];
    answers?: Answers;
  } = {},
) {
  navigation.pathname = path;
  navigation.params = params;
  navigation.search = search;
  const requests = fakeApi({
    "GET /api/account/v1/me": {
      status: 200,
      body: { id: "0199a3f0-0000-7000-8000-000000000001", email: "support@sportsplatform.localhost", platformRoles: ["PLATFORM_SUPPORT"], tenants: [] },
    },
    "GET /api/platform/v1/me/capabilities": { status: 200, body: { actions, scope: null } },
    // Names unlike their codes' words, so a test can tell they came from these reads.
    "GET /api/platform/v1/sports": { status: 200, body: { items: [{ ...ringballVariant, name: "Ringball 7s" }] } },
    "GET /api/platform/v1/sports/RINGBALL_SEVENS": { status: 200, body: { ...ringball, name: "Ringball 7s" } },
    "GET /api/platform/v1/formats": { status: 200, body: { items: [{ ...ladder, name: "Climbing ladder" }, showdown] } },
    "GET /api/platform/v1/formats/LADDER_CLIMB": { status: 200, body: ladder },
    "GET /api/platform/v1/ruleset-templates": { status: 200, body: { items: [quickBout, ringLeague] } },
    "GET /api/platform/v1/ruleset-templates/RING_LEAGUE": { status: 200, body: ringLeague },
    ...answers,
  });
  const Page = pages[page];
  renderWithQuery(
    <PlatformLayout>
      <Page />
    </PlatformLayout>,
  );
  return requests;
}

// A row header holds the row's name, then (for a phone) the values of its wide columns: find it by its name.
const rowOf = (name: string) =>
  screen.getAllByRole("rowheader").find((header) => (header.firstElementChild ?? header).textContent === name)!.closest("tr")!;
const cells = (name: string) =>
  within(rowOf(name))
    .getAllByRole("cell")
    .map((cell) => cell.textContent);
const backLink = (title: string) =>
  within(screen.getByRole("heading", { level: 1, name: title }).closest("header")!).getAllByRole("link")[0]!;
const adminReads = (requests: { path: string }[]) => requests.filter((request) => request.path.startsWith("/api/admin/"));

beforeEach(() => {
  navigation.replace = vi.fn();
});

afterEach(() => {
  cleanup();
  vi.unstubAllGlobals();
});

describe("platform console sport configuration", () => {
  it("lists the sports from the platform read, each linking to its console page", async () => {
    const requests = open("sports");

    const link = await screen.findByRole("link", { name: "Ringball 7s" });
    expect(link.getAttribute("href")).toBe("/platform/sports/RINGBALL_SEVENS");
    expect(cells("Ringball 7s")).toEqual(["Ringball", "Additive", "Head to head", "2"]);
    const entries = screen.getAllByRole("link", { name: "Sports" });
    expect(entries.every((entry) => entry.getAttribute("aria-current") === "page")).toBe(true);
    expect(adminReads(requests)).toEqual([]);
  });

  it("shows a variant in the workspace's viewer, published in UTC, its tab in the address", async () => {
    const requests = open("variant", {
      path: "/platform/sports/RINGBALL_SEVENS",
      params: { variant: "RINGBALL_SEVENS" },
      search: "version=2",
    });

    expect(await screen.findByRole("heading", { level: 1, name: "Ringball 7s" })).toBeTruthy();
    // 21:30 UTC on 1 Nov: still 1 Nov, said to be UTC (a tenant in Kampala reads 2 Nov).
    expect(screen.getByText(/Ringball · version 2 · published 1 Nov 2026 \(UTC\)/)).toBeTruthy();
    expect(backLink("Ringball 7s").getAttribute("href")).toBe("/platform/sports");
    expect(requests.find((request) => request.path === "/api/platform/v1/sports/RINGBALL_SEVENS")?.search).toBe("?version=2");

    fireEvent.mouseDown(screen.getByRole("tab", { name: "Scorer pad" }));
    expect(navigation.replace).toHaveBeenCalledWith("/platform/sports/RINGBALL_SEVENS?version=2&tab=scorer-pad", {
      scroll: false,
    });
  });

  it("lists the formats and shows one, linking within the console", async () => {
    open("formats", { path: "/platform/formats" });

    const link = await screen.findByRole("link", { name: "Climbing ladder" });
    expect(link.getAttribute("href")).toBe("/platform/formats/LADDER_CLIMB");
    expect(cells("Showdown")).toEqual(["Only 2 participants", "Head to head", "1"]);
    cleanup();

    open("format", { path: "/platform/formats/LADDER_CLIMB", params: { code: "LADDER_CLIMB" } });
    expect(await screen.findByRole("heading", { level: 1, name: "Ladder climb" })).toBeTruthy();
    expect(screen.getByText("Version 3 · published 1 Nov 2026 (UTC)")).toBeTruthy();
    expect(backLink("Ladder climb").getAttribute("href")).toBe("/platform/formats");
  });

  it("lists the templates with the platform's sport and format names", async () => {
    open("templates", { path: "/platform/rules-templates" });

    const link = await screen.findByRole("link", { name: "Ring league" });
    expect(link.getAttribute("href")).toBe("/platform/rules-templates/RING_LEAGUE");
    expect((await screen.findAllByText("Climbing ladder, Showdown")).length).toBeGreaterThan(0);
    expect(cells("Ring league")).toEqual(["Ringball 7s", "Climbing ladder, Showdown", "2"]);
  });

  it("shows a template with its pinned sport version's labels, linking to the console's sport and formats", async () => {
    const requests = open("template", { path: "/platform/rules-templates/RING_LEAGUE", params: { code: "RING_LEAGUE" } });

    expect(await screen.findByRole("heading", { level: 1, name: "Ring league" })).toBeTruthy();
    expect(screen.getByText("Version 2 · published 1 Nov 2026 (UTC)")).toBeTruthy();
    // The pinned definition's labels, not the codes as words.
    expect(screen.getAllByText(/Hoop guard/).length).toBeGreaterThan(0);
    expect(screen.getByRole("link", { name: "Ringball 7s, version 2" }).getAttribute("href")).toBe(
      "/platform/sports/RINGBALL_SEVENS?version=2",
    );
    expect(screen.getByRole("link", { name: "Climbing ladder" }).getAttribute("href")).toBe("/platform/formats/LADDER_CLIMB");
    expect(backLink("Ring league").getAttribute("href")).toBe("/platform/rules-templates");
    expect(requests.find((request) => request.path === "/api/platform/v1/sports/RINGBALL_SEVENS")?.search).toBe("?version=2");
    expect(adminReads(requests)).toEqual([]);
  });

  it("answers Page not found for an unknown item or a version that isn't one", async () => {
    open("format", {
      path: "/platform/formats/NO_SUCH",
      params: { code: "NO_SUCH" },
      answers: { "GET /api/platform/v1/formats/NO_SUCH": { status: 404, body: problem("FORMAT_DEFINITION_NOT_FOUND", 404) } },
    });
    expect(await screen.findByRole("heading", { name: "Page not found" })).toBeTruthy();
    cleanup();

    const requests = open("template", {
      path: "/platform/rules-templates/RING_LEAGUE",
      params: { code: "RING_LEAGUE" },
      search: "version=latest",
    });
    expect(await screen.findByRole("heading", { name: "Page not found" })).toBeTruthy();
    expect(requests.some((request) => request.path.startsWith("/api/platform/v1/ruleset-templates"))).toBe(false);
  });

  it("is refused without platform.sport.read, hides the entries and doesn't ask the API", async () => {
    const requests = open("sports", { actions: ["tenant.read", "plan.read"] });

    expect(await screen.findByRole("heading", { name: "You can't open this page" })).toBeTruthy();
    await act(async () => {});
    expect(screen.queryByRole("link", { name: "Formats" })).toBeNull();
    expect(screen.queryByRole("link", { name: "Rules templates" })).toBeNull();
    expect(requests.some((request) => request.path.startsWith("/api/platform/v1/sports"))).toBe(false);
  });
});
