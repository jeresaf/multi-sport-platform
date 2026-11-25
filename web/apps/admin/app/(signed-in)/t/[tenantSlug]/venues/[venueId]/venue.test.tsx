// Registry → Venues → a venue (S07 T6a-1): Overview with the sports named from their definitions and a map link, the
// teams that call it home, the status history, and the status moves the machine allows, each with a required reason
// and the version its dialog opened on; a conflict offers Reload; a viewer sees everything without a change control.
import { cleanup, fireEvent, screen, waitFor, within } from "@testing-library/react";
import { afterEach, describe, expect, it, vi } from "vitest";
import { readOnlyCapabilities } from "@/test/capabilities";
import { fakeApi, problem, renderWithQuery, type Answers, type RecordedRequest } from "@/test/render";
import { bukoto, kisaasi, listPage, venue, venueAnswers } from "@/test/venues";
import TenantWorkspaceLayout from "../../layout";
import type { Venue, VenueStatus } from "../venues-data";
import VenuePage from "./page";

const navigation = vi.hoisted(() => ({ venueId: "", search: "" }));

vi.mock("next/navigation", () => ({
  useParams: () => ({ tenantSlug: "kmsa", venueId: navigation.venueId }),
  usePathname: () => `/t/kmsa/venues/${navigation.venueId}`,
  useSearchParams: () => new URLSearchParams(navigation.search),
  useRouter: () => ({ replace: vi.fn(), push: vi.fn() }),
}));

const reads = ["venue.read", "team.read", "sport.read", "tenant.settings.read"];
const registrar = [...reads, "venue.status.change"];

const created = {
  id: "0199a3f0-0000-7000-8000-0000000000c1",
  fromStatus: null,
  toStatus: "ACTIVE",
  reason: null,
  actorKind: "USER",
  actorId: "0199a3f0-0000-7000-8000-000000000001",
  occurredAt: "2026-11-02T07:30:00Z",
};

const homeTeam = {
  id: "0199a3f0-0000-7000-8000-0000000000d1",
  name: "Ntinda Swifts FC",
  shortName: "Ntinda Swifts",
  slug: "ntinda-swifts-fc",
  variant: "RINGBALL_SEVENS",
  genderCategory: "WOMEN",
  ageGroup: "U17",
  level: "FIRST",
  organisation: { id: "0199a3f0-0000-7000-8000-0000000000e1", displayName: "Ntinda Swifts SC", slug: "ntinda-swifts-sc" },
  status: "ACTIVE",
};

function open(shown: Venue = bukoto, capabilities: Parameters<typeof venueAnswers>[0] = registrar, answers: Answers = {}) {
  navigation.venueId = shown.id;
  const requests = fakeApi({
    ...venueAnswers(capabilities),
    [`GET /api/admin/v1/venues/${shown.id}`]: { status: 200, body: shown },
    [`GET /api/admin/v1/venues/${shown.id}/status-changes`]: { status: 200, body: { items: [created] } },
    "GET /api/admin/v1/teams": listPage([homeTeam]),
    ...answers,
  });
  renderWithQuery(
    <TenantWorkspaceLayout>
      <VenuePage />
    </TenantWorkspaceLayout>,
  );
  return requests;
}

const loaded = (shown: Venue = bukoto) => screen.findByRole("heading", { level: 1, name: shown.name });
const moveLabels = ["Mark unavailable", "Mark available", "Close venue", "Reopen", "Archive", "Unarchive"];
const moveButtons = () => moveLabels.filter((name) => screen.queryByRole("button", { name }) !== null);
const dialog = () => screen.getByRole("dialog");
const sentTo = async (requests: RecordedRequest[], path: string) => {
  let found: RecordedRequest | undefined;
  await waitFor(() => {
    found = requests.find((request) => request.method === "POST" && request.path === path && request.body !== undefined);
    expect(found).toBeTruthy();
  });
  return found!;
};
const statusPath = `/api/admin/v1/venues/${bukoto.id}/status`;

afterEach(() => {
  navigation.search = "";
  cleanup();
  vi.unstubAllGlobals();
});

