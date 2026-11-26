// Registry → Organisations (S07 T6b-1): the directory, searched and filtered through the address, paged by cursor;
// only for whoever holds `organisation.read`, and without a change control for a viewer.
import { act, cleanup, fireEvent, screen, within } from "@testing-library/react";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { academy, kisaasi, listItem, listPage, ntinda, organisationAnswers } from "@/test/organisations";
import { fakeApi, renderWithQuery, type Answers } from "@/test/render";
import TenantWorkspaceLayout from "../layout";
import OrganisationsPage from "./page";

const navigation = vi.hoisted(() => ({ search: "", replace: vi.fn() }));

vi.mock("next/navigation", () => ({
  useParams: () => ({ tenantSlug: "kmsa" }),
  usePathname: () => "/t/kmsa/organisations",
  useSearchParams: () => new URLSearchParams(navigation.search),
  useRouter: () => ({ replace: navigation.replace, push: vi.fn() }),
}));

const viewer = ["organisation.read", "tenant.settings.read"];

function open(actions: string[] = viewer, answers: Answers = {}) {
  const requests = fakeApi({
    ...organisationAnswers(actions),
    "GET /api/admin/v1/organisations": listPage([ntinda, kisaasi, academy].map(listItem)),
    ...answers,
  });
  renderWithQuery(
    <TenantWorkspaceLayout>
      <OrganisationsPage />
    </TenantWorkspaceLayout>,
  );
  return requests;
}

const directoryRequests = (requests: ReturnType<typeof open>) =>
  requests.filter((request) => request.path === "/api/admin/v1/organisations");
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

describe("organisations directory", () => {
  it("lists organisations with their short name, type, place and status, each linking to its page", async () => {
    const requests = open();

    const link = await screen.findByRole("link", { name: "Ntinda Swifts SC" });
    expect(link.getAttribute("href")).toBe(`/t/kmsa/organisations/${ntinda.id}`);
    expect(within(rowOf("Ntinda Swifts SC")).getByText("Ntinda Swifts")).toBeTruthy();
    await within(rowOf("Ntinda Swifts SC")).findAllByText("Kampala, Uganda");
    const cells = within(rowOf("Ntinda Swifts SC")).getAllByRole("cell").map((cell) => cell.textContent);
    expect(cells).toEqual(["Club", "Kampala, Uganda", "Active"]);
    // Without a city, the country alone.
    expect(within(rowOf("Kisaasi Kestrels SC")).getAllByRole("cell")[1]?.textContent).toBe("Uganda");
    expect(within(rowOf("Ntinda Swifts Academy")).getAllByRole("cell")[2]?.textContent).toBe("Suspended");
    // No search or filter: the API's defaults (every status but archived).
    expect(directoryRequests(requests)[0]?.search).toBe("");
    // The navigation's entry, under Registry.
    expect(screen.getAllByRole("link", { name: "Organisations" }).length).toBeGreaterThan(0);
    expect(screen.getAllByText("Registry").length).toBeGreaterThan(0);
  });

  it("asks with the search and filters in the address, and leaves out a type or status the API doesn't know", async () => {
    navigation.search = "q=Ntinda&type=CLUB&status=DISSOLVED";
    const requests = open();

    await screen.findByRole("link", { name: "Ntinda Swifts SC" });
    expect(Object.fromEntries(new URLSearchParams(directoryRequests(requests)[0]!.search))).toEqual({
      q: "Ntinda",
      type: "CLUB",
      status: "DISSOLVED",
    });
    expect((screen.getByLabelText("Search") as HTMLInputElement).value).toBe("Ntinda");
    cleanup();

    navigation.search = "type=GUILD&status=MERGED";
    const second = open();
    await screen.findByRole("link", { name: "Ntinda Swifts SC" });
    expect(directoryRequests(second)[0]?.search).toBe("");
  });

  it("writes the type at once and the search once typing pauses", async () => {
    open();
    await screen.findByRole("link", { name: "Ntinda Swifts SC" });

    fireEvent.change(screen.getByLabelText("Type"), { target: { value: "SCHOOL" } });
    expect(navigation.replace).toHaveBeenLastCalledWith("/t/kmsa/organisations?type=SCHOOL", { scroll: false });

    vi.useFakeTimers();
    fireEvent.change(screen.getByLabelText("Search"), { target: { value: "Kestrels" } });
    expect(navigation.replace).toHaveBeenCalledTimes(1);
    await act(() => vi.advanceTimersByTimeAsync(300));
    expect(navigation.replace).toHaveBeenLastCalledWith("/t/kmsa/organisations?q=Kestrels", { scroll: false });
  });

  it("pages with Show more", async () => {
    const requests = open(viewer, {
      "GET /api/admin/v1/organisations": (request) =>
        request.search.includes("cursor=")
          ? listPage([listItem(academy)])
          : listPage([ntinda, kisaasi].map(listItem), "next-page"),
    });

    fireEvent.click(await screen.findByRole("button", { name: "Show more" }));
    await screen.findByRole("link", { name: "Ntinda Swifts Academy" });
    expect(new URLSearchParams(directoryRequests(requests)[1]!.search).get("cursor")).toBe("next-page");
    expect(screen.queryByRole("button", { name: "Show more" })).toBeNull();
  });

  it("says when nothing matches, and when there is no organisation yet", async () => {
    navigation.search = "q=Ggaba";
    open(viewer, { "GET /api/admin/v1/organisations": listPage([]) });
    expect(await screen.findByText("No organisation matches")).toBeTruthy();
    cleanup();

    navigation.search = "";
    open(viewer, { "GET /api/admin/v1/organisations": listPage([]) });
    expect(await screen.findByText("No organisations yet")).toBeTruthy();
  });

  it("shows a viewer the directory without a change control", async () => {
    open();
    await screen.findByRole("link", { name: "Ntinda Swifts SC" });
    const main = screen.getByRole("heading", { level: 1, name: "Organisations" }).closest("section")!;
    expect(within(main).queryAllByRole("button")).toEqual([]);
  });

  it("refuses whoever doesn't hold organisation.read, without asking for the organisations", async () => {
    const requests = open(["tenant.settings.read"]);
    expect(await screen.findByText("You can't open this page")).toBeTruthy();
    expect(directoryRequests(requests)).toHaveLength(0);
  });
});
