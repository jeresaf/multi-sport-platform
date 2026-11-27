// Registry → Teams → a team → Edit details (S07 T6c-2a): the PUT replaces every detail from the version the page opened
// on, with the current name and the web address; the sport and the name aren't edited here (a new name is a rename);
// only a changed short name, category or organisation runs the warning, which leaves the team itself out; a conflict
// says "This record changed since you opened it" and offers Reload; the organisation and home venue it has stay until
// removed; the header offers Edit details and Rename by capability (Rename not for a disbanded team); a viewer sees
// neither.
import { act, cleanup, fireEvent, screen, waitFor, within } from "@testing-library/react";
import { afterEach, describe, expect, it, vi } from "vitest";
import { readOnlyCapabilities } from "@/test/capabilities";
import { fakeApi, problem, renderWithQuery, type Answers, type RecordedRequest } from "@/test/render";
import { candidate, created, meteors, sparrows, teamAnswers } from "@/test/teams";
import type { Team } from "../../teams-data";
import TenantWorkspaceLayout from "../../../layout";
import TeamPage from "../page";
import EditTeamPage from "./page";

const navigation = vi.hoisted(() => ({ push: vi.fn() }));

vi.mock("next/navigation", () => ({
  useParams: () => ({ tenantSlug: "kmsa", teamId: "0199a3f0-0000-7000-8000-0000000000f1" }),
  usePathname: () => "/t/kmsa/teams/0199a3f0-0000-7000-8000-0000000000f1/edit",
  useSearchParams: () => new URLSearchParams(),
  useRouter: () => ({ replace: vi.fn(), push: navigation.push }),
}));

const reads = ["team.read", "organisation.read", "venue.read", "sport.read", "tenant.settings.read"];
const registrar = [...reads, "team.update", "team.rename", "team.duplicate_check"];
const teamPath = `/api/admin/v1/teams/${meteors.id}`;

