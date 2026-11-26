// Registry → Teams → a team (S07 T6c-1): Overview with its category, organisation, home venue and colours, the
// status and former-name history, the status moves the machine allows, each with a required reason and the version
// its dialog opened on; reforming at the plan's team limit shows the limit; a conflict offers Reload; a viewer sees
// everything without a change control, and the Players and Staff tabs aren't shown before S08.
import { cleanup, fireEvent, screen, waitFor, within } from "@testing-library/react";
import { afterEach, describe, expect, it, vi } from "vitest";
import { readOnlyCapabilities } from "@/test/capabilities";
import { created, kingfishers, meteors, sparrows, statusChange, team, teamAnswers } from "@/test/teams";
import { fakeApi, problem, renderWithQuery, type Answers, type RecordedRequest } from "@/test/render";
import TenantWorkspaceLayout from "../../layout";
import type { Team, TeamStatus } from "../teams-data";
import TeamPage from "./page";

const navigation = vi.hoisted(() => ({ teamId: "", search: "" }));

vi.mock("next/navigation", () => ({
  useParams: () => ({ tenantSlug: "kmsa", teamId: navigation.teamId }),
  usePathname: () => `/t/kmsa/teams/${navigation.teamId}`,
  useSearchParams: () => new URLSearchParams(navigation.search),
  useRouter: () => ({ replace: vi.fn(), push: vi.fn() }),
}));

const reads = ["team.read", "organisation.read", "venue.read", "sport.read", "tenant.settings.read"];
const registrar = [...reads, "team.status.change"];

function open(shown: Team = meteors, capabilities: Parameters<typeof teamAnswers>[0] = registrar, answers: Answers = {}) {
  navigation.teamId = shown.id;
  const requests = fakeApi({
    ...teamAnswers(capabilities),
    [`GET /api/admin/v1/teams/${shown.id}`]: { status: 200, body: shown },
    [`GET /api/admin/v1/teams/${shown.id}/status-changes`]: { status: 200, body: { items: [created] } },
    ...answers,
  });
  renderWithQuery(
    <TenantWorkspaceLayout>
      <TeamPage />
    </TenantWorkspaceLayout>,
  );
  return requests;
}

const loaded = (shown: Team = meteors) => screen.findByRole("heading", { level: 1, name: shown.name });
const moveLabels = ["Reactivate", "Make inactive", "Suspend", "Lift suspension", "Disband", "Reform", "Archive", "Unarchive"];
const moveButtons = () => moveLabels.filter((name) => screen.queryByRole("button", { name }) !== null);
const dialog = () => screen.getByRole("dialog");
const statusPath = (shown: Team = meteors) => `/api/admin/v1/teams/${shown.id}/status`;
const sentTo = async (requests: RecordedRequest[], path: string) => {
  let found: RecordedRequest | undefined;
  await waitFor(() => {
    found = requests.find((request) => request.method === "POST" && request.path === path && request.body !== undefined);
    expect(found).toBeTruthy();
  });
  return found!;
};
const confirm = (label: string, reason: string) => {
  fireEvent.change(within(dialog()).getByLabelText("Reason"), { target: { value: reason } });
  fireEvent.click(within(dialog()).getByRole("button", { name: label }));
};

afterEach(() => {
  navigation.search = "";
  cleanup();
  vi.unstubAllGlobals();
});

