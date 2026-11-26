// Registry → Organisations → Add organisation (S07 T6b-2a): the live duplicate warning compares the names, the type and
// the city; the save waits for "This is a different one" and then sends `duplicatesReviewed`; a save the server sends
// back for review shows its list; the primary venue is found by a search (a closed one can't be chosen); Add sends no
// slug and no country unless one was chosen. The warning's finer rules are the shared hook's (the venue tests).
import { cleanup, fireEvent, screen, waitFor, within } from "@testing-library/react";
import { afterEach, describe, expect, it, vi } from "vitest";
import { readOnlyCapabilities } from "@/test/capabilities";
import { candidate, kisaasi, listPage, ntinda, organisation, organisationAnswers } from "@/test/organisations";
import { fakeApi, problem, renderWithQuery, type Answers, type RecordedRequest } from "@/test/render";
import { bukoto, listItem as venueItem, venue } from "@/test/venues";
import TenantWorkspaceLayout from "../../layout";
import OrganisationsPage from "../page";
import AddOrganisationPage from "./page";

const navigation = vi.hoisted(() => ({ push: vi.fn() }));

vi.mock("next/navigation", () => ({
  useParams: () => ({ tenantSlug: "kmsa" }),
  usePathname: () => "/t/kmsa/organisations/new",
  useSearchParams: () => new URLSearchParams(),
  useRouter: () => ({ replace: vi.fn(), push: navigation.push }),
}));

const reads = ["organisation.read", "venue.read", "tenant.settings.read"];
const registrar = [...reads, "organisation.create", "organisation.duplicate_check"];
const added = organisation({ id: "0199a3f0-0000-7000-8000-0000000000e9", displayName: "Kiwatule Kites FC" });
const closedGround = venue({
  id: "0199a3f0-0000-7000-8000-0000000000b7",
  name: "Kiwatule Old Ground",
  slug: "kiwatule-old-ground",
  status: "CLOSED",
});

function open(answers: Answers = {}, capabilities: Parameters<typeof organisationAnswers>[0] = registrar, page = <AddOrganisationPage />) {
  const requests = fakeApi({
    ...organisationAnswers(capabilities),
    "GET /api/admin/v1/organisations": listPage([]),
    "POST /api/admin/v1/organisations/duplicate-check": { status: 200, body: { candidates: [] } },
    "POST /api/admin/v1/organisations": { status: 201, body: added },
    "GET /api/admin/v1/venues": listPage([venueItem(bukoto), venueItem(closedGround)]),
    ...answers,
  });
  const { queryClient } = renderWithQuery(<TenantWorkspaceLayout>{page}</TenantWorkspaceLayout>);
  return Object.assign(requests, { queryClient });
}

const type = (label: string | RegExp, value: string) =>
  fireEvent.change(screen.getByLabelText(label), { target: { value } });

async function fill(name = "Kiwatule Kites FC") {
  fireEvent.change(await screen.findByLabelText("Name"), { target: { value: name } });
  fireEvent.change(await screen.findByLabelText("Type"), { target: { value: "CLUB" } });
  type("City or town (optional)", "Kampala");
}

const creates = (requests: RecordedRequest[]) =>
  requests.filter((request) => request.method === "POST" && request.path === "/api/admin/v1/organisations");
const checks = (requests: RecordedRequest[]) =>
  requests.filter((request) => request.path === "/api/admin/v1/organisations/duplicate-check");
const save = () => fireEvent.click(screen.getByRole("button", { name: "Add organisation" }));
const panelName = "Is this an organisation you already have?";
const panel = () => screen.getByRole("region", { name: panelName });

afterEach(() => {
  navigation.push.mockReset();
  cleanup();
  vi.unstubAllGlobals();
});

