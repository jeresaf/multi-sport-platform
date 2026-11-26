import { expect, test } from "@playwright/test";
import {
  acceptByApi,
  createTenant,
  inviteMember,
  inviteOwnerOf,
  newPassword,
  ownTenant,
  personEmail,
  platformAdmin,
  signIn,
  signInByApi,
  suspendTenant,
} from "./support/accounts";

// Each test that adds members has a tenant of its own (S06-T7a): KMSA and ELSSC stay the manual demo, and members
// added there on every run would pass STANDARD's staff limit on a reused database.
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
  await expect(page.getByRole("heading", { level: 1, name: "Tenants" })).toBeVisible();
  // Screens that come later answer "not available yet".
  await workspaceNav(page).getByRole("link", { name: "Diagnostics" }).click();
  await expect(page).toHaveURL("/platform/diagnostics");
  await expect(page.getByText("This screen isn't available yet.")).toBeVisible();

  // Not a member of any tenant: the switcher says so and offers the console.
  await page.getByRole("button", { name: "Workspace: Platform console" }).click();
  await expect(page.getByRole("menuitem", { name: "You aren't a member of any organisation yet" })).toBeVisible();
  await expect(page.getByRole("menuitem", { name: "Platform console" })).toBeVisible();
});

test("a tenant owner opens their workspace; the console and unknown tenants are refused", async ({ page, request }) => {
  const tenant = await ownTenant(request, "turaco-sports", "Turaco Sports Club");
  const email = personEmail("moses.okello", tenant.slug);
  const password = newPassword();
  await acceptByApi(request, await inviteOwnerOf(request, tenant.id, email), password);

  // A signed-out deep link comes back to the page asked for after signing in.
  await page.goto(`/t/${tenant.slug}/settings/members`);
  await expect(page).toHaveURL(`/sign-in?returnTo=%2Ft%2F${tenant.slug}%2Fsettings%2Fmembers`);
  await page.getByLabel("Email").fill(email);
  await page.getByLabel("Password").fill(password);
  await page.getByRole("button", { name: "Sign in" }).click();
  await expect(page).toHaveURL(`/t/${tenant.slug}/settings/members`);
  await expect(page.getByRole("heading", { level: 1, name: "Members & roles" })).toBeVisible();

  await workspaceNav(page).getByRole("link", { name: "Overview" }).click();
  await expect(page.getByRole("heading", { level: 1, name: tenant.name })).toBeVisible();
  await expect(workspaceNav(page).getByRole("link", { name: "Organisation", exact: true })).toBeVisible();
  await expect(workspaceNav(page).getByRole("link", { name: "Members & roles" })).toBeVisible();
  // The entries under their groups (S06-T7a).
  await expect(workspaceNav(page).getByRole("list", { name: "Registry" }).getByRole("link", { name: "People" })).toBeVisible();
  await expect(workspaceNav(page).getByRole("list", { name: "Settings" }).getByRole("link", { name: "Organisation", exact: true })).toBeVisible();

  await page.getByRole("button", { name: `Workspace: ${tenant.name}` }).click();
  await expect(page.getByRole("menuitem", { name: tenant.name })).toBeVisible();
  await expect(page.getByRole("menuitem", { name: "Platform console" })).toHaveCount(0);
  await page.keyboard.press("Escape");

  await page.goto("/platform");
  await expect(page.getByRole("heading", { level: 1, name: "You can't open this page" })).toBeVisible();

  await page.goto("/t/no-such-tenant");
  await expect(page.getByRole("heading", { level: 1, name: "Page not found" })).toBeVisible();

});

test("`/` opens the last workspace used in this browser, else the first active tenant", async ({ page, request }) => {
  // Owner of two tenants: Marabou comes first by name.
  const marabou = await ownTenant(request, "marabou-sports", "Marabou Sports Club");
  const papyrus = await ownTenant(request, "papyrus-lakeside", "Papyrus Lakeside Sports Club");
  const email = personEmail("isaac.mugisha", marabou.slug);
  const password = newPassword();
  await acceptByApi(request, await inviteOwnerOf(request, marabou.id, email), password);
  const papyrusLink = await inviteOwnerOf(request, papyrus.id, email);
  // An existing account accepts while signed in as itself (S03 D12).
  await signInByApi(request, email, password);
  await acceptByApi(request, papyrusLink, password);

  await signIn(page, email, password);
  await expect(page).toHaveURL(`/t/${marabou.slug}`);

  await page.getByRole("button", { name: /^Workspace: / }).click();
  await page.getByRole("menuitem", { name: papyrus.name }).click();
  await expect(page.getByRole("heading", { level: 1, name: papyrus.name })).toBeVisible();
  // Remembered after the workspace opens (an effect): wait for it rather than racing it.
  await expect.poll(() => page.evaluate(() => localStorage.getItem("msp.admin.lastWorkspace"))).toBe(`/t/${papyrus.slug}`);

  await page.goto("/");
  await expect(page).toHaveURL(`/t/${papyrus.slug}`);
});

test("a registrar sees only what their role allows, and the server refuses the rest", async ({ page, request }) => {
  const tenant = await ownTenant(request, "pelican-football", "Pelican Football Club");
  const tenantId = tenant.id;
  const owner = { email: personEmail("isaac.mugisha", tenant.slug), password: newPassword() };
  await acceptByApi(request, await inviteOwnerOf(request, tenantId, owner.email), owner.password);

  const registrar = { email: personEmail("joan.nansubuga", tenant.slug), password: newPassword() };
  const link = await inviteMember(request, { ...owner, tenantId }, registrar.email, ["REGISTRAR"]);
  await acceptByApi(request, link, registrar.password);

  await signIn(page, registrar.email, registrar.password);
  await expect(page).toHaveURL(`/t/${tenant.slug}`);
  await expect(workspaceNav(page).getByRole("link", { name: "Organisation", exact: true })).toBeVisible();
  await expect(workspaceNav(page).getByRole("link", { name: "Members & roles" })).toHaveCount(0);
  await expect(workspaceNav(page).getByRole("link", { name: "People" })).toBeVisible();

  // The hidden screen's address answers 403 in the app, and its action 403 from the API (with the registrar's cookie).
  await page.goto(`/t/${tenant.slug}/settings/members`);
  await expect(page.getByRole("heading", { level: 1, name: "You can't open this page" })).toBeVisible();
  const invited = await page.request.post("/api/admin/v1/invitations", {
    data: { email: personEmail("esther.nabirye", tenant.slug), roleCodes: ["RESULTS_OFFICER"] },
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
