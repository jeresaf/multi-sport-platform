// Registry → Venues → a venue → Names & IDs (S07 T6a-2, D10): the aliases and the external identifiers, ended ones
// included; an alias added or removed with a reason and an identifier added or ended, each from the version its dialog
// opened on; the API's refusals under their fields; a conflict offers Reload; a closed venue takes no new one but its
// aliases can still be removed and its identifiers ended; a viewer sees the lists without a change control.
import { cleanup, fireEvent, screen, waitFor, within } from "@testing-library/react";
import { afterEach, describe, expect, it, vi } from "vitest";
import { readOnlyCapabilities } from "@/test/capabilities";
import { fakeApi, problem, renderWithQuery, type Answers, type RecordedRequest } from "@/test/render";
import { bukoto, venue, venueAnswers } from "@/test/venues";
import TenantWorkspaceLayout from "../../layout";
import type { Venue } from "../venues-data";
import VenuePage from "./page";

vi.mock("next/navigation", () => ({
  useParams: () => ({ tenantSlug: "kmsa", venueId: "0199a3f0-0000-7000-8000-0000000000b1" }),
  usePathname: () => "/t/kmsa/venues/0199a3f0-0000-7000-8000-0000000000b1",
  useSearchParams: () => new URLSearchParams("tab=names"),
  useRouter: () => ({ replace: vi.fn(), push: vi.fn() }),
}));

const reads = ["venue.read", "team.read", "sport.read", "tenant.settings.read"];
const changes = ["venue.alias.add", "venue.alias.remove", "venue.external_id.add", "venue.external_id.end"];
const registrar = [...reads, ...changes];
const venuePath = `/api/admin/v1/venues/${bukoto.id}`;

const alias = { id: "0199a3f0-0000-7000-8000-0000000000f1", name: "Bukoto Grounds", kind: "FORMER_NAME" as const };
const council = {
  id: "0199a3f0-0000-7000-8000-0000000000f2",
  authority: "METRO_COUNCIL_NO",
  value: "MC-0042",
  validFrom: "2024-01-15",
  validTo: null,
};
const ended = {
  ...council,
  id: "0199a3f0-0000-7000-8000-0000000000f3",
  value: "MC-0007",
  validFrom: "2019-03-01",
  validTo: "2023-12-31",
};
const named = venue({ aliases: [alias], externalIdentifiers: [council, ended] });

