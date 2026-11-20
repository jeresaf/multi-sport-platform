// Registry → People → a person → Edit details (S06 D8; T7c-2b): the whole biodata, the date of birth from the private
// read. A change to the names, the date of birth or a nationality runs the duplicate check with the person left out,
// and the review (T7b's) asks for a reason; other changes save without one. The update sends the version the page
// opened on.
import { cleanup, fireEvent, screen, waitFor, within } from "@testing-library/react";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { readOnlyCapabilities } from "@/test/capabilities";
import { john, person } from "@/test/people";
import { createQueryClient } from "@/lib/query-client";
import { personPageAnswers, tenantId } from "@/test/person-page";
import { fakeApi, problem, renderWithQuery, type Answer, type Answers, type RecordedRequest } from "@/test/render";
import TenantWorkspaceLayout from "../../../layout";
import { personPrivateQueryKey, personQueryKey } from "../../people-data";
import EditPersonPage from "./page";

const navigation = vi.hoisted(() => ({ personId: "", push: vi.fn() }));

vi.mock("next/navigation", () => ({
  useParams: () => ({ tenantSlug: "kmsa", personId: navigation.personId }),
  usePathname: () => `/t/kmsa/people/${navigation.personId}/edit`,
  useSearchParams: () => new URLSearchParams(),
  useRouter: () => ({ replace: vi.fn(), push: navigation.push }),
}));

const reads = ["person.read", "person.read_private", "sport.read", "tenant.settings.read"];
const registrar = [...reads, "person.update", "person.duplicate_check"];
const privateOf = (dateOfBirth: string | null): Answer => ({
  status: 200,
  body: { id: john.id, dateOfBirth, consents: [] },
});

const candidate = {
  personId: "0199a3f0-0000-7000-8000-0000000000f2",
  displayName: "Jon Ssekandi",
  birthYear: 1998,
  status: "ACTIVE",
  confidence: "LIKELY",
  evidence: [
    { code: "SAME_DATE_OF_BIRTH", value: null },
    { code: "NAME_SIMILARITY", value: "91" },
  ],
  athleteVariants: [],
};
const checked = (candidates: unknown[], checkToken = "token-1"): Answer => ({ status: 200, body: { candidates, checkToken } });

let rendered: ReturnType<typeof renderWithQuery>;

function open(
  capabilities: Parameters<typeof personPageAnswers>[1] = registrar,
  answers: Answers = {},
  shown = john,
  queryClient = createQueryClient(),
) {
  navigation.personId = shown.id;
  const requests = fakeApi({
    ...personPageAnswers(shown, capabilities),
    [`GET /api/admin/v1/people/${shown.id}/private`]: privateOf("1998-03-14"),
    "POST /api/admin/v1/people/duplicate-check": checked([]),
    [`PUT /api/admin/v1/people/${shown.id}`]: (request) => ({
      status: 200,
      body: { ...shown, ...(request.body as object), version: shown.version + 1 },
    }),
    ...answers,
  });
  rendered = renderWithQuery(
    <TenantWorkspaceLayout>
      <EditPersonPage />
    </TenantWorkspaceLayout>,
    queryClient,
  );
  return requests;
}

const sent = (requests: RecordedRequest[], method: string, path: string) =>
  requests.filter((request) => request.method === method && request.path === path);
const checks = (requests: RecordedRequest[]) => sent(requests, "POST", "/api/admin/v1/people/duplicate-check");
const updates = (requests: RecordedRequest[]) => sent(requests, "PUT", `/api/admin/v1/people/${john.id}`);
const type = (label: string, value: string) => fireEvent.change(screen.getByLabelText(label), { target: { value } });
const loaded = () => screen.findByRole("heading", { name: "Edit details" });
const save = () => fireEvent.click(screen.getByRole("button", { name: "Save" }));
const saved = () => waitFor(() => expect(navigation.push).toHaveBeenCalledWith(`/t/kmsa/people/${john.id}`));

beforeEach(() => navigation.push.mockReset());

afterEach(() => {
  cleanup();
  vi.unstubAllGlobals();
});

