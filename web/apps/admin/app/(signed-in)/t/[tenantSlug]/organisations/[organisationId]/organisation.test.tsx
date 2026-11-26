// Registry → Organisations → an organisation (S07 T6b-1): Overview with the parent and member organisations, the
// teams with their sport named from its definition, the status and parent history, the status moves the machine
// allows and the parent dialog, each with a required reason and the version its dialog opened on; a conflict offers
// Reload; a viewer sees everything without a change control.
import { errorMessage } from "@msp/api-client/errors";
import { act, cleanup, fireEvent, screen, waitFor, within } from "@testing-library/react";
import { afterEach, describe, expect, it, vi } from "vitest";
import { readOnlyCapabilities } from "@/test/capabilities";
import {
  academy,
  kisaasi,
  listItem,
  listPage,
  ntinda,
  organisation,
  organisationAnswers,
  seeta,
  summary,
} from "@/test/organisations";
import { fakeApi, problem, renderWithQuery, type Answers, type RecordedRequest } from "@/test/render";
import TenantWorkspaceLayout from "../../layout";
import type { Organisation, OrganisationStatus } from "../organisations-data";
import OrganisationPage from "./page";

const navigation = vi.hoisted(() => ({ organisationId: "", search: "" }));

vi.mock("next/navigation", () => ({
  useParams: () => ({ tenantSlug: "kmsa", organisationId: navigation.organisationId }),
  usePathname: () => `/t/kmsa/organisations/${navigation.organisationId}`,
  useSearchParams: () => new URLSearchParams(navigation.search),
  useRouter: () => ({ replace: vi.fn(), push: vi.fn() }),
}));

const reads = ["organisation.read", "venue.read", "sport.read", "tenant.settings.read"];
const registrar = [...reads, "organisation.status.change", "organisation.parent.set"];

const actor = { actorKind: "USER", actorId: "0199a3f0-0000-7000-8000-000000000001" } as const;
const created = {
  id: "0199a3f0-0000-7000-8000-0000000000c1",
  fromStatus: null,
  toStatus: "ACTIVE",
  reason: null,
  occurredAt: "2026-11-02T07:30:00Z",
  ...actor,
};

const swiftsWomen = {
  id: "0199a3f0-0000-7000-8000-0000000000d1",
  name: "Ntinda Swifts FC",
  slug: "ntinda-swifts-fc-women",
  variant: "RINGBALL_SEVENS",
  genderCategory: "WOMEN",
  ageGroup: "SENIOR",
  level: "FIRST",
  status: "ACTIVE",
} as const;

function open(shown: Organisation = ntinda, capabilities: Parameters<typeof organisationAnswers>[0] = registrar, answers: Answers = {}) {
  navigation.organisationId = shown.id;
  const requests = fakeApi({
    ...organisationAnswers(capabilities),
    [`GET /api/admin/v1/organisations/${shown.id}`]: { status: 200, body: shown },
    [`GET /api/admin/v1/organisations/${shown.id}/status-changes`]: { status: 200, body: { items: [created] } },
    [`GET /api/admin/v1/organisations/${shown.id}/parent-links`]: { status: 200, body: { items: [] } },
    "GET /api/admin/v1/organisations": listPage([ntinda, kisaasi, academy, seeta].map(listItem)),
    ...answers,
  });
  renderWithQuery(
    <TenantWorkspaceLayout>
      <OrganisationPage />
    </TenantWorkspaceLayout>,
  );
  return requests;
}

const loaded = (shown: Organisation = ntinda) => screen.findByRole("heading", { level: 1, name: shown.displayName });
const moveLabels = ["Reactivate", "Make inactive", "Suspend", "Lift suspension", "Dissolve", "Archive", "Unarchive"];
const moveButtons = () => moveLabels.filter((name) => screen.queryByRole("button", { name }) !== null);
const dialog = () => screen.getByRole("dialog");
const sentTo = async (requests: RecordedRequest[], path: string) => {
  let found: RecordedRequest | undefined;
  await waitFor(() => {
    found = requests.find((request) => request.method === "POST" && request.path === path && request.body !== undefined);
    expect(found).toBeTruthy();
  });
  return found!;
};
const statusPath = `/api/admin/v1/organisations/${ntinda.id}/status`;
const parentPath = `/api/admin/v1/organisations/${ntinda.id}/parent`;

afterEach(() => {
  navigation.search = "";
  cleanup();
  vi.useRealTimers();
  vi.unstubAllGlobals();
});

