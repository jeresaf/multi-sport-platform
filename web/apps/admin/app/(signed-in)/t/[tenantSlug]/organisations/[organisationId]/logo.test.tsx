// Registry → Organisations → an organisation → the logo (S07 T6b-2b, D15): adding, changing and removing it from the
// version its dialog opened on; a file over 2 MB refused before sending and the API's refusal of the file (a small
// image) under it; the storage quota in the alert; a conflict offers Reload; a dissolved or archived organisation gets
// no new logo but its logo can be removed; a viewer sees the logo without a control.
import { cleanup, fireEvent, screen, waitFor, within } from "@testing-library/react";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { readOnlyCapabilities } from "@/test/capabilities";
import { ntinda, organisation, organisationAnswers } from "@/test/organisations";
import { fakeApi, problem, renderWithQuery, type Answers, type RecordedRequest } from "@/test/render";
import TenantWorkspaceLayout from "../../layout";
import type { Organisation } from "../organisations-data";
import OrganisationPage from "./page";

vi.mock("next/navigation", () => ({
  useParams: () => ({ tenantSlug: "kmsa", organisationId: "0199a3f0-0000-7000-8000-0000000000e1" }),
  usePathname: () => "/t/kmsa/organisations/0199a3f0-0000-7000-8000-0000000000e1",
  useSearchParams: () => new URLSearchParams(),
  useRouter: () => ({ replace: vi.fn(), push: vi.fn() }),
}));

const reads = ["organisation.read", "venue.read", "sport.read", "tenant.settings.read"];
const changes = ["organisation.logo.set", "organisation.logo.remove"];
const registrar = [...reads, ...changes];
const logoPath = `/api/admin/v1/organisations/${ntinda.id}/logo`;

const logo = {
  thumbnailUrl: "/media/0199a3f0-0000-7000-8000-0000000000a1/thumbnail?token=signed",
  mediumUrl: "/media/0199a3f0-0000-7000-8000-0000000000a1/medium?token=signed",
  expiresAt: "2026-11-26T12:00:00Z",
};
const withLogo = organisation({ logo });

function open(
  shown: Organisation = ntinda,
  capabilities: Parameters<typeof organisationAnswers>[0] = registrar,
  answers: Answers = {},
) {
  const path = `/api/admin/v1/organisations/${shown.id}`;
  return fakeApi({
    ...organisationAnswers(capabilities),
    [`GET ${path}`]: { status: 200, body: shown },
    [`GET ${path}/status-changes`]: { status: 200, body: { items: [] } },
    [`GET ${path}/parent-links`]: { status: 200, body: { items: [] } },
    ...answers,
  });
}

function render(...args: Parameters<typeof open>) {
  const requests = open(...args);
  renderWithQuery(
    <TenantWorkspaceLayout>
      <OrganisationPage />
    </TenantWorkspaceLayout>,
  );
  return requests;
}

const sent = async (requests: RecordedRequest[], method: string, withBody = true) => {
  let found: RecordedRequest | undefined;
  await waitFor(() => {
    found = requests.find(
      (request) => request.method === method && request.path === logoPath && (!withBody || request.body !== undefined),
    );
    expect(found).toBeTruthy();
  });
  return found!;
};
const dialog = () => screen.getByRole("dialog");
const press = (name: string) => fireEvent.click(within(dialog()).getByRole("button", { name }));
const chooseFile = (file: File) =>
  fireEvent.change(within(dialog()).getByLabelText("Logo"), { target: { files: [file] } });
const crest = () => new File(["\x89PNG crest"], "crest.png", { type: "image/png" });
/** What the page puts in its upload form, as `[name, value]` pairs. */
function formAppends() {
  const calls: Array<[string, unknown]> = [];
  const append = FormData.prototype.append as (this: FormData, ...args: unknown[]) => void;
  vi.spyOn(FormData.prototype, "append").mockImplementation(function (this: FormData, ...args: unknown[]) {
    calls.push([args[0] as string, args[1]]);
    append.apply(this, args);
  });
  return calls;
}
const closed = () => waitFor(() => expect(screen.queryByRole("dialog")).toBeNull());
const announced = (text: string) =>
  waitFor(() => expect(screen.getAllByRole("status").map((item) => item.textContent)).toContain(text));

