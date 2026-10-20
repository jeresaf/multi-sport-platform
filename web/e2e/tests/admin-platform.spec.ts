import { expect, test, type Page } from "@playwright/test";
import { platformAdmin, signIn } from "./support/accounts";
import { open } from "./support/pages";

// The platform console's tenants through the screens (S03-T6c2): a taken slug is refused on its field, and a tenant
// goes through its lifecycle (state-machines.md), a reason given where one is required.

const run = process.env.MSP_E2E_RUN!;

/** Confirms the lifecycle change `label` on the tenant's page, with `reason` when the change requires one. */
async function change(page: Page, label: string, announced: string, reason?: string) {
  await page.getByRole("button", { name: label, exact: true }).click();
  const dialog = page.getByRole("dialog");
  if (reason) {
    await dialog.getByLabel("Reason").fill(reason);
  }
  await dialog.getByRole("button", { name: label, exact: true }).click();
  await expect(dialog).toBeHidden();
  await expect(page.getByRole("status").filter({ hasText: announced })).toBeAttached();
}

test("the platform administrator creates a tenant and takes it through its lifecycle", async ({ page }) => {
  // The suspended list is ordered by slug, 25 a page, and a kept local database gathers other runs' suspended tenants:
  // a slug sorting first stays on the first page.
  const slug = `abaana-netball-${run}`;
  const name = `Abaana Netball Circle ${run}`;
  await signIn(page, platformAdmin.email, platformAdmin.password);
  await expect(page).toHaveURL("/platform");
  await open(page, "/platform/tenants");

  // A slug already taken is refused on the slug field; the form keeps what was typed.
  await page.getByRole("button", { name: "New tenant" }).click();
  const create = page.getByRole("dialog");
  await create.getByLabel("Slug").fill("kmsa");
  await create.getByLabel("Name", { exact: true }).fill(name);
  await create.getByLabel("Legal name").fill(`${name} Limited`);
  await create.getByLabel("Country").selectOption("UG");
  await create.getByLabel("Time zone").selectOption("Africa/Kampala");
  await create.getByLabel("Language").fill("en-UG");
  await create.getByLabel("Currency").selectOption("UGX");
  await create.getByRole("button", { name: "Create tenant" }).click();
  await expect(create.getByLabel("Slug")).toHaveAttribute("aria-invalid", "true");
  await expect(create.getByLabel("Name", { exact: true })).toHaveValue(name);

  await create.getByLabel("Slug").fill(slug);
  await create.getByRole("button", { name: "Create tenant" }).click();
  await expect(page.getByRole("heading", { level: 1, name })).toBeVisible();
  await expect(page).toHaveURL(/\/platform\/tenants\/[0-9a-f-]{36}$/);
  const detail = page.url();

  await change(page, "Activate", "Activated.");
  await change(page, "Suspend", "Suspended.", "Annual fee unpaid");

  // The list's status filter finds it among the suspended tenants.
  await open(page, "/platform/tenants");
  await page.getByLabel("Tenants to show").selectOption("SUSPENDED");
  await expect(page.getByRole("row").filter({ hasText: slug })).toContainText("Suspended");
  await page.getByRole("link", { name }).click();
  await expect(page).toHaveURL(detail);

  await change(page, "Restore", "Restored.");
  await change(page, "Archive", "Archived.", "Merged into another association");

  // An archived tenant can't be invited to.
  await expect(page.getByRole("button", { name: "Invite an owner" })).toHaveCount(0);
  await expect(page.getByText("An archived tenant can't be invited to.")).toBeVisible();

  // Unarchived, it is suspended until restored, and can be invited to again.
  await change(page, "Unarchive", "Unarchived: the tenant is suspended until it is restored.", "Association re-formed");
  await expect(page.getByRole("button", { name: "Invite an owner" })).toBeVisible();
  // Restored, it leaves no suspended tenant behind for later runs.
  await change(page, "Restore", "Restored.");
});
