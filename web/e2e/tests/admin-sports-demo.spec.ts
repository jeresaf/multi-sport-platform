import { expect, test, type Page } from "@playwright/test";
import { auditEntries, myUserId, type AuditEntry } from "./support/audit";
import { acceptByApi, createTenant, inviteOwnerOf, newPassword, personEmail, platformAdmin, signIn } from "./support/accounts";
import { open } from "./support/pages";
import { phone, shoot, widths } from "./support/screenshots";

// The S05 slice demo's sport enablement, through the screens against the real API, on a tenant of this run's own
// (parallel specs share KMSA and ELSSC, which stay the manual demo): the owner enables a sport ("1 of 3 sports"),
// disables it with a reason; the platform administrator sets MAX_ENABLED_SPORTS = 0 with a reason, so enabling it again
// is refused by the screen and by the API with "Your plan allows 0 sports"; the override goes and the enable goes
// through. The audit trail keeps both enablements, the disable's reason, the override's and the refusal. The variant is
// read from the list: nothing here names a sport.

const run = process.env.MSP_E2E_RUN!;
const csrf = { "X-CSRF": "1" };

/** Sends the console dialog's command and waits for the panel to announce it. */
async function send(page: Page, submit: string, announced: string) {
  const dialog = page.getByRole("dialog");
  await dialog.getByRole("button", { name: submit, exact: true }).click();
  await expect(dialog).toBeHidden();
  await expect(page.getByRole("status").filter({ hasText: announced })).toBeAttached();
}