function open(
  answers: Answers = {},
  capabilities: Parameters<typeof teamAnswers>[0] = registrar,
  page = <EditTeamPage />,
  shown: Team = meteors,
) {
  const requests = fakeApi({
    ...teamAnswers(capabilities),
    [`GET ${teamPath}`]: { status: 200, body: shown },
    [`GET ${teamPath}/status-changes`]: { status: 200, body: { items: [created] } },
    "POST /api/admin/v1/teams/duplicate-check": { status: 200, body: { candidates: [] } },
    [`PUT ${teamPath}`]: (request) => ({ status: 200, body: { ...shown, ...(request.body as object), version: 6 } }),
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
  requests.filter((request) => request.path === "/api/admin/v1/teams/duplicate-check");

afterEach(() => {
  navigation.push.mockReset();
  cleanup();
  vi.useRealTimers();
  vi.unstubAllGlobals();
});

describe("Edit team details", () => {
  it("saves every detail from the version it opened on, with the web address and without the name", async () => {
    const requests = open();
    await type("Motto (optional)", "Higher together, always");
    // eslint-disable-next-line no-restricted-syntax -- a team's own colour, typed as data
    await type("Third colour (optional)", "#ffffff");
    // The sport and the name are shown, not edited; the name goes to Rename.
    expect(screen.queryByLabelText("Sport")).toBeNull();
    expect(screen.queryByLabelText("Name")).toBeNull();
    expect(screen.getByText("Ringball sevens")).toBeTruthy();
    expect(screen.getByRole("link", { name: "rename" }).getAttribute("href")).toBe(`/t/kmsa/teams/${meteors.id}/rename`);
    save();

    await waitFor(() => expect(puts(requests)).toHaveLength(1));
    const sent = puts(requests)[0]!;
    expect(sent.headers.get("If-Match")).toBe('"5"');
    // No name: the server keeps it, so a rename made meanwhile is the version conflict (pre-review 1).
    expect(sent.body).toEqual({
      genderCategory: "MEN",
      ageGroup: "SENIOR",
      level: "FIRST",
      shortName: "Meteors",
      organisationId: meteors.organisation!.id,
      code: "MUT",
      motto: "Higher together, always",
      // eslint-disable-next-line no-restricted-syntax -- the team's own colours, as saved and sent in capitals
      primaryColour: "#1A3D7C",
      // eslint-disable-next-line no-restricted-syntax
      secondaryColour: "#F2C230",
      // eslint-disable-next-line no-restricted-syntax
      tertiaryColour: "#FFFFFF",
      homeVenueId: meteors.homeVenue!.id,
      foundedYear: 2018,
      slug: "mutundwe-meteors-fc",
      duplicatesReviewed: false,
      duplicateReason: null,
    });
    // A field the server compares didn't change: nothing was checked.
    expect(checks(requests)).toHaveLength(0);
    await waitFor(() => expect(navigation.push).toHaveBeenCalledWith(`/t/kmsa/teams/${meteors.id}`));
  });

  it("warns about other teams once the category changes, never about the team itself", async () => {
    vi.useFakeTimers({ shouldAdvanceTime: true });
    const requests = open({
      "POST /api/admin/v1/teams/duplicate-check": {
        status: 200,
        body: { candidates: [candidate(meteors), candidate(sparrows, "POSSIBLE", [{ code: "NAME_SIMILARITY", value: "0.72" }])] },
      },
    });
    await type("Motto (optional)", "Onward");
    await act(() => vi.advanceTimersByTimeAsync(400));
    expect(checks(requests)).toHaveLength(0);

    await type("Level", "RESERVE");
    await waitFor(() => expect(checks(requests)).toHaveLength(1));
    expect(checks(requests)[0]!.body).toMatchObject({ name: meteors.name, level: "RESERVE", variant: "RINGBALL_SEVENS" });
    const warning = await screen.findByRole("region", { name: "Is this a team you already have?" });
    expect(within(warning).getByText(sparrows.name)).toBeTruthy();
    expect(within(warning).queryByRole("link", { name: `Open ${meteors.name}` })).toBeNull();

    fireEvent.click(within(warning).getByRole("button", { name: "This is a different one" }));
    save();
    await waitFor(() => expect(puts(requests)).toHaveLength(1));
    expect(puts(requests)[0]!.body).toMatchObject({ level: "RESERVE", duplicatesReviewed: true });
  });

  it("says the team changed since it was opened; Reload starts over from it as it is now", async () => {
    let current = meteors;
    let conflict = true;
    const requests = open({
      [`GET ${teamPath}`]: () => ({ status: 200, body: current }),
      [`PUT ${teamPath}`]: (request) =>
        conflict
          ? { status: 409, body: problem("TEAM_CONCURRENCY_CONFLICT", 409) }
          : { status: 200, body: { ...current, ...(request.body as object), version: 7 } },
    });
    await type("Motto (optional)", "Onward");
    save();
    expect(await screen.findByText("This record changed since you opened it")).toBeTruthy();

    current = { ...meteors, motto: "Together we rise", version: 6 };
    conflict = false;
    fireEvent.click(screen.getByRole("button", { name: "Reload" }));
    await waitFor(async () => expect(await field("Motto (optional)")).toHaveProperty("value", "Together we rise"));
    await type("Founded (optional)", "2017");
    save();
    await waitFor(() => expect(puts(requests)).toHaveLength(2));
    const second = puts(requests)[1]!;
    expect(second.headers.get("If-Match")).toBe('"6"');
    expect(second.body).toMatchObject({ motto: "Together we rise", foundedYear: 2017 });
  });

  it("answers a rename made while it was open as a conflict, and Reload shows the new name", async () => {
    let current = meteors;
    open({
      [`GET ${teamPath}`]: () => ({ status: 200, body: current }),
      [`PUT ${teamPath}`]: (request) =>
        (request.headers.get("If-Match") ?? "") === `"${current.version}"`
          ? { status: 200, body: { ...current, ...(request.body as object), version: current.version + 1 } }
          : { status: 409, body: problem("TEAM_CONCURRENCY_CONFLICT", 409) },
    });
    await type("Motto (optional)", "Onward");
    current = { ...meteors, name: "Mutundwe Mighty Meteors FC", version: 6 };
    save();
    expect(await screen.findByText("This record changed since you opened it")).toBeTruthy();
    fireEvent.click(screen.getByRole("button", { name: "Reload" }));
    expect(await screen.findByText("Mutundwe Mighty Meteors FC", { selector: "dd" })).toBeTruthy();
  });

  it("keeps the organisation and home venue it has until they are removed, then sends none", async () => {
    const requests = open();
    const organisationField = await screen.findByRole("group", { name: "Organisation (optional)" });
    expect(within(organisationField).getByText(meteors.organisation!.displayName)).toBeTruthy();
    fireEvent.click(within(organisationField).getByRole("button", { name: "Independent team" }));
    expect(within(organisationField).getByText("None chosen.")).toBeTruthy();
    const venueField = screen.getByRole("group", { name: "Home venue (optional)" });
    expect(within(venueField).getByText(meteors.homeVenue!.name)).toBeTruthy();
    fireEvent.click(within(venueField).getByRole("button", { name: "No home venue" }));
    // Leaving the organisation is a compared change: the check runs, and answers none.
    save();
    await waitFor(() => expect(puts(requests)).toHaveLength(1));
    expect(checks(requests).length).toBeGreaterThan(0);
    expect(puts(requests)[0]!.body).toMatchObject({ organisationId: null, homeVenueId: null });
  });

  it("shows a taken web address under it", async () => {
    open({
      [`PUT ${teamPath}`]: {
        status: 422,
        body: problem("TEAM_SLUG_TAKEN", 422, {
          detail: "Another team has this web address.",
          errors: [{ field: "slug", code: "TEAM_SLUG_TAKEN" }],
        }),
      },
    });
    await type("Web address", "ntinda-swifts-fc");
    save();
    expect(await screen.findByText("Another team has this web address.")).toBeTruthy();
    expect((await field("Web address")).getAttribute("aria-invalid")).toBe("true");
  });

  it("isn't offered to a viewer: no Edit details or Rename in the header, and the page refuses them", async () => {
    open({}, reads, <TeamPage />);
    await screen.findByRole("heading", { level: 1, name: meteors.name });
    for (const name of ["Edit details", "Rename"]) {
      expect(screen.queryByRole("link", { name })).toBeNull();
      expect(screen.queryByRole("button", { name })).toBeNull();
    }
    cleanup();

    open({}, reads);
    expect(await screen.findByText("You can't open this page")).toBeTruthy();
  });

  it("offers Edit details and Rename in the header; Rename is disabled with the reason for a disbanded team", async () => {
    open({}, registrar, <TeamPage />);
    expect((await screen.findByRole("link", { name: "Edit details" })).getAttribute("href")).toBe(
      `/t/kmsa/teams/${meteors.id}/edit`,
    );
    expect(screen.getByRole("link", { name: "Rename" }).getAttribute("href")).toBe(`/t/kmsa/teams/${meteors.id}/rename`);
    cleanup();

    open({}, registrar, <TeamPage />, { ...meteors, status: "DISBANDED" });
    const rename = await screen.findByRole("button", { name: "Rename" });
    expect(rename).toHaveProperty("disabled", true);
    expect(document.getElementById(rename.getAttribute("aria-describedby")!)?.textContent).toContain(
      "is disbanded: reform it before renaming it.",
    );
    // A correction is still possible in any status.
    expect(screen.getByRole("link", { name: "Edit details" })).toBeTruthy();
    cleanup();

    open({}, readOnlyCapabilities(reads, ["team.update", "team.rename", "team.duplicate_check"]), <TeamPage />);
    const edit = await screen.findByRole("button", { name: "Edit details" });
    expect(edit).toHaveProperty("disabled", true);
    expect(document.getElementById(edit.getAttribute("aria-describedby")!)?.textContent).toMatch(/read-only/i);
    expect(screen.getByRole("button", { name: "Rename" })).toHaveProperty("disabled", true);
  });
});
