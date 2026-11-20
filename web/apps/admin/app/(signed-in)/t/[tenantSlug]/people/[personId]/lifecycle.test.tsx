// Registry → People → a person → the header's Edit details and status moves (S06 D9; T7c-2b): only the moves D9 allows
// from the person's status, each with its action; deactivate and reactivate take an optional reason, the others need
// one; marking as deceased is final. Each move sends the version its dialog opened on.
import { cleanup, fireEvent, screen, waitFor, within } from "@testing-library/react";
import { afterEach, describe, expect, it, vi } from "vitest";
import { readOnlyCapabilities } from "@/test/capabilities";
import { john, person } from "@/test/people";
import { personPageAnswers, tenantId } from "@/test/person-page";
import { fakeApi, problem, renderWithQuery, type Answers, type RecordedRequest } from "@/test/render";
import TenantWorkspaceLayout from "../../layout";
import { personQueryKey, type Person, type PersonStatus } from "../people-data";
import PersonPage from "./page";

const navigation = vi.hoisted(() => ({ personId: "", search: "tab=roles" }));

vi.mock("next/navigation", () => ({
  useParams: () => ({ tenantSlug: "kmsa", personId: navigation.personId }),
  usePathname: () => `/t/kmsa/people/${navigation.personId}`,
  useSearchParams: () => new URLSearchParams(navigation.search),
  useRouter: () => ({ replace: vi.fn(), push: vi.fn() }),
}));

const reads = ["person.read", "sport.read", "tenant.settings.read"];
const moveActions = [
  "person.deactivate",
  "person.reactivate",
  "person.archive",
  "person.unarchive",
  "person.mark_deceased",
];
const editActions = ["person.update", "person.read_private", "person.duplicate_check"];
const registrar = [...reads, ...editActions, ...moveActions];

let rendered: ReturnType<typeof renderWithQuery>;

function open(shown: Person, capabilities: Parameters<typeof personPageAnswers>[1] = registrar, answers: Answers = {}) {
  navigation.personId = shown.id;
  const requests = fakeApi({ ...personPageAnswers(shown, capabilities), ...answers });
  rendered = renderWithQuery(
    <TenantWorkspaceLayout>
      <PersonPage />
    </TenantWorkspaceLayout>,
  );
  return requests;
}

const loaded = (shown: Person = john) => screen.findByRole("heading", { level: 1, name: shown.displayName });
const moveButtons = () =>
  ["Deactivate", "Reactivate", "Archive", "Unarchive", "Mark as deceased"].filter(
    (name) => screen.queryByRole("button", { name }) !== null,
  );
const dialog = () => screen.getByRole("dialog");
const sentTo = async (requests: RecordedRequest[], path: string) => {
  let found: RecordedRequest | undefined;
  await waitFor(() => {
    found = requests.find((request) => request.method === "POST" && request.path === path && request.body !== undefined);
    expect(found).toBeTruthy();
  });
  return found!;
};
const movedTo = (status: PersonStatus) => () => ({ status: 200, body: person({ status, version: 8 }) });

afterEach(() => {
  navigation.search = "tab=roles";
  cleanup();
  vi.unstubAllGlobals();
});

