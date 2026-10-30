import { expect, test, type Page } from "@playwright/test";
import { auditEntries, myUserId, type AuditEntry } from "./support/audit";
import {
  acceptByApi,
  createTenant,
  inviteMember,
  inviteOwnerOf,
  kampalaDate,
  newPassword,
  personEmail,
  platformAdmin,
  signIn,
} from "./support/accounts";
import { open } from "./support/pages";
import { phone, shoot, widths } from "./support/screenshots";

// The S04 slice demo, through the screens against the real API, on a tenant of this run's own (parallel specs share
// KMSA and ELSSC, which stay the manual demo): the platform administrator reads the plans and limits the tenant's staff
// users by override; its owner sees the usage, takes the last seat and is refused the next one, by the screen and by
// the API; the override goes and the invitation goes through. The administrator cancels the subscription: every
// member sees the read-only banner, the owner no enabled write control, and the API refuses a change; a new period
// brings full access back. The audit trail has every platform change with its reason, and both refusals.

const run = process.env.MSP_E2E_RUN!;
const csrf = { "X-CSRF": "1" };

/** Sends the console dialog's command and waits for the panel to announce it. */
async function send(page: Page, submit: string, announced: string) {
  const dialog = page.getByRole("dialog");
  await dialog.getByRole("button", { name: submit, exact: true }).click();
  await expect(dialog).toBeHidden();
  await expect(page.getByRole("status").filter({ hasText: announced })).toBeAttached();
}

/** Invites `email` as a viewer from the members page's dialog. */
async function inviteViewer(page: Page, email: string) {
  await page.getByRole("button", { name: "Invite someone" }).click();
  const dialog = page.getByRole("dialog");
  await dialog.getByLabel("Email").fill(email);
  await dialog.getByLabel("Viewer").check();
  await dialog.getByRole("button", { name: "Send invitation" }).click();
  await expect(dialog).toBeHidden();
  await expect(page.getByRole("cell", { name: email, exact: true })).toBeVisible();
}

const banner = (page: Page) => page.getByRole("region", { name: "Subscription" });
/** The page's buttons that can be pressed, the shell's menus aside. */
const enabledButtons = (page: Page) => page.getByRole("main").locator("button:enabled");

