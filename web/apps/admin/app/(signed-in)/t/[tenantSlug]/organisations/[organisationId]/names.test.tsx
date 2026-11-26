// Registry → Organisations → an organisation → Names & IDs (S07 T6b-2b, D10): the aliases and the external
// identifiers, ended ones included; an alias added or removed with a reason and an identifier added or ended, each from
// the version its dialog opened on; the API's refusals under their fields; a conflict offers Reload; a dissolved
// organisation takes no new one but its aliases can still be removed and its identifiers ended; a viewer sees the lists
// without a change control.
import { cleanup, fireEvent, screen, waitFor, within } from "@testing-library/react";
import { afterEach, describe, expect, it, vi } from "vitest";
import { readOnlyCapabilities } from "@/test/capabilities";
import { ntinda, organisation, organisationAnswers } from "@/test/organisations";
import { fakeApi, problem, renderWithQuery, type Answers, type RecordedRequest } from "@/test/render";
import TenantWorkspaceLayout from "../../layout";
import type { Organisation } from "../organisations-data";
import OrganisationPage from "./page";

vi.mock("next/navigation", () => ({
  useParams: () => ({ tenantSlug: "kmsa", organisationId: "0199a3f0-0000-7000-8000-0000000000e1" }),
  usePathname: () => "/t/kmsa/organisations/0199a3f0-0000-7000-8000-0000000000e1",
  useSearchParams: () => new URLSearchParams("tab=names"),
  useRouter: () => ({ replace: vi.fn(), push: vi.fn() }),
}));

const reads = ["organisation.read", "venue.read", "sport.read", "tenant.settings.read"];
const changes = [
  "organisation.alias.add",
  "organisation.alias.remove",
  "organisation.external_id.add",
  "organisation.external_id.end",
];
const registrar = [...reads, ...changes];
const organisationPath = `/api/admin/v1/organisations/${ntinda.id}`;

const alias = { id: "0199a3f0-0000-7000-8000-0000000000f1", name: "Ntinda Swallows SC", kind: "FORMER_NAME" as const };
const clubNumber = {
  id: "0199a3f0-0000-7000-8000-0000000000f2",
  authority: "KMSA_CLUB_NO",
  value: "C-0117",
  validFrom: "2021-02-01",
  validTo: null,
};
const ended = {
  ...clubNumber,
  id: "0199a3f0-0000-7000-8000-0000000000f3",
  value: "C-0042",
  validFrom: "2015-05-04",
  validTo: "2021-01-31",
};
const named = organisation({ aliases: [alias], externalIdentifiers: [clubNumber, ended] });

