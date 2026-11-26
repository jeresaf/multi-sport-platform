// Registry → Teams → Add team (S07 T6c-2a): the sport picker lists only the tenant's enabled sports, named from their
// definitions (none enabled: nothing to add yet); the category reads as Q53 says; the live duplicate warning compares
// the names, sport, category and organisation, and the save waits for "This is a different one"; a save the server
// sends back for review shows its list; the organisation and home venue are found by a search (a dissolved
// organisation and a closed venue can't be chosen; a venue that doesn't list the sport is noted); the colours go in
// order; the server's refusals land under their fields; opened from an organisation, it starts with it chosen.
import { act, cleanup, fireEvent, screen, waitFor, within } from "@testing-library/react";
import { afterEach, describe, expect, it, vi } from "vitest";
import { readOnlyCapabilities } from "@/test/capabilities";
import { listItem as organisationItem, ntinda, organisation, seeta } from "@/test/organisations";
import { fakeApi, problem, renderWithQuery, type Answers, type RecordedRequest } from "@/test/render";
import { candidate, listPage, meteors, sparrows, team, teamAnswers, tenantSport, tenantSports } from "@/test/teams";
import { bukoto, kisaasi as kisaasiGround, listItem as venueItem, venue } from "@/test/venues";
import TenantWorkspaceLayout from "../../layout";
import TeamsPage from "../page";
import AddTeamPage from "./page";

const navigation = vi.hoisted(() => ({ push: vi.fn(), search: "" }));

vi.mock("next/navigation", () => ({
  useParams: () => ({ tenantSlug: "kmsa" }),
  usePathname: () => "/t/kmsa/teams/new",
  useSearchParams: () => new URLSearchParams(navigation.search),
  useRouter: () => ({ replace: vi.fn(), push: navigation.push }),
}));

const reads = ["team.read", "organisation.read", "venue.read", "sport.read", "tenant.sport.read", "tenant.settings.read"];
const registrar = [...reads, "team.create", "team.duplicate_check"];
const added = team({ id: "0199a3f0-0000-7000-8000-0000000000fa", name: "Kiwatule Kites FC", formerNames: [] });
const dissolved = organisation({
  id: "0199a3f0-0000-7000-8000-0000000000e8",
  displayName: "Kiwatule Old Boys FC",
  status: "DISSOLVED",
});
const closedGround = venue({
  id: "0199a3f0-0000-7000-8000-0000000000b7",
  name: "Kiwatule Old Ground",
  slug: "kiwatule-old-ground",
  status: "CLOSED",
});
// Two enabled sports and one that isn't: the picker lists the first two only.
const sports = tenantSports([
  tenantSport("RINGBALL_SEVENS", "Ringball sevens"),
  tenantSport("RINGBALL_TENS", "Ringball tens"),
  tenantSport("RINGBALL_FIVES", "Ringball fives", false),
]);

function open(answers: Answers = {}, capabilities: Parameters<typeof teamAnswers>[0] = registrar, page = <AddTeamPage />) {
  const requests = fakeApi({
    ...teamAnswers(capabilities),
    "GET /api/admin/v1/tenant/sports": sports,
    "GET /api/admin/v1/teams": listPage([]),
    "POST /api/admin/v1/teams/duplicate-check": { status: 200, body: { candidates: [] } },
    "POST /api/admin/v1/teams": { status: 201, body: added },
    "GET /api/admin/v1/organisations": listPage([organisationItem(ntinda), organisationItem(dissolved)]),
    "GET /api/admin/v1/venues": listPage([venueItem(bukoto), venueItem(kisaasiGround), venueItem(closedGround)]),
    ...answers,
  });
  const { queryClient } = renderWithQuery(<TenantWorkspaceLayout>{page}</TenantWorkspaceLayout>);
  return Object.assign(requests, { queryClient });
}

const type = (label: string | RegExp, value: string) =>
  fireEvent.change(screen.getByLabelText(label), { target: { value } });

async function fill(name = "Kiwatule Kites FC") {
  fireEvent.change(await screen.findByLabelText("Sport"), { target: { value: "RINGBALL_SEVENS" } });
  type("Name", name);
  type("Age group", "SENIOR");
  type("Gender", "MEN");
}