describe("Edit details", () => {
  it("shows the person's biodata, the date of birth from the private read", async () => {
    open();
    await loaded();
    expect(screen.getByLabelText<HTMLInputElement>("First name").value).toBe("John");
    expect(screen.getByLabelText<HTMLInputElement>("Last name").value).toBe("Ssekandi");
    // The default display name ("first last") is left empty, so it follows the names.
    expect(screen.getByLabelText<HTMLInputElement>("Display name (optional)").value).toBe("");
    expect(screen.getByLabelText<HTMLInputElement>("Date of birth (optional)").value).toBe("1998-03-14");
    expect(screen.getByLabelText<HTMLSelectElement>("Gender").value).toBe("MALE");
    await waitFor(() => expect(screen.getByLabelText<HTMLSelectElement>("Nationality").value).toBe("UG"));
    expect(screen.getByLabelText<HTMLInputElement>("Height in cm (optional)").value).toBe("178");
    expect(screen.getByLabelText<HTMLInputElement>("Weight in kg (optional)").value).toBe("72.5");
  });

  it("saves a change to the display name, height or weight without a check, on the version it opened on", async () => {
    const requests = open();
    await loaded();
    type("Display name (optional)", "Johnny Ssekandi");
    type("Weight in kg (optional)", "74");
    save();
    await saved();
    expect(checks(requests)).toEqual([]);
    const [update] = updates(requests);
    expect(update?.headers.get("If-Match")).toBe('"7"');
    // The whole biodata: a field left out would be cleared, the date of birth included.
    expect(update?.body).toEqual({
      firstName: "John",
      middleName: null,
      lastName: "Ssekandi",
      otherNames: null,
      displayName: "Johnny Ssekandi",
      gender: "MALE",
      dateOfBirth: "1998-03-14",
      nationality: "UG",
      secondaryNationality: null,
      heightCm: 178,
      weightKg: 74,
      checkToken: null,
      overrideReason: null,
    });
  });

  it("checks a name change with the person left out and no identifiers, and saves with the check's token", async () => {
    const requests = open();
    await loaded();
    type("First name", "Jonathan");
    save();
    await saved();
    expect(checks(requests).map((request) => request.body)).toEqual([
      {
        firstName: "Jonathan",
        middleName: null,
        lastName: "Ssekandi",
        otherNames: null,
        dateOfBirth: "1998-03-14",
        nationality: "UG",
        secondaryNationality: null,
        externalIdentifiers: [],
        personId: john.id,
      },
    ]);
    expect(updates(requests)[0]?.body).toMatchObject({
      firstName: "Jonathan",
      displayName: null,
      checkToken: "token-1",
      overrideReason: null,
    });
  });

  it.each([
    ["Date of birth (optional)", "1998-03-15"],
    ["Nationality", ""],
    ["Second nationality", "UG"],
  ])("checks a change to %s", async (label, value) => {
    const requests = open();
    await loaded();
    await waitFor(() => expect(screen.getByLabelText<HTMLSelectElement>("Nationality").value).toBe("UG"));
    type(label, value);
    save();
    await saved();
    expect(checks(requests)).toHaveLength(1);
  });

  it("reviews the people the check finds and saves only with a reason, as the same person changed", async () => {
    const requests = open(registrar, { "POST /api/admin/v1/people/duplicate-check": checked([candidate]) });
    await loaded();
    type("First name", "Jon");
    save();
    const review = await screen.findByRole("heading", { name: "Check for duplicates" });
    await waitFor(() => expect(document.activeElement).toBe(review));
    expect(screen.getByText(/1 person may be Jon Ssekandi, with your changes/)).toBeTruthy();
    expect(screen.getByRole("heading", { name: "With your changes" })).toBeTruthy();
    // The candidate's birth year only (slice acceptance); the person's own date is what was typed.
    expect(screen.getByText("14 March 1998")).toBeTruthy();
    expect(screen.queryByText(/1998-03-14/)).toBeNull();

    fireEvent.click(screen.getByRole("button", { name: "Save the changes" }));
    expect((await screen.findByRole("alert")).textContent).toBe("Say why this is a different person.");
    expect(updates(requests)).toEqual([]);

    type("Why is Jon Ssekandi a different person?", "Cousins; checked with the club.");
    fireEvent.click(screen.getByRole("button", { name: "Save the changes" }));
    await saved();
    expect(updates(requests)[0]?.body).toMatchObject({
      firstName: "Jon",
      checkToken: "token-1",
      overrideReason: "Cousins; checked with the club.",
    });
  });

  it("goes back to the form from the review, keeping what was typed", async () => {
    open(registrar, { "POST /api/admin/v1/people/duplicate-check": checked([candidate]) });
    await loaded();
    type("First name", "Jon");
    save();
    await screen.findByRole("heading", { name: "Check for duplicates" });
    fireEvent.click(screen.getByRole("button", { name: "Change the details" }));
    await loaded();
    expect(screen.getByLabelText<HTMLInputElement>("First name").value).toBe("Jon");
  });

  it("checks again when someone added a match meanwhile, and shows the new list", async () => {
    let checkCount = 0;
    const requests = open(registrar, {
      "POST /api/admin/v1/people/duplicate-check": () => (++checkCount === 1 ? checked([]) : checked([candidate], "token-2")),
      [`PUT /api/admin/v1/people/${john.id}`]: { status: 409, body: problem("PERSON_DUPLICATE_CHECK_STALE", 409) },
    });
    await loaded();
    type("First name", "Jon");
    save();
    await screen.findByRole("heading", { name: "Check for duplicates" });
    expect(screen.getByText(/just before the save, so the check ran again/)).toBeTruthy();
    expect(checks(requests)).toHaveLength(2);
    expect(updates(requests)).toHaveLength(1);
  });

  it("shows a refused field under it, on the form", async () => {
    open(registrar, {
      [`PUT /api/admin/v1/people/${john.id}`]: {
        status: 422,
        body: problem("PERSON_DATE_OF_BIRTH_IN_FUTURE", 422, {
          errors: [{ field: "dateOfBirth", code: "PERSON_DATE_OF_BIRTH_IN_FUTURE", message: "In the future." }],
        }),
      },
      "POST /api/admin/v1/people/duplicate-check": {
        status: 422,
        body: problem("PERSON_DATE_OF_BIRTH_IN_FUTURE", 422, {
          errors: [{ field: "dateOfBirth", code: "PERSON_DATE_OF_BIRTH_IN_FUTURE", message: "In the future." }],
        }),
      },
    });
    await loaded();
    type("Date of birth (optional)", "2030-01-01");
    save();
    const field = screen.getByLabelText("Date of birth (optional)");
    await waitFor(() => expect(field.getAttribute("aria-invalid")).toBe("true"));
    expect(navigation.push).not.toHaveBeenCalled();
  });

  it("checks the numbers before asking the API", async () => {
    const requests = open();
    await loaded();
    type("Height in cm (optional)", "20");
    save();
    expect(await screen.findByText("Enter whole centimetres from 50 to 260.")).toBeTruthy();
    expect(updates(requests)).toEqual([]);
  });

  it("offers Reload after a concurrency conflict, and starts over from the person read again", async () => {
    let version = 7;
    const requests = open(registrar, {
      [`GET /api/admin/v1/people/${john.id}`]: () => ({
        status: 200,
        body: person({ version, displayName: version === 7 ? "John Ssekandi" : "J. Ssekandi" }),
      }),
      [`PUT /api/admin/v1/people/${john.id}`]: { status: 409, body: problem("PERSON_CONCURRENCY_CONFLICT", 409) },
    });
    await loaded();
    type("Weight in kg (optional)", "74");
    save();
    const conflict = await screen.findByText("This record changed since you opened it");
    version = 8;
    fireEvent.click(within(conflict.closest("[role=alert]") as HTMLElement).getByRole("button", { name: "Reload" }));
    await waitFor(() =>
      expect(screen.getByLabelText<HTMLInputElement>("Display name (optional)").value).toBe("J. Ssekandi"),
    );
    expect(screen.queryByText("This record changed since you opened it")).toBeNull();
    expect(updates(requests)[0]?.headers.get("If-Match")).toBe('"7"');
  });

  it("keeps a display name that isn't the default", async () => {
    const requests = open(registrar, {}, person({ displayName: "Johnny Ssekandi" }));
    await loaded();
    expect(screen.getByLabelText<HTMLInputElement>("Display name (optional)").value).toBe("Johnny Ssekandi");
    type("First name", "Jonathan");
    save();
    await saved();
    expect(updates(requests)[0]?.body).toMatchObject({ firstName: "Jonathan", displayName: "Johnny Ssekandi" });
  });

  it("keeps what is typed when the person is read again in the background (their photo's URLs expire)", async () => {
    let version = 7;
    const requests = open(registrar, {
      [`GET /api/admin/v1/people/${john.id}`]: () => ({ status: 200, body: person({ version }) }),
    });
    await loaded();
    type("Weight in kg (optional)", "74");
    version = 8;
    await rendered.queryClient.refetchQueries({ queryKey: personQueryKey(tenantId, john.id) });
    expect(screen.getByLabelText<HTMLInputElement>("Weight in kg (optional)").value).toBe("74");
    save();
    await saved();
    // The change is based on the version the page opened on: someone else's change since is a conflict, not lost.
    expect(updates(requests)[0]?.headers.get("If-Match")).toBe('"7"');
  });

  it("starts from a fresh read, not a kept date of birth older than the person's version", async () => {
    // The person page kept version 7 and its date of birth; someone has since corrected the date (version 8).
    const queryClient = createQueryClient();
    queryClient.setQueryData(personQueryKey(tenantId, john.id), john);
    queryClient.setQueryData(personPrivateQueryKey(tenantId, john.id), { id: john.id, dateOfBirth: "1998-03-14", consents: [] });
    // The countries kept too, so nothing but the fresh reads holds the form back (re-review R4).
    queryClient.setQueryData(["reference", "countries"], {
      items: [{ code: "UG", alpha3Code: "UGA", numericCode: "800", name: "Uganda" }],
      page: { nextCursor: null, hasMore: false },
    });
    const requests = open(
      registrar,
      {
        [`GET /api/admin/v1/people/${john.id}`]: { status: 200, body: person({ version: 8 }) },
        [`GET /api/admin/v1/people/${john.id}/private`]: privateOf("1998-04-14"),
      },
      john,
      queryClient,
    );
    await loaded();
    expect(screen.getByLabelText<HTMLInputElement>("Date of birth (optional)").value).toBe("1998-04-14");
    type("Weight in kg (optional)", "74");
    save();
    await saved();
    expect(updates(requests)[0]?.headers.get("If-Match")).toBe('"8"');
    expect(updates(requests)[0]?.body).toMatchObject({ dateOfBirth: "1998-04-14" });
    expect(sent(requests, "GET", `/api/admin/v1/people/${john.id}/private`)).toHaveLength(1);
  });

  it("doesn't start the form from a kept date of birth when the fresh private read fails", async () => {
    const queryClient = createQueryClient();
    queryClient.setQueryData(personQueryKey(tenantId, john.id), john);
    queryClient.setQueryData(personPrivateQueryKey(tenantId, john.id), { id: john.id, dateOfBirth: "1998-03-14", consents: [] });
    const requests = open(
      registrar,
      {
        [`GET /api/admin/v1/people/${john.id}`]: { status: 200, body: person({ version: 8 }) },
        [`GET /api/admin/v1/people/${john.id}/private`]: { status: 500, body: problem("PLATFORM_INTERNAL_ERROR", 500) },
      },
      john,
      queryClient,
    );
    expect(await screen.findByRole("alert")).toBeTruthy();
    expect(screen.queryByRole("heading", { name: "Edit details" })).toBeNull();
    expect(screen.queryByLabelText("Date of birth (optional)")).toBeNull();
    expect(updates(requests)).toEqual([]);
  });

  it("reads the private details once: a save marks them stale for the person's page without reading them here", async () => {
    const requests = open();
    await loaded();
    type("First name", "Jonathan");
    save();
    await saved();
    expect(sent(requests, "GET", `/api/admin/v1/people/${john.id}/private`)).toHaveLength(1);
    expect(rendered.queryClient.getQueryState(personPrivateQueryKey(tenantId, john.id))?.isInvalidated).toBe(true);
  });

  it("stays on the review when only the reason is refused", async () => {
    const requests = open(registrar, {
      "POST /api/admin/v1/people/duplicate-check": checked([candidate]),
      [`PUT /api/admin/v1/people/${john.id}`]: {
        status: 422,
        body: problem("VALIDATION_INVALID_REQUEST", 422, {
          errors: [{ field: "overrideReason", code: "VALIDATION_MAX_LENGTH", message: "Too long." }],
        }),
      },
    });
    await loaded();
    type("First name", "Jon");
    save();
    await screen.findByRole("heading", { name: "Check for duplicates" });
    type("Why is Jon Ssekandi a different person?", "Cousins.");
    fireEvent.click(screen.getByRole("button", { name: "Save the changes" }));
    await waitFor(() => expect(updates(requests)).toHaveLength(1));
    await waitFor(() => expect(screen.getByRole("button", { name: "Save the changes" }).hasAttribute("disabled")).toBe(false));
    expect(screen.getByRole("heading", { name: "Check for duplicates" })).toBeTruthy();
    expect(screen.getByLabelText<HTMLTextAreaElement>("Why is Jon Ssekandi a different person?").value).toBe("Cousins.");
  });

  it("renders a long four-part name", async () => {
    const long = person({
      firstName: "Nakimuli",
      middleName: "Ruth",
      lastName: "Nansubuga-Ssempijja",
      otherNames: "Nnaalongo",
      displayName: "Nakimuli Ruth Nansubuga-Ssempijja Nnaalongo",
    });
    open(registrar, {}, long);
    await loaded();
    expect(screen.getByLabelText<HTMLInputElement>("Other names (optional)").value).toBe("Nnaalongo");
    expect(screen.getAllByText("Nakimuli Ruth Nansubuga-Ssempijja Nnaalongo").length).toBeGreaterThan(0);
  });

  it("isn't offered without the private read, which holds the date of birth the update would clear", async () => {
    const requests = open(["person.read", "person.update", "person.duplicate_check", "sport.read", "tenant.settings.read"]);
    expect(await screen.findByRole("heading", { name: "You can't open this page" })).toBeTruthy();
    expect(sent(requests, "GET", `/api/admin/v1/people/${john.id}/private`)).toEqual([]);
  });

  it("says why on a read-only tenant", async () => {
    open(readOnlyCapabilities(reads, ["person.update", "person.duplicate_check"]));
    await loaded();
    expect(document.getElementById("edit-person-unavailable")?.textContent).toMatch(/read-only/i);
    expect(screen.queryByRole("button", { name: "Save" })).toBeNull();
  });
});