function open(
  shown: Organisation = named,
  capabilities: Parameters<typeof organisationAnswers>[0] = registrar,
  answers: Answers = {},
) {
  const requests = fakeApi({
    ...organisationAnswers(capabilities),
    [`GET ${organisationPath}`]: { status: 200, body: shown },
    [`GET ${organisationPath}/status-changes`]: { status: 200, body: { items: [] } },
    [`GET ${organisationPath}/parent-links`]: { status: 200, body: { items: [] } },
    ...answers,
  });
  renderWithQuery(
    <TenantWorkspaceLayout>
      <OrganisationPage />
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

describe("Organisation Names & IDs", () => {
  it("is a tab of the organisation, listing the aliases and the identifiers, ended ones included", async () => {
    open();
    await loaded();
    expect(screen.getByRole("tab", { name: "Names & IDs", selected: true })).toBeTruthy();
    const panel = screen.getByRole("tabpanel");
    expect(within(panel).getAllByText("Ntinda Swallows SC").length).toBeGreaterThan(0);
    expect(within(panel).getAllByText("Former name").length).toBeGreaterThan(0);
    expect(within(panel).getAllByText("From 1 Feb 2021").length).toBeGreaterThan(0);
    expect(within(panel).getAllByText("4 May 2015 to 31 Jan 2021 (ended)").length).toBeGreaterThan(0);
    expect(within(panel).getByRole("button", { name: "End KMSA_CLUB_NO C-0117" })).toBeTruthy();
    expect(within(panel).queryByRole("button", { name: "End KMSA_CLUB_NO C-0042" })).toBeNull();
  });

  it("adds an alias from the version it opened on, and shows a name the organisation already has under the name", async () => {
    let refuse = true;
    const requests = open(named, registrar, {
      [`POST ${organisationPath}/aliases`]: () =>
        refuse
          ? {
              status: 409,
              body: problem("ORGANISATION_ALIAS_EXISTS", 409, {
                detail: "The organisation is already known by this name.",
                errors: [{ field: "name", code: "ORGANISATION_ALIAS_EXISTS" }],
              }),
            }
          : {
              status: 200,
              body: {
                ...named,
                aliases: [alias, { id: "0199a3f0-0000-7000-8000-0000000000f4", name: "The Swifts", kind: "NICKNAME" }],
                version: 4,
              },
            },
    });
    await loaded();
    fireEvent.click(screen.getByRole("button", { name: "Add alias" }));
    expect(within(dialog()).getByRole("heading", { name: `Add an alias for ${named.displayName}` })).toBeTruthy();
    type("Name", "Ntinda Swifts");
    type("Kind", "NICKNAME");
    press("Add alias");
    await waitFor(() => expect(within(dialog()).getByLabelText("Name").getAttribute("aria-invalid")).toBe("true"));
    expect(within(dialog()).getByText("The organisation is already known by this name.")).toBeTruthy();

    refuse = false;
    type("Name", "The Swifts");
    press("Add alias");
    await waitFor(() => expect(screen.queryByRole("dialog")).toBeNull());
    const sent = requests.filter((request) => request.path === `${organisationPath}/aliases`).at(-1)!;
    expect(sent.headers.get("If-Match")).toBe('"3"');
    expect(sent.body).toEqual({ name: "The Swifts", kind: "NICKNAME" });
    expect(await screen.findByText("Alias added.")).toBeTruthy();
    expect(screen.getAllByText("The Swifts").length).toBeGreaterThan(0);
  });

  it("refuses an alias longer than the organisation's name may be, before sending", async () => {
    const requests = open();
    await loaded();
    fireEvent.click(screen.getByRole("button", { name: "Add alias" }));
    type("Name", "N".repeat(121));
    press("Add alias");
    await waitFor(() => expect(within(dialog()).getByLabelText("Name").getAttribute("aria-invalid")).toBe("true"));
    expect(requests.some((request) => request.method === "POST")).toBe(false);
  });

  it("removes an alias with the reason", async () => {
    const requests = open(named, registrar, {
      [`POST ${organisationPath}/aliases/${alias.id}/remove`]: {
        status: 200,
        body: { ...named, aliases: [], version: 4 },
      },
    });
    await loaded();
    fireEvent.click(screen.getByRole("button", { name: "Remove the alias Ntinda Swallows SC" }));
    type("Reason", "Never the club's name");
    press("Remove alias");
    const sent = await posted(requests, `${organisationPath}/aliases/${alias.id}/remove`);
    expect(sent.body).toEqual({ reason: "Never the club's name" });
    expect(sent.headers.get("If-Match")).toBe('"3"');
    expect(await screen.findByText("Alias removed.")).toBeTruthy();
  });

  it("adds an identifier with the authority in capitals, and shows a value already given under it", async () => {
    let refuse = true;
    const requests = open(named, registrar, {
      [`POST ${organisationPath}/external-identifiers`]: () =>
        refuse
          ? {
              status: 409,
              body: problem("ORGANISATION_EXTERNAL_ID_EXISTS", 409, {
                detail: "An organisation already has or had this identifier.",
                errors: [{ field: "value", code: "ORGANISATION_EXTERNAL_ID_EXISTS" }],
              }),
            }
          : { status: 200, body: { ...named, version: 4 } },
    });
    await loaded();
    fireEvent.click(screen.getByRole("button", { name: "Add identifier" }));
    type("Issued by (code)", "company_reg_no");
    type("Identifier", "80020001234567");
    press("Add identifier");
    await waitFor(() => expect(within(dialog()).getByLabelText("Identifier").getAttribute("aria-invalid")).toBe("true"));
    expect(within(dialog()).getByText("An organisation already has or had this identifier.")).toBeTruthy();

    refuse = false;
    press("Add identifier");
    await waitFor(() => expect(screen.queryByRole("dialog")).toBeNull());
    const sent = requests.filter((request) => request.path === `${organisationPath}/external-identifiers`).at(-1)!;
    expect(sent.headers.get("If-Match")).toBe('"3"');
    expect(sent.body).toEqual({ authority: "COMPANY_REG_NO", value: "80020001234567", validFrom: null });
  });

  it("ends an identifier on the day given, with the optional reason", async () => {
    const requests = open(named, registrar, {
      [`POST ${organisationPath}/external-identifiers/${clubNumber.id}/end`]: {
        status: 200,
        body: { ...named, version: 4 },
      },
    });
    await loaded();
    fireEvent.click(screen.getByRole("button", { name: "End KMSA_CLUB_NO C-0117" }));
    await waitFor(() => expect(within(dialog()).getByLabelText("Last day valid")).toHaveProperty("disabled", false));
    type("Last day valid", "2026-06-30");
    type("Reason (optional)", "KMSA renumbered its clubs");
    press("End identifier");
    const sent = await posted(requests, `${organisationPath}/external-identifiers/${clubNumber.id}/end`);
    expect(sent.body).toEqual({ validTo: "2026-06-30", reason: "KMSA renumbered its clubs" });
    expect(sent.headers.get("If-Match")).toBe('"3"');
  });

  it("offers Reload when the organisation changed since the dialog opened", async () => {
    open(named, registrar, {
      [`POST ${organisationPath}/aliases/${alias.id}/remove`]: {
        status: 409,
        body: problem("ORGANISATION_CONCURRENCY_CONFLICT", 409),
      },
    });
    await loaded();
    fireEvent.click(screen.getByRole("button", { name: "Remove the alias Ntinda Swallows SC" }));
    type("Reason", "Never the club's name");
    press("Remove alias");
    expect(await within(dialog()).findByText("This record changed since you opened it")).toBeTruthy();
    expect(within(dialog()).getByRole("button", { name: "Reload" })).toBeTruthy();
  });

  it.each([
    ["DISSOLVED", "dissolved"],
    ["ARCHIVED", "archived"],
  ] as const)(
    "takes no new name or identifier for a %s organisation, but its aliases can still be removed and identifiers ended",
    async (status, word) => {
      open(organisation({ ...named, status }));
      await loaded();
      expect(screen.queryByRole("button", { name: "Add alias" })).toBeNull();
      expect(screen.queryByRole("button", { name: "Add identifier" })).toBeNull();
      expect(screen.getAllByText(`${named.displayName} is ${word}: no new one can be added.`)).toHaveLength(2);
      expect(screen.getByRole("button", { name: "Remove the alias Ntinda Swallows SC" })).toBeTruthy();
      expect(screen.getByRole("button", { name: "End KMSA_CLUB_NO C-0117" })).toBeTruthy();
    },
  );

  it("takes new names for a suspended organisation", async () => {
    open(organisation({ ...named, status: "SUSPENDED" }));
    await loaded();
    expect(screen.getByRole("button", { name: "Add alias" })).toHaveProperty("disabled", false);
    expect(screen.getByRole("button", { name: "Add identifier" })).toHaveProperty("disabled", false);
  });

  it("disables the changes with the reason on a read-only tenant", async () => {
    open(named, readOnlyCapabilities(reads, changes));
    await loaded();
    expect(screen.getByRole("button", { name: "Add alias" })).toHaveProperty("disabled", true);
    expect(screen.getByRole("button", { name: "Remove the alias Ntinda Swallows SC" })).toHaveProperty("disabled", true);
  });

  it("shows a viewer the names and identifiers without a change control", async () => {
    open(named, reads);
    await loaded();
    expect(within(screen.getByRole("tabpanel")).queryAllByRole("button")).toEqual([]);
    expect(within(screen.getByRole("tabpanel")).getAllByText("C-0117").length).toBeGreaterThan(0);
  });
});
