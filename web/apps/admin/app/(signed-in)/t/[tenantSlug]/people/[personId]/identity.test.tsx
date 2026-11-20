// Registry → People → a person → the photo, external identifiers and aliases (S06 D10, D16, D19; T7c-2a): adding,
// changing and removing the photo; adding and ending identifiers; adding and removing aliases. Each control shows only
// with its action; every change sends the version the dialog opened on and shows the person the API answers.
import { act, cleanup, fireEvent, screen, waitFor, within } from "@testing-library/react";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { readOnlyCapabilities } from "@/test/capabilities";
import { brenda, denis, john, person } from "@/test/people";
import { personPageAnswers, tenantId } from "@/test/person-page";
import { fakeApi, problem, renderWithQuery, type Answers, type RecordedRequest } from "@/test/render";
import { todayIn } from "@/lib/tenant-settings";
import TenantWorkspaceLayout from "../../layout";
import { personQueryKey, type Person } from "../people-data";
import PersonPage from "./page";

const navigation = vi.hoisted(() => ({ personId: "", search: "tab=identifiers" }));

vi.mock("next/navigation", () => ({
  useParams: () => ({ tenantSlug: "kmsa", personId: navigation.personId }),
  usePathname: () => `/t/kmsa/people/${navigation.personId}`,
  useSearchParams: () => new URLSearchParams(navigation.search),
  useRouter: () => ({ replace: vi.fn(), push: vi.fn() }),
}));

const viewer = ["person.read", "sport.read", "tenant.settings.read"];
const identityWrites = [
  "person.photo.set",
  "person.photo.remove",
  "person.external_id.add",
  "person.external_id.end",
  "person.alias.add",
  "person.alias.remove",
];
const registrar = [...viewer, ...identityWrites];

const [openIdentifier, endedIdentifier] = denis.externalIdentifiers;
const [alias] = denis.aliases;
const today = () => todayIn("Africa/Kampala");
/** The tenant's tomorrow, YYYY-MM-DD. */
const tomorrow = () => {
  const day = new Date(`${today()}T00:00:00Z`);
  day.setUTCDate(day.getUTCDate() + 1);
  return day.toISOString().slice(0, 10);
};
const archived = (shown: Person) => person({ ...shown, status: "ARCHIVED" });

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

/** The request once sent and, unless it has none (`withBody` false), once its body is read. */
const sent = async (requests: RecordedRequest[], method: string, path: string, withBody = true) => {
  let found: RecordedRequest | undefined;
  await waitFor(() => {
    found = requests.find(
      (request) => request.method === method && request.path === path && (!withBody || request.body !== undefined),
    );
    expect(found).toBeTruthy();
  });
  return found!;
};
const dialog = () => screen.getByRole("dialog");
const type = (label: string | RegExp, value: string) =>
  fireEvent.change(within(dialog()).getByLabelText(label), { target: { value } });
const press = (name: string | RegExp) => fireEvent.click(within(dialog()).getByRole("button", { name }));
const chooseFile = (file: File) =>
  fireEvent.change(within(dialog()).getByLabelText("Photo"), { target: { files: [file] } });
const portrait = () => new File(["\x89PNG portrait"], "portrait.png", { type: "image/png" });
/** What the page puts in its upload forms, as `[name, value]` pairs. */
function formAppends() {
  const calls: Array<[string, unknown]> = [];
  const append = FormData.prototype.append as (this: FormData, ...args: unknown[]) => void;
  vi.spyOn(FormData.prototype, "append").mockImplementation(function (this: FormData, ...args: unknown[]) {
    calls.push([args[0] as string, args[1]]);
    append.apply(this, args);
  });
  return calls;
}
const statusText = (container: HTMLElement) => within(container).getAllByRole("status").map((item) => item.textContent);
const closed = () => waitFor(() => expect(screen.queryByRole("dialog")).toBeNull());

beforeEach(() => {
  // jsdom has no blob: URLs; the preview only needs one to show.
  URL.createObjectURL = vi.fn(() => "blob:portrait");
  URL.revokeObjectURL = vi.fn();
});

