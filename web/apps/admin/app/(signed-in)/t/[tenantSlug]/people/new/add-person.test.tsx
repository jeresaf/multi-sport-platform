// Registry → People → Add person (S06 T7b): search-first create, the duplicate check and its review. The review shows
// evidence and a birth year, never another person's date of birth (slice acceptance); a shared identifier blocks the
// save; a stale or expired check runs again once.
import { cleanup, fireEvent, screen, waitFor, within } from "@testing-library/react";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { readOnlyCapabilities, tenantCapabilities } from "@/test/capabilities";
import { denis, john, listItem, person } from "@/test/people";
import { fakeApi, problem, renderWithQuery, type Answer, type Answers, type RecordedRequest } from "@/test/render";
import TenantWorkspaceLayout from "../../layout";
import AddPersonPage from "./page";

const navigation = vi.hoisted(() => ({ push: vi.fn() }));

vi.mock("next/navigation", () => ({
  useParams: () => ({ tenantSlug: "kmsa" }),
  usePathname: () => "/t/kmsa/people/new",
  useSearchParams: () => new URLSearchParams(),
  useRouter: () => ({ replace: vi.fn(), push: navigation.push }),
}));

const tenantId = "0199a3f0-0000-7000-8000-00000000000a";
const me: Answer = {
  status: 200,
  body: {
    id: "0199a3f0-0000-7000-8000-000000000001",
    email: "brian.ssemakula@kmsa.example",
    platformRoles: [],
    tenants: [
      { tenantId, slug: "kmsa", displayName: "Kampala Metro Sports Association", status: "ACTIVE", roles: ["REGISTRAR"] },
    ],
  },
};

const actions = ["person.read", "person.create"];
const listPage = (items: unknown[], hasMore = false) => ({
  status: 200,
  body: { items, page: { nextCursor: hasMore ? "next" : null, hasMore } },
});

/** The new person once added: Sarah Namubiru, a KMSA netball volunteer (pilot-customer.md style). */
const created = person({
  id: "0199a3f0-0000-7000-8000-0000000000f9",
  firstName: "Sarah",
  lastName: "Namubiru",
  displayName: "Sarah Namubiru",
  gender: "FEMALE",
  birthYear: 1998,
});

const candidate = (overrides: Record<string, unknown> = {}) => ({
  personId: john.id,
  displayName: "John Ssekandi",
  birthYear: 1998,
  status: "ACTIVE",
  confidence: "LIKELY",
  evidence: [
    { code: "SAME_DATE_OF_BIRTH", value: null },
    { code: "NAME_SIMILARITY", value: "86" },
  ],
  athleteVariants: ["RINGBALL_SEVENS"],
  ...overrides,
});

const checked = (candidates: unknown[], checkToken = "token-1"): Answer => ({ status: 200, body: { candidates, checkToken } });
const createdAnswer: Answer = { status: 201, body: created };

function open(granted = actions, answers: Answers = {}, capabilities = tenantCapabilities(granted)) {
  const requests = fakeApi({
    "GET /api/account/v1/me": me,
    "GET /api/admin/v1/me/capabilities": { status: 200, body: capabilities },
    "GET /api/public/v1/reference/countries": listPage([
      { code: "UG", alpha3Code: "UGA", numericCode: "800", name: "Uganda" },
      { code: "KE", alpha3Code: "KEN", numericCode: "404", name: "Kenya" },
    ]),
    "GET /api/admin/v1/people": listPage([]),
    "POST /api/admin/v1/people/duplicate-check": checked([]),
    "POST /api/admin/v1/people": createdAnswer,
    ...answers,
  });
  renderWithQuery(
    <TenantWorkspaceLayout>
      <AddPersonPage />
    </TenantWorkspaceLayout>,
  );
  return requests;
}

const sent = (requests: RecordedRequest[], method: string, path: string) =>
  requests.filter((request) => request.method === method && request.path === path);
const checks = (requests: RecordedRequest[]) => sent(requests, "POST", "/api/admin/v1/people/duplicate-check");
const creates = (requests: RecordedRequest[]) => sent(requests, "POST", "/api/admin/v1/people");

