// Registry → People → a person (S06 T7): the header and the tabs, read-only. Without `person.read_private` the page
// shows the birth year, not the date, and the consent scopes without the guardian or the evidence, and never asks the
// private read; positions and roles are named by the definition (the test-only one, so no sport word is ours). The tab
// is the address's (`?tab=`): the tests open the page on it.
import { cleanup, fireEvent, screen, waitFor, within } from "@testing-library/react";
import { QueryClientProvider } from "@tanstack/react-query";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { brenda, brendaPrivate, denis, john, nakato, person } from "@/test/people";
import { personPageAnswers } from "@/test/person-page";
import { fakeApi, problem, renderWithQuery, type Answers } from "@/test/render";
import TenantWorkspaceLayout from "../../layout";
import { untilExpiry } from "../people-data";
import PersonPage from "./page";

const navigation = vi.hoisted(() => ({ personId: "", search: "", replace: vi.fn() }));

vi.mock("next/navigation", () => ({
  useParams: () => ({ tenantSlug: "kmsa", personId: navigation.personId }),
  usePathname: () => `/t/kmsa/people/${navigation.personId}`,
  useSearchParams: () => new URLSearchParams(navigation.search),
  useRouter: () => ({ replace: navigation.replace, push: vi.fn() }),
}));

const viewer = ["person.read", "sport.read", "tenant.settings.read"];
const registrar = [...viewer, "person.read_private"];

function open(shown: ReturnType<typeof person>, actions = viewer, answers: Answers = {}, search = "") {
  navigation.personId = shown.id;
  navigation.search = search;
  const requests = fakeApi({
    ...personPageAnswers(shown, actions),
    [`GET /api/admin/v1/people/${brenda.id}/private`]: { status: 200, body: brendaPrivate },
    ...answers,
  });
  rendered = renderWithQuery(page());
  return requests;
}

const page = () => (
  <TenantWorkspaceLayout>
    <PersonPage />
  </TenantWorkspaceLayout>
);

// The last render, so a test can render again on another tab (the mocked `useSearchParams` reads the address).
let rendered: ReturnType<typeof renderWithQuery>;
const showTab = (tab: string) => {
  navigation.search = `tab=${tab}`;
  rendered.rerender(<QueryClientProvider client={rendered.queryClient}>{page()}</QueryClientProvider>);
};

const openTab = (name: string) => fireEvent.mouseDown(screen.getByRole("tab", { name }));
const fact = (label: string) => screen.getByText(label, { selector: "dt" }).nextElementSibling?.textContent;
const privateRequests = (requests: ReturnType<typeof open>) => requests.filter((request) => request.path.endsWith("/private"));

beforeEach(() => {
  navigation.replace.mockReset();
});

afterEach(() => {
  cleanup();
  vi.unstubAllGlobals();
});

