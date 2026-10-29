import { expect, test, type Page } from "@playwright/test";
import { auditEntries, myUserId, type AuditEntry } from "./support/audit";
import { createTenant, kampalaDate, platformAdmin, signIn } from "./support/accounts";
import { open } from "./support/pages";

// The platform console's tenants through the screens (S03-T6c2): a taken slug is refused on its field, and a tenant
// goes through its lifecycle (state-machines.md), a reason given where one is required. Its subscription is renewed,
// moved to a higher plan, given an override and cancelled from the Subscription panel (S04-T4a), each change audited.

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
  const slug = `acacia-runners-${run}`;
  const name = `Acacia Road Runners ${run}`;
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

/** Sends the subscription dialog that is open with `submit`, and waits for the panel to announce `announced`. */
async function send(page: Page, submit: string, announced: string) {
  const dialog = page.getByRole("dialog");
  await dialog.getByRole("button", { name: submit, exact: true }).click();
  await expect(dialog).toBeHidden();
  await expect(page.getByRole("status").filter({ hasText: announced })).toBeAttached();
}

test("the platform administrator manages a tenant's subscription from its panel", async ({ page, request }) => {
  const slug = `kingfisher-masters-${run}`;
  // Created and activated with a STANDARD period from yesterday (support/accounts.ts).
  const tenantId = await createTenant(request, slug, `Kingfisher Masters Hockey Club ${run}`);
  await signIn(page, platformAdmin.email, platformAdmin.password);
  await expect(page).toHaveURL("/platform");
  await open(page, `/platform/tenants/${tenantId}`);
  const panel = page.locator("section", { has: page.getByRole("heading", { name: "Subscription", exact: true }) });
  await expect(panel.getByRole("heading", { name: "Current period" })).toBeVisible();

  // Renewed for another year: a scheduled period follows the current one.
  await panel.getByRole("button", { name: "Renew" }).click();
  await page.getByRole("dialog").getByLabel("Last day").fill(kampalaDate(545));
  await send(page, "Renew", "Renewed.");
  await expect(panel.getByText(/^Next: Standard, paid, from /)).toBeVisible();
  await expect(panel.getByRole("row").filter({ hasText: "Scheduled" })).toHaveCount(1);

  // Moved to PRO today: the STANDARD period is cancelled ("Changed to PRO") and a PRO one takes its place.
  await panel.getByRole("button", { name: "Change plan" }).click();
  await page.getByRole("dialog").getByLabel("New plan").selectOption("PRO");
  await send(page, "Change plan", "Plan changed.");
  await expect(panel.getByRole("row").filter({ hasText: "Changed to PRO" })).toHaveCount(1);
  await expect(panel.getByRole("button", { name: "Change plan" })).toHaveCount(0);

  // Five staff seats by override, then removed: the history keeps it with both reasons.
  await panel.getByRole("button", { name: "Set an override" }).click();
  const override = page.getByRole("dialog");
  await override.getByLabel("Entitlement").selectOption("MAX_STAFF_USERS");
  await override.getByLabel("Limit (users)").fill("5");
  await override.getByLabel("Reason").fill("Club committee only during the masters league");
  await send(page, "Set override", "Override set.");
  const staff = panel.getByRole("listitem").filter({ hasText: "Club committee only during the masters league" });
  await expect(staff).toContainText("Staff users: 5 users");
  // The effective value says where it comes from.
  await expect(panel.getByRole("listitem").filter({ has: page.getByText("Staff users", { exact: true }) })).toContainText(
    "5 users · Override",
  );

  await staff.getByRole("button", { name: "Remove" }).click();
  await page.getByRole("dialog").getByLabel("Reason").fill("Masters league over");
  await send(page, "Remove override", "Override removed.");
  await expect(panel.getByText("No overrides.")).toBeVisible();
  await panel.getByText("Earlier overrides (1)").click();
  await expect(panel.getByText(/Masters league over/)).toBeVisible();

  // Cancelled: the current and the scheduled periods end; only assigning or renewing is left.
  await panel.getByRole("button", { name: "Cancel subscription" }).click();
  await page.getByRole("dialog").getByLabel("Reason").fill("Club wound up");
  await send(page, "Cancel subscription", "Subscription cancelled.");
  await expect(panel.getByRole("row").filter({ hasText: "Club wound up" })).toHaveCount(2);
  await expect(panel.getByRole("button", { name: "Cancel subscription" })).toHaveCount(0);

  // Every change is audited with the administrator and its reason.
  const adminId = await myUserId(page.request);
  const by = (actionCode: string, reason?: string) => (entry: AuditEntry) =>
    entry.actionCode === actionCode &&
    entry.outcome === "SUCCESS" &&
    entry.actorId === adminId &&
    (reason === undefined || entry.reason === reason);
  for (const [action, reason] of [
    ["subscription.renew", undefined],
    ["subscription.change_plan", undefined],
    ["entitlement.override", "Club committee only during the masters league"],
    ["entitlement.override.remove", "Masters league over"],
    ["subscription.cancel", "Club wound up"],
  ] as const) {
    await auditEntries(request, { tenantId, action }, [by(action, reason)]);
  }
});