describe("Person header actions", () => {
  it.each<[PersonStatus, string[]]>([
    ["ACTIVE", ["Deactivate", "Archive", "Mark as deceased"]],
    ["INACTIVE", ["Reactivate", "Archive", "Mark as deceased"]],
    ["ARCHIVED", ["Unarchive"]],
    ["DECEASED", []],
  ])("offers only D9's moves from %s, and Edit details in every status", async (status, expected) => {
    const shown = person({ status });
    open(shown);
    await loaded(shown);
    expect(moveButtons()).toEqual(expected);
    expect(screen.getByRole("link", { name: "Edit details" }).getAttribute("href")).toBe(`/t/kmsa/people/${john.id}/edit`);
  });

  it("shows a viewer neither Edit details nor a move", async () => {
    open(john, reads);
    await loaded();
    expect(moveButtons()).toEqual([]);
    expect(screen.queryByRole("link", { name: "Edit details" })).toBeNull();
    expect(screen.queryByRole("button", { name: "Edit details" })).toBeNull();
  });

  it("doesn't offer Edit details without the private read", async () => {
    open(john, [...reads, "person.update", "person.duplicate_check", ...moveActions]);
    await loaded();
    expect(screen.queryByRole("link", { name: "Edit details" })).toBeNull();
    expect(moveButtons()).toEqual(["Deactivate", "Archive", "Mark as deceased"]);
  });

  it("on a read-only tenant, doesn't show Edit details to a user who couldn't use it once writable", async () => {
    open(john, readOnlyCapabilities(reads, ["person.update", "person.duplicate_check", ...moveActions]));
    await loaded();
    expect(screen.queryByRole("button", { name: "Edit details" })).toBeNull();
    expect(screen.getByRole("button", { name: "Archive" }).hasAttribute("disabled")).toBe(true);
  });

  it("on a read-only tenant, shows them disabled with one reason", async () => {
    open(john, readOnlyCapabilities([...reads, "person.read_private"], ["person.update", "person.duplicate_check", ...moveActions]));
    await loaded();
    const edit = screen.getByRole("button", { name: "Edit details" });
    const archive = screen.getByRole("button", { name: "Archive" });
    expect(edit.hasAttribute("disabled")).toBe(true);
    expect(archive.hasAttribute("disabled")).toBe(true);
    const note = document.getElementById(edit.getAttribute("aria-describedby") ?? "");
    expect(note?.textContent).toMatch(/read-only/i);
    expect(archive.getAttribute("aria-describedby")).toBe(edit.getAttribute("aria-describedby"));
  });

  it("deactivates without a reason, on the version it opened on, then announces it and focuses the status", async () => {
    // On Overview, which shows the status history.
    navigation.search = "tab=overview";
    const requests = open(john, registrar, {
      [`POST /api/admin/v1/people/${john.id}/deactivate`]: movedTo("INACTIVE"),
      [`GET /api/admin/v1/people/${john.id}/private`]: { status: 200, body: { id: john.id, dateOfBirth: null, consents: [] } },
    });
    await loaded();
    fireEvent.click(screen.getByRole("button", { name: "Deactivate" }));
    expect(within(dialog()).getByLabelText("Reason (optional)")).toBeTruthy();
    fireEvent.click(within(dialog()).getByRole("button", { name: "Deactivate" }));
    const request = await sentTo(requests, `/api/admin/v1/people/${john.id}/deactivate`);
    expect(request.body).toEqual({ reason: null });
    expect(request.headers.get("If-Match")).toBe('"7"');
    await waitFor(() => expect(screen.queryByRole("dialog")).toBeNull());
    expect(screen.getAllByRole("status").map((item) => item.textContent)).toContain("John Ssekandi: made inactive.");
    await waitFor(() => expect(document.activeElement?.id).toBe("person-status"));
    expect(moveButtons()).toEqual(["Reactivate", "Archive", "Mark as deceased"]);
    // The status history is read again.
    await waitFor(() => expect(requests.filter((item) => item.path.endsWith("/status-changes"))).toHaveLength(2));
  });

  it("sends the version the dialog opened on, not a newer read's", async () => {
    let version = 7;
    const requests = open(john, registrar, {
      [`GET /api/admin/v1/people/${john.id}`]: () => ({
        status: 200,
        body: person({ version, displayName: version === 7 ? "John Ssekandi" : "J. Ssekandi" }),
      }),
      [`POST /api/admin/v1/people/${john.id}/archive`]: movedTo("ARCHIVED"),
    });
    await loaded();
    fireEvent.click(screen.getByRole("button", { name: "Archive" }));
    // The page reads the person again while the dialog is open (the photo's URLs expire), and shows the newer read.
    version = 8;
    await rendered.queryClient.refetchQueries({ queryKey: personQueryKey(tenantId, john.id) });
    await screen.findByRole("heading", { level: 1, name: "J. Ssekandi", hidden: true });
    fireEvent.change(within(dialog()).getByLabelText("Reason"), { target: { value: "Left the club." } });
    fireEvent.click(within(dialog()).getByRole("button", { name: "Archive" }));
    expect((await sentTo(requests, `/api/admin/v1/people/${john.id}/archive`)).headers.get("If-Match")).toBe('"7"');
  });

  it("sends the optional reason when one is given", async () => {
    const shown = person({ status: "INACTIVE" });
    const requests = open(shown, registrar, { [`POST /api/admin/v1/people/${john.id}/reactivate`]: movedTo("ACTIVE") });
    await loaded(shown);
    fireEvent.click(screen.getByRole("button", { name: "Reactivate" }));
    fireEvent.change(within(dialog()).getByLabelText("Reason (optional)"), { target: { value: "  Back for the new season.  " } });
    fireEvent.click(within(dialog()).getByRole("button", { name: "Reactivate" }));
    expect((await sentTo(requests, `/api/admin/v1/people/${john.id}/reactivate`)).body).toEqual({
      reason: "Back for the new season.",
    });
  });

  it.each([
    ["ACTIVE", "Archive", "archive"],
    ["ARCHIVED", "Unarchive", "unarchive"],
    ["INACTIVE", "Mark as deceased", "mark-deceased"],
  ] as const)("from %s, %s needs a reason", async (status, label, path) => {
    const shown = person({ status });
    const requests = open(shown, registrar, { [`POST /api/admin/v1/people/${john.id}/${path}`]: movedTo("ARCHIVED") });
    await loaded(shown);
    fireEvent.click(screen.getByRole("button", { name: label }));
    fireEvent.click(within(dialog()).getByRole("button", { name: label }));
    expect(await within(dialog()).findByText("Give a reason: it is kept in the audit trail.")).toBeTruthy();
    expect(requests.some((request) => request.path.endsWith(`/${path}`))).toBe(false);
    fireEvent.change(within(dialog()).getByLabelText("Reason"), { target: { value: "Left the association in 2025." } });
    fireEvent.click(within(dialog()).getByRole("button", { name: label }));
    expect((await sentTo(requests, `/api/admin/v1/people/${john.id}/${path}`)).body).toEqual({
      reason: "Left the association in 2025.",
    });
  });

  it("says marking as deceased is final", async () => {
    open(john);
    await loaded();
    fireEvent.click(screen.getByRole("button", { name: "Mark as deceased" }));
    expect(within(dialog()).getByText(/This is final/)).toBeTruthy();
  });

  it("keeps the API's refusal in the dialog", async () => {
    open(john, registrar, {
      [`POST /api/admin/v1/people/${john.id}/archive`]: { status: 409, body: problem("PERSON_STATUS_INVALID", 409) },
    });
    await loaded();
    fireEvent.click(screen.getByRole("button", { name: "Archive" }));
    fireEvent.change(within(dialog()).getByLabelText("Reason"), { target: { value: "Duplicate record." } });
    fireEvent.click(within(dialog()).getByRole("button", { name: "Archive" }));
    expect(await within(dialog()).findByText(/status/i, { selector: "[role=alert] *" })).toBeTruthy();
  });

  it("offers Reload after a concurrency conflict", async () => {
    const requests = open(john, registrar, {
      [`POST /api/admin/v1/people/${john.id}/deactivate`]: { status: 409, body: problem("PERSON_CONCURRENCY_CONFLICT", 409) },
    });
    await loaded();
    fireEvent.click(screen.getByRole("button", { name: "Deactivate" }));
    fireEvent.click(within(dialog()).getByRole("button", { name: "Deactivate" }));
    fireEvent.click(await within(dialog()).findByRole("button", { name: "Reload" }));
    await waitFor(() => expect(screen.queryByRole("dialog")).toBeNull());
    await waitFor(() =>
      expect(requests.filter((item) => item.method === "GET" && item.path === `/api/admin/v1/people/${john.id}`).length).toBe(2),
    );
  });
});
