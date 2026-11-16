// Tenant workspace → Formats: the published formats, and a format's stages and parameters (S05 D17), read from the API
// and shown in the format's own words; only for whoever holds `format.read`. The formats are test-only
// (test/formats.ts), so every word on screen must have come from them.
import { cleanup, screen, within } from "@testing-library/react";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { tenantCapabilities } from "@/test/capabilities";
import { ladder, showdown } from "@/test/formats";
import { fakeApi, problem, renderWithQuery, type Answer, type Answers } from "@/test/render";
import TenantWorkspaceLayout from "../layout";
import FormatPage from "./[code]/page";
import FormatsPage from "./page";

const navigation = vi.hoisted(() => ({
  pathname: "/t/kmsa/formats",
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

const reads = ["format.read", "tenant.settings.read"];
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

function open(page: "list" | "format", actions = reads, answers: Answers = {}) {
  const requests = fakeApi({
    "GET /api/account/v1/me": me,
    "GET /api/admin/v1/me/capabilities": { status: 200, body: tenantCapabilities(actions) },
    "GET /api/admin/v1/tenant/settings": settings,
    "GET /api/admin/v1/formats": { status: 200, body: { items: [ladder, showdown] } },
    "GET /api/admin/v1/formats/LADDER_CLIMB": { status: 200, body: ladder },
    "GET /api/admin/v1/formats/SHOWDOWN": { status: 200, body: showdown },
    ...answers,
  });
  renderWithQuery(<TenantWorkspaceLayout>{page === "list" ? <FormatsPage /> : <FormatPage />}</TenantWorkspaceLayout>);
  return requests;
}

function openFormat(code = "LADDER_CLIMB", search = "", actions = reads, answers: Answers = {}) {
  navigation.pathname = `/t/kmsa/formats/${code}`;
  navigation.code = code;
  navigation.search = search;
  return open("format", actions, answers);
}

// A row header holds the row's name, then (for a phone) the values of its wide columns: find it by its name.
const rowOf = (name: string) =>
  screen.getAllByRole("rowheader").find((header) => (header.firstElementChild ?? header).textContent === name)!.closest("tr")!;
const cells = (name: string) =>
  within(rowOf(name))
    .getAllByRole("cell")
    .map((cell) => cell.textContent);

beforeEach(() => {
  navigation.pathname = "/t/kmsa/formats";
  navigation.search = "";
  navigation.code = undefined;
});

afterEach(() => {
  cleanup();
  vi.unstubAllGlobals();
});

describe("formats list", () => {
  it("lists the published formats, each linking to its page", async () => {
    open("list");

    const link = await screen.findByRole("link", { name: "Ladder climb" });
    expect(link.getAttribute("href")).toBe("/t/kmsa/formats/LADDER_CLIMB");
    expect(cells("Ladder climb")).toEqual(["4 to 24 participants", "Head to head, Field", "3"]);
    expect(within(rowOf("Ladder climb")).getByText(ladder.description!)).toBeTruthy();
    expect(cells("Showdown")).toEqual(["Only 2 participants", "Head to head", "1"]);
    // The navigation's entry for the screen.
    expect(screen.getAllByRole("link", { name: "Formats" }).length).toBeGreaterThan(0);
  });

  it("says when nothing is published yet", async () => {
    open("list", reads, { "GET /api/admin/v1/formats": { status: 200, body: { items: [] } } });

    expect(await screen.findByText("No formats yet")).toBeTruthy();
    expect(screen.getByText("No formats are published yet. Ask the platform team.")).toBeTruthy();
  });

  it("shows a failure with its reference", async () => {
    open("list", reads, { "GET /api/admin/v1/formats": { status: 500, body: problem("PLATFORM_UNEXPECTED_ERROR", 500) } });

    expect(await screen.findByText("Reference: kmsa-admin.test")).toBeTruthy();
  });

  it("is refused without format.read, and doesn't ask the API", async () => {
    const requests = open("list", ["tenant.settings.read"]);

    expect(await screen.findByRole("heading", { name: "You can't open this page" })).toBeTruthy();
    expect(requests.some((request) => request.path.startsWith("/api/admin/v1/formats"))).toBe(false);
  });
});

describe("a format", () => {
  it("shows the header and the overview, published in the tenant's time zone", async () => {
    const requests = openFormat();

    expect(await screen.findByRole("heading", { level: 1, name: "Ladder climb" })).toBeTruthy();
    expect(screen.getByText("Version 3 · published 2 Nov 2026")).toBeTruthy();
    expect(screen.getAllByRole("heading", { level: 2 }).map((heading) => heading.textContent)).toEqual(["Overview", "Parameters"]);
    expect(screen.getByText(ladder.contentHash)).toBeTruthy();
    expect(screen.getByText(ladder.description!)).toBeTruthy();
    expect(screen.getByText("4 to 24 participants")).toBeTruthy();
    expect(screen.getByText("Head to head, Field")).toBeTruthy();
    const stages = within(screen.getByRole("list", { name: "Stages" })).getAllByRole("listitem");
    expect(stages.map((stage) => stage.textContent)).toEqual([
      "Opening rounds: league stage, drawn by the round robin generator",
      "Climb: knockout stage, drawn by the ladder generator",
    ]);
    const header = screen.getByRole("heading", { level: 1, name: "Ladder climb" }).closest("header")!;
    expect(within(header).getByRole("link", { name: "Formats" }).getAttribute("href")).toBe("/t/kmsa/formats");
    expect(requests.find((request) => request.path === "/api/admin/v1/formats/LADDER_CLIMB")?.search).toBe("");
  });

  it("shows each parameter with its type, default and what may be chosen", async () => {
    openFormat();

    await screen.findByRole("heading", { level: 1, name: "Ladder climb" });
    expect(cells("Challenges per week")).toEqual(["Whole number", "2", "1 to 3"]);
    expect(within(rowOf("Challenges per week")).getByText(ladder.parameters.challenges!.description!)).toBeTruthy();
    expect(cells("Open ladder")).toEqual(["Yes or no", "No", "Either"]);
    expect(cells("Starting order")).toEqual(["One of a list", "By entry time", "One of: Ranked, Random, By entry time"]);
  });

  it("says None for a format without parameters or a description, and leaves out a generator named as its stage", async () => {
    openFormat("SHOWDOWN");

    await screen.findByRole("heading", { level: 1, name: "Showdown" });
    expect(within(screen.getByRole("list", { name: "Stages" })).getByRole("listitem").textContent).toBe("Final: single stage");
    expect(screen.getAllByText("None")).toHaveLength(2);
    expect(screen.queryByRole("table")).toBeNull();
  });

  it("opens the version the address names", async () => {
    const requests = openFormat("LADDER_CLIMB", "version=2");

    await screen.findByRole("heading", { level: 1, name: "Ladder climb" });
    expect(requests.find((request) => request.path === "/api/admin/v1/formats/LADDER_CLIMB")?.search).toBe("?version=2");
  });

  it("is not found for a version that isn't one, without asking the API", async () => {
    const requests = openFormat("LADDER_CLIMB", "version=0");

    expect(await screen.findByRole("heading", { name: "Page not found" })).toBeTruthy();
    expect(requests.some((request) => request.path.startsWith("/api/admin/v1/formats/"))).toBe(false);
  });

  it("is not found for an unknown format", async () => {
    openFormat("LADDER_CLIMB", "", reads, {
      "GET /api/admin/v1/formats/LADDER_CLIMB": { status: 404, body: problem("FORMAT_DEFINITION_NOT_FOUND", 404) },
    });

    expect(await screen.findByRole("heading", { name: "Page not found" })).toBeTruthy();
  });

  it("shows any other failure with its reference, keeping the way back", async () => {
    openFormat("LADDER_CLIMB", "", reads, {
      "GET /api/admin/v1/formats/LADDER_CLIMB": { status: 500, body: problem("PLATFORM_UNEXPECTED_ERROR", 500) },
    });

    expect(await screen.findByText("Reference: kmsa-admin.test")).toBeTruthy();
    expect(within(screen.getByRole("main")).getByRole("link", { name: "Formats" }).getAttribute("href")).toBe("/t/kmsa/formats");
  });

  it("is refused without format.read, and doesn't ask the API", async () => {
    const requests = openFormat("LADDER_CLIMB", "", ["tenant.settings.read"]);

    expect(await screen.findByRole("heading", { name: "You can't open this page" })).toBeTruthy();
    expect(requests.some((request) => request.path.startsWith("/api/admin/v1/formats"))).toBe(false);
  });
});
