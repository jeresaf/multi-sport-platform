// Accounts for the admin tests. The platform administrator is this run's (playwright.config.ts, start-api.sh); every
// other person is invited by it with a run-unique address, so a run never depends on another run's data.
import { expect, type APIRequestContext, type Page } from "@playwright/test";
import { linkFromEmail } from "./mailpit";
import { open } from "./pages";

export const platformAdmin = {
  email: process.env.MSP_E2E_ADMIN_EMAIL!,
  password: process.env.MSP_E2E_ADMIN_PASSWORD!,
};

const run = process.env.MSP_E2E_RUN!;

/** A run-unique address for a fictional person (pilot-customer.md), e.g. `grace.nakato.3fa1c2d4.kmsa@msp-e2e.example`. */
export function personEmail(name: string, tenantSlug: string): string {
  return `${name}.${run}.${tenantSlug}@msp-e2e.example`;
}

/** Every state-changing call through the admin app's rewrite carries X-CSRF (S03 D7). */
const csrf = { "X-CSRF": "1" };

export async function signInByApi(request: APIRequestContext, email: string, password: string) {
  const response = await request.post("/api/account/v1/login", { data: { email, password }, headers: csrf });
  expect(response.status(), "sign-in").toBe(204);
}

/** The ID of the active tenant with `slug`, read by the platform administrator. */
export async function tenantId(request: APIRequestContext, slug: string): Promise<string> {
  const tenants = await request.get("/api/platform/v1/tenants", { params: { status: "ACTIVE", limit: 100 } });
  expect(tenants.status()).toBe(200);
  const { items } = (await tenants.json()) as { items: { id: string; slug: string }[] };
  const tenant = items.find((item) => item.slug === slug);
  expect(tenant, `tenant ${slug}`).toBeDefined();
  return tenant!.id;
}

/** The platform administrator invites `email` as the owner of the tenant with `slug`; returns the email's accept link. */
export async function inviteOwner(request: APIRequestContext, slug: string, email: string): Promise<string> {
  await signInByApi(request, platformAdmin.email, platformAdmin.password);
  return inviteOwnerOf(request, await tenantId(request, slug), email);
}

/** As `inviteOwner`, for a tenant known by its ID (one the test created). */
export async function inviteOwnerOf(request: APIRequestContext, id: string, email: string): Promise<string> {
  await signInByApi(request, platformAdmin.email, platformAdmin.password);
  const invited = await request.post(`/api/platform/v1/tenants/${id}/owner-invitations`, {
    data: { email },
    headers: csrf,
  });
  expect(invited.status(), "owner invitation").toBe(201);

  return linkFromEmail(request, email, "/accept-invitation");
}

/** The platform administrator creates and activates a tenant of this run's own (fictional, pilot-customer.md style). */
export async function createTenant(request: APIRequestContext, slug: string, displayName: string): Promise<string> {
  await signInByApi(request, platformAdmin.email, platformAdmin.password);
  const created = await request.post("/api/platform/v1/tenants", {
    data: {
      slug,
      legalName: displayName,
      displayName,
      countryCode: "UG",
      timeZone: "Africa/Kampala",
      defaultLocale: "en-UG",
      currencyCode: "UGX",
    },
    headers: csrf,
  });
  expect(created.status(), "create tenant").toBe(201);
  const { id } = (await created.json()) as { id: string };
  const activated = await request.post(`/api/platform/v1/tenants/${id}/activate`, { headers: csrf });
  expect(activated.status(), "activate tenant").toBe(200);
  await assignSubscription(request, id);
  return id;
}

/** A date in Kampala `days` from today, as the API's dates (`2026-10-23`): subscription days are the tenant's (S04 D3). */
function kampalaDate(days: number): string {
  const today = new Intl.DateTimeFormat("en-CA", { timeZone: "Africa/Kampala" }).format(new Date());
  const date = new Date(`${today}T00:00:00Z`);
  date.setUTCDate(date.getUTCDate() + days);
  return date.toISOString().slice(0, 10);
}

/**
 * Gives a tenant a STANDARD period from yesterday to 179 days ahead (yesterday, so it has started even if Kampala's date
 * turns between this runner and the API, #66 R7), as the signed-in platform administrator of `request`, so an
 * activated tenant has full access instead of being read-only (S04 D9, D18). The console's Subscription panel comes
 * with S04-T4.
 */
export async function assignSubscription(request: APIRequestContext, id: string) {
  const assigned = await request.post(`/api/platform/v1/tenants/${id}/subscription/periods`, {
    data: { planCode: "STANDARD", kind: "PAID", startsOn: kampalaDate(-1), endsOn: kampalaDate(179) },
    headers: csrf,
  });
  expect(assigned.status(), "assign subscription").toBe(201);
}

export async function suspendTenant(request: APIRequestContext, id: string, reason: string) {
  await signInByApi(request, platformAdmin.email, platformAdmin.password);
  const suspended = await request.post(`/api/platform/v1/tenants/${id}/suspend`, {
    data: { reason },
    headers: csrf,
  });
  expect(suspended.status(), "suspend tenant").toBe(200);
}

/** A member who can invite (signed in as `inviter`) invites `email` with `roleCodes`; returns the email's accept link. */
export async function inviteMember(
  request: APIRequestContext,
  inviter: { email: string; password: string; tenantId: string },
  email: string,
  roleCodes: string[],
): Promise<string> {
  await signInByApi(request, inviter.email, inviter.password);
  const invited = await request.post("/api/admin/v1/invitations", {
    data: { email, roleCodes },
    headers: { ...csrf, "X-Tenant-Id": inviter.tenantId },
  });
  expect(invited.status(), "member invitation").toBe(201);

  return linkFromEmail(request, email, "/accept-invitation");
}

/** The signed-in user's tenant ID for `slug`, from `/me`. */
export async function myTenantId(request: APIRequestContext, slug: string): Promise<string> {
  const me = await request.get("/api/account/v1/me");
  expect(me.status()).toBe(200);
  const { tenants } = (await me.json()) as { tenants: { tenantId: string; slug: string }[] };
  const tenant = tenants.find((item) => item.slug === slug);
  expect(tenant, `membership in ${slug}`).toBeDefined();
  return tenant!.tenantId;
}

/** Accepts an invitation for a new account through the API, so a test can start from a member who can sign in. */
export async function acceptByApi(request: APIRequestContext, link: string, password: string) {
  const token = new URL(link).searchParams.get("token");
  const accepted = await request.post("/api/account/v1/invitations/accept", { data: { token, password }, headers: csrf });
  expect(accepted.status(), `accept: ${await accepted.text()}`).toBe(200);
}

export async function signIn(page: Page, email: string, password: string) {
  await open(page, "/sign-in");
  await page.getByLabel("Email").fill(email);
  await page.getByLabel("Password").fill(password);
  await page.getByRole("button", { name: "Sign in" }).click();
}

export const newPassword = () => `e2e ${run} ${Math.random().toString(36).slice(2)} passphrase`;
