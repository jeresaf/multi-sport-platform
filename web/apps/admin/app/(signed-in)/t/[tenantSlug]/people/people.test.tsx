// Registry → People (S06 T7): the directory, searched and filtered through the address, paged by cursor, with the
// status and Minor or Age unknown badges; only for whoever holds `person.read`.
import { act, cleanup, fireEvent, screen, within } from "@testing-library/react";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { tenantCapabilities } from "@/test/capabilities";
import { brenda, denis, john, listItem, nakato } from "@/test/people";
import { fakeApi, renderWithQuery, type Answer, type Answers } from "@/test/render";
import TenantWorkspaceLayout from "../layout";
import PeoplePage from "./page";

const navigation = vi.hoisted(() => ({ search: "", replace: vi.fn() }));

vi.mock("next/navigation", () => ({
  useParams: () => ({ tenantSlug: "kmsa" }),
  usePathname: () => "/t/kmsa/people",
  useSearchParams: () => new URLSearchParams(navigation.search),
  useRouter: () => ({ replace: navigation.replace, push: vi.fn() }),
}));

const tenantId = "0199a3f0-0000-7000-8000-00000000000a";
const me: Answer = {
  status: 200,
  body: {
    id: "0199a3f0-0000-7000-8000-000000000001",
    email: "brian.ssemakula@kmsa.example",
    platformRoles: [],
    tenants: [
      { tenantId, slug: "kmsa", displayName: "Kampala Metro Sports Association", status: "ACTIVE", roles: ["REGISTRAR"] },
    ],
  },
};

const reads = ["person.read", "tenant.sport.read"];
const page = (items: unknown[], nextCursor: string | null = null) => ({
  status: 200,
  body: { items, page: { nextCursor, hasMore: nextCursor !== null } },
});

const tenantSport = (variant: string, name: string, enabled: boolean) => ({
  variant,
  sport: variant.split("_")[0],
  name,
  scoringFamily: "ADDITIVE",
  enabled,
  enabledSince: enabled ? "2026-11-01T07:00:00Z" : null,
});

function open(actions = reads, answers: Answers = {}) {
  const requests = fakeApi({
    "GET /api/account/v1/me": me,
    "GET /api/admin/v1/me/capabilities": { status: 200, body: tenantCapabilities(actions) },
    "GET /api/public/v1/reference/countries": page([{ code: "UG", alpha3Code: "UGA", numericCode: "800", name: "Uganda" }]),
    "GET /api/admin/v1/tenant/sports": {
      status: 200,
      body: {
        variants: [tenantSport("RINGBALL_SEVENS", "Ringball sevens", true), tenantSport("HOOPBALL_FIVES", "Hoopball fives", false)],
        sportQuota: null,
      },
    },
    "GET /api/admin/v1/people": page([john, brenda, nakato].map(listItem)),
    ...answers,
  });
  renderWithQuery(
    <TenantWorkspaceLayout>
      <PeoplePage />
    </TenantWorkspaceLayout>,
  );
  return requests;
}

const peopleRequests = (requests: ReturnType<typeof open>) =>
  requests.filter((request) => request.path === "/api/admin/v1/people");

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