afterEach(() => {
  cleanup();
  vi.unstubAllGlobals();
  vi.restoreAllMocks();
});

describe("photo", () => {
  it("shows a viewer no photo control", async () => {
    open(brenda, viewer);
    expect(await screen.findByRole("img", { name: "Photo of Brenda Akello" })).toBeTruthy();
    expect(screen.queryByRole("button", { name: /photo$/ })).toBeNull();
  });

  it("on a read-only tenant, shows the controls disabled with the reason", async () => {
    open(brenda, readOnlyCapabilities(viewer, identityWrites));
    const change = await screen.findByRole("button", { name: "Change photo" });
    expect(change).toHaveProperty("disabled", true);
    expect(document.getElementById(change.getAttribute("aria-describedby")!)?.textContent).toMatch(/read-only/i);
    expect(screen.getByRole("button", { name: "Remove photo" })).toHaveProperty("disabled", true);
  });

  it("adds a photo with a preview, on the version it opened on, then focuses Change photo", async () => {
    const requests = open(john, registrar, {
      [`PUT /api/admin/v1/people/${john.id}/photo`]: {
        status: 200,
        body: person({ ...john, photo: brenda.photo, version: 8 }),
      },
    });
    const appended = formAppends();

    fireEvent.click(await screen.findByRole("button", { name: "Add photo" }));
    expect(within(dialog()).getByRole("button", { name: "Add photo" })).toHaveProperty("disabled", true);
    const chosen = portrait();
    chooseFile(chosen);
    expect(within(dialog()).getByRole<HTMLImageElement>("img", { name: "The chosen photo" }).src).toBe("blob:portrait");
    press("Add photo");

    const upload = await sent(requests, "PUT", `/api/admin/v1/people/${john.id}/photo`);
    expect(upload.headers.get("If-Match")).toBe('"7"');
    expect(upload.headers.get("X-CSRF")).toBe("1");
    expect(upload.headers.get("Content-Type")).toMatch(/^multipart\/form-data; boundary=/);
    expect(appended).toEqual([["file", chosen]]);

    await closed();
    expect(screen.getByRole("img", { name: "Photo of John Ssekandi" })).toBeTruthy();
    expect(statusText(document.body)).toContain("Photo added.");
    await waitFor(() => expect(document.activeElement?.textContent).toBe("Change photo"));
    // The preview's URL is released with the dialog.
    expect(URL.revokeObjectURL).toHaveBeenCalledWith("blob:portrait");
  });

  it("refuses a file over 5 MB before sending, and shows the API's refusal under the file", async () => {
    const requests = open(john, registrar, {
      [`PUT /api/admin/v1/people/${john.id}/photo`]: {
        status: 422,
        body: problem("MEDIA_TYPE_NOT_ALLOWED", 422, { errors: [{ field: "file", code: "MEDIA_TYPE_NOT_ALLOWED" }] }),
      },
    });

    fireEvent.click(await screen.findByRole("button", { name: "Add photo" }));
    const large = portrait();
    Object.defineProperty(large, "size", { value: 6 * 1024 * 1024 });
    chooseFile(large);
    press("Add photo");
    expect(await within(dialog()).findByText(/larger than 5 MB/)).toBeTruthy();
    expect(requests.some((request) => request.method === "PUT")).toBe(false);

    chooseFile(new File(["GIF89a"], "portrait.gif", { type: "image/gif" }));
    press("Add photo");
    const input = within(dialog()).getByLabelText("Photo");
    await waitFor(() => expect(input.getAttribute("aria-invalid")).toBe("true"));
    expect(within(dialog()).getByText(/The file's type isn't accepted here/)).toBeTruthy();
    // The refused file isn't sent again: the focus goes to the input and Add photo waits for another (#111 R4).
    expect(document.activeElement).toBe(input);
    expect(within(dialog()).getByRole("button", { name: "Add photo" })).toHaveProperty("disabled", true);
    expect(within(dialog()).queryByRole("img", { name: "The chosen photo" })).toBeNull();
  });

  it("shows the storage quota's refusal in the alert, with the plan's limit, and clears the file", async () => {
    open(john, registrar, {
      [`PUT /api/admin/v1/people/${john.id}/photo`]: {
        status: 403,
        body: problem("ENTITLEMENT_QUOTA_EXCEEDED", 403, { detail: "Your plan allows 10 GB of storage." }),
      },
    });

    fireEvent.click(await screen.findByRole("button", { name: "Add photo" }));
    chooseFile(portrait());
    press("Add photo");

    // The quota names no field: the refusal is the dialog's, not the file's (#112 R1).
    expect((await within(dialog()).findByRole("alert")).textContent).toContain("Your plan allows 10 GB of storage.");
    expect(within(dialog()).getByLabelText("Photo").getAttribute("aria-invalid")).toBeNull();
    // Sending the same file again can't help: another is chosen once storage is freed or the plan changes.
    expect(within(dialog()).getByRole("button", { name: "Add photo" })).toHaveProperty("disabled", true);
  });

  it("keeps the chosen file after a server failure, so it can be sent again", async () => {
    let uploads = 0;
    const requests = open(john, registrar, {
      [`PUT /api/admin/v1/people/${john.id}/photo`]: () =>
        ++uploads === 1
          ? { status: 503, body: problem("PLATFORM_UNAVAILABLE", 503) }
          : { status: 200, body: person({ ...john, photo: brenda.photo, version: 8 }) },
    });

    fireEvent.click(await screen.findByRole("button", { name: "Add photo" }));
    chooseFile(portrait());
    press("Add photo");

    expect(await within(dialog()).findByRole("alert")).toBeTruthy();
    // #112 R4: only a refusal of the file (or of its storage) clears it.
    expect(within(dialog()).getByRole("img", { name: "The chosen photo" })).toBeTruthy();
    press("Add photo");
    await closed();
    expect(requests.filter((request) => request.method === "PUT")).toHaveLength(2);
  });

  it("removes the photo, then focuses Add photo", async () => {
    const requests = open(brenda, registrar, {
      [`DELETE /api/admin/v1/people/${brenda.id}/photo`]: {
        status: 200,
        body: person({ ...brenda, photo: null, version: 8 }),
      },
    });

    fireEvent.click(await screen.findByRole("button", { name: "Remove photo" }));
    press("Remove photo");

    const removal = await sent(requests, "DELETE", `/api/admin/v1/people/${brenda.id}/photo`, false);
    expect(removal.headers.get("If-Match")).toBe('"7"');
    await closed();
    expect(screen.getByRole("img", { name: "No photo of Brenda Akello" })).toBeTruthy();
    expect(screen.queryByRole("button", { name: "Remove photo" })).toBeNull();
    await waitFor(() => expect(document.activeElement?.textContent).toBe("Add photo"));
  });

  it("adds no photo to an archived person, but removes one", async () => {
    open(archived(brenda));
    const change = await screen.findByRole("button", { name: "Change photo" });
    expect(change).toHaveProperty("disabled", true);
    expect(document.getElementById(change.getAttribute("aria-describedby")!)?.textContent).toBe(
      "Brenda Akello is archived: no new photo can be added.",
    );
    expect(screen.getByRole("button", { name: "Remove photo" })).toHaveProperty("disabled", false);
  });
});

