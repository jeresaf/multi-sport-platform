// Registry → Teams → a team → Rename (S07 T6c-2a): the new name from a day after the current name's first day and not
// after the tenant's today, with the short name and code from then on (prefilled, a cleared one sent as null); the
// new names run the duplicate warning, the team itself left out; the server's date and unchanged-name refusals land
// under their fields; a conflict offers Reload; a disbanded or archived team isn't renamed; a viewer is refused.
import { cleanup, fireEvent, screen, waitFor, within } from "@testing-library/react";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { fakeApi, problem, renderWithQuery, type Answers, type RecordedRequest } from "@/test/render";
import { candidate, created, meteors, sparrows, teamAnswers } from "@/test/teams";
import type { Team } from "../../teams-data";
import TenantWorkspaceLayout from "../../../layout";
import RenameTeamPage from "./page";

const navigation = vi.hoisted(() => ({ push: vi.fn() }));

vi.mock("next/navigation", () => ({
  useParams: () => ({ tenantSlug: "kmsa", teamId: "0199a3f0-0000-7000-8000-0000000000f1" }),
  usePathname: () => "/t/kmsa/teams/0199a3f0-0000-7000-8000-0000000000f1/rename",
  useSearchParams: () => new URLSearchParams(),
  useRouter: () => ({ replace: vi.fn(), push: navigation.push }),
}));

const reads = ["team.read", "organisation.read", "venue.read", "sport.read", "tenant.settings.read"];
const registrar = [...reads, "team.rename", "team.duplicate_check"];
const teamPath = `/api/admin/v1/teams/${meteors.id}`;
const renamed: Team = {
  ...meteors,
  name: "Mutundwe Mighty Meteors FC",
  nameSince: "2026-07-01",
  formerNames: [
    { id: "0199a3f0-0000-7000-8000-0000000000fb", name: meteors.name, shortName: "Meteors", code: "MUT", validFrom: "2026-01-01", validTo: "2026-06-30" },
    ...meteors.formerNames,
  ],
  version: 6,
};

function open(answers: Answers = {}, capabilities: Parameters<typeof teamAnswers>[0] = registrar, shown: Team = meteors) {
  const requests = fakeApi({
    ...teamAnswers(capabilities),
    [`GET ${teamPath}`]: { status: 200, body: shown },
    [`GET ${teamPath}/status-changes`]: { status: 200, body: { items: [created] } },
    "POST /api/admin/v1/teams/duplicate-check": { status: 200, body: { candidates: [] } },
    [`POST ${teamPath}/rename`]: { status: 200, body: renamed },
    ...answers,
  });
  renderWithQuery(
    <TenantWorkspaceLayout>
      <RenameTeamPage />
    </TenantWorkspaceLayout>,
  );
  return requests;
}

const field = (label: string) => screen.findByLabelText(label);
const type = async (label: string, value: string) => fireEvent.change(await field(label), { target: { value } });
const rename = () => fireEvent.click(screen.getByRole("button", { name: "Rename" }));
const renames = (requests: RecordedRequest[]) => requests.filter((request) => request.path === `${teamPath}/rename`);
const checks = (requests: RecordedRequest[]) =>
  requests.filter((request) => request.path === "/api/admin/v1/teams/duplicate-check");

beforeEach(() => {
  // 26 November 2026, noon in Kampala: the tenant's today.
  vi.useFakeTimers({ shouldAdvanceTime: true, now: new Date("2026-11-26T09:00:00Z") });
});

afterEach(() => {
  navigation.push.mockReset();
  cleanup();
  vi.useRealTimers();
  vi.unstubAllGlobals();
});