describe("person page", () => {
  it("shows the header: photo, display name, status, Minor, public profile, and a way back to People", async () => {
    open(brenda);

    const heading = await screen.findByRole("heading", { level: 1, name: "Brenda Akello" });
    const header = heading.closest("header")!;
    expect(within(header).getByRole("img", { name: "Photo of Brenda Akello" }).getAttribute("src")).toBe(
      "/api/public/v1/media/medium-token",
    );
    expect(within(header).getByText("Active")).toBeTruthy();
    expect(within(header).getByText("Minor")).toBeTruthy();
    expect(within(header).getByText("Public profile off")).toBeTruthy();
    expect(within(header).getByRole("link", { name: "People" }).getAttribute("href")).toBe("/t/kmsa/people");
    expect(screen.getByRole("tab", { name: "Overview", selected: true })).toBeTruthy();
  });

  it("without person.read_private: the birth year, the consent scopes, no guardian or evidence, no private read", async () => {
    const requests = open(brenda);

    await screen.findByRole("heading", { level: 1, name: "Brenda Akello" });
    expect(fact("Born")).toBe("2011");
    expect(screen.queryByText("Date of birth")).toBeNull();
    expect(screen.queryByText(/14 Mar 2011/)).toBeNull();
    cleanup();

    expect(privateRequests(requests)).toEqual([]);
    const consentRequests = open(brenda, viewer, {}, "tab=consent");
    expect(await screen.findByText("What is agreed now")).toBeTruthy();
    expect(screen.getAllByText("Data processing, Public profile").length).toBeGreaterThan(0);
    expect(screen.getByText("Data processing, Public profile, Photo")).toBeTruthy();
    expect(screen.getByText("Her mother asked for the photo to come down")).toBeTruthy();
    expect(screen.getAllByText("On file")).toHaveLength(2);
    expect(screen.queryByText("Guardian")).toBeNull();
    expect(screen.queryByText("Grace Akello")).toBeNull();
    expect(screen.queryByText("+256 772 000 147")).toBeNull();
    expect(screen.queryByRole("link", { name: /signed form/ })).toBeNull();
    expect(privateRequests(consentRequests)).toEqual([]);
  });

  it("with person.read_private: the date of birth, the guardian and the evidence to download", async () => {
    const requests = open(brenda, registrar);

    await screen.findByText("Date of birth");
    expect(fact("Date of birth")).toBe("14 Mar 2011");
    expect(screen.queryByText("Born")).toBeNull();
    expect(privateRequests(requests)).toHaveLength(1);
    cleanup();

    const consentRequests = open(brenda, registrar, {}, "tab=consent");
    expect((await screen.findAllByText("Grace Akello")).length).toBe(2);
    expect(screen.getAllByText("Mother").length).toBe(2);
    const download = screen.getAllByRole("link", { name: "Download the signed form" })[0]!;
    expect(download.getAttribute("href")).toBe("/api/public/v1/media/evidence-token");
    expect(download.hasAttribute("download")).toBe(true);
    expect(privateRequests(consentRequests)).toHaveLength(1);
  });

  it("asks the audited private read only for Overview and Consent, the tabs that show it", async () => {
    for (const tab of ["roles", "identifiers"]) {
      const requests = open(brenda, registrar, {}, `tab=${tab}`);
      // The private read would be asked with the person's, before the page shows a tab.
      expect(await screen.findByRole("tab", { selected: true })).toBeTruthy();
      expect(privateRequests(requests)).toEqual([]);
      cleanup();
    }
  });

  it("reads the private details once per visit, however often the tabs change", async () => {
    const requests = open(brenda, registrar, {}, "tab=overview");
    expect(await screen.findByText("14 Mar 2011")).toBeTruthy();

    showTab("roles");
    expect(await screen.findByRole("tab", { name: "Roles", selected: true })).toBeTruthy();
    showTab("overview");
    expect(await screen.findByText("14 Mar 2011")).toBeTruthy();
    showTab("consent");
    expect((await screen.findAllByText("Grace Akello")).length).toBe(2);
    expect(privateRequests(requests)).toHaveLength(1);
  });

  it("names positions and roles in the definition's words, with every profile's status", async () => {
    open(denis, viewer, {}, "tab=roles");

    expect(await screen.findByRole("heading", { level: 4, name: "Ringball sevens" })).toBeTruthy();
    expect(await screen.findByText("Wing ringer")).toBeTruthy();
    expect(screen.getByText("Hoop guard")).toBeTruthy();
    expect(screen.getByText("Left")).toBeTruthy();
    expect(screen.getByText("Denis Opoka (the display name)")).toBeTruthy();
    expect(screen.getByText("Head coach")).toBeTruthy();
    expect(screen.getAllByText("Level 1 coaching certificate").length).toBeGreaterThan(0);
    expect(screen.getByText("Ring judge")).toBeTruthy();
    expect(screen.getAllByText("Expired").length).toBeGreaterThan(0);
    const official = screen.getByRole("heading", { level: 4, name: "Official profile" }).closest("article")!;
    expect(within(official).getByText("Inactive")).toBeTruthy();
    // An adult with no consent: no Consent tab.
    expect(screen.queryByRole("tab", { name: "Consent" })).toBeNull();
  });

  it("lists external identifiers, ended ones included, and aliases", async () => {
    open(denis, viewer, {}, "tab=identifiers");

    expect(await screen.findByText("KM-2026-0147")).toBeTruthy();
    expect(screen.getAllByText("From 15 Jan 2026").length).toBeGreaterThan(0);
    expect(screen.getAllByText("1 Feb 2019 to 31 Dec 2025 (ended)").length).toBeGreaterThan(0);
    expect(screen.getByText("Opoka D.")).toBeTruthy();
    expect(screen.getAllByText("Another spelling").length).toBeGreaterThan(0);
  });

  it("shows initials without a photo, four name parts in full, and says when there is no role", async () => {
    open(nakato, viewer, {}, "tab=roles");

    const heading = await screen.findByRole("heading", { level: 1, name: "Nakato Irene Nalubega Namaganda" });
    expect(heading.className).toContain("break-words");
    const photo = screen.getByRole("img", { name: "No photo of Nakato Irene Nalubega Namaganda" });
    expect(photo.textContent).toBe("NN");
    expect(screen.getByText("Age unknown")).toBeTruthy();
    expect(screen.getByText("No roles yet")).toBeTruthy();
    // Not known to be an adult: the Consent tab is offered though nothing is recorded.
    expect(screen.getByRole("tab", { name: "Consent" })).toBeTruthy();
    cleanup();

    open(nakato, viewer, {}, "tab=consent");
    expect(await screen.findByText("No consent recorded yet.")).toBeTruthy();
    expect(screen.getByText(/Without a date of birth this person is protected as a minor/)).toBeTruthy();
  });

  it("shows the biodata and the status history in the tenant's time zone", async () => {
    open(john);

    await screen.findByRole("heading", { level: 1, name: "John Ssekandi" });
    await waitFor(() => expect(fact("Nationality")).toBe("Uganda"));
    expect(fact("Height")).toBe("178 cm");
    expect(fact("Weight")).toBe("72.5 kg");
    expect(fact("Middle name")).toBe("None");
    expect(await screen.findByText("Recorded as active")).toBeTruthy();
    // 21:30 UTC on 2 Nov is 00:30 on 3 Nov in Kampala.
    expect(screen.getAllByText("3 Nov 2026, 00:30").length).toBeGreaterThan(0);
  });

  it("opens the tab the address names, and puts a chosen tab in the address", async () => {
    open(john, viewer, {}, "tab=identifiers");

    expect(await screen.findByRole("tab", { name: "External IDs & aliases", selected: true })).toBeTruthy();
    openTab("Roles");
    expect(navigation.replace).toHaveBeenLastCalledWith(`/t/kmsa/people/${john.id}?tab=roles`, { scroll: false });
  });

  it("says Page not found for a person the tenant doesn't have", async () => {
    open(john, viewer, { [`GET /api/admin/v1/people/${john.id}`]: { status: 404, body: problem("PERSON_NOT_FOUND", 404) } });

    expect(await screen.findByRole("heading", { level: 1, name: "Page not found" })).toBeTruthy();
  });

  it("is refused without person.read, and doesn't ask the API", async () => {
    const requests = open(john, ["sport.read"]);

    expect(await screen.findByRole("heading", { name: "You can't open this page" })).toBeTruthy();
    expect(requests.some((request) => request.path.startsWith("/api/admin/v1/people"))).toBe(false);
  });
});

describe("signed URLs", () => {
  it("are read again a minute before the first one expires", () => {
    const now = Date.parse("2026-11-20T10:00:00Z");
    expect(untilExpiry(["2026-11-20T10:10:00Z", "2026-11-20T10:05:00Z"], now)).toBe(4 * 60 * 1000);
    expect(untilExpiry([null, undefined], now)).toBe(false);
    // Already due: soon, not in a tight loop.
    expect(untilExpiry(["2026-11-20T10:00:30Z"], now)).toBe(1000);
  });
});
