import { expect, test } from "@playwright/test";
import {
  acceptByApi,
  createTenant,
  inviteMember,
  inviteOwner,
  inviteOwnerOf,
  myTenantId,
  newPassword,
  personEmail,
  platformAdmin,
  signIn,
  signInByApi,
  suspendTenant,
} from "./support/accounts";

const kmsa = "Kampala Metro Sports Association";
const workspaceNav = (page: import("@playwright/test").Page) => page.getByRole("navigation", { name: "Workspace" });

test("the platform administrator works in the console, with its navigation", async ({ page }) => {
  await signIn(page, platformAdmin.email, platformAdmin.password);

  await expect(page).toHaveURL("/platform");
  await expect(page.getByRole("heading", { level: 1, name: "Platform console" })).toBeVisible();
  for (const entry of ["Overview", "Tenants", "Diagnostics", "Audit"]) {
    await expect(workspaceNav(page).getByRole("link", { name: entry })).toBeVisible();
  }

  await workspaceNav(page).getByRole("link", { name: "Tenants" }).click();
  await expect(page).toHaveURL("/platform/tenants");
  await expect(page.getByText("This screen isn't available yet.")).toBeVisible();

  // Not a member of any tenant: the switcher says so and offers the console.
  await page.getByRole("button", { name: "Workspace: Platform console" }).click();
  await expect(page.getByRole("menuitem", { name: "You aren't a member of any organisation yet" })).toBeVisible();
  await expect(page.getByRole("menuitem", { name: "Platform console" })).toBeVisible();
});

test("a tenant owner opens their workspace; the console and unknown tenants are refused", async ({ page, request }) => {
  const email = personEmail("moses.okello", "kmsa");
  const password = newPassword();
  await acceptByApi(request, await inviteOwner(request, "kmsa", email), password);

  // A signed-out deep link comes back to the page asked for after signing in.
  await page.goto("/t/kmsa/settings/members");
  await expect(page).toHaveURL("/sign-in?returnTo=%2Ft%2Fkmsa%2Fsettings%2Fmembers");
  await page.getByLabel("Email").fill(email);
  await page.getByLabel("Password").fill(password);
  await page.getByRole("button", { name: "Sign in" }).click();
  await expect(page).toHaveURL("/t/kmsa/settings/members");
  await expect(page.getByRole("heading", { level: 1, name: "Members & roles" })).toBeVisible();

  await workspaceNav(page).getByRole("link", { name: "Overview" }).click();
  await expect(page.getByRole("heading", { level: 1, name: kmsa })).toBeVisible();
  await expect(workspaceNav(page).getByRole("link", { name: "Organisation" })).toBeVisible();
  await expect(workspaceNav(page).getByRole("link", { name: "Members & roles" })).toBeVisible();

  await page.getByRole("button", { name: `Workspace: ${kmsa}` }).click();
  await expect(page.getByRole("menuitem", { name: kmsa })).toBeVisible();
  await expect(page.getByRole("menuitem", { name: "Platform console" })).toHaveCount(0);
  await page.keyboard.press("Escape");

  await page.goto("/platform");
  await expect(page.getByRole("heading", { level: 1, name: "You can't open this page" })).toBeVisible();

  await page.goto("/t/no-such-tenant");
  await expect(page.getByRole("heading", { level: 1, name: "Page not found" })).toBeVisible();

});

test("`/` opens the last workspace used in this browser, else the first active tenant", async ({ page, request }) => {
  // Owner of both DEMO tenants: ELSSC ("Eastern Lakes…") comes first by name.
  const email = personEmail("isaac.mugisha", "elssc");
  const password = newPassword();
  await acceptByApi(request, await inviteOwner(request, "elssc", email), password);
  const kmsaLink = await inviteOwner(request, "kmsa", email);
  // An existing account accepts while signed in as itself (S03 D12).
  await signInByApi(request, email, password);
  await acceptByApi(request, kmsaLink, password);

  await signIn(page, email, password);
  await expect(page).toHaveURL("/t/elssc");

  await page.getByRole("button", { name: /^Workspace: / }).click();
  await page.getByRole("menuitem", { name: kmsa }).click();
  await expect(page.getByRole("heading", { level: 1, name: kmsa })).toBeVisible();
  // Remembered after the workspace opens (an effect): wait for it rather than racing it.
  await expect.poll(() => page.evaluate(() => localStorage.getItem("msp.admin.lastWorkspace"))).toBe("/t/kmsa");

  await page.goto("/");
  await expect(page).toHaveURL("/t/kmsa");
});

test("a registrar sees only what their role allows, and the server refuses the rest", async ({ page, request }) => {
  const owner = { email: personEmail("isaac.mugisha", "kmsa"), password: newPassword() };
  await acceptByApi(request, await inviteOwner(request, "kmsa", owner.email), owner.password);
  await signInByApi(request, owner.email, owner.password);
  const tenantId = await myTenantId(request, "kmsa");

  const registrar = { email: personEmail("joan.nansubuga", "kmsa"), password: newPassword() };
  const link = await inviteMember(request, { ...owner, tenantId }, registrar.email, ["REGISTRAR"]);
  await acceptByApi(request, link, registrar.password);

  await signIn(page, registrar.email, registrar.password);
  await expect(page).toHaveURL("/t/kmsa");
  await expect(workspaceNav(page).getByRole("link", { name: "Organisation" })).toBeVisible();
  await expect(workspaceNav(page).getByRole("link", { name: "Members & roles" })).toHaveCount(0);

  // The hidden screen's address answers 403 in the app, and its action 403 from the API (with the registrar's cookie).
  await page.goto("/t/kmsa/settings/members");
  await expect(page.getByRole("heading", { level: 1, name: "You can't open this page" })).toBeVisible();
  const invited = await page.request.post("/api/admin/v1/invitations", {
    data: { email: personEmail("esther.nabirye", "kmsa"), roleCodes: ["RESULTS_OFFICER"] },
    headers: { "X-CSRF": "1", "X-Tenant-Id": tenantId },
  });
  expect(invited.status()).toBe(403);
  expect(await invited.json()).toMatchObject({ code: "AUTH_FORBIDDEN" });
});

test("the owner of a suspended tenant is told why its workspace is closed", async ({ page, request }) => {
  const run = process.env.MSP_E2E_RUN!;
  // A test-only tenant (pilot-customer.md, "Other tenants"), made run-unique.
  const slug = `sunbird-juniors-${run}`;
  const name = `Sunbird Junior Sports Club ${run}`;
  const id = await createTenant(request, slug, name);
  const email = personEmail("sarah.achieng", slug);
  const password = newPassword();
  await acceptByApi(request, await inviteOwnerOf(request, id, email), password);
  await suspendTenant(request, id, "Subscription unpaid since September");

  await signIn(page, email, password);

  await expect(page).toHaveURL(`/t/${slug}`);
  await expect(page.getByRole("heading", { level: 1, name: `${name} is suspended` })).toBeVisible();
  await expect(page.getByText("Subscription unpaid since September")).toBeVisible();
  await expect(workspaceNav(page)).toHaveCount(0);
});
