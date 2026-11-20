// Registry → People → a person → Consent and the public profile (S06 D14, D15, D19; T7c-1b): recording a guardian's
// consent with its evidence, replacing the evidence, withdrawing scopes, and turning the public profile on and off.
// Each control shows only with its action; every change sends the version the dialog opened on and shows the person
// the API answers.
import { cleanup, fireEvent, screen, waitFor, within } from "@testing-library/react";
import { afterEach, describe, expect, it, vi } from "vitest";
import { readOnlyCapabilities } from "@/test/capabilities";
import { brenda, brendaPrivate, john, nakato, person } from "@/test/people";
import { personPageAnswers } from "@/test/person-page";
import { fakeApi, problem, renderWithQuery, type Answers, type RecordedRequest } from "@/test/render";
import { todayIn } from "@/lib/tenant-settings";
import TenantWorkspaceLayout from "../../layout";
import type { Person } from "../people-data";
import PersonPage from "./page";

const navigation = vi.hoisted(() => ({ personId: "", search: "tab=consent" }));

vi.mock("next/navigation", () => ({
  useParams: () => ({ tenantSlug: "kmsa", personId: navigation.personId }),
  usePathname: () => `/t/kmsa/people/${navigation.personId}`,
  useSearchParams: () => new URLSearchParams(navigation.search),
  useRouter: () => ({ replace: vi.fn(), push: vi.fn() }),
}));

const viewer = ["person.read", "sport.read", "tenant.settings.read"];
const consentWrites = [
  "person.consent.record",
  "person.consent.withdraw",
  "person.consent_evidence.upload",
  "person.public_profile.enable",
  "person.public_profile.disable",
];
const registrar = [...viewer, "person.read_private", ...consentWrites];

const newConsentId = "0199a3f0-0000-7000-8000-0000000000c9";
const recordedAt = "2026-11-20T08:00:00Z";

/** Nakato (age unknown) after her aunt's consent is recorded. */
const nakatoWithConsent = (hasEvidence: boolean, version: number) =>
  person({
    ...nakato,
    activeConsentScopes: ["DATA_PROCESSING", "PUBLIC_PROFILE"],
    consents: [
      {
        id: newConsentId,
        scopes: ["DATA_PROCESSING", "PUBLIC_PROFILE"],
        givenOn: "2026-11-18",
        status: "ACTIVE",
        hasEvidence,
        recordedAt,
        withdrawnAt: null,
        withdrawalReason: null,
        replacesConsentId: null,
      },
    ],
    version,
  });

function open(shown: Person, capabilities: Parameters<typeof personPageAnswers>[1] = registrar, answers: Answers = {}) {
  navigation.personId = shown.id;
  const requests = fakeApi({
    ...personPageAnswers(shown, capabilities),
    [`GET /api/admin/v1/people/${shown.id}/private`]: {
      status: 200,
      body: shown.id === brenda.id ? brendaPrivate : { id: shown.id, dateOfBirth: null, consents: [] },
    },
    ...answers,
  });
  renderWithQuery(
    <TenantWorkspaceLayout>
      <PersonPage />
    </TenantWorkspaceLayout>,
  );
  return requests;
}

const sent = async (requests: RecordedRequest[], method: string, path: string) => {
  let found: RecordedRequest | undefined;
  await waitFor(() => {
    found = requests.find((request) => request.method === method && request.path === path);
    expect(found).toBeTruthy();
  });
  return found!;
};
const dialog = () => screen.getByRole("dialog");
const type = (label: string | RegExp, value: string) =>
  fireEvent.change(within(dialog()).getByLabelText(label), { target: { value } });
const check = (label: string) => fireEvent.click(within(dialog()).getByRole("checkbox", { name: label }));
const chooseFile = (label: string | RegExp, file: File) =>
  fireEvent.change(within(dialog()).getByLabelText(label), { target: { files: [file] } });
const signedForm = () => new File(["%PDF-1.7 signed form"], "consent-form.pdf", { type: "application/pdf" });
const tabStatus = () => within(screen.getByRole("tabpanel")).getByRole("status").textContent;