describe("external identifiers", () => {
  it("shows a viewer the identifiers and aliases without a control", async () => {
    open(denis, viewer);
    expect(await screen.findByText("KM-2026-0147")).toBeTruthy();
    expect(screen.queryByRole("button", { name: "Add identifier" })).toBeNull();
    expect(screen.queryByRole("button", { name: /^End / })).toBeNull();
    expect(screen.queryByRole("button", { name: "Add alias" })).toBeNull();
    expect(screen.queryByRole("button", { name: /^Remove the alias/ })).toBeNull();
  });

  it("on a read-only tenant, shows the controls disabled with the reason", async () => {
    open(denis, readOnlyCapabilities(viewer, identityWrites));
    const add = await screen.findByRole("button", { name: "Add identifier" });
    expect(add).toHaveProperty("disabled", true);
    expect(document.getElementById(add.getAttribute("aria-describedby")!)?.textContent).toMatch(/read-only/i);
    expect(screen.getByRole("button", { name: `End KMSA_REGISTRATION ${openIdentifier!.value}` })).toHaveProperty(
      "disabled",
      true,
    );
    expect(screen.getByRole("button", { name: "Add alias" })).toHaveProperty("disabled", true);
    expect(screen.getByRole("button", { name: "Remove the alias Opoka D." })).toHaveProperty("disabled", true);
  });

  it("adds an identifier, upper-casing its issuer, then focuses the identifiers", async () => {
    const added = { id: "0199a3f0-0000-7000-8000-0000000000a4", authority: "SCHOOL_ADMISSION_NO", value: "KSS/2019/044", validFrom: "2019-02-04", validTo: null };
    const requests = open(denis, registrar, {
      [`POST /api/admin/v1/people/${denis.id}/external-identifiers`]: {
        status: 200,
        body: person({ ...denis, externalIdentifiers: [...denis.externalIdentifiers, added], version: 8 }),
      },
    });

    fireEvent.click(await screen.findByRole("button", { name: "Add identifier" }));
    const validFrom = within(dialog()).getByLabelText("Valid from (optional)");
    await waitFor(() => expect(validFrom.getAttribute("max")).toBe(today()));
    type(/^Issued by/, " school_admission_no ");
    type("Identifier", " KSS/2019/044 ");
    type("Valid from (optional)", "2019-02-04");
    press("Add identifier");

    const request = await sent(requests, "POST", `/api/admin/v1/people/${denis.id}/external-identifiers`);
    expect(request.headers.get("If-Match")).toBe('"7"');
    expect(request.body).toEqual({ authority: "SCHOOL_ADMISSION_NO", value: "KSS/2019/044", validFrom: "2019-02-04" });
    await closed();
    expect(screen.getByText("KSS/2019/044")).toBeTruthy();
    expect(statusText(screen.getByRole("tabpanel"))).toContain("Identifier added.");
    await waitFor(() => expect(document.activeElement?.id).toBe("identifiers-heading"));
  });

  it("checks the identifier before sending: the issuer's code and a start no later than today (Q46)", async () => {
    const requests = open(denis);
    fireEvent.click(await screen.findByRole("button", { name: "Add identifier" }));
    await waitFor(() => expect(within(dialog()).getByLabelText("Valid from (optional)").getAttribute("max")).toBe(today()));
    type(/^Issued by/, "KMSA REG");
    type("Valid from (optional)", tomorrow());
    press("Add identifier");

    expect(await within(dialog()).findByText(/Use 2 to 40 capital letters/)).toBeTruthy();
    expect(within(dialog()).getByText("Enter the identifier.")).toBeTruthy();
    expect(within(dialog()).getByText("An identifier can't start later than today.")).toBeTruthy();
    expect(requests.some((request) => request.method === "POST")).toBe(false);
  });

  it.each([
    { code: "PERSON_EXTERNAL_ID_EXISTS", field: "value", label: "Identifier", message: /a value is never reused/ },
    {
      code: "PERSON_EXTERNAL_ID_AUTHORITY_RESERVED",
      field: "authority",
      label: /^Issued by/,
      message: /National identity documents/,
    },
  ])("shows $code under its field", async ({ code, field, label, message }) => {
    open(denis, registrar, {
      [`POST /api/admin/v1/people/${denis.id}/external-identifiers`]: {
        status: code.endsWith("EXISTS") ? 409 : 422,
        body: problem(code, code.endsWith("EXISTS") ? 409 : 422, { errors: [{ field, code }] }),
      },
    });
    fireEvent.click(await screen.findByRole("button", { name: "Add identifier" }));
    type(/^Issued by/, "KMSA_REGISTRATION");
    type("Identifier", "KM-2026-0147");
    press("Add identifier");

    await waitFor(() => expect(within(dialog()).getByLabelText(label).getAttribute("aria-invalid")).toBe("true"));
    expect(within(dialog()).getByText(message)).toBeTruthy();
    // Shown under the field, not again in the alert.
    expect(within(dialog()).queryByRole("alert")).toBeNull();
  });

  it("ends an identifier on the day given, with a reason, then focuses the identifiers", async () => {
    const ended = { ...openIdentifier!, validTo: "2026-11-01" };
    const requests = open(denis, registrar, {
      [`POST /api/admin/v1/people/${denis.id}/external-identifiers/${openIdentifier!.id}/end`]: {
        status: 200,
        body: person({ ...denis, externalIdentifiers: [ended, endedIdentifier!], version: 8 }),
      },
    });

    // An ended identifier has no End.
    expect(await screen.findByText("1 Feb 2019 to 31 Dec 2025 (ended)", { selector: "td" })).toBeTruthy();
    expect(screen.getAllByRole("button", { name: /^End / })).toHaveLength(1);
    fireEvent.click(screen.getByRole("button", { name: "End KMSA_REGISTRATION KM-2026-0147" }));
    const lastDay = within(dialog()).getByLabelText<HTMLInputElement>("Last day valid");
    // The end starts at the tenant's today, between the start and today.
    await waitFor(() => expect(lastDay.value).toBe(today()));
    expect(lastDay.getAttribute("min")).toBe("2026-01-15");
    expect(lastDay.getAttribute("max")).toBe(today());
    type("Last day valid", "2026-11-01");
    type("Reason (optional)", "Re-registered under a new number");
    press("End identifier");

    const request = await sent(
      requests,
      "POST",
      `/api/admin/v1/people/${denis.id}/external-identifiers/${openIdentifier!.id}/end`,
    );
    expect(request.headers.get("If-Match")).toBe('"7"');
    expect(request.body).toEqual({ validTo: "2026-11-01", reason: "Re-registered under a new number" });
    await closed();
    expect(screen.queryByRole("button", { name: /^End / })).toBeNull();
    await waitFor(() => expect(document.activeElement?.id).toBe("identifiers-heading"));
  });

  it("doesn't end an identifier before it started, and sends no reason when none is given", async () => {
    const requests = open(denis, registrar, {
      [`POST /api/admin/v1/people/${denis.id}/external-identifiers/${openIdentifier!.id}/end`]: {
        status: 200,
        body: { ...denis, version: 8 },
      },
    });
    fireEvent.click(await screen.findByRole("button", { name: "End KMSA_REGISTRATION KM-2026-0147" }));
    type("Last day valid", "2026-01-14");
    press("End identifier");
    expect(await within(dialog()).findByText("It can't end before it started (15 Jan 2026).")).toBeTruthy();
    expect(requests.some((request) => request.method === "POST")).toBe(false);

    await waitFor(() => expect(within(dialog()).getByLabelText("Last day valid")).toHaveProperty("disabled", false));
    type("Last day valid", today());
    press("End identifier");
    const request = await sent(
      requests,
      "POST",
      `/api/admin/v1/people/${denis.id}/external-identifiers/${openIdentifier!.id}/end`,
    );
    expect(request.body).toEqual({ validTo: today(), reason: null });
  });

  it("can't end an identifier that starts after today (Q46)", async () => {
    const later = { ...openIdentifier!, validFrom: tomorrow() };
    open(person({ ...denis, externalIdentifiers: [later] }));
    const end = await screen.findByRole("button", { name: "End KMSA_REGISTRATION KM-2026-0147" });
    await waitFor(() => expect(end).toHaveProperty("disabled", true));
    expect(document.getElementById(end.getAttribute("aria-describedby")!)?.textContent).toMatch(/^Can be ended from /);
  });

  it("on a read-only tenant, gives both reasons for an identifier that starts after today", async () => {
    const later = { ...openIdentifier!, validFrom: tomorrow() };
    open(person({ ...denis, externalIdentifiers: [later] }), readOnlyCapabilities(viewer, identityWrites));
    const end = await screen.findByRole("button", { name: "End KMSA_REGISTRATION KM-2026-0147" });
    await waitFor(() => expect(end.getAttribute("aria-describedby")?.split(" ")).toHaveLength(2));
    const reasons = end
      .getAttribute("aria-describedby")!
      .split(" ")
      .map((id) => document.getElementById(id)?.textContent);
    expect(reasons[0]).toMatch(/read-only/i);
    expect(reasons[1]).toMatch(/^Can be ended from /);
  });
});

