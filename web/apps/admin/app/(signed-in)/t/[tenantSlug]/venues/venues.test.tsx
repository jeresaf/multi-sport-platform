// Registry → Venues (S07 T6a-1): the directory, searched and filtered through the address, paged by cursor, sports
// named from their definitions; only for whoever holds `venue.read`, and without a change control for a viewer.
import { act, cleanup, fireEvent, screen, within } from "@testing-library/react";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { readOnlyCapabilities } from "@/test/capabilities";
import { fakeApi, renderWithQuery, type Answers } from "@/test/render";
import { bukoto, kisaasi, listItem, listPage, metroArena, venueAnswers } from "@/test/venues";
import TenantWorkspaceLayout from "../layout";
import VenuesPage from "./page";

const navigation = vi.hoisted(() => ({ search: "", replace: vi.fn() }));

vi.mock("next/navigation", () => ({
  useParams: () => ({ tenantSlug: "kmsa" }),
  usePathname: () => "/t/kmsa/venues",
  useSearchParams: () => new URLSearchParams(navigation.search),
  useRouter: () => ({ replace: navigation.replace, push: vi.fn() }),
}));

const viewer = ["venue.read", "sport.read", "tenant.settings.read"];

function open(actions: Parameters<typeof venueAnswers>[0] = viewer, answers: Answers = {}) {
  const requests = fakeApi({
    ...venueAnswers(actions),
    "GET /api/admin/v1/venues": listPage([bukoto, kisaasi, metroArena].map(listItem)),
    ...answers,
  });
  renderWithQuery(
    <TenantWorkspaceLayout>
      <VenuesPage />
    </TenantWorkspaceLayout>,
  );
  return requests;
}

const venueRequests = (requests: ReturnType<typeof open>) =>
  requests.filter((request) => request.path === "/api/admin/v1/venues");
const rowOf = (name: string) => screen.getByRole("link", { name }).closest("tr")!;

beforeEach(() => {
  navigation.search = "";
  navigation.replace.mockReset();
});

afterEach(() => {
  cleanup();
  vi.useRealTimers();
  vi.unstubAllGlobals();
});