/** Fills the record form for Nakato's aunt. */
async function fillConsent() {
  fireEvent.click(await screen.findByRole("button", { name: "Record consent" }));
  type("Guardian's name", "Sarah Namaganda");
  type("Relationship", "OTHER");
  type(/^Contact/, "+256 701 000 233");
  check("Data processing");
  check("Public profile");
  type("Given on", "2026-11-18");
}

afterEach(() => {
  cleanup();
  vi.unstubAllGlobals();
});

describe("consent", () => {
  it("shows a viewer the records without a control", async () => {
    open(brenda, viewer);

    expect(await screen.findByText("Her mother asked for the photo to come down")).toBeTruthy();
    expect(screen.queryByRole("button", { name: "Record consent" })).toBeNull();
    expect(screen.queryByRole("button", { name: /^Withdraw/ })).toBeNull();
    expect(screen.queryByRole("button", { name: /evidence/ })).toBeNull();
    expect(screen.queryByRole("button", { name: /public profile$/ })).toBeNull();
  });

  it("on a read-only tenant, shows the controls disabled with the reason", async () => {
    open(brenda, readOnlyCapabilities([...viewer, "person.read_private"], consentWrites));

    const record = await screen.findByRole("button", { name: "Record consent" });
    expect(record).toHaveProperty("disabled", true);
    const note = document.getElementById(record.getAttribute("aria-describedby")!);
    expect(note?.textContent).toMatch(/read-only/i);
    expect(screen.getByRole("button", { name: /^Withdraw: the consent given on/ })).toHaveProperty("disabled", true);
    expect(screen.getByRole("button", { name: /^Replace evidence: / })).toHaveProperty("disabled", true);
    expect(screen.getByRole("button", { name: "Turn on public profile" })).toHaveProperty("disabled", true);
  });

  it("records a consent and its evidence on the version that answered, then focuses it", async () => {
    const requests = open(nakato, registrar, {
      [`POST /api/admin/v1/people/${nakato.id}/consents`]: { status: 200, body: nakatoWithConsent(false, 8) },
      [`PUT /api/admin/v1/people/${nakato.id}/consents/${newConsentId}/evidence`]: {
        status: 200,
        body: nakatoWithConsent(true, 9),
      },
    });

    await fillConsent();
    // The date given starts at the tenant's today and can't be later.
    expect(within(dialog()).getByLabelText("Given on").getAttribute("max")).toBe(todayIn("Africa/Kampala"));
    expect(within(dialog()).getByText(/Placeholder wording, to be replaced by the approved text/)).toBeTruthy();
    chooseFile("Evidence (optional)", signedForm());
    fireEvent.click(within(dialog()).getByRole("button", { name: "Record consent" }));

    const record = await sent(requests, "POST", `/api/admin/v1/people/${nakato.id}/consents`);
    expect(record.headers.get("If-Match")).toBe('"7"');
    expect(record.body).toEqual({
      guardianName: "Sarah Namaganda",
      relationship: "OTHER",
      contact: "+256 701 000 233",
      scopes: ["DATA_PROCESSING", "PUBLIC_PROFILE"],
      givenOn: "2026-11-18",
    });
    const upload = await sent(requests, "PUT", `/api/admin/v1/people/${nakato.id}/consents/${newConsentId}/evidence`);
    expect(upload.headers.get("If-Match")).toBe('"8"');
    expect(upload.headers.get("X-CSRF")).toBe("1");
    expect(upload.headers.get("Content-Type")).toMatch(/^multipart\/form-data; boundary=/);
    // The file in the field `file` (jsdom's File loses its name on the way through the test's Request; the browser keeps it).
    const file = (upload.body as FormData).get("file") as Blob;
    expect(await file.text()).toBe("%PDF-1.7 signed form");

    await waitFor(() => expect(screen.queryByRole("dialog")).toBeNull());
    expect(tabStatus()).toBe("Consent recorded with its evidence.");
    await waitFor(() => expect(document.activeElement?.id).toBe(`consent-${newConsentId}`));
    expect(screen.getByText("On file")).toBeTruthy();
    // The guardians changed: the private read is asked again.
    await waitFor(() =>
      expect(requests.filter((request) => request.path.endsWith("/private")).length).toBeGreaterThan(1),
    );
  });

  it("keeps the consent when its evidence is refused, and uploads another file", async () => {
    let uploads = 0;
    const requests = open(nakato, registrar, {
      [`POST /api/admin/v1/people/${nakato.id}/consents`]: { status: 200, body: nakatoWithConsent(false, 8) },
      [`PUT /api/admin/v1/people/${nakato.id}/consents/${newConsentId}/evidence`]: () =>
        ++uploads === 1
          ? {
              status: 422,
              body: problem("MEDIA_TYPE_NOT_ALLOWED", 422, {
                errors: [{ field: "file", code: "MEDIA_TYPE_NOT_ALLOWED" }],
              }),
            }
          : { status: 200, body: nakatoWithConsent(true, 9) },
    });

    await fillConsent();
    chooseFile("Evidence (optional)", new File(["GIF89a"], "form.gif", { type: "image/gif" }));
    fireEvent.click(within(dialog()).getByRole("button", { name: "Record consent" }));

    expect(await within(dialog()).findByText(/The consent is recorded\. Its evidence wasn't uploaded/)).toBeTruthy();
    const fileInput = within(dialog()).getByLabelText("Evidence");
    await waitFor(() => expect(fileInput.getAttribute("aria-invalid")).toBe("true"));
    expect(within(dialog()).getByLabelText("Guardian's name")).toHaveProperty("disabled", true);

    chooseFile("Evidence", signedForm());
    fireEvent.click(within(dialog()).getByRole("button", { name: "Upload evidence" }));
    await waitFor(() => expect(screen.queryByRole("dialog")).toBeNull());
    expect(tabStatus()).toBe("Consent recorded with its evidence.");
    // One consent recorded; the second upload is based on its answer too.
    expect(requests.filter((request) => request.method === "POST" && request.path.endsWith("/consents"))).toHaveLength(1);
    const lastUpload = requests.filter((request) => request.method === "PUT").at(-1)!;
    expect(lastUpload.headers.get("If-Match")).toBe('"8"');
  });

  it("closing after a refused upload announces the consent without its evidence", async () => {
    open(nakato, registrar, {
      [`POST /api/admin/v1/people/${nakato.id}/consents`]: { status: 200, body: nakatoWithConsent(false, 8) },
      [`PUT /api/admin/v1/people/${nakato.id}/consents/${newConsentId}/evidence`]: {
        status: 413,
        body: problem("MEDIA_TOO_LARGE", 413, { errors: [{ field: "file", code: "MEDIA_TOO_LARGE" }] }),
      },
    });

    await fillConsent();
    chooseFile("Evidence (optional)", signedForm());
    fireEvent.click(within(dialog()).getByRole("button", { name: "Record consent" }));
    await within(dialog()).findByText(/The consent is recorded/);
    fireEvent.click(within(dialog()).getByRole("button", { name: "Cancel" }));

    await waitFor(() => expect(screen.queryByRole("dialog")).toBeNull());
    expect(tabStatus()).toBe("Consent recorded without its evidence.");
  });

  it("checks the form and the file before sending, and shows a refused field under it", async () => {
    const requests = open(nakato, registrar, {
      [`POST /api/admin/v1/people/${nakato.id}/consents`]: {
        status: 400,
        body: problem("VALIDATION_INVALID_REQUEST", 400, {
          errors: [{ field: "givenOn", code: "VALIDATION_INVALID_REQUEST" }],
        }),
      },
    });

    fireEvent.click(await screen.findByRole("button", { name: "Record consent" }));
    fireEvent.click(within(dialog()).getByRole("button", { name: "Record consent" }));
    expect(await within(dialog()).findByText("Enter the guardian's name.")).toBeTruthy();
    expect(within(dialog()).getByText("Choose at least one thing the guardian agrees to.")).toBeTruthy();

    type("Guardian's name", "Sarah Namaganda");
    type("Relationship", "OTHER");
    type(/^Contact/, "+256 701 000 233");
    check("Photo");
    const large = signedForm();
    Object.defineProperty(large, "size", { value: 11 * 1024 * 1024 });
    chooseFile("Evidence (optional)", large);
    fireEvent.click(within(dialog()).getByRole("button", { name: "Record consent" }));
    expect(await within(dialog()).findByText(/larger than 10 MB/)).toBeTruthy();
    expect(requests.some((request) => request.method === "POST")).toBe(false);

    chooseFile("Evidence (optional)", signedForm());
    type("Given on", "2026-11-19");
    fireEvent.click(within(dialog()).getByRole("button", { name: "Record consent" }));
    await waitFor(() =>
      expect(within(dialog()).getByLabelText("Given on").getAttribute("aria-invalid")).toBe("true"),
    );
    expect(requests.some((request) => request.method === "PUT")).toBe(false);
  });

  it("offers Reload after a concurrency conflict", async () => {
    const requests = open(nakato, registrar, {
      [`POST /api/admin/v1/people/${nakato.id}/consents`]: {
        status: 409,
        body: problem("PERSON_CONCURRENCY_CONFLICT", 409),
      },
    });

    await fillConsent();
    fireEvent.click(within(dialog()).getByRole("button", { name: "Record consent" }));
    fireEvent.click(await within(dialog()).findByRole("button", { name: /Reload/ }));

    await waitFor(() => expect(screen.queryByRole("dialog")).toBeNull());
    await waitFor(() =>
      expect(requests.filter((request) => request.path === `/api/admin/v1/people/${nakato.id}`).length).toBe(2),
    );
  });

  it("withdraws part of a consent with a reason and focuses the record that carries the rest", async () => {
    const successorId = "0199a3f0-0000-7000-8000-0000000000c3";
    const [active, withdrawn] = brenda.consents;
    const answered = person({
      ...brenda,
      activeConsentScopes: ["DATA_PROCESSING"],
      consents: [
        { ...active!, id: successorId, scopes: ["DATA_PROCESSING"], recordedAt, replacesConsentId: active!.id },
        { ...active!, status: "WITHDRAWN", withdrawnAt: recordedAt, withdrawalReason: "The guardian asked by phone" },
        withdrawn!,
      ],
      version: 8,
    });
    const requests = open(brenda, registrar, {
      [`POST /api/admin/v1/people/${brenda.id}/consents/${active!.id}/withdraw`]: { status: 200, body: answered },
    });

    fireEvent.click(await screen.findByRole("button", { name: "Withdraw: the consent given on 2 Nov 2026" }));
    // Only the consent's own scopes are offered.
    expect(within(dialog()).queryByRole("checkbox", { name: "Photo" })).toBeNull();
    fireEvent.click(within(dialog()).getByRole("button", { name: "Withdraw" }));
    expect(await within(dialog()).findByText("Choose at least one thing to withdraw.")).toBeTruthy();
    expect(within(dialog()).getByText("Give a reason: it is kept in the audit trail.")).toBeTruthy();

    check("Public profile");
    type("Reason", "The guardian asked by phone");
    fireEvent.click(within(dialog()).getByRole("button", { name: "Withdraw" }));

    const withdraw = await sent(requests, "POST", `/api/admin/v1/people/${brenda.id}/consents/${active!.id}/withdraw`);
    expect(withdraw.headers.get("If-Match")).toBe('"7"');
    expect(withdraw.body).toEqual({ scopes: ["PUBLIC_PROFILE"], reason: "The guardian asked by phone" });
    await waitFor(() => expect(screen.queryByRole("dialog")).toBeNull());
    expect(tabStatus()).toBe("Consent withdrawn in part; what is left is in a new record.");
    await waitFor(() => expect(document.activeElement?.id).toBe(`consent-${successorId}`));
  });

  it("replaces the evidence of an active consent", async () => {
    const [active] = brenda.consents;
    const requests = open(brenda, registrar, {
      [`PUT /api/admin/v1/people/${brenda.id}/consents/${active!.id}/evidence`]: {
        status: 200,
        body: { ...brenda, version: 8 },
      },
    });

    fireEvent.click(await screen.findByRole("button", { name: "Replace evidence: the consent given on 2 Nov 2026" }));
    const replace = within(dialog()).getByRole("button", { name: "Replace evidence" });
    expect(replace).toHaveProperty("disabled", true);
    chooseFile("Evidence", signedForm());
    fireEvent.click(replace);

    const upload = await sent(requests, "PUT", `/api/admin/v1/people/${brenda.id}/consents/${active!.id}/evidence`);
    expect(upload.headers.get("If-Match")).toBe('"7"');
    await waitFor(() => expect(screen.queryByRole("dialog")).toBeNull());
    expect(tabStatus()).toBe("Evidence replaced.");
  });

  it("records nothing new for an archived person, but still withdraws", async () => {
    open(person({ ...brenda, status: "ARCHIVED" }));

    expect(await screen.findByText(/is archived: no new consent can be recorded/)).toBeTruthy();
    expect(screen.queryByRole("button", { name: "Record consent" })).toBeNull();
    expect(screen.queryByRole("button", { name: /evidence/ })).toBeNull();
    expect(screen.getByRole("button", { name: /^Withdraw: / })).toBeTruthy();
  });

  it("offers an adult no new consent, but lets their consent be withdrawn", async () => {
    open(person({ ...john, consents: brenda.consents, activeConsentScopes: brenda.activeConsentScopes }));

    expect(await screen.findByRole("button", { name: /^Withdraw: / })).toBeTruthy();
    expect(screen.queryByRole("button", { name: "Record consent" })).toBeNull();
  });
});

describe("public profile", () => {
  it("can't be turned on for someone not known to be an adult without consent to it", async () => {
    const requests = open(nakato);

    const turnOn = await screen.findByRole("button", { name: "Turn on public profile" });
    expect(turnOn).toHaveProperty("disabled", true);
    expect(document.getElementById(turnOn.getAttribute("aria-describedby")!)?.textContent).toMatch(
      /needs a guardian's consent to it: record it on the Consent tab/,
    );
    expect(requests.some((request) => request.path.endsWith("/public-profile/enable"))).toBe(false);
  });

  it("turns on a minor's profile with consent, sending the version, and says what is shown", async () => {
    const requests = open(brenda, registrar, {
      [`POST /api/admin/v1/people/${brenda.id}/public-profile/enable`]: {
        status: 200,
        body: { ...brenda, publicProfileEnabled: true, version: 8 },
      },
    });

    fireEvent.click(await screen.findByRole("button", { name: "Turn on public profile" }));
    expect(within(dialog()).getByText(/shows only their display name/)).toBeTruthy();
    fireEvent.click(within(dialog()).getByRole("button", { name: "Turn on" }));

    const enable = await sent(requests, "POST", `/api/admin/v1/people/${brenda.id}/public-profile/enable`);
    expect(enable.headers.get("If-Match")).toBe('"7"');
    expect(enable.body).toBeUndefined();
    expect(await screen.findByText("Public profile on")).toBeTruthy();
    expect(screen.getByRole("button", { name: "Turn off public profile" })).toBeTruthy();
    expect(screen.getByText("Public profile turned on.")).toBeTruthy();
  });

  it("says why a profile that is on isn't shown, and turns it off", async () => {
    const shown = person({ ...nakato, publicProfileEnabled: true });
    const requests = open(shown, registrar, {
      [`POST /api/admin/v1/people/${nakato.id}/public-profile/disable`]: {
        status: 200,
        body: { ...shown, publicProfileEnabled: false, version: 8 },
      },
    });

    expect(
      await screen.findByText("Public profile on (not shown without a guardian's consent to the public profile)"),
    ).toBeTruthy();
    fireEvent.click(screen.getByRole("button", { name: "Turn off public profile" }));
    fireEvent.click(within(dialog()).getByRole("button", { name: "Turn off" }));

    await sent(requests, "POST", `/api/admin/v1/people/${nakato.id}/public-profile/disable`);
    expect(await screen.findByText("Public profile off")).toBeTruthy();
  });

  it("isn't turned on for an archived person, but is turned off", async () => {
    open(person({ ...john, status: "ARCHIVED" }));
    const turnOn = await screen.findByRole("button", { name: "Turn on public profile" });
    expect(turnOn).toHaveProperty("disabled", true);
    expect(document.getElementById(turnOn.getAttribute("aria-describedby")!)?.textContent).toMatch(/archived/);

    cleanup();
    open(person({ ...john, status: "ARCHIVED", publicProfileEnabled: true }));
    expect(await screen.findByRole("button", { name: "Turn off public profile" })).toHaveProperty("disabled", false);
  });
});
