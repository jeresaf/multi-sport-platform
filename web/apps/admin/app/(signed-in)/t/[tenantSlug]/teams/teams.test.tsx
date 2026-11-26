// Registry → Teams (S07 T6c-1): the directory, searched and filtered through the address, paged by cursor, with each
// team's sport named from its definition; only for whoever holds `team.read`, and without a change control for a
// viewer.
import { act, cleanup, fireEvent, screen, within } from "@testing-library/react";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { kingfishers, listItem, listPage, meteors, sparrows, teamAnswers } from "@/test/teams";
import { fakeApi, problem, renderWithQuery, type Answers } from "@/test/render";
import TenantWorkspaceLayout from "../layout";
import TeamsPage from "./page";

const navigation = vi.hoisted(() => ({ search: "", replace: vi.fn() }));

vi.mock("next/navigation", () => ({
  useParams: () => ({ tenantSlug: "kmsa" }),
  usePathname: () => "/t/kmsa/teams",
  useSearchParams: () => new URLSearchParams(navigation.search),
  useRouter: () => ({ replace: navigation.replace, push: vi.fn() }),
}));

const viewer = ["team.read", "organisation.read", "sport.read", "tenant.settings.read"];

function open(actions: string[] = viewer, answers: Answers = {}) {
  const requests = fakeApi({
    ...teamAnswers(actions),
    "GET /api/admin/v1/teams": listPage([meteors, sparrows, kingfishers].map(listItem)),
    ...answers,
  });
  renderWithQuery(
    <TenantWorkspaceLayout>
      <TeamsPage />
    </TenantWorkspaceLayout>,
  );
  return requests;
}

const directoryRequests = (requests: ReturnType<typeof open>) =>
  requests.filter((request) => request.path === "/api/admin/v1/teams");
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