const type = (label: string, value: string) => fireEvent.change(screen.getByLabelText(label), { target: { value } });

async function enterName(first = "John", last = "Ssekandi") {
  await screen.findByRole("heading", { name: "Who are you adding?" });
  type("First name", first);
  type("Last name", last);
  fireEvent.click(screen.getByRole("button", { name: "Continue" }));
  await screen.findByRole("heading", { name: "Their details" });
}

/** Through the name and the details, to the check. */
async function submitDetails(details: () => void = () => undefined) {
  await enterName();
  details();
  fireEvent.click(screen.getByRole("button", { name: "Check and add" }));
}

const reviewOf = (name: string) =>
  screen.getByRole("link", { name: `Open ${name}` }).closest("li") as HTMLElement;

beforeEach(() => navigation.push.mockReset());

afterEach(() => {
  cleanup();
  vi.unstubAllGlobals();
});

describe("add person: the name first", () => {
  it("lists the people already registered with the typed name, each linking to their page", async () => {
    const requests = open(actions, {
      "GET /api/admin/v1/people": (request) =>
        new URLSearchParams(request.search).get("q") === "John Ssekandi" ? listPage([listItem(john)], true) : listPage([]),
    });
    await screen.findByRole("heading", { name: "Who are you adding?" });
    expect(screen.getByText("People with a similar name show here as you type.")).toBeTruthy();

    type("First name", " John ");
    type("Last name", "Ssekandi");

    const link = await screen.findByRole("link", { name: "John Ssekandi" });
    expect(link.getAttribute("href")).toBe(`/t/kmsa/people/${john.id}`);
    expect(screen.getByText("Born 1998")).toBeTruthy();
    expect(screen.getByText(/The first 10 are shown/)).toBeTruthy();
    const search = new URLSearchParams(sent(requests, "GET", "/api/admin/v1/people").at(-1)!.search);
    expect(search.get("limit")).toBe("10");
  });

  it("says when no one has the name, and asks for both names before going on", async () => {
    open();
    await screen.findByRole("heading", { name: "Who are you adding?" });
    fireEvent.click(screen.getByRole("button", { name: "Continue" }));
    expect(await screen.findByText("Enter the first name.")).toBeTruthy();
    expect(screen.getByText("Enter the last name.")).toBeTruthy();

    type("First name", "Sarah");
    type("Last name", "Namubiru");
    expect(await screen.findByText("No one with this name yet")).toBeTruthy();
  });
});

describe("add person: no one matches", () => {
  it("checks the details and adds the person with the check's token, then opens their page", async () => {
    const requests = open();
    await submitDetails(() => {
      type("Date of birth (optional)", "1998-05-02");
      type("Nationality", "UG");
      type("Height in cm (optional)", "171");
      fireEvent.click(screen.getByRole("button", { name: "Add an identifier" }));
      type("Issued by (code)", "kmsa_reg_no");
      type("Identifier", " KM-2026-0201 ");
    });

    await waitFor(() => expect(navigation.push).toHaveBeenCalledWith(`/t/kmsa/people/${created.id}`));
    expect(checks(requests)[0]!.body).toEqual({
      firstName: "John",
      middleName: null,
      lastName: "Ssekandi",
      otherNames: null,
      dateOfBirth: "1998-05-02",
      nationality: "UG",
      secondaryNationality: null,
      externalIdentifiers: [{ authority: "KMSA_REG_NO", value: "KM-2026-0201" }],
    });
    expect(creates(requests)[0]!.body).toMatchObject({
      displayName: null,
      gender: null,
      heightCm: 171,
      weightKg: null,
      externalIdentifiers: [{ authority: "KMSA_REG_NO", value: "KM-2026-0201", validFrom: null }],
      checkToken: "token-1",
      overrideReason: null,
    });
  });

  it("checks the numbers and identifiers before asking the API", async () => {
    const requests = open();
    await submitDetails(() => {
      type("Height in cm (optional)", "171.5");
      type("Weight in kg (optional)", "64.25");
      fireEvent.click(screen.getByRole("button", { name: "Add an identifier" }));
      type("Issued by (code)", "KMSA REG");
    });

    expect(await screen.findByText("Enter whole centimetres from 50 to 260.")).toBeTruthy();
    expect(screen.getByText("Enter kilograms from 10 to 300, with at most one decimal.")).toBeTruthy();
    expect(screen.getByText(/Use 2 to 40 capital letters/)).toBeTruthy();
    expect(screen.getByText("Enter the identifier.")).toBeTruthy();
    expect(checks(requests)).toEqual([]);
  });
});