describe("Organisation page", () => {
  it("shows the overview: names, contact, primary venue, parent and member organisations", async () => {
    const shown = organisation({ parent: summary(kisaasi), children: [summary(academy)] });
    open(shown);
    await loaded();
    const overview = screen.getByRole("tabpanel");
    expect(within(overview).getByText("Ntinda Swifts Sports Club Limited")).toBeTruthy();
    expect(within(overview).getByText("1998")).toBeTruthy();
    expect(await within(overview).findByText("Uganda")).toBeTruthy();
    const website = within(overview).getByRole("link", { name: /ntinda-swifts\.example/ });
    expect(website.getAttribute("rel")).toBe("noopener noreferrer");
    expect(within(overview).getByRole("link", { name: "Bukoto Community Stadium" }).getAttribute("href")).toBe(
      `/t/kmsa/venues/${ntinda.primaryVenue!.id}`,
    );
    expect(within(overview).getByRole("link", { name: "Kisaasi Kestrels SC" }).getAttribute("href")).toBe(
      `/t/kmsa/organisations/${kisaasi.id}`,
    );
    const members = within(overview).getByRole("list", { name: "Organisations under Ntinda Swifts SC" });
    expect(within(members).getByRole("link", { name: "Ntinda Swifts Academy" })).toBeTruthy();
    // A member that isn't active says so; colour alone never does.
    expect(within(members).getByText("Suspended")).toBeTruthy();
    expect(screen.getByRole("img", { name: "No logo for Ntinda Swifts SC" })).toBeTruthy();
  });

  it("says what isn't recorded, and links only a web address", async () => {
    open(organisation({ ...kisaasi, id: ntinda.id, website: "javascript:alert(1)" }));
    await loaded(kisaasi);
    const overview = screen.getByRole("tabpanel");
    expect(within(overview).queryByRole("link", { name: /javascript/ })).toBeNull();
    expect(within(overview).getByText("javascript:alert(1)")).toBeTruthy();
    expect(within(overview).getAllByText("Not recorded").length).toBe(5); // legal name, founded, city, email, phone
    expect(within(overview).getAllByText("None").length).toBe(5); // short name, description, venue, parent, members
  });

  it("shows the venue's name without a link to whoever can't read venues", async () => {
    open(ntinda, ["organisation.read", "sport.read", "tenant.settings.read"]);
    await loaded();
    const overview = screen.getByRole("tabpanel");
    expect(within(overview).getByText("Bukoto Community Stadium")).toBeTruthy();
    expect(within(overview).queryByRole("link", { name: "Bukoto Community Stadium" })).toBeNull();
  });

  it("shows the logo the read signs", async () => {
    open(organisation({ logo: { thumbnailUrl: "https://api.example/t.webp", mediumUrl: "https://api.example/m.webp", expiresAt: "2026-11-26T09:00:00Z" } }));
    await loaded();
    expect(screen.getByRole("img", { name: "Logo of Ntinda Swifts SC" }).getAttribute("src")).toBe("https://api.example/m.webp");
  });

  it("lists its teams with category, sport from the definition and status", async () => {
    navigation.search = "tab=teams";
    open(organisation({ teams: [swiftsWomen] }));
    await loaded();
    const row = (await screen.findByText("Ntinda Swifts FC")).closest("tr")!;
    await within(row).findAllByText("Ringball sevens");
    expect(within(row).getAllByRole("cell").map((cell) => cell.textContent)).toEqual(["Senior Women", "Ringball sevens", "Active"]);
    cleanup();

    open(ntinda);
    expect(await screen.findByText("Ntinda Swifts SC has no teams yet.")).toBeTruthy();
  });

  it("shows the status history and the parent links with their dates and reasons", async () => {
    navigation.search = "tab=history";
    open(ntinda, registrar, {
      [`GET /api/admin/v1/organisations/${ntinda.id}/parent-links`]: {
        status: 200,
        body: {
          items: [
            { id: "0199a3f0-0000-7000-8000-0000000000f2", parent: summary(academy), validFrom: "2026-03-01", validTo: null, reason: "Moved under the academy.", setAt: "2026-03-01T08:00:00Z", ...actor },
            { id: "0199a3f0-0000-7000-8000-0000000000f1", parent: summary(kisaasi), validFrom: "2024-01-15", validTo: "2026-03-01", reason: "Joined Kisaasi.", setAt: "2024-01-15T08:00:00Z", ...actor },
          ],
        },
      },
    });
    await loaded();
    expect(await screen.findByText("Recorded as active")).toBeTruthy();
    const current = (await screen.findByRole("link", { name: "Ntinda Swifts Academy" })).closest("tr")!;
    expect(within(current).getAllByRole("cell").map((cell) => cell.textContent)).toEqual(["From 1 Mar 2026", "Moved under the academy."]);
    const ended = screen.getByRole("link", { name: "Kisaasi Kestrels SC" }).closest("tr")!;
    expect(within(ended).getAllByRole("cell")[0]?.textContent).toBe("15 Jan 2024 to 1 Mar 2026");
  });

  it("says when it never had a parent", async () => {
    navigation.search = "tab=history";
    open();
    expect(await screen.findByText("Ntinda Swifts SC has never had a parent organisation.")).toBeTruthy();
  });

  it("answers an unknown organisation with Not found", async () => {
    open(ntinda, registrar, {
      [`GET /api/admin/v1/organisations/${ntinda.id}`]: { status: 404, body: problem("ORGANISATION_NOT_FOUND", 404) },
    });
    expect(await screen.findByText(/not found|doesn.t exist/i)).toBeTruthy();
  });
});