function open(shown: Venue = named, capabilities: Parameters<typeof venueAnswers>[0] = registrar, answers: Answers = {}) {
  const requests = fakeApi({
    ...venueAnswers(capabilities),
    [`GET ${venuePath}`]: { status: 200, body: shown },
    [`GET ${venuePath}/status-changes`]: { status: 200, body: { items: [] } },
    ...answers,
  });
  renderWithQuery(
    <TenantWorkspaceLayout>
      <VenuePage />
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

describe("Venue Names & IDs", () => {
  it("lists the aliases and the identifiers, ended ones included", async () => {
    open();
    await loaded();
    const panel = screen.getByRole("tabpanel");
    expect(within(panel).getAllByText("Bukoto Grounds").length).toBeGreaterThan(0);
    expect(within(panel).getAllByText("Former name").length).toBeGreaterThan(0);
    expect(within(panel).getAllByText("From 15 Jan 2024").length).toBeGreaterThan(0);
    expect(within(panel).getAllByText("1 Mar 2019 to 31 Dec 2023 (ended)").length).toBeGreaterThan(0);
    // Only the open identifier can be ended.
    expect(within(panel).getByRole("button", { name: "End METRO_COUNCIL_NO MC-0042" })).toBeTruthy();
    expect(within(panel).queryByRole("button", { name: "End METRO_COUNCIL_NO MC-0007" })).toBeNull();
  });

  it("adds an alias from the version it opened on, and shows one the venue already has under the name", async () => {
    let refuse = true;
    const requests = open(named, registrar, {
      [`POST ${venuePath}/aliases`]: () =>
        refuse
          ? {
              status: 409,
              body: problem("VENUE_ALIAS_EXISTS", 409, {
                detail: "The venue already has this name.",
                errors: [{ field: "name", code: "VENUE_ALIAS_EXISTS" }],
              }),
            }
          : {
              status: 200,
              body: {
                ...named,
                aliases: [alias, { id: "0199a3f0-0000-7000-8000-0000000000f4", name: "The Stadium", kind: "NICKNAME" }],
                version: 5,
              },
            },
    });
    await loaded();
    fireEvent.click(screen.getByRole("button", { name: "Add alias" }));
    expect(within(dialog()).getByRole("heading", { name: `Add an alias for ${named.name}` })).toBeTruthy();
    type("Name", "Bukoto Grounds");
    type("Kind", "NICKNAME");
    press("Add alias");
    await waitFor(() => expect(within(dialog()).getByLabelText("Name").getAttribute("aria-invalid")).toBe("true"));
    expect(within(dialog()).getByText("The venue already has this name.")).toBeTruthy();

    refuse = false;
    type("Name", "The Stadium");
    press("Add alias");
    await waitFor(() => expect(screen.queryByRole("dialog")).toBeNull());
    const sent = requests.filter((request) => request.path === `${venuePath}/aliases`).at(-1)!;
    expect(sent.headers.get("If-Match")).toBe('"4"');
    expect(sent.body).toEqual({ name: "The Stadium", kind: "NICKNAME" });
    expect(await screen.findByText("Alias added.")).toBeTruthy();
    expect(screen.getAllByText("The Stadium").length).toBeGreaterThan(0);
  });

  it("removes an alias with the reason", async () => {
    const requests = open(named, registrar, {
      [`POST ${venuePath}/aliases/${alias.id}/remove`]: { status: 200, body: { ...named, aliases: [], version: 5 } },
    });
    await loaded();
    fireEvent.click(screen.getByRole("button", { name: "Remove the alias Bukoto Grounds" }));
    type("Reason", "Never used for this ground");
    press("Remove alias");
    const sent = await posted(requests, `${venuePath}/aliases/${alias.id}/remove`);
    expect(sent.body).toEqual({ reason: "Never used for this ground" });
    expect(sent.headers.get("If-Match")).toBe('"4"');
    expect(await screen.findByText("Alias removed.")).toBeTruthy();
  });

  it("adds an identifier with the authority in capitals, and shows a value already given under it", async () => {
    let refuse = true;
    const requests = open(named, registrar, {
      [`POST ${venuePath}/external-identifiers`]: () =>
        refuse
          ? {
              status: 409,
              body: problem("VENUE_EXTERNAL_ID_EXISTS", 409, {
                detail: "Another venue has or had this identifier.",
                errors: [{ field: "value", code: "VENUE_EXTERNAL_ID_EXISTS" }],
              }),
            }
          : { status: 200, body: { ...named, version: 5 } },
    });
    await loaded();
    fireEvent.click(screen.getByRole("button", { name: "Add identifier" }));
    type("Issued by (code)", "kmsa_ground_no");
    type("Identifier", "G-014");
    press("Add identifier");
    await waitFor(() => expect(within(dialog()).getByLabelText("Identifier").getAttribute("aria-invalid")).toBe("true"));
    expect(within(dialog()).getByText("Another venue has or had this identifier.")).toBeTruthy();

    refuse = false;
    press("Add identifier");
    await waitFor(() => expect(screen.queryByRole("dialog")).toBeNull());
    const sent = requests.filter((request) => request.path === `${venuePath}/external-identifiers`).at(-1)!;
    expect(sent.body).toEqual({ authority: "KMSA_GROUND_NO", value: "G-014", validFrom: null });
  });

  it("ends an identifier on the day given, with the optional reason", async () => {
    const requests = open(named, registrar, {
      [`POST ${venuePath}/external-identifiers/${council.id}/end`]: { status: 200, body: { ...named, version: 5 } },
    });
    await loaded();
    fireEvent.click(screen.getByRole("button", { name: "End METRO_COUNCIL_NO MC-0042" }));
    await waitFor(() => expect(within(dialog()).getByLabelText("Last day valid")).toHaveProperty("disabled", false));
    type("Last day valid", "2026-06-30");
    type("Reason (optional)", "Council renumbered its facilities");
    press("End identifier");
    const sent = await posted(requests, `${venuePath}/external-identifiers/${council.id}/end`);
    expect(sent.body).toEqual({ validTo: "2026-06-30", reason: "Council renumbered its facilities" });
  });

  it("offers Reload when the venue changed since the dialog opened", async () => {
    open(named, registrar, {
      [`POST ${venuePath}/aliases/${alias.id}/remove`]: { status: 409, body: problem("VENUE_CONCURRENCY_CONFLICT", 409) },
    });
    await loaded();
    fireEvent.click(screen.getByRole("button", { name: "Remove the alias Bukoto Grounds" }));
    type("Reason", "Never used for this ground");
    press("Remove alias");
    expect(await within(dialog()).findByText("This record changed since you opened it")).toBeTruthy();
    expect(within(dialog()).getByRole("button", { name: "Reload" })).toBeTruthy();
  });

  it("takes no new name or identifier for a closed venue, but its aliases can still be removed and identifiers ended", async () => {
    open(venue({ ...named, status: "CLOSED" }));
    await loaded();
    expect(screen.queryByRole("button", { name: "Add alias" })).toBeNull();
    expect(screen.queryByRole("button", { name: "Add identifier" })).toBeNull();
    expect(screen.getAllByText(`${named.name} is closed: no new one can be added.`)).toHaveLength(2);
    expect(screen.getByRole("button", { name: "Remove the alias Bukoto Grounds" })).toBeTruthy();
    expect(screen.getByRole("button", { name: "End METRO_COUNCIL_NO MC-0042" })).toBeTruthy();
  });

  it("disables the changes with the reason on a read-only tenant", async () => {
    open(named, readOnlyCapabilities(reads, changes));
    await loaded();
    expect(screen.getByRole("button", { name: "Add alias" })).toHaveProperty("disabled", true);
    expect(screen.getByRole("button", { name: "Remove the alias Bukoto Grounds" })).toHaveProperty("disabled", true);
  });

  it("shows a viewer the names and identifiers without a change control", async () => {
    open(named, reads);
    await loaded();
    expect(within(screen.getByRole("tabpanel")).queryAllByRole("button")).toEqual([]);
    expect(within(screen.getByRole("tabpanel")).getAllByText("MC-0042").length).toBeGreaterThan(0);
  });
});