describe("Venue page", () => {
  it("shows the overview: address, time zone, capacity, surfaces, sports from the definitions and a map link", async () => {
    open();
    await loaded();
    const overview = screen.getByRole("tabpanel");
    expect(within(overview).getByText("Plot 12, Bukoto Street")).toBeTruthy();
    expect(within(overview).getByText("Africa/Kampala")).toBeTruthy();
    expect(within(overview).getByText("3,000 seats")).toBeTruthy();
    expect(within(overview).getByText("Natural grass")).toBeTruthy();
    expect(await within(overview).findByText("Ringball sevens")).toBeTruthy();
    expect(within(overview).getByText("Uganda")).toBeTruthy();
    const map = within(overview).getByRole("link", { name: /Open in a map/ });
    expect(map.getAttribute("href")).toBe("https://www.openstreetmap.org/?mlat=0.3476&mlon=32.5925#map=17/0.3476/32.5925");
    expect(map.getAttribute("rel")).toBe("noopener noreferrer");
  });

  it("says what isn't recorded, and offers no map without coordinates", async () => {
    open(kisaasi);
    await loaded(kisaasi);
    const overview = screen.getByRole("tabpanel");
    expect(within(overview).queryByRole("link", { name: /Open in a map/ })).toBeNull();
    expect(within(overview).getAllByText("Not recorded").length).toBe(4); // capacity, address, region, coordinates
    expect(within(overview).getAllByText("None").length).toBe(3); // short name, surfaces, sports
  });

  it("lists the teams that call it home, asked by the venue's id", async () => {
    navigation.search = "tab=teams";
    const requests = open();
    await loaded();
    const row = (await screen.findByText("Ntinda Swifts FC")).closest("tr")!;
    const cells = within(row).getAllByRole("cell").map((cell) => cell.textContent);
    expect(cells).toEqual(["Under-17 Girls", "Ringball sevens", "Ntinda Swifts SC", "Active"]);
    const teams = requests.find((request) => request.path === "/api/admin/v1/teams")!;
    expect(new URLSearchParams(teams.search).get("homeVenueId")).toBe(bukoto.id);
  });

  it("says when no team calls it home, and doesn't ask without team.read", async () => {
    navigation.search = "tab=teams";
    open(bukoto, registrar, { "GET /api/admin/v1/teams": listPage([]) });
    expect(await screen.findByText("No team calls Bukoto Community Stadium home.")).toBeTruthy();
    cleanup();

    const requests = open(bukoto, ["venue.read", "sport.read", "tenant.settings.read"]);
    expect(await screen.findByText("Your role doesn't include viewing teams.")).toBeTruthy();
    expect(requests.some((request) => request.path === "/api/admin/v1/teams")).toBe(false);
  });

  it("shows the status history", async () => {
    navigation.search = "tab=history";
    open();
    await loaded();
    expect(await screen.findByText("Recorded as active")).toBeTruthy();
  });

  it("answers an unknown venue with Not found", async () => {
    open(bukoto, registrar, {
      [`GET /api/admin/v1/venues/${bukoto.id}`]: { status: 404, body: problem("VENUE_NOT_FOUND", 404) },
    });
    expect(await screen.findByText(/not found|doesn.t exist/i)).toBeTruthy();
  });
});