describe("Add organisation", () => {
  it("adds an organisation without a slug, in the tenant's country, with the venue found, and opens it", async () => {
    const requests = open();
    await fill();
    await waitFor(() => expect(checks(requests).length).toBeGreaterThan(0));
    expect(checks(requests).at(-1)!.body).toEqual({
      type: "CLUB",
      displayName: "Kiwatule Kites FC",
      legalName: null,
      shortName: null,
      city: "Kampala",
    });
    type("Founded (optional)", "2004");
    type("Website (optional)", "https://kiwatule-kites.example");
    type("Description (optional)", "A youth club in Kiwatule.\nIt trains on Saturdays.");

    type("Find a venue", "Bukoto");
    fireEvent.click(await screen.findByRole("button", { name: `Choose ${bukoto.name}` }));
    expect(await screen.findByText(bukoto.name)).toBeTruthy();
    expect(document.activeElement).toBe(screen.getByLabelText("Find another venue"));
    save();

    await waitFor(() => expect(creates(requests)).toHaveLength(1));
    expect(creates(requests)[0]!.body).toEqual({
      type: "CLUB",
      displayName: "Kiwatule Kites FC",
      legalName: null,
      shortName: null,
      city: "Kampala",
      foundedYear: 2004,
      website: "https://kiwatule-kites.example",
      email: null,
      phone: null,
      description: "A youth club in Kiwatule.\nIt trains on Saturdays.",
      primaryVenueId: bukoto.id,
      country: null,
      duplicatesReviewed: false,
      duplicateReason: null,
    });
    await waitFor(() => expect(navigation.push).toHaveBeenCalledWith(`/t/kmsa/organisations/${added.id}`));
  });

  it("shows a closed venue but doesn't let it be chosen", async () => {
    open();
    await screen.findByLabelText("Name");
    type("Find a venue", "Kiwatule");
    const choose = await screen.findByRole("button", { name: `Choose ${closedGround.name}` });
    expect(choose).toHaveProperty("disabled", true);
    expect(screen.getByText("Closed: it can't be a primary venue.")).toBeTruthy();
  });

  it("warns of an organisation that may be the same, opens it, or saves once told it is a different one", async () => {
    const requests = open({
      "POST /api/admin/v1/organisations/duplicate-check": {
        status: 200,
        body: {
          candidates: [
            candidate(ntinda, "POSSIBLE", [
              { code: "NAME_SIMILARITY", value: "0.81" },
              { code: "SAME_TYPE", value: null },
              { code: "SAME_CITY", value: null },
            ]),
          ],
        },
      },
    });
    await fill("Ntinda Swift SC");

    const warning = await screen.findByRole("region", { name: panelName });
    expect(within(warning).getByText("Possibly the same")).toBeTruthy();
    expect(within(warning).getByText("Names 81% alike")).toBeTruthy();
    expect(within(warning).getByText("Same type")).toBeTruthy();
    expect(within(warning).getByText("Same city")).toBeTruthy();
    expect(within(warning).getByRole("link", { name: `Open ${ntinda.displayName}` }).getAttribute("href")).toBe(
      `/t/kmsa/organisations/${ntinda.id}`,
    );

    save();
    expect(await within(panel()).findByRole("alert")).toBeTruthy();
    expect(within(panel()).getByRole("alert").textContent).toBe(
      "Open the existing organisation, or say this is a different one.",
    );
    expect(creates(requests)).toHaveLength(0);

    fireEvent.click(within(panel()).getByRole("button", { name: "This is a different one" }));
    fireEvent.change(within(panel()).getByLabelText("Why is it different? (optional)"), {
      target: { value: "A new club in Ntinda's east side." },
    });
    save();
    await waitFor(() => expect(creates(requests)).toHaveLength(1));
    expect(creates(requests)[0]!.body).toMatchObject({
      duplicatesReviewed: true,
      duplicateReason: "A new club in Ntinda's east side.",
    });
  });

  it("asks nothing until there is a type and a name with a letter or digit", async () => {
    const requests = open();
    fireEvent.change(await screen.findByLabelText("Name"), { target: { value: "Kiwatule Kites FC" } });
    await new Promise((resolve) => setTimeout(resolve, 400));
    expect(checks(requests)).toHaveLength(0);
    type("Type", "CLUB");
    await waitFor(() => expect(checks(requests)).toHaveLength(1));
  });

  it("shows the list the server sent back when someone added a matching organisation just before the save", async () => {
    const requests = open({
      "POST /api/admin/v1/organisations": (request) =>
        (request.body as { duplicatesReviewed: boolean }).duplicatesReviewed
          ? { status: 201, body: added }
          : {
              status: 422,
              body: problem("ORGANISATION_DUPLICATE_REVIEW_REQUIRED", 422, {
                errors: [{ field: "duplicatesReviewed", code: "ORGANISATION_DUPLICATE_REVIEW_REQUIRED" }],
                candidates: [candidate(kisaasi)],
              }),
            },
    });
    await fill();
    await waitFor(() => expect(checks(requests).length).toBeGreaterThan(0));
    save();

    const warning = await screen.findByRole("region", { name: panelName });
    expect(within(warning).getByText(kisaasi.displayName)).toBeTruthy();
    expect(within(warning).getByRole("alert").textContent).toContain("Someone added an organisation that may match");
    await waitFor(() => expect(document.activeElement).toBe(warning));
    expect(screen.queryByText("That didn't work")).toBeNull();

    fireEvent.click(within(warning).getByRole("button", { name: "This is a different one" }));
    save();
    await waitFor(() => expect(creates(requests)).toHaveLength(2));
    expect(creates(requests)[1]!.body).toMatchObject({ duplicatesReviewed: true, duplicateReason: null });
  });

  it("marks the duplicate checks it kept stale once an organisation is added", async () => {
    const requests = open();
    await fill();
    await waitFor(() => expect(checks(requests).length).toBeGreaterThan(0));
    save();
    await waitFor(() => expect(navigation.push).toHaveBeenCalled());
    const kept = requests.queryClient.getQueryCache().findAll({ queryKey: ["organisation-duplicates"] });
    expect(kept.length).toBeGreaterThan(0);
    expect(kept.every((query) => query.state.isInvalidated)).toBe(true);
  });

  it("checks the founding year, the website and the email before saving", async () => {
    const requests = open();
    await fill();
    const year = new Date().getFullYear();
    type("Founded (optional)", String(year + 1));
    type("Website (optional)", "kiwatule-kites.example");
    type("Email (optional)", "office at kites");
    save();
    expect(await screen.findByText(`Enter a year from 1800 to ${year}.`)).toBeTruthy();
    expect(screen.getByText("Enter the full address, starting with https:// or http://.")).toBeTruthy();
    expect(screen.getByText("Enter an email address such as office@example.org.")).toBeTruthy();
    expect(creates(requests)).toHaveLength(0);
  });

  it("puts a venue closed since it was found under the venue field, and a refused web address in the alert", async () => {
    open({
      "POST /api/admin/v1/organisations": {
        status: 422,
        body: problem("ORGANISATION_PRIMARY_VENUE_UNAVAILABLE", 422),
      },
    });
    await fill();
    type("Find a venue", "Bukoto");
    fireEvent.click(await screen.findByRole("button", { name: `Choose ${bukoto.name}` }));
    save();
    const venueField = screen.getByRole("group", { name: "Primary venue (optional)" });
    expect(
      await within(venueField).findByText("This venue is closed or archived now. Choose another, or no primary venue."),
    ).toBeTruthy();
    expect(screen.queryByText("That didn't work")).toBeNull();
    cleanup();

    open({
      "POST /api/admin/v1/organisations": {
        status: 422,
        body: problem("ORGANISATION_SLUG_TAKEN", 422, { errors: [{ field: "slug", code: "ORGANISATION_SLUG_TAKEN" }] }),
      },
    });
    await fill();
    save();
    expect(await screen.findByText("That didn't work")).toBeTruthy();
    expect(screen.queryByLabelText("Web address")).toBeNull();
  });

  it("says why no venue can be chosen without access to the venues", async () => {
    open({}, ["organisation.read", "tenant.settings.read", "organisation.create", "organisation.duplicate_check"]);
    await screen.findByLabelText("Name");
    expect(screen.getByText("Choosing a venue needs access to the venues.")).toBeTruthy();
    expect(screen.queryByLabelText("Find a venue")).toBeNull();
  });

  it("is refused to whoever can't add organisations, and disabled with the reason on a read-only tenant", async () => {
    open({}, reads);
    expect(await screen.findByText("You can't open this page")).toBeTruthy();
    cleanup();

    open({}, readOnlyCapabilities(reads, ["organisation.create", "organisation.duplicate_check"]));
    await screen.findByRole("heading", { level: 1, name: "Add organisation" });
    expect((await screen.findAllByText(/read-only/i)).length).toBeGreaterThan(0);
    expect(screen.queryByLabelText("Name")).toBeNull();
  });
});

describe("The directory's Add organisation", () => {
  it("links to the form for whoever may add one", async () => {
    open({}, registrar, <OrganisationsPage />);
    const link = await screen.findByRole("link", { name: "Add organisation" });
    expect(link.getAttribute("href")).toBe("/t/kmsa/organisations/new");
  });

  it("is disabled with the reason on a read-only tenant, and not shown to a viewer", async () => {
    open({}, readOnlyCapabilities(reads, ["organisation.create", "organisation.duplicate_check"]), <OrganisationsPage />);
    const button = await screen.findByRole("button", { name: "Add organisation" });
    expect(button).toHaveProperty("disabled", true);
    const note = document.getElementById(button.getAttribute("aria-describedby")!);
    expect(note?.textContent).toMatch(/read-only/i);
    cleanup();

    open({}, reads, <OrganisationsPage />);
    await screen.findByRole("heading", { level: 1, name: "Organisations" });
    expect(screen.queryByRole("link", { name: "Add organisation" })).toBeNull();
    expect(screen.queryByRole("button", { name: "Add organisation" })).toBeNull();
  });
});
