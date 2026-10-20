import { expect, test, type Page } from "@playwright/test";
import {
  acceptByApi,
  createTenant,
  inviteOwner,
  inviteOwnerOf,
  newPassword,
  personEmail,
  platformAdmin,
  signIn,
  signInByApi,
  suspendTenant,
  tenantId,
} from "./support/accounts";
import { open } from "./support/pages";
import { phone, shoot, widths } from "./support/screenshots";

// A screen still reading its data (`Loading`, a polite status).
const loading = (page: Page) => page.getByRole("status").filter({ hasText: "Loading" });

test("screenshots of the signed-out pages", async ({ page, request }) => {
  await open(page, "/sign-in");
  await page.getByRole("button", { name: "Sign in" }).click();
  await expect(page.getByText("Enter your email address.")).toBeVisible();
  await shoot(page, "sign-in-errors", [phone, ...widths]);

  await open(page, "/forgot-password");
  await expect(page.getByRole("heading", { level: 1, name: "Forgot your password?" })).toBeVisible();
  await shoot(page, "forgot-password", [phone, ...widths]);

  await page.goto(await inviteOwner(request, "elssc", personEmail("daniel.kato", "elssc")));
  await expect(page.getByLabel("Choose a password")).toBeVisible();
  await shoot(page, "accept-invitation", [phone, ...widths]);
});

test("screenshots of the platform console", async ({ page }) => {
  await signIn(page, platformAdmin.email, platformAdmin.password);
  await expect(page.getByRole("heading", { level: 1, name: "Platform console" })).toBeVisible();
  await shoot(page, "platform-console", [phone, ...widths]);

  await page.getByRole("button", { name: /^Account menu: / }).click();
  await expect(page.getByRole("menuitem", { name: "Sign out" })).toBeVisible();
  await shoot(page, "user-menu", widths);
  await page.keyboard.press("Escape");

  // Screens that come later answer "not available yet".
  await page.goto("/platform/diagnostics");
  await expect(page.getByText("This screen isn't available yet.")).toBeVisible();
  await shoot(page, "not-available", widths);
});

test("screenshots of the platform console's tenants", async ({ page, request }) => {
  await signInByApi(request, platformAdmin.email, platformAdmin.password);
  const kmsa = await tenantId(request, "kmsa");
  await signIn(page, platformAdmin.email, platformAdmin.password);
  await expect(page.getByRole("heading", { level: 1, name: "Platform console" })).toBeVisible();

  await page.goto("/platform/tenants");
  await expect(page.getByRole("heading", { level: 1, name: "Tenants" })).toBeVisible();
  await expect(page.getByRole("row").nth(1)).toBeVisible();
  await expect(loading(page)).toHaveCount(0);
  await shoot(page, "tenants", [phone, ...widths]);

  await page.getByRole("button", { name: "New tenant" }).click();
  const create = page.getByRole("dialog");
  await expect(create.getByRole("option", { name: "Uganda", exact: true })).toBeAttached();
  await create.getByRole("button", { name: "Create tenant" }).click();
  await expect(create.getByText("Choose a country.")).toBeVisible();
  await shoot(page, "tenant-create", [phone, ...widths]);
  await page.keyboard.press("Escape");

  await page.goto(`/platform/tenants/${kmsa}`);
  await expect(page.getByRole("heading", { level: 1, name: "Kampala Metro Sports Association" })).toBeVisible();
  await expect(page.getByText("Uganda")).toBeVisible();
  await shoot(page, "tenant-detail", [phone, ...widths]);

  // The dialogs only: the demo tenant stays active for the other tests.
  await page.getByRole("button", { name: "Suspend" }).click();
  await expect(page.getByRole("dialog").getByLabel("Reason")).toBeVisible();
  await shoot(page, "tenant-reason-dialog", [phone, ...widths]);
  await page.keyboard.press("Escape");

  await page.getByRole("button", { name: "Invite an owner" }).click();
  await expect(page.getByRole("dialog").getByLabel("Email")).toBeVisible();
  await shoot(page, "owner-invite-dialog", [phone, ...widths]);
  await page.keyboard.press("Escape");
});

test("screenshots of a tenant workspace", async ({ page, request }) => {
  const email = personEmail("grace.nakato", "kmsa");
  const password = newPassword();
  await acceptByApi(request, await inviteOwner(request, "kmsa", email), password);
  await signIn(page, email, password);
  await expect(page.getByRole("heading", { level: 1, name: "Kampala Metro Sports Association" })).toBeVisible();
  await shoot(page, "tenant-workspace", [phone, ...widths]);

  await page.getByRole("button", { name: "Workspace: Kampala Metro Sports Association" }).click();
  await expect(page.getByRole("menu")).toBeVisible();
  await shoot(page, "workspace-switcher", widths);
  await page.keyboard.press("Escape");

  await page.setViewportSize(phone);
  await page.getByRole("button", { name: "Open navigation" }).click();
  await expect(page.getByRole("dialog")).toBeVisible();
  await shoot(page, "navigation-sheet", [phone]);
  await page.keyboard.press("Escape");

  await page.goto("/t/kmsa/settings/organisation");
  await expect(page.getByRole("heading", { level: 1, name: "Organisation" })).toBeVisible();
  await expect(page.getByLabel("Time zone")).toHaveValue("Africa/Kampala");
  await expect(loading(page)).toHaveCount(0);
  await shoot(page, "organisation", [phone, ...widths]);

  await page.goto("/t/kmsa/settings/members");
  await expect(page.getByRole("heading", { level: 1, name: "Members & roles" })).toBeVisible();
  // Oldest first, 25 a page: on a database that earlier runs used, this run's owner may be on a later page.
  await expect(page.getByRole("row").filter({ hasText: "Tenant owner" }).first()).toBeVisible();
  await expect(loading(page)).toHaveCount(0);
  await shoot(page, "members", [phone, ...widths]);

  await page.goto("/platform");
  await expect(page.getByRole("heading", { level: 1, name: "You can't open this page" })).toBeVisible();
  await shoot(page, "forbidden", [phone, ...widths]);

  await page.goto("/t/no-such-tenant");
  await expect(page.getByRole("heading", { level: 1, name: "Page not found" })).toBeVisible();
  await shoot(page, "not-found", [phone, ...widths]);
});

test("screenshots of a suspended tenant", async ({ page, request }) => {
  const run = process.env.MSP_E2E_RUN!;
  const slug = `acacia-schools-${run}`;
  const id = await createTenant(request, slug, `Acacia Schools Netball Circle ${run}`);
  const email = personEmail("harriet.akello", slug);
  const password = newPassword();
  await acceptByApi(request, await inviteOwnerOf(request, id, email), password);
  await suspendTenant(request, id, "Subscription unpaid since September");

  await signIn(page, email, password);
  await expect(page.getByText("Subscription unpaid since September")).toBeVisible();
  await shoot(page, "tenant-suspended", [phone, ...widths]);
});