describe("Team page", () => {
  it("shows the overview: category, sport from its definition, organisation, home venue, code, motto and colours", async () => {
    open();
    await loaded();
    // The header: initials without a logo, the category and the sport.
    expect(screen.getByRole("img", { name: "No logo for Mutundwe Meteors FC" })).toBeTruthy();
    expect(await screen.findByText("Senior Men · Ringball sevens")).toBeTruthy();
    const overview = screen.getByRole("tabpanel");
    expect(within(overview).getByText("Meteors")).toBeTruthy();
    expect(within(overview).getByText("Senior")).toBeTruthy();
    expect(within(overview).getByText("Men")).toBeTruthy();
    expect(within(overview).getByText("First team")).toBeTruthy();
    expect(within(overview).getByText("MUT")).toBeTruthy();
    expect(within(overview).getByText("Higher together")).toBeTruthy();
    expect(within(overview).getByText("2018")).toBeTruthy();
    expect(within(overview).getByRole("link", { name: "Mutundwe Meteors SC" }).getAttribute("href")).toBe(
      `/t/kmsa/organisations/${meteors.organisation!.id}`,
    );
    expect(within(overview).getByRole("link", { name: "Bukoto Community Stadium" }).getAttribute("href")).toBe(
      `/t/kmsa/venues/${meteors.homeVenue!.id}`,
    );
    // Colours as swatches with their hex (upper case), the swatch hidden from screen readers.
    expect(within(overview).getByText(meteors.primaryColour!)).toBeTruthy();
    const secondary = within(overview).getByText(meteors.secondaryColour!.toUpperCase());
    const swatch = secondary.previousElementSibling as HTMLElement;
    expect(swatch.getAttribute("aria-hidden")).toBe("true");
    // Painted with the team's own colour (jsdom answers it as rgb()).
    expect(swatch.style.backgroundColor).toMatch(/^rgb\(/);
  });

  it("says what isn't recorded, names youth categories in words and an independent team as such", async () => {
    open(kingfishers);
    await loaded(kingfishers);
    let overview = screen.getByRole("tabpanel");
    expect(within(overview).getByText("Independent")).toBeTruthy();
    expect(within(overview).getByText("Reserve")).toBeTruthy();
    cleanup();

    open(sparrows);
    await loaded(sparrows);
    overview = screen.getByRole("tabpanel");
    expect(within(overview).getByText("Under-17")).toBeTruthy();
    expect(within(overview).getByText("Boys")).toBeTruthy();
    expect(within(overview).getByText("Not recorded")).toBeTruthy();
    // No home venue, short name, code, motto or colour.
    expect(within(overview).getAllByText("None").length).toBe(7);
  });

  it("names the organisation and the venue without links for whoever can't read them", async () => {
    open(meteors, ["team.read", "sport.read", "tenant.settings.read"]);
    await loaded();
    const overview = screen.getByRole("tabpanel");
    expect(within(overview).getByText("Mutundwe Meteors SC")).toBeTruthy();
    expect(within(overview).getByText("Bukoto Community Stadium")).toBeTruthy();
    expect(within(overview).queryAllByRole("link")).toEqual([]);
  });

  it("shows the logo the read signs", async () => {
    const url = "https://api.kmsa.example/media/logo-medium.webp?sig=1";
    const shown = team({ logo: { thumbnailUrl: url, mediumUrl: url, expiresAt: "2026-11-26T14:00:00Z" } });
    open(shown);
    await loaded();
    expect(screen.getByRole("img", { name: "Logo of Mutundwe Meteors FC" }).getAttribute("src")).toBe(url);
  });

  it("shows the status history and the former names with the dates each was used", async () => {
    navigation.search = "tab=history";
    open(meteors, registrar, {
      [`GET /api/admin/v1/teams/${meteors.id}/status-changes`]: {
        status: 200,
        body: {
          items: [
            statusChange("0199a3f0-0000-7000-8000-0000000000c2", "ACTIVE", "SUSPENDED", "Registration fees unpaid.", "2026-11-10T09:00:00Z"),
            created,
          ],
        },
      },
    });
    await loaded();
    const history = screen.getByRole("tabpanel");
    expect(await within(history).findByText("Active → Suspended")).toBeTruthy();
    expect(within(history).getAllByText("Registration fees unpaid.").length).toBeGreaterThan(0);
    const former = within(history).getByText("Mutundwe Weavers FC").closest("tr")!;
    expect(within(former).getAllByText("1 Jan 2018 to 31 Dec 2025").length).toBeGreaterThan(0);
    expect(within(former).getAllByText("Weavers").length).toBeGreaterThan(0);
    expect(within(former).getAllByText("MWV").length).toBeGreaterThan(0);
  });

  it("says when a team has had no other name", async () => {
    navigation.search = "tab=history";
    open(sparrows);
    await loaded(sparrows);
    expect(screen.getByText("Seeta Sparrows FC Under-17 has had no other name.")).toBeTruthy();
  });

  it("shows Overview and History only: Players and Staff come with team memberships", async () => {
    open();
    await loaded();
    expect(screen.getAllByRole("tab").map((tab) => tab.textContent)).toEqual(["Overview", "History"]);
  });

  it("answers an unknown team with Not found", async () => {
    open(meteors, registrar, {
      [`GET /api/admin/v1/teams/${meteors.id}`]: { status: 404, body: problem("TEAM_NOT_FOUND", 404) },
    });
    expect(await screen.findByText(/not found|doesn.t exist/i)).toBeTruthy();
  });

  it("refuses whoever doesn't hold team.read, without asking for the team", async () => {
    const requests = open(meteors, ["tenant.settings.read"]);
    expect(await screen.findByText("You can't open this page")).toBeTruthy();
    expect(requests.some((request) => request.path === `/api/admin/v1/teams/${meteors.id}`)).toBe(false);
  });
});

describe("Team status moves", () => {
  it.each<[TeamStatus, string[]]>([
    ["ACTIVE", ["Make inactive", "Suspend", "Disband", "Archive"]],
    ["INACTIVE", ["Reactivate", "Suspend", "Disband", "Archive"]],
    ["SUSPENDED", ["Make inactive", "Lift suspension", "Disband", "Archive"]],
    ["DISBANDED", ["Reform", "Archive"]],
    ["ARCHIVED", ["Unarchive"]],
  ])("offers only the machine's moves from %s", async (status, expected) => {
    const shown = team({ status });
    open(shown);
    await loaded(shown);
    expect(moveButtons()).toEqual(expected);
  });

  it("shows a viewer every tab without a change control", async () => {
    open(meteors, reads);
    await loaded();
    expect(moveButtons()).toEqual([]);
    const main = screen.getByRole("heading", { level: 1, name: meteors.name }).closest("section")!;
    expect(within(main).queryAllByRole("button")).toEqual([]);
    fireEvent.mouseDown(screen.getByRole("tab", { name: "History" }));
    expect(within(main).queryAllByRole("button")).toEqual([]);
  });

  it("on a read-only tenant, shows the moves disabled with the reason", async () => {
    open(meteors, readOnlyCapabilities(reads, ["team.status.change"]));
    await loaded();
    const button = screen.getByRole("button", { name: "Disband" });
    expect(button.hasAttribute("disabled")).toBe(true);
    expect(document.getElementById(button.getAttribute("aria-describedby") ?? "")?.textContent).toMatch(/read-only/i);
  });

  it("needs a reason, sends the target and the version, then announces it and focuses the status", async () => {
    const requests = open(meteors, registrar, {
      [`POST ${statusPath()}`]: { status: 200, body: team({ status: "DISBANDED", version: 6 }) },
    });
    await loaded();
    fireEvent.click(screen.getByRole("button", { name: "Disband" }));
    expect(within(dialog()).getByText(/frees a place under your plan's team limit/)).toBeTruthy();
    fireEvent.click(within(dialog()).getByRole("button", { name: "Disband" }));
    expect(await within(dialog()).findByText("Give a reason: it is kept in the audit trail.")).toBeTruthy();
    expect(requests.some((request) => request.method === "POST")).toBe(false);

    confirm("Disband", "The club stopped running a reserve side.");
    const request = await sentTo(requests, statusPath());
    expect(request.body).toEqual({ status: "DISBANDED", reason: "The club stopped running a reserve side." });
    expect(request.headers.get("If-Match")).toBe('"5"');
    await waitFor(() => expect(screen.queryByRole("dialog")).toBeNull());
    expect(screen.getAllByRole("status").map((item) => item.textContent)).toContain("Mutundwe Meteors FC: disbanded.");
    await waitFor(() => expect(document.activeElement?.id).toBe("team-status"));
    expect(moveButtons()).toEqual(["Reform", "Archive"]);
  });

  it("reforms a disbanded team as active, and at the plan's team limit shows the limit with the dialog still open", async () => {
    const requests = open(kingfishers, registrar, {
      [`POST ${statusPath(kingfishers)}`]: {
        status: 403,
        body: problem("ENTITLEMENT_QUOTA_EXCEEDED", 403, { detail: "Your plan allows 40 teams." }),
      },
    });
    await loaded(kingfishers);
    fireEvent.click(screen.getByRole("button", { name: "Reform" }));
    expect(within(dialog()).getByText(/counts toward your plan's team limit again/)).toBeTruthy();
    confirm("Reform", "The reserve side plays again this season.");
    expect((await sentTo(requests, statusPath(kingfishers))).body).toEqual({
      status: "ACTIVE",
      reason: "The reserve side plays again this season.",
    });
    expect(await within(dialog()).findByText(/Your plan allows 40 teams\./)).toBeTruthy();
  });

  it.each<[TeamStatus, string, boolean]>([
    ["DISBANDED", "disbanded", false],
    ["SUSPENDED", "suspended", true],
  ])("unarchives to the status it had before (%s), saying when it takes a place again", async (before, word, counts) => {
    const archived = team({ status: "ARCHIVED" });
    const history = {
      items: [
        statusChange("0199a3f0-0000-7000-8000-0000000000c3", before, "ARCHIVED", "Old record.", "2026-11-20T09:00:00Z"),
        statusChange("0199a3f0-0000-7000-8000-0000000000c2", "ACTIVE", before, "Folded.", "2026-11-10T09:00:00Z"),
        created,
      ],
    };
    const requests = open(archived, registrar, {
      [`GET /api/admin/v1/teams/${archived.id}/status-changes`]: { status: 200, body: history },
      [`POST ${statusPath(archived)}`]: { status: 200, body: team({ status: before, version: 6 }) },
    });
    await loaded(archived);
    fireEvent.click(screen.getByRole("button", { name: "Unarchive" }));
    const description = await within(dialog()).findByText(new RegExp(`goes back to ${word}`));
    expect(/team limit/.test(description.textContent ?? "")).toBe(counts);
    confirm("Unarchive", "Archived by mistake.");
    expect((await sentTo(requests, statusPath(archived))).body).toEqual({ status: before, reason: "Archived by mistake." });
  });

  it("offers Reload after a concurrency conflict", async () => {
    const requests = open(meteors, registrar, {
      [`POST ${statusPath()}`]: { status: 409, body: problem("TEAM_CONCURRENCY_CONFLICT", 409) },
    });
    await loaded();
    fireEvent.click(screen.getByRole("button", { name: "Archive" }));
    confirm("Archive", "Duplicate record.");
    expect(await within(dialog()).findByText("This record changed since you opened it")).toBeTruthy();
    fireEvent.click(within(dialog()).getByRole("button", { name: "Reload" }));
    await waitFor(() => expect(screen.queryByRole("dialog")).toBeNull());
    await waitFor(() =>
      expect(requests.filter((item) => item.method === "GET" && item.path === `/api/admin/v1/teams/${meteors.id}`).length).toBe(2),
    );
  });
});