describe("Venue status moves", () => {
  it.each<[VenueStatus, string[]]>([
    ["ACTIVE", ["Mark unavailable", "Close venue", "Archive"]],
    ["TEMPORARILY_UNAVAILABLE", ["Mark available", "Close venue", "Archive"]],
    ["CLOSED", ["Reopen", "Archive"]],
    ["ARCHIVED", ["Unarchive"]],
  ])("offers only the machine's moves from %s", async (status, expected) => {
    const shown = venue({ status });
    open(shown);
    await loaded(shown);
    expect(moveButtons()).toEqual(expected);
  });

  it("shows a viewer every tab without a change control", async () => {
    open(bukoto, reads);
    await loaded();
    expect(moveButtons()).toEqual([]);
    for (const tab of ["Overview", "Teams", "History"]) {
      expect(screen.getByRole("tab", { name: tab })).toBeTruthy();
    }
    expect(screen.queryByRole("link", { name: /Edit/ })).toBeNull();
  });

  it("on a read-only tenant, shows the moves disabled with the reason", async () => {
    open(bukoto, readOnlyCapabilities(reads, ["venue.status.change"]));
    await loaded();
    const close = screen.getByRole("button", { name: "Close venue" });
    expect(close.hasAttribute("disabled")).toBe(true);
    expect(document.getElementById(close.getAttribute("aria-describedby") ?? "")?.textContent).toMatch(/read-only/i);
  });

  it("needs a reason, sends the target and the version, then announces it and focuses the status", async () => {
    const requests = open(bukoto, registrar, {
      [`POST ${statusPath}`]: { status: 200, body: venue({ status: "CLOSED", version: 5 }) },
    });
    await loaded();
    fireEvent.click(screen.getByRole("button", { name: "Close venue" }));
    fireEvent.click(within(dialog()).getByRole("button", { name: "Close venue" }));
    expect(await within(dialog()).findByText("Give a reason: it is kept in the audit trail.")).toBeTruthy();
    expect(requests.some((request) => request.method === "POST")).toBe(false);

    fireEvent.change(within(dialog()).getByLabelText("Reason"), { target: { value: "The main stand is being rebuilt." } });
    fireEvent.click(within(dialog()).getByRole("button", { name: "Close venue" }));
    const request = await sentTo(requests, statusPath);
    expect(request.body).toEqual({ status: "CLOSED", reason: "The main stand is being rebuilt." });
    expect(request.headers.get("If-Match")).toBe('"4"');
    await waitFor(() => expect(screen.queryByRole("dialog")).toBeNull());
    expect(screen.getAllByRole("status").map((item) => item.textContent)).toContain("Bukoto Community Stadium: closed.");
    await waitFor(() => expect(document.activeElement?.id).toBe("venue-status"));
    expect(moveButtons()).toEqual(["Reopen", "Archive"]);
  });

  it("unarchives to the status the venue had before it was archived", async () => {
    const archived = venue({ status: "ARCHIVED" });
    const history = {
      items: [
        { ...created, id: "0199a3f0-0000-7000-8000-0000000000c3", fromStatus: "CLOSED", toStatus: "ARCHIVED", reason: "Sold." },
        { ...created, id: "0199a3f0-0000-7000-8000-0000000000c2", fromStatus: "ACTIVE", toStatus: "CLOSED", reason: "Shut." },
        created,
      ],
    };
    const requests = open(archived, registrar, {
      [`GET /api/admin/v1/venues/${archived.id}/status-changes`]: { status: 200, body: history },
      [`POST ${statusPath}`]: { status: 200, body: venue({ status: "CLOSED", version: 5 }) },
    });
    await loaded(archived);
    fireEvent.click(screen.getByRole("button", { name: "Unarchive" }));
    expect(await within(dialog()).findByText(/goes back to closed/)).toBeTruthy();
    fireEvent.change(within(dialog()).getByLabelText("Reason"), { target: { value: "Archived by mistake." } });
    fireEvent.click(within(dialog()).getByRole("button", { name: "Unarchive" }));
    expect((await sentTo(requests, statusPath)).body).toEqual({ status: "CLOSED", reason: "Archived by mistake." });
  });

  it("offers Reload after a concurrency conflict", async () => {
    const requests = open(bukoto, registrar, {
      [`POST ${statusPath}`]: { status: 409, body: problem("VENUE_CONCURRENCY_CONFLICT", 409) },
    });
    await loaded();
    fireEvent.click(screen.getByRole("button", { name: "Archive" }));
    fireEvent.change(within(dialog()).getByLabelText("Reason"), { target: { value: "Merged with another record." } });
    fireEvent.click(within(dialog()).getByRole("button", { name: "Archive" }));
    expect(await within(dialog()).findByText("This record changed since you opened it")).toBeTruthy();
    fireEvent.click(within(dialog()).getByRole("button", { name: "Reload" }));
    await waitFor(() => expect(screen.queryByRole("dialog")).toBeNull());
    await waitFor(() =>
      expect(requests.filter((item) => item.method === "GET" && item.path === `/api/admin/v1/venues/${bukoto.id}`).length).toBe(2),
    );
  });
});