describe("aliases", () => {
  it("adds an alias, then focuses the aliases", async () => {
    const requests = open(denis, registrar, {
      [`POST /api/admin/v1/people/${denis.id}/aliases`]: {
        status: 200,
        body: person({
          ...denis,
          aliases: [...denis.aliases, { id: "0199a3f0-0000-7000-8000-0000000000a5", name: "Denno", kind: "NICKNAME" }],
          version: 8,
        }),
      },
    });

    fireEvent.click(await screen.findByRole("button", { name: "Add alias" }));
    press("Add alias");
    expect(await within(dialog()).findByText("Enter the name.")).toBeTruthy();
    expect(within(dialog()).getByText("Choose what kind of name it is.")).toBeTruthy();
    type("Name", " Denno ");
    type("Kind", "NICKNAME");
    press("Add alias");

    const request = await sent(requests, "POST", `/api/admin/v1/people/${denis.id}/aliases`);
    expect(request.headers.get("If-Match")).toBe('"7"');
    expect(request.body).toEqual({ name: "Denno", kind: "NICKNAME" });
    await closed();
    expect(screen.getByText("Denno")).toBeTruthy();
    expect(screen.getAllByText("Nickname").length).toBeGreaterThan(0);
    await waitFor(() => expect(document.activeElement?.id).toBe("aliases-heading"));
  });

  it("shows PERSON_ALIAS_EXISTS under the name", async () => {
    open(denis, registrar, {
      [`POST /api/admin/v1/people/${denis.id}/aliases`]: {
        status: 409,
        body: problem("PERSON_ALIAS_EXISTS", 409, { errors: [{ field: "name", code: "PERSON_ALIAS_EXISTS" }] }),
      },
    });
    fireEvent.click(await screen.findByRole("button", { name: "Add alias" }));
    type("Name", "Denis Opoka");
    type("Kind", "SPELLING");
    press("Add alias");
    await waitFor(() => expect(within(dialog()).getByLabelText("Name").getAttribute("aria-invalid")).toBe("true"));
    expect(within(dialog()).getByText(/already known by this name/)).toBeTruthy();
  });

  it("removes an alias with a reason, then focuses the aliases", async () => {
    const requests = open(denis, registrar, {
      [`POST /api/admin/v1/people/${denis.id}/aliases/${alias!.id}/remove`]: {
        status: 200,
        body: person({ ...denis, aliases: [], version: 8 }),
      },
    });

    fireEvent.click(await screen.findByRole("button", { name: "Remove the alias Opoka D." }));
    press("Remove alias");
    expect(await within(dialog()).findByText(/Give a reason/)).toBeTruthy();
    type("Reason", "Entered by mistake");
    press("Remove alias");

    const request = await sent(requests, "POST", `/api/admin/v1/people/${denis.id}/aliases/${alias!.id}/remove`);
    expect(request.headers.get("If-Match")).toBe('"7"');
    expect(request.body).toEqual({ reason: "Entered by mistake" });
    await closed();
    expect(screen.queryByText("Opoka D.")).toBeNull();
    await waitFor(() => expect(document.activeElement?.id).toBe("aliases-heading"));
  });

  it("adds nothing to an archived person, but still ends identifiers and removes aliases", async () => {
    open(archived(denis));
    expect(await screen.findAllByText("Denis Opoka is archived: no new one can be added.")).toHaveLength(2);
    expect(screen.queryByRole("button", { name: "Add identifier" })).toBeNull();
    expect(screen.queryByRole("button", { name: "Add alias" })).toBeNull();
    expect(screen.getByRole("button", { name: "End KMSA_REGISTRATION KM-2026-0147" })).toHaveProperty("disabled", false);
    expect(screen.getByRole("button", { name: "Remove the alias Opoka D." })).toHaveProperty("disabled", false);
  });
});