describe("add person: the duplicate review", () => {
  it("shows each candidate next to the new person with the evidence and a birth year, never a date of birth", async () => {
    const requests = open(actions, { "POST /api/admin/v1/people/duplicate-check": checked([candidate()]) });
    await submitDetails(() => type("Date of birth (optional)", "1998-05-02"));

    expect(await screen.findByRole("heading", { name: "Is this someone you already have?" })).toBeTruthy();
    const review = reviewOf("John Ssekandi");
    expect(within(review).getByText("Likely the same person")).toBeTruthy();
    expect(within(review).getByText("Same date of birth")).toBeTruthy();
    expect(within(review).getByText("Names 86% alike")).toBeTruthy();
    expect(within(review).getByText("Ringball sevens")).toBeTruthy();
    // The new person's own date (typed by the registrar), and only a year for the person already registered.
    const [mine, theirs] = within(review).getAllByRole("definition").reduce<[string[], string[]]>(
      (columns, cell, index) => {
        columns[index < 3 ? 0 : 1].push(cell.textContent ?? "");
        return columns;
      },
      [[], []],
    );
    expect(mine).toContain("2 May 1998");
    expect(theirs).toContain("1998");
    expect(theirs.join(" ")).not.toMatch(/\d{1,2} \w+ \d{4}|\d{4}-\d{2}-\d{2}/);
    expect(within(review).getByRole("link", { name: "Open John Ssekandi" }).getAttribute("href")).toBe(
      `/t/kmsa/people/${john.id}`,
    );
    expect(creates(requests)).toEqual([]);
  });

  it("names every kind of evidence and marks an archived candidate", async () => {
    open(actions, {
      "POST /api/admin/v1/people/duplicate-check": checked([
        candidate({
          status: "ARCHIVED",
          confidence: "POSSIBLE",
          birthYear: null,
          athleteVariants: [],
          evidence: [
            { code: "BIRTH_YEAR_WITHIN_ONE", value: null },
            { code: "SAME_NAME_KEY", value: null },
            { code: "SAME_LAST_NAME", value: null },
            { code: "SAME_NATIONALITY", value: null },
            { code: "ALIAS_MATCH", value: null },
          ],
        }),
      ]),
    });
    await submitDetails();

    const review = await screen.findByRole("list", { name: "People who may match" });
    for (const text of [
      "Possibly the same person",
      "Born within a year of each other",
      "Same names, in any order",
      "Same last name",
      "Same nationality",
      "Matches one of their other names (aliases)",
    ]) {
      expect(within(review).getAllByText(text).length).toBeGreaterThan(0);
    }
    // In the registered person's own column (the new person's Born also says "Not recorded"; review #109 R7).
    const theirs = within(within(review).getByRole("heading", { name: "Already registered" }).parentElement!);
    for (const text of ["Archived", "No sport recorded", "Not recorded"]) {
      expect(theirs.getByText(text)).toBeTruthy();
    }
  });

  it("adds the person past the candidates only with a reason, sent with the check's token", async () => {
    const requests = open(actions, { "POST /api/admin/v1/people/duplicate-check": checked([candidate()]) });
    await submitDetails();
    await screen.findByRole("heading", { name: "Is this someone you already have?" });

    fireEvent.click(screen.getByRole("button", { name: "Add as a new person" }));
    // Announced, and the focus is on the box (review #109 R2).
    expect((await screen.findByRole("alert")).textContent).toBe("Say why this is a different person.");
    const box = screen.getByLabelText("Why is John Ssekandi a different person?");
    expect(document.activeElement).toBe(box);
    expect(box.getAttribute("aria-invalid")).toBe("true");
    expect(creates(requests)).toEqual([]);

    type("Why is John Ssekandi a different person?", "  Different parents; checked with Ntinda Swifts SC.  ");
    fireEvent.click(screen.getByRole("button", { name: "Add as a new person" }));
    await waitFor(() => expect(navigation.push).toHaveBeenCalledWith(`/t/kmsa/people/${created.id}`));
    expect(checks(requests)).toHaveLength(1);
    expect(creates(requests)[0]!.body).toMatchObject({
      checkToken: "token-1",
      overrideReason: "Different parents; checked with Ntinda Swifts SC.",
    });
  });

  it("blocks the save when an identifier is someone else's, and goes back to change it", async () => {
    open(actions, {
      "POST /api/admin/v1/people/duplicate-check": checked([
        candidate({ confidence: "EXACT_IDENTIFIER", evidence: [{ code: "SAME_EXTERNAL_ID", value: "KMSA_REG_NO" }] }),
      ]),
    });
    await submitDetails(() => {
      fireEvent.click(screen.getByRole("button", { name: "Add an identifier" }));
      type("Issued by (code)", "KMSA_REG_NO");
      type("Identifier", "KM-2026-0147");
    });

    expect(await screen.findByText("This identifier is already in use")).toBeTruthy();
    expect(screen.getByText("Holds the same KMSA_REG_NO identifier")).toBeTruthy();
    expect(screen.queryByRole("button", { name: "Add as a new person" })).toBeNull();
    expect(screen.queryByLabelText(/different person/)).toBeNull();

    fireEvent.click(screen.getByRole("button", { name: "Change the details" }));
    expect(await screen.findByRole("heading", { name: "Their details" })).toBeTruthy();
    expect((screen.getByLabelText("Identifier") as HTMLInputElement).value).toBe("KM-2026-0147");
  });

  it("checks again when someone added a match meanwhile, and shows the new list", async () => {
    let round = 0;
    const requests = open(actions, {
      "POST /api/admin/v1/people/duplicate-check": () =>
        ++round === 1
          ? checked([candidate()])
          : checked([candidate(), candidate({ personId: denis.id, displayName: "Denis Opoka" })], "token-2"),
      "POST /api/admin/v1/people": { status: 409, body: problem("PERSON_DUPLICATE_CHECK_STALE", 409) },
    });
    await submitDetails();
    await screen.findByRole("heading", { name: "Is this someone you already have?" });

    type("Why is John Ssekandi a different person?", "Different parents");
    fireEvent.click(screen.getByRole("button", { name: "Add as a new person" }));

    expect(await screen.findByText(/Someone added a person who may match/)).toBeTruthy();
    expect(screen.getByRole("link", { name: "Open Denis Opoka" })).toBeTruthy();
    // The reason typed is kept for the second look; nothing more is sent until the registrar confirms.
    expect((screen.getByLabelText("Why is John Ssekandi a different person?") as HTMLTextAreaElement).value).toBe(
      "Different parents",
    );
    expect(checks(requests)).toHaveLength(2);
    expect(creates(requests)).toHaveLength(1);
    expect(navigation.push).not.toHaveBeenCalled();
  });

  it("checks again when the check expired and saves when the same people were already reviewed", async () => {
    let round = 0;
    const requests = open(actions, {
      "POST /api/admin/v1/people/duplicate-check": () => checked([candidate()], `token-${++round}`),
      "POST /api/admin/v1/people": (request) =>
        (request.body as { checkToken: string }).checkToken === "token-1"
          ? { status: 422, body: problem("PERSON_DUPLICATE_CHECK_REQUIRED", 422) }
          : createdAnswer,
    });
    await submitDetails();
    await screen.findByRole("heading", { name: "Is this someone you already have?" });

    type("Why is John Ssekandi a different person?", "Different parents");
    fireEvent.click(screen.getByRole("button", { name: "Add as a new person" }));

    await waitFor(() => expect(navigation.push).toHaveBeenCalledWith(`/t/kmsa/people/${created.id}`));
    expect(creates(requests).map((request) => request.body)).toMatchObject([
      { checkToken: "token-1", overrideReason: "Different parents" },
      { checkToken: "token-2", overrideReason: "Different parents" },
    ]);
  });

  it.each([
    ["other people", [candidate(), candidate({ personId: denis.id, displayName: "Denis Opoka" })]],
    ["a stronger match than the one reviewed", [candidate({ confidence: "LIKELY" })]],
  ])("checks again when the check expired and shows the review again for %s", async (_, again) => {
    let round = 0;
    const requests = open(actions, {
      "POST /api/admin/v1/people/duplicate-check": () =>
        ++round === 1 ? checked([candidate({ confidence: "POSSIBLE" })]) : checked(again, "token-2"),
      "POST /api/admin/v1/people": { status: 422, body: problem("PERSON_DUPLICATE_CHECK_REQUIRED", 422) },
    });
    await submitDetails();
    await screen.findByRole("heading", { name: "Is this someone you already have?" });

    type("Why is John Ssekandi a different person?", "Different parents");
    fireEvent.click(screen.getByRole("button", { name: "Add as a new person" }));

    expect(await screen.findByText(/The check had to run again/)).toBeTruthy();
    expect(checks(requests)).toHaveLength(2);
    expect(creates(requests)).toHaveLength(1);
    expect(navigation.push).not.toHaveBeenCalled();
  });

  it("adds the person without a reason when the second check finds no one", async () => {
    let round = 0;
    const requests = open(actions, {
      "POST /api/admin/v1/people/duplicate-check": () =>
        ++round === 1 ? checked([candidate()]) : checked([], "token-2"),
      "POST /api/admin/v1/people": (request) =>
        (request.body as { checkToken: string }).checkToken === "token-1"
          ? { status: 422, body: problem("PERSON_DUPLICATE_CHECK_REQUIRED", 422) }
          : createdAnswer,
    });
    await submitDetails();
    await screen.findByRole("heading", { name: "Is this someone you already have?" });
    type("Why is John Ssekandi a different person?", "Different parents");
    fireEvent.click(screen.getByRole("button", { name: "Add as a new person" }));

    await waitFor(() => expect(navigation.push).toHaveBeenCalledWith(`/t/kmsa/people/${created.id}`));
    expect(creates(requests)[1]!.body).toMatchObject({ checkToken: "token-2", overrideReason: null });
  });

  it("checks again only once: a second refusal is shown", async () => {
    const requests = open(actions, {
      "POST /api/admin/v1/people": { status: 409, body: problem("PERSON_DUPLICATE_CHECK_STALE", 409) },
    });
    await submitDetails();

    expect(await screen.findByText("That didn't work")).toBeTruthy();
    expect(checks(requests)).toHaveLength(2);
    expect(creates(requests)).toHaveLength(2);
    expect(navigation.push).not.toHaveBeenCalled();
  });

  it("doesn't say \"again\" to a registrar who saw no review before the check went stale", async () => {
    let round = 0;
    open(actions, {
      "POST /api/admin/v1/people/duplicate-check": () => (++round === 1 ? checked([]) : checked([candidate()], "token-2")),
      "POST /api/admin/v1/people": { status: 409, body: problem("PERSON_DUPLICATE_CHECK_STALE", 409) },
    });
    await submitDetails();

    expect(await screen.findByText(/Someone added a person who may match just before the save/)).toBeTruthy();
    expect(screen.queryByText(/while you were reviewing/)).toBeNull();
  });

  it("drops empty identifier rows, so a refused identifier is shown under its own row", async () => {
    const requests = open(actions, {
      "POST /api/admin/v1/people": {
        status: 409,
        body: problem("PERSON_EXTERNAL_ID_EXISTS", 409, {
          errors: [{ field: "externalIdentifiers[0].value", code: "PERSON_EXTERNAL_ID_EXISTS" }],
        }),
      },
    });
    await submitDetails(() => {
      fireEvent.click(screen.getByRole("button", { name: "Add an identifier" }));
      fireEvent.click(screen.getByRole("button", { name: "Add an identifier" }));
      const [, second] = screen.getAllByLabelText("Issued by (code)");
      fireEvent.change(second!, { target: { value: "KMSA_REG_NO" } });
      fireEvent.change(screen.getAllByLabelText("Identifier")[1]!, { target: { value: "KM-2026-0147" } });
    });

    await waitFor(() => expect(screen.getByLabelText("Identifier").getAttribute("aria-invalid")).toBe("true"));
    expect((screen.getByLabelText("Identifier") as HTMLInputElement).value).toBe("KM-2026-0147");
    expect(checks(requests)[0]!.body).toMatchObject({
      externalIdentifiers: [{ authority: "KMSA_REG_NO", value: "KM-2026-0147" }],
    });
  });

  it("goes back to the details with the error under an identifier someone took meanwhile", async () => {
    open(actions, {
      "POST /api/admin/v1/people": {
        status: 409,
        body: problem("PERSON_EXTERNAL_ID_EXISTS", 409, {
          errors: [{ field: "externalIdentifiers[0].value", code: "PERSON_EXTERNAL_ID_EXISTS" }],
        }),
      },
    });
    await submitDetails(() => {
      fireEvent.click(screen.getByRole("button", { name: "Add an identifier" }));
      type("Issued by (code)", "KMSA_REG_NO");
      type("Identifier", "KM-2026-0147");
    });

    const field = await screen.findByLabelText("Identifier");
    await waitFor(() => expect(field.getAttribute("aria-invalid")).toBe("true"));
    expect(screen.getByRole("heading", { name: "Their details" })).toBeTruthy();
    // Shown under the field only: no alert repeating it.
    expect(screen.queryByText("That didn't work")).toBeNull();
  });

  it("goes back to the name when the API refuses it", async () => {
    open(actions, {
      "POST /api/admin/v1/people/duplicate-check": {
        status: 422,
        body: problem("PERSON_NAME_INVALID", 422, { errors: [{ field: "firstName", code: "PERSON_NAME_INVALID" }] }),
      },
    });
    await submitDetails();

    expect(await screen.findByRole("heading", { name: "Who are you adding?" })).toBeTruthy();
    expect(screen.getByLabelText("First name").getAttribute("aria-invalid")).toBe("true");
  });

  it("renders a long four-part name in the review", async () => {
    open(actions, {
      "POST /api/admin/v1/people/duplicate-check": checked([
        candidate({ displayName: "Nakato Irene Nalubega Namaganda", birthYear: null }),
      ]),
    });
    await enterName("Nakato", "Namaganda");
    type("Display name (optional)", "Nakato Irene Nalubega Namaganda");
    fireEvent.click(screen.getByRole("button", { name: "Check and add" }));

    expect(await screen.findByText(/may be Nakato Irene Nalubega Namaganda/)).toBeTruthy();
    expect(screen.getByRole("link", { name: "Open Nakato Irene Nalubega Namaganda" })).toBeTruthy();
  });
});

describe("add person: access", () => {
  it("is refused without person.create", async () => {
    const requests = open(["person.read"]);
    expect(await screen.findByRole("heading", { name: "You can't open this page" })).toBeTruthy();
    expect(checks(requests)).toEqual([]);
  });

  it("shows neither the name's matches nor Open links without person.read", async () => {
    const requests = open(["person.create"], { "POST /api/admin/v1/people/duplicate-check": checked([candidate()]) });
    await submitDetails();

    expect(await screen.findByRole("list", { name: "People who may match" })).toBeTruthy();
    expect(screen.queryByRole("link", { name: "Open John Ssekandi" })).toBeNull();
    expect(sent(requests, "GET", "/api/admin/v1/people")).toEqual([]);
  });

  it("says why on a read-only tenant", async () => {
    open([], {}, readOnlyCapabilities(["person.read"], ["person.create"]));
    expect(await screen.findByRole("heading", { name: "Add person" })).toBeTruthy();
    expect(screen.queryByRole("button", { name: "Continue" })).toBeNull();
  });
});