test("slice demo: enable a sport within the plan, refused at an override of 0, enabled again", async ({
  page,
  browser,
  request,
}) => {
  // Two signed-in people, two console changes and the audit trail's polls.
  test.slow();
  const slug = `shoebill-sports-${run}`;
  // Created and activated on STANDARD from yesterday (support/accounts.ts).
  const tenantId = await createTenant(request, slug, `Shoebill Community Sports Club ${run}`);
  const owner = { email: personEmail("agnes.nambi", slug), password: newPassword() };
  await acceptByApi(request, await inviteOwnerOf(request, tenantId, owner.email), owner.password);

  // The owner opens Settings → Enabled sports from the navigation and enables the first variant.
  const ownerContext = await browser.newContext();
  const ownerPage = await ownerContext.newPage();
  await signIn(ownerPage, owner.email, owner.password);
  await expect(ownerPage).toHaveURL(`/t/${slug}`);
  await ownerPage.getByRole("navigation").getByRole("link", { name: "Enabled sports" }).first().click();
  await expect(ownerPage.getByRole("heading", { level: 1, name: "Enabled sports" })).toBeVisible();
  await expect(ownerPage.getByText("Used: 0 of 3 sports")).toBeVisible();

  const listed = await ownerContext.request.get("/api/admin/v1/tenant/sports", { headers: { "X-Tenant-Id": tenantId } });
  expect(listed.status()).toBe(200);
  const [first] = ((await listed.json()) as { variants: { variant: string; name: string }[] }).variants;
  expect(first, "a published sport variant").toBeDefined();
  const { variant, name } = first!;

  await ownerPage.getByRole("button", { name: `Enable ${name}` }).click();
  await expect(ownerPage.getByRole("status").filter({ hasText: `${name} is enabled.` })).toBeAttached();
  await expect(ownerPage.getByText("Used: 1 of 3 sports")).toBeVisible();
  await expect(ownerPage.getByRole("button", { name: `Disable ${name}` })).toBeVisible();
  await shoot(ownerPage, "tenant-enabled-sports", [phone, ...widths]);

  // The owner disables it with a reason.
  await ownerPage.getByRole("button", { name: `Disable ${name}` }).click();
  const disableDialog = ownerPage.getByRole("dialog");
  await disableDialog.getByLabel("Reason").fill("The season starts after the school holidays");
  await disableDialog.getByRole("button", { name: "Disable", exact: true }).click();
  await expect(disableDialog).toBeHidden();
  await expect(ownerPage.getByRole("status").filter({ hasText: `${name} is disabled.` })).toBeAttached();
  await expect(ownerPage.getByText("Used: 0 of 3 sports")).toBeVisible();

  // The platform administrator sets the override MAX_ENABLED_SPORTS = 0.
  await signIn(page, platformAdmin.email, platformAdmin.password);
  await expect(page).toHaveURL("/platform");
  await open(page, `/platform/tenants/${tenantId}`);
  const panel = page.locator("section", { has: page.getByRole("heading", { name: "Subscription", exact: true }) });
  await panel.getByRole("button", { name: "Set an override" }).click();
  const override = page.getByRole("dialog");
  await override.getByLabel("Entitlement").selectOption("MAX_ENABLED_SPORTS");
  await override.getByLabel("Limit (sports)").fill("0");
  await override.getByLabel("Reason").fill("No sports until the club's registration is complete");
  await send(page, "Set override", "Override set.");

  // The owner's Enable is disabled with the plan's limit, and the API refuses it with the limit too.
  await ownerPage.reload();
  await expect(ownerPage.getByText("Used: 0 of 0 sports")).toBeVisible();
  await expect(ownerPage.getByRole("button", { name: `Enable ${name}` })).toBeDisabled();
  await expect(ownerPage.getByText("Your plan allows 0 sports.")).toBeVisible();
  await shoot(ownerPage, "tenant-enabled-sports-at-limit", [phone, ...widths]);

  const refused = await ownerContext.request.post(`/api/admin/v1/tenant/sports/${encodeURIComponent(variant)}/enable`, {
    headers: { ...csrf, "X-Tenant-Id": tenantId },
  });
  expect(refused.status()).toBe(403);
  expect(await refused.json()).toMatchObject({
    code: "ENTITLEMENT_QUOTA_EXCEEDED",
    detail: "Your plan allows 0 sports.",
  });

  // The administrator removes the override (STANDARD's 3 again): the enable goes through.
  await page.reload();
  await panel
    .getByRole("listitem")
    .filter({ hasText: "No sports until the club's registration is complete" })
    .getByRole("button", { name: "Remove" })
    .click();
  await page.getByRole("dialog").getByLabel("Reason").fill("Registration complete");
  await send(page, "Remove override", "Override removed.");

  await ownerPage.reload();
  await ownerPage.getByRole("button", { name: `Enable ${name}` }).click();
  await expect(ownerPage.getByRole("status").filter({ hasText: `${name} is enabled.` })).toBeAttached();
  await expect(ownerPage.getByText("Used: 1 of 3 sports")).toBeVisible();
  const ownerId = await myUserId(ownerContext.request);
  await ownerContext.close();

  // The audit trail, read as the administrator: two enablements and the disable with its reason (each its own row in
  // tenancy.tenant_sports, as the integration tests read), the refusal, and the override's changes with their reasons.
  const audit = page.request;
  const adminId = await myUserId(audit);
  const success = (actionCode: string, actorId: string, reason?: string) => (entry: AuditEntry) =>
    entry.actionCode === actionCode &&
    entry.outcome === "SUCCESS" &&
    entry.actorId === actorId &&
    (reason === undefined || entry.reason === reason);
  await auditEntries(audit, { tenantId, action: "tenant.sport.enable" }, [
    (entry) =>
      entry.outcome === "DENIED" && entry.errorCode === "ENTITLEMENT_QUOTA_EXCEEDED" && entry.actorId === ownerId,
  ]);
  // Enabling again adds an enablement: two successes, each on its own row.
  const enablements = async () =>
    (await auditEntries(audit, { tenantId, action: "tenant.sport.enable" }, [])).filter(
      success("tenant.sport.enable", ownerId),
    );
  await expect
    .poll(async () => new Set((await enablements()).map((entry) => entry.entityId)).size, {
      message: "tenant.sport.enable successes on different enablements",
      intervals: [2_000, 3_000],
      timeout: 30_000,
    })
    .toBe(2);
  await auditEntries(audit, { tenantId, action: "tenant.sport.disable" }, [
    success("tenant.sport.disable", ownerId, "The season starts after the school holidays"),
  ]);
  for (const [action, reason] of [
    ["entitlement.override", "No sports until the club's registration is complete"],
    ["entitlement.override.remove", "Registration complete"],
  ] as const) {
    await auditEntries(audit, { tenantId, action }, [success(action, adminId, reason)]);
  }
});