beforeEach(() => {
  // jsdom has no blob: URLs; the preview only needs one to show.
  URL.createObjectURL = vi.fn(() => "blob:crest");
  URL.revokeObjectURL = vi.fn();
});

afterEach(() => {
  cleanup();
  vi.unstubAllGlobals();
  vi.restoreAllMocks();
});

describe("Organisation logo", () => {
  it("adds a logo with a preview, from the version it opened on, then focuses Change logo", async () => {
    const appends = formAppends();
    const requests = render(ntinda, registrar, {
      [`PUT ${logoPath}`]: { status: 200, body: organisation({ logo, version: 4 }) },
    });

    expect(await screen.findByRole("img", { name: "No logo for Ntinda Swifts SC" })).toBeTruthy();
    fireEvent.click(screen.getByRole("button", { name: "Add logo" }));
    expect(within(dialog()).getByRole("heading", { name: "Add the logo of Ntinda Swifts SC" })).toBeTruthy();
    expect(within(dialog()).getByRole("button", { name: "Add logo" })).toHaveProperty("disabled", true);
    expect(within(dialog()).getByLabelText("Logo").getAttribute("accept")).toBe("image/png,image/jpeg,image/webp");
    const file = crest();
    chooseFile(file);
    expect(within(dialog()).getByRole<HTMLImageElement>("img", { name: "The chosen logo" }).src).toBe("blob:crest");
    press("Add logo");

    const upload = await sent(requests, "PUT");
    expect(upload.headers.get("If-Match")).toBe('"3"');
    expect(appends).toEqual([["file", file]]);
    await closed();
    expect(screen.getByRole<HTMLImageElement>("img", { name: "Logo of Ntinda Swifts SC" }).getAttribute("src")).toBe(
      logo.mediumUrl,
    );
    await announced("Logo added.");
    await waitFor(() => expect(document.activeElement?.textContent).toBe("Change logo"));
    expect(URL.revokeObjectURL).toHaveBeenCalledWith("blob:crest");
  });

  it("changes a logo, saying the new one replaces it", async () => {
    const requests = render(withLogo, registrar, {
      [`PUT ${logoPath}`]: { status: 200, body: organisation({ logo, version: 4 }) },
    });
    fireEvent.click(await screen.findByRole("button", { name: "Change logo" }));
    expect(within(dialog()).getByText(/The new logo replaces the current one/)).toBeTruthy();
    chooseFile(crest());
    press("Change logo");
    await sent(requests, "PUT");
    await closed();
    await announced("Logo changed.");
  });

  it("refuses a file over 2 MB before sending, and shows the API's refusal of a small image under the file", async () => {
    const requests = render(ntinda, registrar, {
      [`PUT ${logoPath}`]: {
        status: 422,
        body: problem("MEDIA_IMAGE_TOO_SMALL", 422, {
          detail: "The image is too small.",
          errors: [{ field: "file", code: "MEDIA_IMAGE_TOO_SMALL" }],
        }),
      },
    });

    fireEvent.click(await screen.findByRole("button", { name: "Add logo" }));
    const large = crest();
    Object.defineProperty(large, "size", { value: 2 * 1024 * 1024 + 1 });
    chooseFile(large);
    press("Add logo");
    expect(await within(dialog()).findByText(/larger than 2 MB/)).toBeTruthy();
    expect(requests.some((request) => request.method === "PUT")).toBe(false);

    chooseFile(crest());
    press("Add logo");
    const input = within(dialog()).getByLabelText("Logo");
    await waitFor(() => expect(input.getAttribute("aria-invalid")).toBe("true"));
    // The refused file isn't sent again: the focus goes to the input and Add logo waits for another (#111 R4).
    expect(document.activeElement).toBe(input);
    expect(within(dialog()).getByRole("button", { name: "Add logo" })).toHaveProperty("disabled", true);
    expect(within(dialog()).queryByRole("img", { name: "The chosen logo" })).toBeNull();
  });

  it("shows the storage quota's refusal in the alert", async () => {
    render(ntinda, registrar, {
      [`PUT ${logoPath}`]: {
        status: 403,
        body: problem("ENTITLEMENT_QUOTA_EXCEEDED", 403, { detail: "Your plan allows 10 GB of storage." }),
      },
    });
    fireEvent.click(await screen.findByRole("button", { name: "Add logo" }));
    chooseFile(crest());
    press("Add logo");
    expect((await within(dialog()).findByRole("alert")).textContent).toContain("Your plan allows 10 GB of storage.");
    expect(within(dialog()).getByLabelText("Logo").getAttribute("aria-invalid")).toBeNull();
  });

  it("offers Reload when the organisation changed since the dialog opened", async () => {
    render(ntinda, registrar, {
      [`PUT ${logoPath}`]: { status: 409, body: problem("ORGANISATION_CONCURRENCY_CONFLICT", 409) },
    });
    fireEvent.click(await screen.findByRole("button", { name: "Add logo" }));
    chooseFile(crest());
    press("Add logo");
    expect(await within(dialog()).findByText("This record changed since you opened it")).toBeTruthy();
    expect(within(dialog()).getByRole("button", { name: "Reload" })).toBeTruthy();
  });

  it("removes the logo, then focuses Add logo", async () => {
    const requests = render(withLogo, registrar, {
      [`DELETE ${logoPath}`]: { status: 200, body: organisation({ logo: null, version: 4 }) },
    });
    fireEvent.click(await screen.findByRole("button", { name: "Remove logo" }));
    expect(within(dialog()).getByRole("heading", { name: "Remove the logo of Ntinda Swifts SC" })).toBeTruthy();
    press("Remove logo");

    const removal = await sent(requests, "DELETE", false);
    expect(removal.headers.get("If-Match")).toBe('"3"');
    await closed();
    expect(screen.getByRole("img", { name: "No logo for Ntinda Swifts SC" })).toBeTruthy();
    expect(screen.queryByRole("button", { name: "Remove logo" })).toBeNull();
    await announced("Logo removed.");
    await waitFor(() => expect(document.activeElement?.textContent).toBe("Add logo"));
  });

  it.each([
    ["DISSOLVED", "dissolved"],
    ["ARCHIVED", "archived"],
  ] as const)("adds no logo to a %s organisation, but removes one", async (status, word) => {
    render(organisation({ logo, status }));
    const change = await screen.findByRole("button", { name: "Change logo" });
    expect(change).toHaveProperty("disabled", true);
    expect(document.getElementById(change.getAttribute("aria-describedby")!)?.textContent).toBe(
      `Ntinda Swifts SC is ${word}: no new logo can be added.`,
    );
    expect(screen.getByRole("button", { name: "Remove logo" })).toHaveProperty("disabled", false);
    // No new one can follow: the removal doesn't promise one (pre-review 3).
    fireEvent.click(screen.getByRole("button", { name: "Remove logo" }));
    expect(within(dialog()).getByText("The logo is no longer shown, here or on the public site.")).toBeTruthy();
  });

  it("disables both with the reason on a read-only tenant", async () => {
    render(withLogo, readOnlyCapabilities(reads, changes));
    const change = await screen.findByRole("button", { name: "Change logo" });
    expect(change).toHaveProperty("disabled", true);
    expect(screen.getByRole("button", { name: "Remove logo" })).toHaveProperty("disabled", true);
    // One note for the same reason.
    expect(screen.getByRole("button", { name: "Remove logo" }).getAttribute("aria-describedby")).toBe(
      change.getAttribute("aria-describedby"),
    );
  });

  it("shows a viewer the logo without a control", async () => {
    render(withLogo, reads);
    expect(await screen.findByRole("img", { name: "Logo of Ntinda Swifts SC" })).toBeTruthy();
    expect(screen.queryByRole("button", { name: /logo$/ })).toBeNull();
  });
});