describe("Organisation status moves", () => {
  it.each<[OrganisationStatus, string[]]>([
    ["ACTIVE", ["Make inactive", "Suspend", "Dissolve", "Archive"]],
    ["INACTIVE", ["Reactivate", "Suspend", "Dissolve", "Archive"]],
    ["SUSPENDED", ["Make inactive", "Lift suspension", "Dissolve", "Archive"]],
    ["DISSOLVED", ["Archive"]],
    ["ARCHIVED", ["Unarchive"]],
  ])("offers only the machine's moves from %s", async (status, expected) => {
    const shown = organisation({ status });
    open(shown);
    await loaded(shown);
    expect(moveButtons()).toEqual(expected);
  });

  it("shows a viewer every tab without a change control", async () => {
    open(ntinda, reads);
    await loaded();
    expect(moveButtons()).toEqual([]);
    for (const tab of ["Overview", "Teams", "History"]) {
      expect(screen.getByRole("tab", { name: tab })).toBeTruthy();
    }
    expect(screen.queryByRole("button", { name: /parent/i })).toBeNull();
    const main = screen.getByRole("heading", { level: 1, name: ntinda.displayName }).closest("section")!;
    expect(within(main).queryAllByRole("button")).toEqual([]);
  });

  it("on a read-only tenant, shows the moves and the parent disabled with the reason", async () => {
    open(ntinda, readOnlyCapabilities(reads, ["organisation.status.change", "organisation.parent.set"]));
    await loaded();
    for (const name of ["Dissolve", "Set parent"]) {
      const button = screen.getByRole("button", { name });
      expect(button.hasAttribute("disabled")).toBe(true);
      expect(document.getElementById(button.getAttribute("aria-describedby") ?? "")?.textContent).toMatch(/read-only/i);
    }
  });

  it("needs a reason, sends the target and the version, then announces it and focuses the status", async () => {
    const requests = open(ntinda, registrar, {
      [`POST ${statusPath}`]: { status: 200, body: organisation({ status: "SUSPENDED", version: 4 }) },
    });
    await loaded();
    fireEvent.click(screen.getByRole("button", { name: "Suspend" }));
    // An organisation's status never changes its teams' (RULE-ORG-005).
    expect(within(dialog()).getByText(/Its teams keep their own statuses/)).toBeTruthy();
    fireEvent.click(within(dialog()).getByRole("button", { name: "Suspend" }));
    expect(await within(dialog()).findByText("Give a reason: it is kept in the audit trail.")).toBeTruthy();
    expect(requests.some((request) => request.method === "POST")).toBe(false);

    fireEvent.change(within(dialog()).getByLabelText("Reason"), { target: { value: "Fees unpaid for the season." } });
    fireEvent.click(within(dialog()).getByRole("button", { name: "Suspend" }));
    const request = await sentTo(requests, statusPath);
    expect(request.body).toEqual({ status: "SUSPENDED", reason: "Fees unpaid for the season." });
    expect(request.headers.get("If-Match")).toBe('"3"');
    await waitFor(() => expect(screen.queryByRole("dialog")).toBeNull());
    expect(screen.getAllByRole("status").map((item) => item.textContent)).toContain("Ntinda Swifts SC: suspended.");
    await waitFor(() => expect(document.activeElement?.id).toBe("organisation-status"));
    expect(moveButtons()).toEqual(["Make inactive", "Lift suspension", "Dissolve", "Archive"]);
  });

  it("unarchives to the status it had before it was archived, so a dissolved one stays dissolved", async () => {
    const archived = organisation({ status: "ARCHIVED" });
    const history = {
      items: [
        { ...created, id: "0199a3f0-0000-7000-8000-0000000000c3", fromStatus: "DISSOLVED", toStatus: "ARCHIVED", reason: "Old record." },
        { ...created, id: "0199a3f0-0000-7000-8000-0000000000c2", fromStatus: "ACTIVE", toStatus: "DISSOLVED", reason: "Wound up." },
        created,
      ],
    };
    const requests = open(archived, registrar, {
      [`GET /api/admin/v1/organisations/${archived.id}/status-changes`]: { status: 200, body: history },
      [`POST ${statusPath}`]: { status: 200, body: organisation({ status: "DISSOLVED", version: 4 }) },
    });
    await loaded(archived);
    fireEvent.click(screen.getByRole("button", { name: "Unarchive" }));
    expect(await within(dialog()).findByText(/goes back to dissolved/)).toBeTruthy();
    fireEvent.change(within(dialog()).getByLabelText("Reason"), { target: { value: "Archived by mistake." } });
    fireEvent.click(within(dialog()).getByRole("button", { name: "Unarchive" }));
    expect((await sentTo(requests, statusPath)).body).toEqual({ status: "DISSOLVED", reason: "Archived by mistake." });
  });

  it("offers Reload after a concurrency conflict", async () => {
    const requests = open(ntinda, registrar, {
      [`POST ${statusPath}`]: { status: 409, body: problem("ORGANISATION_CONCURRENCY_CONFLICT", 409) },
    });
    await loaded();
    fireEvent.click(screen.getByRole("button", { name: "Archive" }));
    fireEvent.change(within(dialog()).getByLabelText("Reason"), { target: { value: "Merged with another record." } });
    fireEvent.click(within(dialog()).getByRole("button", { name: "Archive" }));
    expect(await within(dialog()).findByText("This record changed since you opened it")).toBeTruthy();
    fireEvent.click(within(dialog()).getByRole("button", { name: "Reload" }));
    await waitFor(() => expect(screen.queryByRole("dialog")).toBeNull());
    await waitFor(() =>
      expect(requests.filter((item) => item.method === "GET" && item.path === `/api/admin/v1/organisations/${ntinda.id}`).length).toBe(2),
    );
  });
});

