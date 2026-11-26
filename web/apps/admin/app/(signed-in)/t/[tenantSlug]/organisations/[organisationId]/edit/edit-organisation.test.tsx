// Registry → Organisations → an organisation → Edit details (S07 T6b-2a): the PUT replaces every detail from the
// version the page opened on, with the country and the web address; only a changed name, type or city runs the warning
// (owner, T6b plan), which leaves the organisation itself out; a conflict says "This record changed since you opened
// it" and offers Reload; the primary venue it has stays, whatever its status, until removed; a viewer sees no Edit
// details.
import { act, cleanup, fireEvent, screen, waitFor, within } from "@testing-library/react";
import { afterEach, describe, expect, it, vi } from "vitest";
import { readOnlyCapabilities } from "@/test/capabilities";
import { candidate, kisaasi, listPage, ntinda, organisationAnswers } from "@/test/organisations";
import { fakeApi, problem, renderWithQuery, type Answers, type RecordedRequest } from "@/test/render";
import { bukoto, listItem as venueItem } from "@/test/venues";
import TenantWorkspaceLayout from "../../../layout";
import OrganisationPage from "../page";
import EditOrganisationPage from "./page";

const navigation = vi.hoisted(() => ({ push: vi.fn() }));

vi.mock("next/navigation", () => ({
  useParams: () => ({ tenantSlug: "kmsa", organisationId: "0199a3f0-0000-7000-8000-0000000000e1" }),
  usePathname: () => "/t/kmsa/organisations/0199a3f0-0000-7000-8000-0000000000e1/edit",
  useSearchParams: () => new URLSearchParams(),
  useRouter: () => ({ replace: vi.fn(), push: navigation.push }),
}));

const reads = ["organisation.read", "venue.read", "team.read", "sport.read", "tenant.settings.read"];
const registrar = [...reads, "organisation.update", "organisation.duplicate_check"];
const organisationPath = `/api/admin/v1/organisations/${ntinda.id}`;

function open(answers: Answers = {}, capabilities: Parameters<typeof organisationAnswers>[0] = registrar, page = <EditOrganisationPage />) {
  const requests = fakeApi({
    ...organisationAnswers(capabilities),
    [`GET ${organisationPath}`]: { status: 200, body: ntinda },
    [`GET ${organisationPath}/status-changes`]: { status: 200, body: { items: [] } },
    "POST /api/admin/v1/organisations/duplicate-check": { status: 200, body: { candidates: [] } },
    [`PUT ${organisationPath}`]: (request) => ({
      status: 200,
      body: { ...ntinda, ...(request.body as object), version: 4 },
    }),
    ...answers,
  });
  renderWithQuery(<TenantWorkspaceLayout>{page}</TenantWorkspaceLayout>);
  return requests;
}

const field = (label: string) => screen.findByLabelText(label);
const type = async (label: string, value: string) => fireEvent.change(await field(label), { target: { value } });
const save = () => fireEvent.click(screen.getByRole("button", { name: "Save" }));
const puts = (requests: RecordedRequest[]) => requests.filter((request) => request.method === "PUT");
const checks = (requests: RecordedRequest[]) =>
  requests.filter((request) => request.path === "/api/admin/v1/organisations/duplicate-check");

afterEach(() => {
  navigation.push.mockReset();
  cleanup();
  vi.useRealTimers();
  vi.unstubAllGlobals();
});