test("slice demo: plans, the staff quota by override, read-only and back, with the audit trail", async ({
  page,
  browser,
  request,
}) => {
  // Five signed-in people, three console changes and the audit trail's polls.
  test.slow();
  const slug = `hornbill-volleyball-${run}`;
  const name = `Hornbill Volleyball Club ${run}`;
  // Created and activated on STANDARD from yesterday (support/accounts.ts).
  const tenantId = await createTenant(request, slug, name);
  const owner = { email: personEmail("sarah.achieng", slug), password: newPassword() };
  await acceptByApi(request, await inviteOwnerOf(request, tenantId, owner.email), owner.password);
  const inviter = { ...owner, tenantId };
  const viewer = { email: personEmail("isaac.mugisha", slug), password: newPassword() };
  await acceptByApi(request, await inviteMember(request, inviter, viewer.email, ["VIEWER"]), viewer.password);
  // Two invitations still pending: each takes a seat (S04 D13), so the tenant has 4 staff users.
  await inviteMember(request, inviter, personEmail("brenda.akello", slug), ["VIEWER"]);
  await inviteMember(request, inviter, personEmail("julius.wasswa", slug), ["VIEWER"]);

  // The platform administrator compares the plans, then limits the tenant to one more staff user than it has.
  await signIn(page, platformAdmin.email, platformAdmin.password);
  await expect(page).toHaveURL("/platform");
  await open(page, "/platform/plans");
  await expect(page.getByRole("heading", { level: 1, name: "Plans" })).toBeVisible();
  await expect(page.getByRole("columnheader", { name: /Standard/ })).toBeVisible();
  await expect(page.getByRole("columnheader", { name: /Pro/ })).toBeVisible();

  await open(page, `/platform/tenants/${tenantId}`);
  const panel = page.locator("section", { has: page.getByRole("heading", { name: "Subscription", exact: true }) });
  await panel.getByRole("button", { name: "Set an override" }).click();
  const override = page.getByRole("dialog");
  await override.getByLabel("Entitlement").selectOption("MAX_STAFF_USERS");
  await override.getByLabel("Limit (users)").fill("5");
  await override.getByLabel("Reason").fill("Committee and coaches only until the league starts");
  await send(page, "Set override", "Override set.");

  // The owner sees 4 of 5 with the warning, takes the last seat, and inviting is then disabled with why.
  const ownerContext = await browser.newContext();
  const ownerPage = await ownerContext.newPage();
  await signIn(ownerPage, owner.email, owner.password);
  await expect(ownerPage).toHaveURL(`/t/${slug}`);
  await open(ownerPage, `/t/${slug}/settings/subscription`);
  const staff = ownerPage.getByRole("listitem").filter({ hasText: "Staff users" });
  await expect(staff).toContainText("Used: 4 of 5 users");
  await expect(staff).toContainText("Near the limit");

  await open(ownerPage, `/t/${slug}/settings/members`);
  await ownerPage.getByRole("button", { name: "Invite someone" }).click();
  await expect(ownerPage.getByRole("dialog")).toContainText("Used: 4 of 5 staff users");
  await ownerPage.getByRole("dialog").getByRole("button", { name: "Cancel" }).click();
  await inviteViewer(ownerPage, personEmail("ruth.namirembe", slug));
  await expect(ownerPage.getByRole("button", { name: "Invite someone" })).toBeDisabled();
  await expect(ownerPage.getByText("Your plan allows 5 staff users.")).toBeVisible();
  await shoot(ownerPage, "tenant-invite-at-limit", [phone, ...widths]);

  // The API refuses it too, with the limit.
  const refused = await ownerContext.request.post("/api/admin/v1/invitations", {
    data: { email: personEmail("moses.ouma", slug), roleCodes: ["VIEWER"] },
    headers: { ...csrf, "X-Tenant-Id": tenantId },
  });
  expect(refused.status()).toBe(403);
  expect(await refused.json()).toMatchObject({
    code: "ENTITLEMENT_QUOTA_EXCEEDED",
    detail: "Your plan allows 5 staff users.",
  });

  // The administrator removes the override (STANDARD's limit again): the invitation goes through.
  await page.reload();
  const granted = panel
    .getByRole("listitem")
    .filter({ hasText: "Committee and coaches only until the league starts" });
  await granted.getByRole("button", { name: "Remove" }).click();
  await page.getByRole("dialog").getByLabel("Reason").fill("League fixtures confirmed");
  await send(page, "Remove override", "Override removed.");

  await ownerPage.reload();
  await expect(ownerPage.getByRole("button", { name: "Invite someone" })).toBeEnabled();
  await inviteViewer(ownerPage, personEmail("moses.ouma", slug));

  // The administrator cancels the subscription: read-only.
  await panel.getByRole("button", { name: "Cancel subscription" }).click();
  await page.getByRole("dialog").getByLabel("Reason").fill("Season fee not paid");
  await send(page, "Cancel subscription", "Subscription cancelled.");

  await ownerPage.reload();
  await expect(banner(ownerPage)).toContainText("This workspace is read-only");
  await expect(ownerPage.getByRole("button", { name: "Invite someone" })).toBeDisabled();
  await expect(ownerPage.getByRole("button", { name: `Change ${viewer.email}` })).toBeDisabled();
  await expect(ownerPage.getByText("Read-only: the subscription was cancelled").first()).toBeVisible();
  await expect(enabledButtons(ownerPage)).toHaveCount(0);
  await shoot(ownerPage, "tenant-members-read-only", [phone, ...widths]);

  await open(ownerPage, `/t/${slug}/settings/organisation`);
  await expect(ownerPage.getByRole("group", { name: "Organisation settings" })).toHaveAttribute("disabled");
  await expect(ownerPage.getByRole("button", { name: "Save changes" })).toBeDisabled();
  await expect(ownerPage.getByText("Read-only: the subscription was cancelled")).toBeVisible();
  await expect(enabledButtons(ownerPage)).toHaveCount(0);
  await shoot(ownerPage, "tenant-organisation-read-only", [phone, ...widths]);

  // The API refuses a change.
  const patched = await ownerContext.request.patch("/api/admin/v1/tenant/settings", {
    data: { startingSoonMinutes: 45 },
    headers: { ...csrf, "X-Tenant-Id": tenantId },
  });
  expect(patched.status()).toBe(403);
  expect(((await patched.json()) as { code: string }).code).toBe("TENANT_READ_ONLY");

  // Every member sees the banner.
  const viewerContext = await browser.newContext();
  const viewerPage = await viewerContext.newPage();
  await signIn(viewerPage, viewer.email, viewer.password);
  await expect(viewerPage).toHaveURL(`/t/${slug}`);
  await expect(banner(viewerPage)).toContainText("This workspace is read-only");
  await viewerContext.close();

  // A new period: full access returns.
  await panel.getByRole("button", { name: "Assign a period" }).click();
  await page.getByRole("dialog").getByLabel("Last day").fill(kampalaDate(179));
  await send(page, "Assign period", "Period assigned.");

  await ownerPage.reload();
  await expect(ownerPage.getByRole("group", { name: "Organisation settings" })).not.toHaveAttribute("disabled");
  await expect(banner(ownerPage)).toHaveCount(0);
  await expect(ownerPage.getByText("Read-only: the subscription was cancelled")).toHaveCount(0);
  const ownerId = await myUserId(ownerContext.request);
  await ownerContext.close();

  // The audit trail, read as the administrator (`request` was last signed in as the owner): every platform change with
  // its reason, and the owner's two refusals.
  const audit = page.request;
  const adminId = await myUserId(audit);
  const by = (actionCode: string, reason?: string) => (entry: AuditEntry) =>
    entry.actionCode === actionCode &&
    entry.outcome === "SUCCESS" &&
    entry.actorId === adminId &&
    (reason === undefined || entry.reason === reason);
  for (const [action, reason] of [
    ["entitlement.override", "Committee and coaches only until the league starts"],
    ["entitlement.override.remove", "League fixtures confirmed"],
    ["subscription.cancel", "Season fee not paid"],
    ["subscription.assign", undefined],
  ] as const) {
    await auditEntries(audit, { tenantId, action }, [by(action, reason)]);
  }
  const denied = (actionCode: string, errorCode: string) => (entry: AuditEntry) =>
    entry.actionCode === actionCode &&
    entry.outcome === "DENIED" &&
    entry.errorCode === errorCode &&
    entry.actorId === ownerId;
  await auditEntries(audit, { tenantId, actorId: ownerId, action: "membership.invite" }, [
    denied("membership.invite", "ENTITLEMENT_QUOTA_EXCEEDED"),
  ]);
  await auditEntries(audit, { tenantId, actorId: ownerId, action: "tenant.settings.update" }, [
    denied("tenant.settings.update", "TENANT_READ_ONLY"),
  ]);
});