const creates = (requests: RecordedRequest[]) =>
  requests.filter((request) => request.method === "POST" && request.path === "/api/admin/v1/teams");
const checks = (requests: RecordedRequest[]) =>
  requests.filter((request) => request.path === "/api/admin/v1/teams/duplicate-check");
const save = () => fireEvent.click(screen.getByRole("button", { name: "Add team" }));
const panelName = "Is this a team you already have?";
const panel = () => screen.getByRole("region", { name: panelName });
const options = (label: string) =>
  within(screen.getByLabelText(label)).getAllByRole("option").map((option) => option.textContent);

afterEach(() => {
  navigation.push.mockReset();
  navigation.search = "";
  cleanup();
  vi.useRealTimers();
  vi.unstubAllGlobals();
});

describe("Add team", () => {
  it("adds a team in an enabled sport with its organisation, home venue and colours, and opens it", async () => {
    const requests = open();
    await screen.findByLabelText("Sport");
    // Only the enabled sports, named from their definitions (AR-29).
    expect(options("Sport")).toEqual(["Choose…", "Ringball sevens", "Ringball tens"]);
    await fill();
    type("Short name (optional)", "Kites");
    type("Code (optional)", "kik");
    type("Level", "RESERVE");
    type("Founded (optional)", "2004");
    type("Name used since (optional)", "2020-03-01");

    type("Find an organisation", "Ntinda");
    fireEvent.click(await screen.findByRole("button", { name: `Choose ${ntinda.displayName}` }));
    const organisationGroup = screen.getByRole("group", { name: "Organisation (optional)" });
    expect(within(organisationGroup).getByRole("status").textContent).toBe(
      `${ntinda.displayName} chosen as the organisation.`,
    );
    type("Find a venue", "Bukoto");
    fireEvent.click(await screen.findByRole("button", { name: `Choose ${bukoto.name}` }));
    // Bukoto lists the sport: no note.
    expect(screen.queryByText(/among the sports it hosts/)).toBeNull();

    // eslint-disable-next-line no-restricted-syntax -- a team's own colour, typed as data
    type("Primary colour (optional)", "#1a3d7c");
    // eslint-disable-next-line no-restricted-syntax -- a team's own colour, typed as data
    type("Secondary colour (optional)", "#F2C230");
    type("Motto (optional)", "Fly high");
    await waitFor(() => expect(checks(requests).length).toBeGreaterThan(0));
    expect(checks(requests).at(-1)!.body).toEqual({
      variant: "RINGBALL_SEVENS",
      name: "Kiwatule Kites FC",
      genderCategory: "MEN",
      ageGroup: "SENIOR",
      level: "RESERVE",
      shortName: "Kites",
      organisationId: ntinda.id,
    });
    save();

    await waitFor(() => expect(creates(requests)).toHaveLength(1));
    expect(creates(requests)[0]!.body).toEqual({
      variant: "RINGBALL_SEVENS",
      name: "Kiwatule Kites FC",
      genderCategory: "MEN",
      ageGroup: "SENIOR",
      level: "RESERVE",
      shortName: "Kites",
      organisationId: ntinda.id,
      code: "KIK",
      motto: "Fly high",
      // eslint-disable-next-line no-restricted-syntax -- the team's own colours, sent in capitals
      primaryColour: "#1A3D7C",
      // eslint-disable-next-line no-restricted-syntax
      secondaryColour: "#F2C230",
      tertiaryColour: null,
      homeVenueId: bukoto.id,
      foundedYear: 2004,
      nameSince: "2020-03-01",
      duplicatesReviewed: false,
      duplicateReason: null,
    });
    await waitFor(() => expect(navigation.push).toHaveBeenCalledWith(`/t/kmsa/teams/${added.id}`));
  });

  it("sends no slug, no name date and no organisation unless chosen, and starts from the only enabled sport", async () => {
    const requests = open({ "GET /api/admin/v1/tenant/sports": tenantSports() });
    const sport = (await screen.findByLabelText("Sport")) as HTMLSelectElement;
    expect(sport.value).toBe("RINGBALL_SEVENS");
    expect(options("Sport")).toEqual(["Ringball sevens"]);
    type("Name", "Nsambya Nightingales");
    type("Age group", "VETERANS");
    type("Gender", "MIXED");
    save();
    await waitFor(() => expect(creates(requests)).toHaveLength(1));
    expect(creates(requests)[0]!.body).toMatchObject({
      variant: "RINGBALL_SEVENS",
      level: "FIRST",
      organisationId: null,
      homeVenueId: null,
      nameSince: null,
    });
    expect(creates(requests)[0]!.body).not.toHaveProperty("slug");
  });

  it("says there is nothing to add while no sport is enabled", async () => {
    open({
      "GET /api/admin/v1/tenant/sports": tenantSports([tenantSport("RINGBALL_SEVENS", "Ringball sevens", false)]),
    });
    expect(await screen.findByText(/No sport is enabled for your organisation yet/)).toBeTruthy();
    expect(screen.getByRole("link", { name: "Settings → Enabled sports" }).getAttribute("href")).toBe(
      "/t/kmsa/settings/sports",
    );
    expect(screen.queryByLabelText("Name")).toBeNull();
    expect(screen.queryByRole("button", { name: "Add team" })).toBeNull();
  });

  it("reads Boys and Girls in the youth age groups, both words until an age group is chosen", async () => {
    open();
    await screen.findByLabelText("Gender");
    expect(options("Gender")).toEqual(["Choose…", "Men / Boys", "Women / Girls", "Mixed"]);
    type("Age group", "U17");
    expect(options("Gender")).toEqual(["Choose…", "Boys", "Girls", "Mixed"]);
    type("Age group", "U18");
    expect(options("Gender")).toEqual(["Choose…", "Men", "Women", "Mixed"]);
    expect(options("Level")).toEqual(["First team", "Reserve", "Development", "Academy"]);
  });

  it("warns of a team that may be the same, opens it, or saves once told it is a different one", async () => {
    const requests = open({
      "POST /api/admin/v1/teams/duplicate-check": {
        status: 200,
        body: {
          candidates: [
            candidate(meteors, "POSSIBLE", [
              { code: "NAME_SIMILARITY", value: "0.86" },
              { code: "SAME_ORGANISATION", value: null },
            ]),
          ],
        },
      },
    });
    await fill("Mutundwe Meteor FC");

    const warning = await screen.findByRole("region", { name: panelName });
    expect(within(warning).getByText("Possibly the same")).toBeTruthy();
    expect(within(warning).getByText("Names 86% alike")).toBeTruthy();
    expect(within(warning).getByText("Same organisation")).toBeTruthy();
    expect(within(warning).getByText("Ringball sevens")).toBeTruthy();
    expect(within(warning).getByText("Senior Men")).toBeTruthy();
    // The organisation shares the first team's name: the candidate's name and its organisation fact.
    expect(within(warning).getAllByText(meteors.organisation!.displayName)).toHaveLength(2);
    expect(within(warning).getByRole("link", { name: `Open ${meteors.name}` }).getAttribute("href")).toBe(
      `/t/kmsa/teams/${meteors.id}`,
    );

    save();
    expect(await within(panel()).findByRole("alert")).toBeTruthy();
    expect(within(panel()).getByRole("alert").textContent).toBe("Open the existing team, or say this is a different one.");
    expect(creates(requests)).toHaveLength(0);

    fireEvent.click(within(panel()).getByRole("button", { name: "This is a different one" }));
    fireEvent.change(within(panel()).getByLabelText("Why is it different? (optional)"), {
      target: { value: "A new club in Mutundwe's west side." },
    });
    save();
    await waitFor(() => expect(creates(requests)).toHaveLength(1));
    expect(creates(requests)[0]!.body).toMatchObject({
      duplicatesReviewed: true,
      duplicateReason: "A new club in Mutundwe's west side.",
    });
  });

  it("asks nothing until there is a sport, a category and a name with a letter or digit", async () => {
    vi.useFakeTimers({ shouldAdvanceTime: true });
    const requests = open();
    fireEvent.change(await screen.findByLabelText("Sport"), { target: { value: "RINGBALL_SEVENS" } });
    type("Name", "Kiwatule Kites FC");
    type("Age group", "SENIOR");
    // Past the warning's 300 ms pause: the clock is moved on, not waited for.
    await act(() => vi.advanceTimersByTimeAsync(400));
    expect(checks(requests)).toHaveLength(0);
    type("Gender", "WOMEN");
    await waitFor(() => expect(checks(requests)).toHaveLength(1));
  });

  it("shows the list the server sent back when someone added a matching team just before the save", async () => {
    const requests = open({
      "POST /api/admin/v1/teams": (request) =>
        (request.body as { duplicatesReviewed: boolean }).duplicatesReviewed
          ? { status: 201, body: added }
          : {
              status: 422,
              body: problem("TEAM_DUPLICATE_REVIEW_REQUIRED", 422, {
                errors: [{ field: "duplicatesReviewed", code: "TEAM_DUPLICATE_REVIEW_REQUIRED" }],
                candidates: [candidate(sparrows)],
              }),
            },
    });
    await fill();
    await waitFor(() => expect(checks(requests).length).toBeGreaterThan(0));
    save();

    const warning = await screen.findByRole("region", { name: panelName });
    expect(within(warning).getByText(sparrows.name)).toBeTruthy();
    expect(within(warning).getByRole("alert").textContent).toContain("Someone added a team that may match");
    await waitFor(() => expect(document.activeElement).toBe(warning));

    fireEvent.click(within(warning).getByRole("button", { name: "This is a different one" }));
    save();
    await waitFor(() => expect(creates(requests)).toHaveLength(2));
    expect(creates(requests)[1]!.body).toMatchObject({ duplicatesReviewed: true, duplicateReason: null });
  });

  it("marks the duplicate checks and the subscription's team count stale once a team is added", async () => {
    const requests = open();
    await fill();
    await waitFor(() => expect(checks(requests).length).toBeGreaterThan(0));
    save();
    await waitFor(() => expect(navigation.push).toHaveBeenCalled());
    const kept = requests.queryClient.getQueryCache().findAll({ queryKey: ["team-duplicates"] });
    expect(kept.length).toBeGreaterThan(0);
    expect(kept.every((query) => query.state.isInvalidated)).toBe(true);
  });

  it("checks the colours' order, the code, the founding year and the name's first day before saving", async () => {
    // 31 December, 22:00 in Kampala (UTC+3): the tenant's date has already turned.
    vi.useFakeTimers({ shouldAdvanceTime: true, now: new Date("2026-12-31T22:00:00Z") });
    const requests = open();
    await fill();
    type("Code (optional)", "K1");
    // eslint-disable-next-line no-restricted-syntax -- a team's own colour, typed as data
    type("Secondary colour (optional)", "#F2C230");
    type("Third colour (optional)", "white");
    type("Founded (optional)", "2028");
    type("Name used since (optional)", "2027-01-02");
    save();
    expect(await screen.findByText("Use three letters, such as NTS.")).toBeTruthy();
    expect(screen.getByText("Enter the primary colour first.")).toBeTruthy();
    expect(screen.getByText(/Enter a colour as # and six digits/)).toBeTruthy();
    expect(screen.getByText("Enter a year from 1800 to 2027.")).toBeTruthy();
    expect(screen.getByText("Choose a day no later than today.")).toBeTruthy();
    expect(creates(requests)).toHaveLength(0);
  });

  it("shows a dissolved organisation and a closed venue but doesn't let them be chosen", async () => {
    open();
    await screen.findByLabelText("Name");
    type("Find an organisation", "Kiwatule");
    expect(await screen.findByRole("button", { name: `Choose ${dissolved.displayName}` })).toHaveProperty("disabled", true);
    expect(screen.getByText("Dissolved: it can't take a team.")).toBeTruthy();
    type("Find a venue", "Kiwatule");
    expect(await screen.findByRole("button", { name: `Choose ${closedGround.name}` })).toHaveProperty("disabled", true);
    expect(screen.getByText("Closed: it can't be a home venue.")).toBeTruthy();
  });

  it("notes a home venue that doesn't list the sport, and lets it be chosen", async () => {
    open();
    await fill();
    type("Find a venue", "Kisaasi");
    fireEvent.click(await screen.findByRole("button", { name: `Choose ${kisaasiGround.name}` }));
    expect(
      await screen.findByText(
        `${kisaasiGround.name} doesn't list Ringball sevens among the sports it hosts. It can still be the home venue.`,
      ),
    ).toBeTruthy();
  });

  it("puts an organisation dissolved since it was chosen under its field, and the team limit in the alert", async () => {
    const requests = open({
      "POST /api/admin/v1/teams": {
        status: 422,
        body: problem("TEAM_ORGANISATION_UNAVAILABLE", 422, {
          errors: [{ field: "organisationId", code: "TEAM_ORGANISATION_UNAVAILABLE" }],
        }),
      },
    });
    await fill();
    type("Find an organisation", "Ntinda");
    fireEvent.click(await screen.findByRole("button", { name: `Choose ${ntinda.displayName}` }));
    save();
    const group = screen.getByRole("group", { name: "Organisation (optional)" });
    expect((await within(group).findByRole("alert")).textContent).toContain("dissolved or archived since you chose it");
    await waitFor(() => expect(creates(requests)).toHaveLength(1));

    cleanup();
    vi.unstubAllGlobals();
    open({
      "POST /api/admin/v1/teams": {
        status: 403,
        body: problem("ENTITLEMENT_QUOTA_EXCEEDED", 403, { detail: "Your plan allows 40 teams." }),
      },
    });
    await fill();
    save();
    expect(await screen.findByText(/Your plan allows 40 teams\./)).toBeTruthy();
  });

  it("starts with the organisation it was opened from, unless that one can't take a team", async () => {
    navigation.search = `organisationId=${ntinda.id}`;
    const requests = open({ [`GET /api/admin/v1/organisations/${ntinda.id}`]: { status: 200, body: ntinda } });
    await fill("Ntinda United FC Women");
    const group = screen.getByRole("group", { name: "Organisation (optional)" });
    expect(within(group).getByText(ntinda.displayName)).toBeTruthy();
    expect(screen.getByRole("link", { name: "Cancel" }).getAttribute("href")).toBe(
      `/t/kmsa/organisations/${ntinda.id}?tab=teams`,
    );
    save();
    await waitFor(() => expect(creates(requests)).toHaveLength(1));
    expect(creates(requests)[0]!.body).toMatchObject({ organisationId: ntinda.id });

    cleanup();
    vi.unstubAllGlobals();
    // A dissolved organisation takes no team (D6): the form starts with none.
    navigation.search = `organisationId=${seeta.id}`;
    open({ [`GET /api/admin/v1/organisations/${seeta.id}`]: { status: 200, body: seeta } });
    await screen.findByLabelText("Name");
    expect(within(screen.getByRole("group", { name: "Organisation (optional)" })).getByText("None chosen.")).toBeTruthy();
  });

  it("is refused to whoever can't add teams, and disabled with the reason on a read-only tenant", async () => {
    open({}, reads);
    expect(await screen.findByText("You can't open this page")).toBeTruthy();
    cleanup();
    open({}, readOnlyCapabilities(reads, ["team.create", "team.duplicate_check"]));
    await screen.findByRole("heading", { level: 1, name: "Add team" });
    expect((await screen.findAllByText(/read-only/i)).length).toBeGreaterThan(0);
    expect(screen.queryByLabelText("Name")).toBeNull();
  });
});

describe("Teams directory: Add team", () => {
  it("links to the form for whoever may add one", async () => {
    open({}, registrar, <TeamsPage />);
    expect((await screen.findByRole("link", { name: "Add team" })).getAttribute("href")).toBe("/t/kmsa/teams/new");
  });

  it("is disabled with the reason on a read-only tenant, and not shown to a viewer", async () => {
    open({}, readOnlyCapabilities(reads, ["team.create", "team.duplicate_check"]), <TeamsPage />);
    expect(await screen.findByRole("button", { name: "Add team" })).toHaveProperty("disabled", true);
    cleanup();
    open({}, reads, <TeamsPage />);
    await screen.findByRole("heading", { name: "Teams" });
    expect(screen.queryByRole("link", { name: "Add team" })).toBeNull();
    expect(screen.queryByRole("button", { name: "Add team" })).toBeNull();
  });
});