describe("Organisation parent", () => {
  // The search waits 300 ms after the last key: the clock is moved on, not waited for.
  const search = async (text: string) => {
    vi.useFakeTimers({ shouldAdvanceTime: true });
    fireEvent.change(within(dialog()).getByLabelText("Find the parent"), { target: { value: text } });
    await act(() => vi.advanceTimersByTimeAsync(300));
  };

  it("finds a parent by search, leaves out itself and its members, and sends the choice, the reason and the version", async () => {
    const shown = organisation({ children: [summary(academy)] });
    const requests = open(shown, registrar, {
      [`POST ${parentPath}`]: { status: 200, body: organisation({ parent: summary(kisaasi), version: 4 }) },
    });
    await loaded();
    fireEvent.click(screen.getByRole("button", { name: "Set parent" }));
    // Nothing refused yet: no alert.
    expect(within(dialog()).queryByRole("alert")).toBeNull();
    expect(within(dialog()).queryByLabelText("No parent")).toBeNull();
    await search("Kis");
    const choice = await within(dialog()).findByLabelText("Kisaasi Kestrels SC");
    // Itself and its member organisation aren't offered; a dissolved one is, as unavailable.
    expect(within(dialog()).queryByLabelText("Ntinda Swifts SC")).toBeNull();
    expect(within(dialog()).queryByLabelText(/Ntinda Swifts Academy/)).toBeNull();
    expect(within(dialog()).getByLabelText(/Seeta Sparrows FC/)).toHaveProperty("disabled", true);
    expect(within(dialog()).getByText("Dissolved: it can't take member organisations.")).toBeTruthy();
    const directory = requests.filter((request) => request.path === "/api/admin/v1/organisations").at(-1)!;
    expect(new URLSearchParams(directory.search).get("q")).toBe("Kis");

    fireEvent.click(within(dialog()).getByRole("button", { name: "Save parent" }));
    expect(await within(dialog()).findByText("Choose the parent, or no parent.")).toBeTruthy();
    fireEvent.click(choice);
    fireEvent.change(within(dialog()).getByLabelText("Reason"), { target: { value: "Merged into Kisaasi Kestrels." } });
    fireEvent.click(within(dialog()).getByRole("button", { name: "Save parent" }));
    const request = await sentTo(requests, parentPath);
    expect(request.body).toEqual({ parentId: kisaasi.id, reason: "Merged into Kisaasi Kestrels." });
    expect(request.headers.get("If-Match")).toBe('"3"');
    await waitFor(() => expect(screen.queryByRole("dialog")).toBeNull());
    expect(await screen.findByRole("button", { name: "Change parent" })).toBeTruthy();
  });

  it("clears the parent with No parent", async () => {
    const requests = open(organisation({ parent: summary(kisaasi) }), registrar, {
      [`POST ${parentPath}`]: { status: 200, body: organisation({ version: 4 }) },
    });
    await loaded();
    fireEvent.click(screen.getByRole("button", { name: "Change parent" }));
    expect(within(dialog()).getByText(/Its parent is Kisaasi Kestrels SC/)).toBeTruthy();
    fireEvent.click(within(dialog()).getByLabelText("No parent"));
    fireEvent.change(within(dialog()).getByLabelText("Reason"), { target: { value: "Now independent." } });
    fireEvent.click(within(dialog()).getByRole("button", { name: "Save parent" }));
    expect((await sentTo(requests, parentPath)).body).toEqual({ parentId: null, reason: "Now independent." });
  });

  it("shows a cycle the server finds under the choice", async () => {
    const requests = open(ntinda, registrar, {
      [`POST ${parentPath}`]: {
        status: 422,
        body: problem("ORGANISATION_PARENT_CYCLE", 422, {
          errors: [{ field: "parentId", code: "ORGANISATION_PARENT_CYCLE", message: "It is below this organisation." }],
        }),
      },
    });
    await loaded();
    fireEvent.click(screen.getByRole("button", { name: "Set parent" }));
    await search("Kis");
    fireEvent.click(await within(dialog()).findByLabelText("Kisaasi Kestrels SC"));
    fireEvent.change(within(dialog()).getByLabelText("Reason"), { target: { value: "Restructure." } });
    fireEvent.click(within(dialog()).getByRole("button", { name: "Save parent" }));
    await sentTo(requests, parentPath);
    await waitFor(() => expect(dialog().querySelector("#parent-choice-error")).toBeTruthy());
    // The server's refusal of the field, not the form's own "Choose the parent…".
    const shown = dialog().querySelector("#parent-choice-error")!.textContent;
    expect([errorMessage("ORGANISATION_PARENT_CYCLE"), "It is below this organisation."]).toContain(shown);
  });

  it("offers a dissolved organisation no new parent, only removing the one it has", async () => {
    open(organisation({ status: "DISSOLVED" }));
    await loaded();
    const set = screen.getByRole("button", { name: "Set parent" });
    expect(set.hasAttribute("disabled")).toBe(true);
    expect(document.getElementById(set.getAttribute("aria-describedby") ?? "")?.textContent).toMatch(
      /Dissolved: it can't take a parent/,
    );
    cleanup();

    const requests = open(organisation({ status: "DISSOLVED", parent: summary(kisaasi) }), registrar, {
      [`POST ${parentPath}`]: { status: 200, body: organisation({ status: "DISSOLVED", version: 4 }) },
    });
    await loaded();
    fireEvent.click(screen.getByRole("button", { name: "Remove parent" }));
    expect(within(dialog()).queryByLabelText("Find the parent")).toBeNull();
    fireEvent.click(within(dialog()).getByLabelText("No parent"));
    fireEvent.change(within(dialog()).getByLabelText("Reason"), { target: { value: "Wound up." } });
    fireEvent.click(within(dialog()).getByRole("button", { name: "Save parent" }));
    expect((await sentTo(requests, parentPath)).body).toEqual({ parentId: null, reason: "Wound up." });
  });

  it("offers Reload after a concurrency conflict", async () => {
    open(organisation({ parent: summary(kisaasi) }), registrar, {
      [`POST ${parentPath}`]: { status: 409, body: problem("ORGANISATION_CONCURRENCY_CONFLICT", 409) },
    });
    await loaded();
    fireEvent.click(screen.getByRole("button", { name: "Change parent" }));
    fireEvent.click(within(dialog()).getByLabelText("No parent"));
    fireEvent.change(within(dialog()).getByLabelText("Reason"), { target: { value: "Now independent." } });
    fireEvent.click(within(dialog()).getByRole("button", { name: "Save parent" }));
    expect(await within(dialog()).findByText("This record changed since you opened it")).toBeTruthy();
  });
});