describe("people directory", () => {
  it("lists people with their birth year, nationality, status and age badge, each linking to their page", async () => {
    const requests = open();

    const link = await screen.findByRole("link", { name: "Brenda Akello" });
    expect(link.getAttribute("href")).toBe(`/t/kmsa/people/${brenda.id}`);
    expect(within(rowOf("Brenda Akello")).getByText("Minor")).toBeTruthy();
    expect(within(rowOf("Nakato Irene Nalubega Namaganda")).getByText("Age unknown")).toBeTruthy();
    // An adult has no age badge.
    expect(within(rowOf("John Ssekandi")).queryByText("Minor")).toBeNull();
    expect(within(rowOf("John Ssekandi")).queryByText("Age unknown")).toBeNull();
    // The wide columns, and the same values stacked under the name for a phone.
    const cells = within(rowOf("John Ssekandi")).getAllByRole("cell").map((cell) => cell.textContent);
    expect(cells).toEqual(["1998", "Uganda", "Active"]);
    expect(within(rowOf("Nakato Irene Nalubega Namaganda")).getAllByText("Not recorded").length).toBeGreaterThan(0);
    expect(within(rowOf("John Ssekandi")).getByText("Born:")).toBeTruthy();
    // No search or filter: the API's defaults (every state but archived).
    expect(peopleRequests(requests)[0]?.search).toBe("");
    // The navigation's entry, under its group.
    expect(screen.getAllByRole("link", { name: "People" }).length).toBeGreaterThan(0);
    expect(screen.getAllByText("Registry").length).toBeGreaterThan(0);
  });

  it("asks with the search and filters in the address", async () => {
    navigation.search = "q=Ssekandi&status=ARCHIVED&ageStatus=MINOR&profile=ATHLETE&variant=RINGBALL_SEVENS&status2=x";
    const requests = open();

    await screen.findByRole("link", { name: "John Ssekandi" });
    const query = new URLSearchParams(peopleRequests(requests)[0]!.search);
    expect(Object.fromEntries(query)).toEqual({
      q: "Ssekandi",
      status: "ARCHIVED",
      ageStatus: "MINOR",
      profile: "ATHLETE",
      variant: "RINGBALL_SEVENS",
    });
    expect((screen.getByLabelText("Search") as HTMLInputElement).value).toBe("Ssekandi");
    expect((screen.getByLabelText("Status") as HTMLSelectElement).value).toBe("ARCHIVED");
  });

  it("leaves out a filter value the API doesn't know", async () => {
    navigation.search = "status=GONE&profile=COACH";
    const requests = open();

    await screen.findByRole("link", { name: "John Ssekandi" });
    expect(peopleRequests(requests)[0]?.search).toBe("");
  });

  it("puts a filter in the address at once, and the search once the typing pauses", async () => {
    open();
    await screen.findByRole("link", { name: "John Ssekandi" });

    fireEvent.change(screen.getByLabelText("Age"), { target: { value: "MINOR" } });
    expect(navigation.replace).toHaveBeenLastCalledWith("/t/kmsa/people?ageStatus=MINOR", { scroll: false });

    vi.useFakeTimers();
    fireEvent.change(screen.getByLabelText("Search"), { target: { value: "Ssekandi John " } });
    navigation.replace.mockClear();
    act(() => vi.advanceTimersByTime(200));
    expect(navigation.replace).not.toHaveBeenCalled();
    act(() => vi.advanceTimersByTime(100));
    expect(navigation.replace).toHaveBeenLastCalledWith("/t/kmsa/people?q=Ssekandi+John", { scroll: false });
  });

  it("offers the tenant's enabled sports only", async () => {
    open();
    await screen.findByRole("link", { name: "John Ssekandi" });

    const options = within(screen.getByLabelText("Sport")).getAllByRole("option").map((option) => option.textContent);
    expect(options).toEqual(["Any sport", "Ringball sevens"]);
  });

  it("shows more with the next page's cursor", async () => {
    const requests = open(reads, {
      "GET /api/admin/v1/people": (request) =>
        request.search.includes("cursor=next-page") ? page([listItem(denis)]) : page([john, brenda].map(listItem), "next-page"),
    });

    fireEvent.click(await screen.findByRole("button", { name: "Show more" }));
    expect(await screen.findByRole("link", { name: "Denis Opoka" })).toBeTruthy();
    expect(screen.getByRole("link", { name: "John Ssekandi" })).toBeTruthy();
    expect(new URLSearchParams(peopleRequests(requests)[1]!.search).get("cursor")).toBe("next-page");
    expect(screen.queryByRole("button", { name: "Show more" })).toBeNull();
  });

  it("says when there is no one yet, or no one matches", async () => {
    open(reads, { "GET /api/admin/v1/people": page([]) });
    expect(await screen.findByText("No people yet")).toBeTruthy();
    cleanup();

    navigation.search = "q=Nobody";
    open(reads, { "GET /api/admin/v1/people": page([]) });
    expect(await screen.findByText("No one matches")).toBeTruthy();
  });

  it("is refused without person.read, and doesn't ask the API", async () => {
    const requests = open(["tenant.sport.read"]);

    expect(await screen.findByRole("heading", { name: "You can't open this page" })).toBeTruthy();
    expect(peopleRequests(requests)).toEqual([]);
    expect(screen.queryByRole("link", { name: "People" })).toBeNull();
  });
});
