// Registry → Teams → a team → Names & IDs (S07 T6c-2b, D10): the nicknames and spellings and the external identifiers,
// ended ones included; an alias added (never a former name: those come from a rename) or removed with a reason and an
// identifier added or ended, each from the version its dialog opened on; a conflict offers Reload; a disbanded or
// archived team takes no new one but its aliases can still be removed and its identifiers ended; a viewer sees the
// lists without a change control.
import { cleanup, fireEvent, screen, waitFor, within } from "@testing-library/react";
import { afterEach, describe, expect, it, vi } from "vitest";
import { readOnlyCapabilities } from "@/test/capabilities";
import { created, meteors, team, teamAnswers } from "@/test/teams";
import { fakeApi, problem, renderWithQuery, type Answers, type RecordedRequest } from "@/test/render";
import TenantWorkspaceLayout from "../../layout";
import type { Team } from "../teams-data";
import TeamPage from "./page";

vi.mock("next/navigation", () => ({
  useParams: () => ({ tenantSlug: "kmsa", teamId: "0199a3f0-0000-7000-8000-0000000000f1" }),
  usePathname: () => "/t/kmsa/teams/0199a3f0-0000-7000-8000-0000000000f1",
  useSearchParams: () => new URLSearchParams("tab=names"),
  useRouter: () => ({ replace: vi.fn(), push: vi.fn() }),
}));

const reads = ["team.read", "organisation.read", "venue.read", "sport.read", "tenant.settings.read"];
const changes = ["team.alias.add", "team.alias.remove", "team.external_id.add", "team.external_id.end"];
const registrar = [...reads, ...changes];
const teamPath = `/api/admin/v1/teams/${meteors.id}`;

const nickname = { id: "0199a3f0-0000-7000-8000-0000000000d1", name: "The Meteors", kind: "NICKNAME" as const };
const teamNumber = {
  id: "0199a3f0-0000-7000-8000-0000000000d2",
  authority: "KMSA_TEAM_NO",
  value: "T-0231",
  validFrom: "2024-02-01",
  validTo: null,
};
const ended = { ...teamNumber, id: "0199a3f0-0000-7000-8000-0000000000d3", value: "T-0088", validFrom: "2018-03-01", validTo: "2024-01-31" };
const named = team({ aliases: [nickname], externalIdentifiers: [teamNumber, ended] });

function open(shown: Team = named, capabilities: Parameters<typeof teamAnswers>[0] = registrar, answers: Answers = {}) {
  const requests = fakeApi({
    ...teamAnswers(capabilities),
    [`GET ${teamPath}`]: { status: 200, body: shown },
    [`GET ${teamPath}/status-changes`]: { status: 200, body: { items: [created] } },
    ...answers,
  });
  renderWithQuery(
    <TenantWorkspaceLayout>
      <TeamPage />
    </TenantWorkspaceLayout>,
  );
  return requests;
}

const loaded = () => screen.findByRole("heading", { name: "Aliases" });
const dialog = () => screen.getByRole("dialog");
const type = (label: string, value: string) =>
  fireEvent.change(within(dialog()).getByLabelText(label), { target: { value } });
const press = (name: string) => fireEvent.click(within(dialog()).getByRole("button", { name }));
const posted = async (requests: RecordedRequest[], path: string) => {
  let found: RecordedRequest | undefined;
  await waitFor(() => {
    found = requests.find((request) => request.method === "POST" && request.path === path && request.body !== undefined);
    expect(found).toBeTruthy();
  });
  return found!;
};

afterEach(() => {
  cleanup();
  vi.unstubAllGlobals();
});