describe("versions", () => {
  it("offers Reload after a concurrency conflict", async () => {
    const requests = open(denis, registrar, {
      [`POST /api/admin/v1/people/${denis.id}/aliases`]: { status: 409, body: problem("PERSON_CONCURRENCY_CONFLICT", 409) },
    });
    fireEvent.click(await screen.findByRole("button", { name: "Add alias" }));
    type("Name", "Denno");
    type("Kind", "NICKNAME");
    press("Add alias");
    fireEvent.click(await within(dialog()).findByRole("button", { name: /Reload/ }));

    await closed();
    await waitFor(() =>
      expect(requests.filter((request) => request.path === `/api/admin/v1/people/${denis.id}`).length).toBe(2),
    );
  });

  // Every dialog keeps the version it opened on (#110 R1): a later read while it is open (the photo's signed URLs are
  // read again before they expire) doesn't become the change's basis.
  it.each([
    {
      dialogName: "Change photo",
      method: "PUT",
      path: `/api/admin/v1/people/${denis.id}/photo`,
      shown: person({ ...denis, photo: brenda.photo }),
      act: () => chooseFile(portrait()),
      submit: () => press("Change photo"),
      withBody: true,
    },
    {
      dialogName: "Remove photo",
      method: "DELETE",
      path: `/api/admin/v1/people/${denis.id}/photo`,
      shown: person({ ...denis, photo: brenda.photo }),
      act: () => undefined,
      submit: () => press("Remove photo"),
      withBody: false,
    },
    {
      dialogName: "Add identifier",
      method: "POST",
      path: `/api/admin/v1/people/${denis.id}/external-identifiers`,
      shown: denis,
      act: () => {
        type(/^Issued by/, "KMSA_REGISTRATION");
        type("Identifier", "KM-2026-0300");
      },
      submit: () => press("Add identifier"),
      withBody: true,
    },
    {
      dialogName: "End KMSA_REGISTRATION KM-2026-0147",
      method: "POST",
      path: `/api/admin/v1/people/${denis.id}/external-identifiers/${openIdentifier!.id}/end`,
      shown: denis,
      act: () => type("Last day valid", "2026-11-01"),
      submit: () => press("End identifier"),
      withBody: true,
    },
    {
      dialogName: "Add alias",
      method: "POST",
      path: `/api/admin/v1/people/${denis.id}/aliases`,
      shown: denis,
      act: () => {
        type("Name", "Denno");
        type("Kind", "NICKNAME");
      },
      submit: () => press("Add alias"),
      withBody: true,
    },
    {
      dialogName: "Remove the alias Opoka D.",
      method: "POST",
      path: `/api/admin/v1/people/${denis.id}/aliases/${alias!.id}/remove`,
      shown: denis,
      act: () => type("Reason", "Entered by mistake"),
      submit: () => press("Remove alias"),
      withBody: true,
    },
  ])("$dialogName sends the version it opened on, though the person is read again", async (row) => {
    const requests = open(row.shown, registrar, {
      [`${row.method} ${row.path}`]: { status: 200, body: { ...row.shown, version: 9 } },
    });
    fireEvent.click(await screen.findByRole("button", { name: row.dialogName }));
    row.act();
    await act(async () => {
      rendered.queryClient.setQueryData(personQueryKey(tenantId, row.shown.id), { ...row.shown, version: 8 });
      // The cache tells its observers on the next tick.
      await new Promise((resolve) => setTimeout(resolve, 0));
    });
    row.submit();

    const request = await sent(requests, row.method, row.path, row.withBody);
    expect(request.headers.get("If-Match")).toBe('"7"');
  });
});