describe("Rename a team", () => {
  it("renames it from a day with the short name and code from then on, and opens its history", async () => {
    const requests = open();
    expect(await screen.findByText(/has had its name since 1 Jan 2026/)).toBeTruthy();
    // The current short name and code are kept unless changed; the day starts at today.
    expect(await field("Short name (optional)")).toHaveProperty("value", "Meteors");
    expect(await field("Code (optional)")).toHaveProperty("value", "MUT");
    expect(await field("New name from")).toHaveProperty("value", "2026-11-26");
    await type("New name", "Mutundwe Mighty Meteors FC");
    await type("Code (optional)", "");
    await type("New name from", "2026-07-01");
    rename();

    await waitFor(() => expect(renames(requests)).toHaveLength(1));
    const sent = renames(requests)[0]!;
    expect(sent.headers.get("If-Match")).toBe('"5"');
    expect(sent.body).toEqual({
      name: "Mutundwe Mighty Meteors FC",
      shortName: "Meteors",
      code: null,
      effectiveOn: "2026-07-01",
      duplicatesReviewed: false,
      duplicateReason: null,
    });
    await waitFor(() => expect(navigation.push).toHaveBeenCalledWith(`/t/kmsa/teams/${meteors.id}?tab=history`));
  });

  it("refuses a day on or before the current name's first day, or after today, before sending", async () => {
    const requests = open();
    await type("New name", "Mutundwe Mighty Meteors FC");
    await type("New name from", "2026-01-01");
    rename();
    expect(await screen.findByText("Choose a day after the current name's first day.")).toBeTruthy();
    await type("New name from", "2026-11-27");
    rename();
    expect(await screen.findByText("Choose a day no later than today.")).toBeTruthy();
    expect(renames(requests)).toHaveLength(0);
  });

  it("checks the new names against other teams in the sport and category, the team itself left out", async () => {
    const requests = open({
      "POST /api/admin/v1/teams/duplicate-check": {
        status: 200,
        body: { candidates: [candidate(meteors), candidate(sparrows, "POSSIBLE", [{ code: "NAME_SIMILARITY", value: "0.74" }])] },
      },
    });
    await field("New name");
    // While the names are the team's own, nothing is checked.
    await type("New name", meteors.name);
    await vi.advanceTimersByTimeAsync(400);
    expect(checks(requests)).toHaveLength(0);

    await type("New name", "Seeta Sparrows Meteors");
    await waitFor(() => expect(checks(requests).length).toBeGreaterThan(0));
    expect(checks(requests).at(-1)!.body).toEqual({
      variant: meteors.variant,
      name: "Seeta Sparrows Meteors",
      genderCategory: "MEN",
      ageGroup: "SENIOR",
      level: "FIRST",
      shortName: "Meteors",
      organisationId: meteors.organisation!.id,
    });
    const warning = await screen.findByRole("region", { name: "Is this a team you already have?" });
    expect(within(warning).getByText(sparrows.name)).toBeTruthy();
    expect(within(warning).getByText("Names 74% alike")).toBeTruthy();
    expect(within(warning).queryByRole("link", { name: `Open ${meteors.name}` })).toBeNull();

    rename();
    expect(await within(warning).findByRole("alert")).toBeTruthy();
    expect(renames(requests)).toHaveLength(0);
    fireEvent.click(within(warning).getByRole("button", { name: "This is a different one" }));
    rename();
    await waitFor(() => expect(renames(requests)).toHaveLength(1));
    expect(renames(requests)[0]!.body).toMatchObject({ duplicatesReviewed: true });
  });

  it("puts the server's date and unchanged-name refusals under their fields", async () => {
    let code = "TEAM_RENAME_DATE_INVALID";
    open({
      [`POST ${teamPath}/rename`]: () => ({
        status: 422,
        body: problem(code, 422, {
          errors: [{ field: code === "TEAM_NAME_UNCHANGED" ? "name" : "effectiveOn", code }],
        }),
      }),
    });
    await type("New name", "Mutundwe Mighty Meteors FC");
    rename();
    expect(await screen.findByText("Choose a day after 1 Jan 2026 and no later than today.")).toBeTruthy();

    code = "TEAM_NAME_UNCHANGED";
    await type("New name", "Mutundwe meteors FC");
    rename();
    expect(await screen.findByText("This is the team's name already. Enter the new name.")).toBeTruthy();
  });

  it("says the team changed since it was opened and offers Reload", async () => {
    open({ [`POST ${teamPath}/rename`]: { status: 409, body: problem("TEAM_CONCURRENCY_CONFLICT", 409) } });
    await type("New name", "Mutundwe Mighty Meteors FC");
    rename();
    expect(await screen.findByText("This record changed since you opened it")).toBeTruthy();
    expect(screen.getByRole("button", { name: "Reload" })).toBeTruthy();
  });

  it("isn't offered for a disbanded or archived team, and is refused to a viewer", async () => {
    open({}, registrar, { ...meteors, status: "DISBANDED" });
    expect(await screen.findByText(`${meteors.name} is disbanded: reform it before renaming it.`)).toBeTruthy();
    expect(screen.queryByLabelText("New name")).toBeNull();
    cleanup();
    vi.unstubAllGlobals();

    open({}, registrar, { ...meteors, status: "ARCHIVED" });
    expect(await screen.findByText(`${meteors.name} is archived: unarchive it before renaming it.`)).toBeTruthy();
    cleanup();
    vi.unstubAllGlobals();

    open({}, reads);
    expect(await screen.findByText("You can't open this page")).toBeTruthy();
  });
});
