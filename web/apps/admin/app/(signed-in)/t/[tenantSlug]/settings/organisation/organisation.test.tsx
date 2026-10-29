// Tenant settings → Organisation: read-only without the update capability, only changed settings sent with
// If-Match, a concurrency conflict offers a reload, the API's field errors go under their fields.
import { cleanup, fireEvent, screen, waitFor } from "@testing-library/react";
import { afterEach, describe, expect, it, vi } from "vitest";
import { tenantCapabilities } from "@/test/capabilities";
import { fakeApi, problem, renderWithQuery, type Answer } from "@/test/render";
import TenantWorkspaceLayout from "../../layout";
import OrganisationPage from "./page";
import { changedSettings } from "./organisation-form";

vi.mock("next/navigation", () => ({
  useParams: () => ({ tenantSlug: "kmsa" }),
  usePathname: () => "/t/kmsa/settings/organisation",
  useRouter: () => ({ replace: vi.fn(), push: vi.fn() }),
}));

const tenantId = "0199a3f0-0000-7000-8000-00000000000a";
const me: Answer = {
  status: 200,
  body: {
    id: "0199a3f0-0000-7000-8000-000000000001",
    email: "grace.nakato@kmsa.example",
    platformRoles: [],
    tenants: [
      { tenantId, slug: "kmsa", displayName: "Kampala Metro Sports Association", status: "ACTIVE", roles: ["VIEWER"] },
    ],
  },
};
const settings = {
  timeZone: "Africa/Kampala",
  defaultLocale: "en-UG",
  currencyCode: "UGX",
  publicEnabled: true,
  startingSoonMinutes: 30,
  minorRegistrationConsent: "REQUIRED",
  updatedAt: "2026-10-18T09:00:00Z",
  version: 3,
} as const;
const reference = {
  "GET /api/public/v1/reference/time-zones": {
    status: 200,
    body: { items: [{ code: "Africa/Kampala", countryCode: "UG" }, { code: "Africa/Nairobi", countryCode: "KE" }] },
  },
  "GET /api/public/v1/reference/currencies": {
    status: 200,
    body: { items: [{ code: "UGX", numericCode: "800", name: "Ugandan shilling", minorUnits: 0 }] },
  },
};

function open(actions: string[], answers: Record<string, Answer | (() => Answer)> = {}) {
  const requests = fakeApi({
    "GET /api/account/v1/me": me,
    "GET /api/admin/v1/me/capabilities": { status: 200, body: tenantCapabilities(actions) },
    "GET /api/admin/v1/tenant/settings": { status: 200, body: settings },
    ...reference,
    ...answers,
  });
  renderWithQuery(
    <TenantWorkspaceLayout>
      <OrganisationPage />
    </TenantWorkspaceLayout>,
  );
  return requests;
}

afterEach(() => {
  cleanup();
  vi.unstubAllGlobals();
  localStorage.clear();
});

describe("organisation settings", () => {
  it("shows a viewer the settings without a way to change them", async () => {
    open(["tenant.settings.read"]);

    const zone = await screen.findByLabelText("Time zone");
    await waitFor(() => expect((zone as HTMLSelectElement).value).toBe("Africa/Kampala"));
    expect(screen.getByText(/You can see these settings but not change them/)).toBeTruthy();
    expect(screen.getByRole("group", { name: "Organisation settings" }).hasAttribute("disabled")).toBe(true);
    expect(screen.queryByRole("button", { name: "Save changes" })).toBeNull();
  });

  it("sends only the changed settings, with the version it read as If-Match", async () => {
    const requests = open(["tenant.settings.read", "tenant.settings.update"], {
      "PATCH /api/admin/v1/tenant/settings": { status: 200, body: { ...settings, startingSoonMinutes: 45, version: 4 } },
    });

    const minutes = await screen.findByLabelText(/Starting soon/);
    fireEvent.change(minutes, { target: { value: "45" } });
    fireEvent.click(screen.getByRole("button", { name: "Save changes" }));

    expect(await screen.findByText("Saved.")).toBeTruthy();
    const patch = requests.find((request) => request.method === "PATCH")!;
    expect(patch.body).toEqual({ startingSoonMinutes: 45 });
    expect(patch.headers.get("If-Match")).toBe('"3"');
    expect(patch.headers.get("X-Tenant-Id")).toBe(tenantId);
    expect(patch.headers.get("X-CSRF")).toBe("1");
  });

  it("says the record changed since it was opened, and reloads it", async () => {
    let reads = 0;
    open(["tenant.settings.read", "tenant.settings.update"], {
      "GET /api/admin/v1/tenant/settings": () => ({
        status: 200,
        body: ++reads === 1 ? settings : { ...settings, publicEnabled: false, version: 4 },
      }),
      "PATCH /api/admin/v1/tenant/settings": { status: 409, body: problem("TENANT_CONCURRENCY_CONFLICT", 409) },
    });

    fireEvent.change(await screen.findByLabelText(/Starting soon/), { target: { value: "60" } });
    fireEvent.click(screen.getByRole("button", { name: "Save changes" }));
    fireEvent.click(await screen.findByRole("button", { name: "Reload" }));

    await waitFor(() => expect(screen.getByRole("checkbox", { name: "Public site" }).getAttribute("aria-checked")).toBe("false"));
    expect(screen.queryByText("This record changed since you opened it")).toBeNull();
    expect((screen.getByLabelText(/Starting soon/) as HTMLInputElement).value).toBe("30");
  });

  it("puts the API's refusal of a value under its field", async () => {
    open(["tenant.settings.read", "tenant.settings.update"], {
      "PATCH /api/admin/v1/tenant/settings": {
        status: 400,
        body: problem("VALIDATION_INVALID_REQUEST", 400, {
          errors: [{ field: "defaultLocale", code: "VALIDATION_INVALID_VALUE", message: "Unknown language." }],
        }),
      },
    });

    fireEvent.change(await screen.findByLabelText("Language"), { target: { value: "xx-ZZ" } });
    fireEvent.click(screen.getByRole("button", { name: "Save changes" }));

    const language = screen.getByLabelText("Language");
    await waitFor(() => expect(language.getAttribute("aria-invalid")).toBe("true"));
    expect(screen.queryByRole("alert")).toBeNull();
  });

  it("refuses the page to someone without tenant.settings.read", async () => {
    const requests = open(["membership.read"]);

    expect(await screen.findByText("You can't open this page")).toBeTruthy();
    expect(requests.some((request) => request.path === "/api/admin/v1/tenant/settings")).toBe(false);
  });
});

describe("changedSettings", () => {
  it("leaves out what didn't change", () => {
    const values = {
      timeZone: settings.timeZone,
      defaultLocale: settings.defaultLocale,
      currencyCode: "KES",
      publicEnabled: true,
      startingSoonMinutes: settings.startingSoonMinutes,
      minorRegistrationConsent: settings.minorRegistrationConsent,
    };
    expect(changedSettings(settings, values)).toEqual({
      currencyCode: "KES",
    });
  });
});