describe("Team Names & IDs", () => {
  it("is a tab of the team, listing the aliases and the identifiers, ended ones included", async () => {
    open();
    await loaded();
    expect(screen.getByRole("tab", { name: "Names & IDs", selected: true })).toBeTruthy();
    const panel = screen.getByRole("tabpanel");
    expect(within(panel).getAllByText("The Meteors").length).toBeGreaterThan(0);
    expect(within(panel).getAllByText("Nickname").length).toBeGreaterThan(0);
    expect(within(panel).getAllByText("1 Mar 2018 to 31 Jan 2024 (ended)").length).toBeGreaterThan(0);
    expect(within(panel).getByRole("button", { name: "End KMSA_TEAM_NO T-0231" })).toBeTruthy();
    expect(within(panel).queryByRole("button", { name: "End KMSA_TEAM_NO T-0088" })).toBeNull();
    // A former name is the team's earlier name (History), never an alias of its own here.
    expect(within(panel).queryByText("Mutundwe Weavers FC")).toBeNull();
  });

  it("adds a nickname or spelling, never a former name, from the version it opened on", async () => {
    const requests = open(named, registrar, {
      [`POST ${teamPath}/aliases`]: {
        status: 200,
        body: { ...named, aliases: [nickname, { id: "0199a3f0-0000-7000-8000-0000000000d4", name: "Mutundwe Meteors", kind: "SPELLING" }], version: 6 },
      },
    });
    await loaded();
    fireEvent.click(screen.getByRole("button", { name: "Add alias" }));
    expect(within(dialog()).getByRole("heading", { name: `Add an alias for ${named.name}` })).toBeTruthy();
    const kinds = within(within(dialog()).getByLabelText("Kind")).getAllByRole("option").map((option) => option.textContent);
    expect(kinds).not.toContain("Former name");
    expect(kinds).toEqual(expect.arrayContaining(["Nickname", "Another spelling"]));
    type("Name", "Mutundwe Meteors");
    type("Kind", "SPELLING");
    press("Add alias");
    const sent = await posted(requests, `${teamPath}/aliases`);
    expect(sent.headers.get("If-Match")).toBe('"5"');
    expect(sent.body).toEqual({ name: "Mutundwe Meteors", kind: "SPELLING" });
    expect(await screen.findByText("Alias added.")).toBeTruthy();
  });

  it("refuses an alias longer than a team name may be, before sending", async () => {
    const requests = open();
    await loaded();
    fireEvent.click(screen.getByRole("button", { name: "Add alias" }));
    type("Name", "M".repeat(121));
    press("Add alias");
    await waitFor(() => expect(within(dialog()).getByLabelText("Name").getAttribute("aria-invalid")).toBe("true"));
    expect(requests.some((request) => request.method === "POST")).toBe(false);
  });

  it("removes an alias with the reason", async () => {
    const requests = open(named, registrar, {
      [`POST ${teamPath}/aliases/${nickname.id}/remove`]: { status: 200, body: { ...named, aliases: [], version: 6 } },
    });
    await loaded();
    fireEvent.click(screen.getByRole("button", { name: "Remove the alias The Meteors" }));
    type("Reason", "Not what supporters call them");
    press("Remove alias");
    const sent = await posted(requests, `${teamPath}/aliases/${nickname.id}/remove`);
    expect(sent.body).toEqual({ reason: "Not what supporters call them" });
    expect(sent.headers.get("If-Match")).toBe('"5"');
    expect(await screen.findByText("Alias removed.")).toBeTruthy();
  });

  it("adds an identifier with the authority in capitals, and ends one with the optional reason", async () => {
    const requests = open(named, registrar, {
      [`POST ${teamPath}/external-identifiers`]: { status: 200, body: { ...named, version: 6 } },
      [`POST ${teamPath}/external-identifiers/${teamNumber.id}/end`]: { status: 200, body: { ...named, version: 6 } },
    });
    await loaded();
    fireEvent.click(screen.getByRole("button", { name: "Add identifier" }));
    type("Issued by (code)", "league_reg_no");
    type("Identifier", "L-2026-014");
    press("Add identifier");
    const added = await posted(requests, `${teamPath}/external-identifiers`);
    expect(added.headers.get("If-Match")).toBe('"5"');
    expect(added.body).toEqual({ authority: "LEAGUE_REG_NO", value: "L-2026-014", validFrom: null });
    await waitFor(() => expect(screen.queryByRole("dialog")).toBeNull());

    fireEvent.click(screen.getByRole("button", { name: "End KMSA_TEAM_NO T-0231" }));
    await waitFor(() => expect(within(dialog()).getByLabelText("Last day valid")).toHaveProperty("disabled", false));
    type("Last day valid", "2026-06-30");
    type("Reason (optional)", "KMSA renumbered its teams");
    press("End identifier");
    const end = await posted(requests, `${teamPath}/external-identifiers/${teamNumber.id}/end`);
    expect(end.body).toEqual({ validTo: "2026-06-30", reason: "KMSA renumbered its teams" });
  });

  it("offers Reload when the team changed since the dialog opened", async () => {
    open(named, registrar, {
      [`POST ${teamPath}/aliases/${nickname.id}/remove`]: { status: 409, body: problem("TEAM_CONCURRENCY_CONFLICT", 409) },
    });
    await loaded();
    fireEvent.click(screen.getByRole("button", { name: "Remove the alias The Meteors" }));
    type("Reason", "Not what supporters call them");
    press("Remove alias");
    expect(await within(dialog()).findByText("This record changed since you opened it")).toBeTruthy();
    expect(within(dialog()).getByRole("button", { name: "Reload" })).toBeTruthy();
  });

  it.each([
    ["DISBANDED", "disbanded"],
    ["ARCHIVED", "archived"],
  ] as const)("takes no new name or identifier for a %s team, but removes and ends", async (status, word) => {
    open(team({ ...named, status }));
    await loaded();
    expect(screen.queryByRole("button", { name: "Add alias" })).toBeNull();
    expect(screen.queryByRole("button", { name: "Add identifier" })).toBeNull();
    expect(screen.getAllByText(`${named.name} is ${word}: no new one can be added.`)).toHaveLength(2);
    expect(screen.getByRole("button", { name: "Remove the alias The Meteors" })).toBeTruthy();
    expect(screen.getByRole("button", { name: "End KMSA_TEAM_NO T-0231" })).toBeTruthy();
  });

  it("takes new names for a suspended team", async () => {
    open(team({ ...named, status: "SUSPENDED" }));
    await loaded();
    expect(screen.getByRole("button", { name: "Add alias" })).toHaveProperty("disabled", false);
    expect(screen.getByRole("button", { name: "Add identifier" })).toHaveProperty("disabled", false);
  });

  it("disables the changes with the reason on a read-only tenant", async () => {
    open(named, readOnlyCapabilities(reads, changes));
    await loaded();
    expect(screen.getByRole("button", { name: "Add alias" })).toHaveProperty("disabled", true);
    expect(screen.getByRole("button", { name: "Remove the alias The Meteors" })).toHaveProperty("disabled", true);
  });

  it("shows a viewer the names and identifiers without a change control", async () => {
    open(named, reads);
    await loaded();
    expect(within(screen.getByRole("tabpanel")).queryAllByRole("button")).toEqual([]);
    expect(within(screen.getByRole("tabpanel")).getAllByText("T-0231").length).toBeGreaterThan(0);
  });
});