describe("Edit organisation details", () => {
  it("saves every detail from the version it opened on, with the country, web address and primary venue", async () => {
    const requests = open();
    await type("Phone (optional)", "+256 700 000 102");
    save();

    await waitFor(() => expect(puts(requests)).toHaveLength(1));
    const sent = puts(requests)[0]!;
    expect(sent.headers.get("If-Match")).toBe('"3"');
    expect(sent.body).toEqual({
      type: "CLUB",
      displayName: "Ntinda Swifts SC",
      legalName: "Ntinda Swifts Sports Club Limited",
      shortName: "Ntinda Swifts",
      city: "Kampala",
      foundedYear: 1998,
      website: "https://ntinda-swifts.example",
      email: "secretary@ntinda-swifts.example",
      phone: "+256 700 000 102",
      description: "A multi-sport community club in Ntinda.",
      primaryVenueId: ntinda.primaryVenue!.id,
      country: "UG",
      slug: "ntinda-swifts-sc",
      duplicatesReviewed: false,
      duplicateReason: null,
    });
    // Nothing the warning compares changed: it never asked.
    expect(checks(requests)).toHaveLength(0);
    await waitFor(() => expect(navigation.push).toHaveBeenCalledWith(`/t/kmsa/organisations/${ntinda.id}`));
  });

  it("warns about other organisations once a compared field changes, never about the organisation itself", async () => {
    const requests = open({
      "POST /api/admin/v1/organisations/duplicate-check": {
        status: 200,
        body: { candidates: [candidate(ntinda), candidate(kisaasi, "POSSIBLE")] },
      },
    });
    await type("Short name (optional)", "Kestrels");
    const warning = await screen.findByRole("region", { name: "Is this an organisation you already have?" });
    expect(within(warning).getByText(kisaasi.displayName)).toBeTruthy();
    expect(within(warning).queryByText(ntinda.displayName)).toBeNull();
    expect(checks(requests).at(-1)!.body).toEqual({
      type: "CLUB",
      displayName: "Ntinda Swifts SC",
      legalName: "Ntinda Swifts Sports Club Limited",
      shortName: "Kestrels",
      city: "Kampala",
    });

    fireEvent.click(within(warning).getByRole("button", { name: "This is a different one" }));
    save();
    await waitFor(() => expect(puts(requests)).toHaveLength(1));
    expect(puts(requests)[0]!.body).toMatchObject({ shortName: "Kestrels", duplicatesReviewed: true });
  });

  it("checks a change of type too, but not a change of the description", async () => {
    vi.useFakeTimers({ shouldAdvanceTime: true });
    const requests = open();
    await type("Description (optional)", "A community club.");
    // Past the warning's 300 ms pause: the clock is moved on, not waited for.
    await act(() => vi.advanceTimersByTimeAsync(400));
    expect(checks(requests)).toHaveLength(0);
    await type("Type", "ACADEMY");
    await waitFor(() => expect(checks(requests)).toHaveLength(1));
    expect(checks(requests)[0]!.body).toMatchObject({ type: "ACADEMY" });
  });

  it("says the organisation changed since it was opened; Reload starts over from it as it is now", async () => {
    let current = ntinda;
    let conflict = true;
    const requests = open({
      [`GET ${organisationPath}`]: () => ({ status: 200, body: current }),
      [`PUT ${organisationPath}`]: (request) =>
        conflict
          ? { status: 409, body: problem("ORGANISATION_CONCURRENCY_CONFLICT", 409) }
          : { status: 200, body: { ...current, ...(request.body as object), version: 5 } },
    });
    await type("Phone (optional)", "+256 700 000 102");
    save();
    expect(await screen.findByText("This record changed since you opened it")).toBeTruthy();

    current = { ...ntinda, phone: "+256 700 000 199", version: 4 };
    conflict = false;
    fireEvent.click(screen.getByRole("button", { name: "Reload" }));
    await waitFor(async () => expect(await field("Phone (optional)")).toHaveProperty("value", "+256 700 000 199"));
    await type("Founded (optional)", "1999");
    save();
    await waitFor(() => expect(puts(requests)).toHaveLength(2));
    const second = puts(requests)[1]!;
    expect(second.headers.get("If-Match")).toBe('"4"');
    expect(second.body).toMatchObject({ phone: "+256 700 000 199", foundedYear: 1999 });
  });

  it("keeps the primary venue it has until it is removed, then sends none", async () => {
    const requests = open();
    const venueField = await screen.findByRole("group", { name: "Primary venue (optional)" });
    expect(within(venueField).getByText(ntinda.primaryVenue!.name)).toBeTruthy();
    fireEvent.click(within(venueField).getByRole("button", { name: "No primary venue" }));
    expect(within(venueField).getByText("None chosen.")).toBeTruthy();
    expect(document.activeElement).toBe(screen.getByLabelText("Find a venue"));
    save();
    await waitFor(() => expect(puts(requests)).toHaveLength(1));
    expect(puts(requests)[0]!.body).toMatchObject({ primaryVenueId: null });
  });

  it("without access to the venues, still removes the primary venue, announced, with the focus kept in the field", async () => {
    const requests = open({}, ["organisation.read", "tenant.settings.read", "organisation.update", "organisation.duplicate_check"]);
    const venueField = await screen.findByRole("group", { name: "Primary venue (optional)" });
    expect(within(venueField).getByText("Choosing a venue needs access to the venues.")).toBeTruthy();
    fireEvent.click(within(venueField).getByRole("button", { name: "No primary venue" }));
    expect(within(venueField).getByRole("status").textContent).toBe("No primary venue.");
    expect(document.activeElement?.textContent).toBe("None chosen.");
    save();
    await waitFor(() => expect(puts(requests)).toHaveLength(1));
    expect(puts(requests)[0]!.body).toMatchObject({ primaryVenueId: null });
  });

  it("saves an email the server took although a stricter check would refuse it (an intranet address)", async () => {
    const requests = open({ [`GET ${organisationPath}`]: { status: 200, body: { ...ntinda, email: "ofisi@kmsa" } } });
    await type("Phone (optional)", "+256 700 000 103");
    save();
    await waitFor(() => expect(puts(requests)).toHaveLength(1));
    expect(puts(requests)[0]!.body).toMatchObject({ email: "ofisi@kmsa" });
  });

  it("lets the saved venue, closed since, be chosen again after it was removed", async () => {
    const closed = { ...venueItem(bukoto), status: "CLOSED" as const };
    open({ "GET /api/admin/v1/venues": listPage([closed]) });
    const venueField = await screen.findByRole("group", { name: "Primary venue (optional)" });
    fireEvent.click(within(venueField).getByRole("button", { name: "No primary venue" }));
    await type("Find a venue", "Bukoto");
    const choose = await within(venueField).findByRole("button", { name: `Choose ${bukoto.name}` });
    expect(choose).toHaveProperty("disabled", false);
    fireEvent.click(choose);
    expect(within(venueField).getByText(bukoto.name)).toBeTruthy();
  });

  it("shows a taken web address under it", async () => {
    open({
      [`PUT ${organisationPath}`]: {
        status: 422,
        body: problem("ORGANISATION_SLUG_TAKEN", 422, {
          detail: "Another organisation has this web address.",
          errors: [{ field: "slug", code: "ORGANISATION_SLUG_TAKEN" }],
        }),
      },
    });
    await type("Web address", "Ntinda Swifts");
    save();
    expect(await screen.findByText("Use lower-case letters and digits, with single hyphens between words.")).toBeTruthy();
    await type("Web address", "kisaasi-kestrels-sc");
    save();
    expect(await screen.findByText("Another organisation has this web address.")).toBeTruthy();
    expect((await field("Web address")).getAttribute("aria-invalid")).toBe("true");
  });

  it("isn't offered to a viewer: no Edit details in the header, and the page refuses them", async () => {
    open({}, reads, <OrganisationPage />);
    await screen.findByRole("heading", { level: 1, name: ntinda.displayName });
    expect(screen.queryByRole("link", { name: "Edit details" })).toBeNull();
    expect(screen.queryByRole("button", { name: "Edit details" })).toBeNull();
    cleanup();

    open({}, reads);
    expect(await screen.findByText("You can't open this page")).toBeTruthy();
  });

  it("offers Edit details in the header to whoever may change it, disabled with the reason on a read-only tenant", async () => {
    open({}, registrar, <OrganisationPage />);
    const link = await screen.findByRole("link", { name: "Edit details" });
    expect(link.getAttribute("href")).toBe(`/t/kmsa/organisations/${ntinda.id}/edit`);
    cleanup();

    open({}, readOnlyCapabilities(reads, ["organisation.update", "organisation.duplicate_check"]), <OrganisationPage />);
    const button = await screen.findByRole("button", { name: "Edit details" });
    expect(button).toHaveProperty("disabled", true);
    expect(document.getElementById(button.getAttribute("aria-describedby")!)?.textContent).toMatch(/read-only/i);
  });
});
