// Accounts for the admin tests. The platform administrator is this run's (playwright.config.ts, start-api.sh); every
// other person is invited by it with a run-unique address, so a run never depends on another run's data.
import { expect, type APIRequestContext, type Page } from "@playwright/test";
import { linkFromEmail } from "./mailpit";

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

/** The platform administrator invites `email` as the owner of the tenant with `slug`; returns the email's accept link. */
export async function inviteOwner(request: APIRequestContext, slug: string, email: string): Promise<string> {
  await signInByApi(request, platformAdmin.email, platformAdmin.password);

  const tenants = await request.get("/api/platform/v1/tenants", { params: { status: "ACTIVE", limit: 100 } });
  expect(tenants.status()).toBe(200);
  const { items } = (await tenants.json()) as { items: { id: string; slug: string }[] };
  const tenant = items.find((item) => item.slug === slug);
  expect(tenant, `tenant ${slug}`).toBeDefined();

  const invited = await request.post(`/api/platform/v1/tenants/${tenant!.id}/owner-invitations`, {
    data: { email },
    headers: csrf,
  });
  expect(invited.status(), "owner invitation").toBe(201);

  return linkFromEmail(request, email, "/accept-invitation");
}

/** Accepts an invitation for a new account through the API, so a test can start from a member who can sign in. */
export async function acceptByApi(request: APIRequestContext, link: string, password: string) {
  const token = new URL(link).searchParams.get("token");
  const accepted = await request.post("/api/account/v1/invitations/accept", { data: { token, password }, headers: csrf });
  expect(accepted.status(), "accept").toBe(200);
}

export async function signIn(page: Page, email: string, password: string) {
  await page.goto("/sign-in");
  await page.getByLabel("Email").fill(email);
  await page.getByLabel("Password").fill(password);
  await page.getByRole("button", { name: "Sign in" }).click();
}

export const newPassword = () => `e2e ${run} ${Math.random().toString(36).slice(2)} passphrase`;