describe("venues directory", () => {
  it("lists venues with their type, city, sports from the definitions and status, each linking to its page", async () => {
    const requests = open();

    const link = await screen.findByRole("link", { name: "Bukoto Community Stadium" });
    expect(link.getAttribute("href")).toBe(`/t/kmsa/venues/${bukoto.id}`);
    // The sport's name comes from its definition, never from a code in the web app.
    await within(rowOf("Bukoto Community Stadium")).findAllByText("Ringball sevens");
    const cells = within(rowOf("Bukoto Community Stadium")).getAllByRole("cell").map((cell) => cell.textContent);
    expect(cells).toEqual(["Stadium", "Kampala, Uganda", "Ringball sevens", "Active"]);
    expect(within(rowOf("Kisaasi Recreation Ground")).getAllByRole("cell")[2]?.textContent).toBe("None");
    expect(within(rowOf("Metro Indoor Arena")).getAllByRole("cell")[3]?.textContent).toBe("Temporarily unavailable");
    // No search or filter: the API's defaults (every status but archived).
    expect(venueRequests(requests)[0]?.search).toBe("");
    // The navigation's entry, under Registry.
    expect(screen.getAllByRole("link", { name: "Venues" }).length).toBeGreaterThan(0);
    expect(screen.getAllByText("Registry").length).toBeGreaterThan(0);
  });

  it("asks with the search and filters in the address, and leaves out a status the API doesn't know", async () => {
    navigation.search = "q=Bukoto&city=Kampala&status=CLOSED&variant=RINGBALL_SEVENS";
    const requests = open();

    await screen.findByRole("link", { name: "Bukoto Community Stadium" });
    expect(Object.fromEntries(new URLSearchParams(venueRequests(requests)[0]!.search))).toEqual({
      q: "Bukoto",
      city: "Kampala",
      status: "CLOSED",
      variant: "RINGBALL_SEVENS",
    });
    expect((screen.getByLabelText("Search") as HTMLInputElement).value).toBe("Bukoto");
    expect((screen.getByLabelText("City") as HTMLInputElement).value).toBe("Kampala");
    cleanup();

    navigation.search = "status=DEMOLISHED";
    const second = open();
    await screen.findByRole("link", { name: "Bukoto Community Stadium" });
    expect(venueRequests(second)[0]?.search).toBe("");
  });

  it("writes the status at once and the search once typing pauses", async () => {
    open();
    await screen.findByRole("link", { name: "Bukoto Community Stadium" });

    fireEvent.change(screen.getByLabelText("Status"), { target: { value: "ARCHIVED" } });
    expect(navigation.replace).toHaveBeenLastCalledWith("/t/kmsa/venues?status=ARCHIVED", { scroll: false });

    vi.useFakeTimers();
    fireEvent.change(screen.getByLabelText("City"), { target: { value: "Naguru" } });
    expect(navigation.replace).toHaveBeenCalledTimes(1);
    await act(() => vi.advanceTimersByTimeAsync(300));
    expect(navigation.replace).toHaveBeenLastCalledWith("/t/kmsa/venues?city=Naguru", { scroll: false });
  });

  it("pages with Show more", async () => {
    const requests = open(viewer, {
      "GET /api/admin/v1/venues": (request) =>
        request.search.includes("cursor=")
          ? listPage([listItem(metroArena)])
          : listPage([bukoto, kisaasi].map(listItem), "next-page"),
    });

    fireEvent.click(await screen.findByRole("button", { name: "Show more" }));
    await screen.findByRole("link", { name: "Metro Indoor Arena" });
    expect(new URLSearchParams(venueRequests(requests)[1]!.search).get("cursor")).toBe("next-page");
    expect(screen.queryByRole("button", { name: "Show more" })).toBeNull();
  });

  it("says when nothing matches, and when there is no venue yet", async () => {
    navigation.search = "q=Ggaba";
    open(viewer, { "GET /api/admin/v1/venues": listPage([]) });
    expect(await screen.findByText("No venue matches")).toBeTruthy();
    cleanup();

    navigation.search = "";
    open(viewer, { "GET /api/admin/v1/venues": listPage([]) });
    expect(await screen.findByText("No venues yet")).toBeTruthy();
  });

  it("shows a viewer the directory without a change control", async () => {
    open();
    await screen.findByRole("link", { name: "Bukoto Community Stadium" });
    const main = screen.getByRole("heading", { level: 1, name: "Venues" }).closest("section")!;
    // Only Show more's kind of button would appear; a viewer gets no create, edit or status control (Add venue is a
    // link when allowed, so both roles are checked: review #138 R1).
    expect(within(main).queryAllByRole("button")).toEqual([]);
    expect(within(main).queryByRole("link", { name: "Add venue" })).toBeNull();
  });

  it("offers Add venue to whoever may add venues, and disables it with the reason on a read-only tenant", async () => {
    open([...viewer, "venue.create", "venue.duplicate_check"]);
    const add = await screen.findByRole("link", { name: "Add venue" });
    expect(add.getAttribute("href")).toBe("/t/kmsa/venues/new");
    cleanup();

    open(readOnlyCapabilities(viewer, ["venue.create", "venue.duplicate_check"]));
    const disabled = await screen.findByRole("button", { name: "Add venue" });
    expect(disabled).toHaveProperty("disabled", true);
    expect(disabled.getAttribute("aria-describedby")).toBe("add-venue-unavailable");
    expect(document.getElementById("add-venue-unavailable")?.textContent).toMatch(/read-only/i);
    expect(screen.queryByRole("link", { name: "Add venue" })).toBeNull();
  });

  it("refuses whoever doesn't hold venue.read, without asking for the venues", async () => {
    const requests = open(["sport.read", "tenant.settings.read"]);
    expect(await screen.findByText("You can't open this page")).toBeTruthy();
    expect(venueRequests(requests)).toHaveLength(0);
  });
});