describe("teams directory", () => {
  it("lists teams with their short name, category, sport, organisation and status, each linking to its page", async () => {
    const requests = open();

    const link = await screen.findByRole("link", { name: "Mutundwe Meteors FC" });
    expect(link.getAttribute("href")).toBe(`/t/kmsa/teams/${meteors.id}`);
    expect(within(rowOf("Mutundwe Meteors FC")).getByText("Meteors")).toBeTruthy();
    // The sport is named from its definition once the variants are read.
    await within(rowOf("Mutundwe Meteors FC")).findAllByText("Ringball sevens");
    const cells = within(rowOf("Mutundwe Meteors FC")).getAllByRole("cell").map((cell) => cell.textContent);
    expect(cells).toEqual(["Senior Men", "Ringball sevens", "Mutundwe Meteors SC", "Active"]);
    expect(
      within(rowOf("Mutundwe Meteors FC")).getAllByRole("link", { name: "Mutundwe Meteors SC" })[0]!.getAttribute("href"),
    ).toBe(`/t/kmsa/organisations/${meteors.organisation!.id}`);
    expect(within(rowOf("Seeta Sparrows FC Under-17")).getAllByRole("cell")[0]?.textContent).toBe("Under-17 Boys");
    const independent = within(rowOf("Kireka Kingfishers FC Reserves")).getAllByRole("cell");
    expect(independent.map((cell) => cell.textContent)).toEqual([
      "Senior Men · Reserve",
      "Ringball sevens",
      "Independent",
      "Disbanded",
    ]);
    // No search or filter: the API's defaults (every status but archived).
    expect(directoryRequests(requests)[0]?.search).toBe("");
    // The navigation's entry, under Registry.
    expect(screen.getAllByRole("link", { name: "Teams" }).length).toBeGreaterThan(0);
  });

  it("names the organisation without a link for whoever can't read organisations", async () => {
    open(["team.read", "sport.read", "tenant.settings.read"]);
    await screen.findByRole("link", { name: "Mutundwe Meteors FC" });
    expect(within(rowOf("Mutundwe Meteors FC")).queryByRole("link", { name: "Mutundwe Meteors SC" })).toBeNull();
    expect(within(rowOf("Mutundwe Meteors FC")).getAllByText("Mutundwe Meteors SC").length).toBeGreaterThan(0);
  });

  it("asks with the search and filters in the address, and leaves out a category or status the API doesn't know", async () => {
    navigation.search = "q=Seeta&variant=RINGBALL_SEVENS&genderCategory=MEN&ageGroup=U17&level=FIRST&status=DISBANDED";
    const requests = open();

    await screen.findByRole("link", { name: "Mutundwe Meteors FC" });
    expect(Object.fromEntries(new URLSearchParams(directoryRequests(requests)[0]!.search))).toEqual({
      q: "Seeta",
      variant: "RINGBALL_SEVENS",
      genderCategory: "MEN",
      ageGroup: "U17",
      level: "FIRST",
      status: "DISBANDED",
    });
    expect((screen.getByLabelText("Search") as HTMLInputElement).value).toBe("Seeta");
    expect((screen.getByLabelText("Age group") as HTMLSelectElement).value).toBe("U17");
    cleanup();

    navigation.search = "genderCategory=OPEN&ageGroup=U99&level=FOURTH&status=MERGED";
    const second = open();
    await screen.findByRole("link", { name: "Mutundwe Meteors FC" });
    expect(directoryRequests(second)[0]?.search).toBe("");
  });

  it("offers the sports from their definitions and the categories in words", async () => {
    open();
    await screen.findByRole("link", { name: "Mutundwe Meteors FC" });
    const sport = await screen.findByLabelText("Sport");
    expect(within(sport).getAllByRole("option").map((option) => option.textContent)).toEqual(["Any sport", "Ringball sevens"]);
    const gender = screen.getByLabelText("Gender");
    expect(within(gender).getAllByRole("option").map((option) => option.textContent)).toEqual([
      "Any gender",
      "Men",
      "Women",
      "Mixed",
    ]);
    const age = within(screen.getByLabelText("Age group")).getAllByRole("option").map((option) => option.textContent);
    expect(age.slice(0, 3)).toEqual(["Any age group", "Senior", "Under-6"]);
    expect(age.at(-1)).toBe("Veterans");
  });

  it("writes a filter at once and the search once typing pauses", async () => {
    open();
    await screen.findByRole("link", { name: "Mutundwe Meteors FC" });

    fireEvent.change(screen.getByLabelText("Level"), { target: { value: "RESERVE" } });
    expect(navigation.replace).toHaveBeenLastCalledWith("/t/kmsa/teams?level=RESERVE", { scroll: false });

    vi.useFakeTimers();
    fireEvent.change(screen.getByLabelText("Search"), { target: { value: "Kireka" } });
    expect(navigation.replace).toHaveBeenCalledTimes(1);
    await act(() => vi.advanceTimersByTimeAsync(300));
    expect(navigation.replace).toHaveBeenLastCalledWith("/t/kmsa/teams?q=Kireka", { scroll: false });
  });

  it("pages with Show more", async () => {
    const requests = open(viewer, {
      "GET /api/admin/v1/teams": (request) =>
        request.search.includes("cursor=")
          ? listPage([listItem(kingfishers)])
          : listPage([meteors, sparrows].map(listItem), "next-page"),
    });

    fireEvent.click(await screen.findByRole("button", { name: "Show more" }));
    await screen.findByRole("link", { name: "Kireka Kingfishers FC Reserves" });
    expect(new URLSearchParams(directoryRequests(requests)[1]!.search).get("cursor")).toBe("next-page");
    expect(screen.queryByRole("button", { name: "Show more" })).toBeNull();
  });

  it("says when nothing matches, when there is no team yet, and when the list can't be read", async () => {
    navigation.search = "q=Ggaba";
    open(viewer, { "GET /api/admin/v1/teams": listPage([]) });
    expect(await screen.findByText("No team matches")).toBeTruthy();
    cleanup();

    navigation.search = "";
    open(viewer, { "GET /api/admin/v1/teams": listPage([]) });
    expect(await screen.findByText("No teams yet")).toBeTruthy();
    cleanup();

    open(viewer, { "GET /api/admin/v1/teams": { status: 500, body: problem("PLATFORM_UNEXPECTED_ERROR", 500) } });
    expect(await screen.findByRole("alert")).toBeTruthy();
  });

  it("shows a viewer the directory without a change control", async () => {
    open();
    await screen.findByRole("link", { name: "Mutundwe Meteors FC" });
    const main = screen.getByRole("heading", { level: 1, name: "Teams" }).closest("section")!;
    expect(within(main).queryAllByRole("button")).toEqual([]);
  });

  it("refuses whoever doesn't hold team.read, without asking for the teams", async () => {
    const requests = open(["tenant.settings.read"]);
    expect(await screen.findByText("You can't open this page")).toBeTruthy();
    expect(directoryRequests(requests)).toHaveLength(0);
    expect(screen.queryByRole("link", { name: "Teams" })).toBeNull();
  });
});
