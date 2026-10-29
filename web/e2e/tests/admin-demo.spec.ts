import { expect, test, type Page } from "@playwright/test";
import { auditEntries, myUserId, type AuditEntry } from "./support/audit";
import { kampalaDate, newPassword, personEmail, platformAdmin, signIn, signInByApi } from "./support/accounts";
import { linkFromEmail } from "./support/mailpit";
import { open } from "./support/pages";
import { shoot, widths } from "./support/screenshots";

// The S03 slice demo, through the screens against the real API: a platform administrator creates a tenant and invites
// its owner; the owner joins, changes the settings and invites a viewer; the viewer reads them only, and the API
// refuses their change; the audit trail has every step with the real user.

const run = process.env.MSP_E2E_RUN!;

/** Opens the invitation link in the person's own browser, creates their account and signs them in. */
async function join(page: Page, link: string, email: string, password: string, tenantName: string, slug: string) {
  await page.goto(link);
  await expect(page.getByRole("heading", { level: 1, name: `Join ${tenantName}` })).toBeVisible();
  await page.getByLabel("Choose a password").fill(password);
  await page.getByLabel("Type it again").fill(password);
  await page.getByRole("button", { name: "Create account and accept" }).click();
  await expect(page.getByRole("heading", { level: 1, name: `You've joined ${tenantName}` })).toBeVisible();
  await signIn(page, email, password);
  await expect(page).toHaveURL(`/t/${slug}`);
}

test("slice demo: platform admin → owner → viewer, with the audit trail", async ({ page, browser, request }) => {
  const slug = `mvule-youth-${run}`;
  const name = `Mvule Youth Football League ${run}`;
  const owner = { email: personEmail("grace.nakato", slug), password: newPassword() };
  const viewer = { email: personEmail("esther.nabirye", slug), password: newPassword() };

  // The platform administrator (created with the CLI, start-api.sh) creates the tenant.
  await signIn(page, platformAdmin.email, platformAdmin.password);
  await expect(page).toHaveURL("/platform");
  await open(page, "/platform/tenants");
  await page.getByRole("button", { name: "New tenant" }).click();
  const create = page.getByRole("dialog");
  await create.getByLabel("Slug").fill(slug);
  await create.getByLabel("Name", { exact: true }).fill(name);
  await create.getByLabel("Legal name").fill(`${name} Limited`);
  await create.getByLabel("Country").selectOption("UG");
  await create.getByLabel("Time zone").selectOption("Africa/Kampala");
  await create.getByLabel("Language").fill("en-UG");
  await create.getByLabel("Currency").selectOption("UGX");
  await create.getByRole("button", { name: "Create tenant" }).click();
  await expect(page.getByRole("heading", { level: 1, name })).toBeVisible();
  await expect(page).toHaveURL(/\/platform\/tenants\/[0-9a-f-]{36}$/);
  const tenantId = page.url().split("/").at(-1)!;

  // … activates it (without a subscription it would be read-only, S04 D9: the dialog says so), assigns it a period
  // (from yesterday, so it has started even if Kampala's date turns meanwhile, #66 R7) and invites its owner.
  await page.getByRole("button", { name: "Activate" }).click();
  await expect(page.getByRole("dialog")).toContainText("It has no subscription");
  await page.getByRole("dialog").getByRole("button", { name: "Activate" }).click();
  await expect(page.getByRole("status").filter({ hasText: "Activated." })).toBeAttached();
  await page.getByRole("button", { name: "Assign a period" }).click();
  const period = page.getByRole("dialog");
  await period.getByLabel("Plan").selectOption("STANDARD");
  await period.getByLabel("First day").fill(kampalaDate(-1));
  await period.getByLabel("Last day").fill(kampalaDate(179));
  await period.getByRole("button", { name: "Assign period" }).click();
  await expect(period).toBeHidden();
  await expect(page.getByRole("heading", { name: "Current period" })).toBeVisible();
  await page.getByRole("button", { name: "Invite an owner" }).click();
  await page.getByRole("dialog").getByLabel("Email").fill(owner.email);
  await page.getByRole("dialog").getByRole("button", { name: "Send invitation" }).click();
  await expect(page.getByText(`Invited ${owner.email}.`)).toBeVisible();
  await shoot(page, "demo-tenant-invited", widths);

  // The owner accepts from the email (Mailpit), in a browser of their own, and changes a setting.
  const ownerContext = await browser.newContext();
  const ownerPage = await ownerContext.newPage();
  const ownerLink = await linkFromEmail(request, owner.email, "/accept-invitation");
  await join(ownerPage, ownerLink, owner.email, owner.password, name, slug);
  await expect(ownerPage.getByText("Your roles: Tenant owner")).toBeVisible();
  await open(ownerPage, `/t/${slug}/settings/organisation`);
  await ownerPage.getByLabel(/Starting soon/).fill("45");
  await ownerPage.getByRole("button", { name: "Save changes" }).click();
  await expect(ownerPage.getByText("Saved.")).toBeVisible();

  // … and invites a viewer.
  await open(ownerPage, `/t/${slug}/settings/members`);
  await ownerPage.getByRole("button", { name: "Invite someone" }).click();
  const invite = ownerPage.getByRole("dialog");
  await invite.getByLabel("Email").fill(viewer.email);
  await invite.getByRole("checkbox", { name: "Viewer" }).click();
  await invite.getByRole("button", { name: "Send invitation" }).click();
  await expect(invite).toBeHidden();
  await expect(ownerPage.getByRole("row").filter({ hasText: viewer.email })).toContainText("Pending");

  // The viewer accepts and sees the settings read-only.
  const viewerContext = await browser.newContext();
  const viewerPage = await viewerContext.newPage();
  const viewerLink = await linkFromEmail(request, viewer.email, "/accept-invitation");
  await join(viewerPage, viewerLink, viewer.email, viewer.password, name, slug);
  await open(viewerPage, `/t/${slug}/settings/organisation`);
  await expect(viewerPage.getByText("You can see these settings but not change them.")).toBeVisible();
  await expect(viewerPage.getByLabel(/Starting soon/)).toHaveValue("45");
  await expect(viewerPage.getByLabel(/Starting soon/)).toBeDisabled();
  await expect(viewerPage.getByRole("button", { name: "Save changes" })).toHaveCount(0);
  await shoot(viewerPage, "demo-viewer-settings", widths);

  // A change the viewer sends anyway, with their own session, is refused.
  const refused = await viewerPage.request.patch("/api/admin/v1/tenant/settings", {
    data: { startingSoonMinutes: 60 },
    headers: { "X-CSRF": "1", "X-Tenant-Id": tenantId },
  });
  expect(refused.status()).toBe(403);
  expect(await refused.json()).toMatchObject({ code: "AUTH_FORBIDDEN" });

  // The audit trail, read by the platform administrator: every step with the real user.
  const adminId = await myUserId(page.request);
  const ownerId = await myUserId(ownerPage.request);
  const viewerId = await myUserId(viewerPage.request);
  await signInByApi(request, platformAdmin.email, platformAdmin.password);

  const by = (actionCode: string, actorId: string) => (entry: AuditEntry) =>
    entry.actionCode === actionCode &&
    entry.outcome === "SUCCESS" &&
    entry.actorKind === "USER" &&
    entry.actorId === actorId;
  // A new account accepts before it exists: the anonymous caller accepts, and the entry's metadata names the user.
  const accepted = (userId: string) => (entry: AuditEntry) =>
    entry.actionCode === "invitation.accept" &&
    entry.outcome === "SUCCESS" &&
    (entry.metadata as { userId?: string; accountCreated?: boolean } | null)?.userId === userId &&
    (entry.metadata as { accountCreated?: boolean }).accountCreated === true;
  // One read per action: the tenant's own entries (every audited read in it, and these reads too) would crowd
  // a single page.
  const steps: [string, (entry: AuditEntry) => boolean][] = [
    ["tenant.create", by("tenant.create", adminId)],
    ["tenant.activate", by("tenant.activate", adminId)],
    ["subscription.assign", by("subscription.assign", adminId)],
    ["tenant.owner.invite", by("tenant.owner.invite", adminId)],
    ["invitation.accept", accepted(ownerId)],
    ["tenant.settings.update", by("tenant.settings.update", ownerId)],
    ["membership.invite", by("membership.invite", ownerId)],
    ["invitation.accept", accepted(viewerId)],
  ];
  for (const [action, expected] of steps) {
    await auditEntries(request, { tenantId, action }, [expected]);
  }

  await auditEntries(request, { tenantId, actorId: viewerId, action: "tenant.settings.update" }, [
    (entry) => entry.outcome === "DENIED" && entry.errorCode === "AUTH_FORBIDDEN",
  ]);

  // A sign-in is audited before there is a session: actor ANONYMOUS, the user as its entity (#43 N1).
  for (const userId of [ownerId, viewerId]) {
    await auditEntries(request, { action: "account.login", entityType: "User", entityId: userId }, [
      (entry) => entry.outcome === "SUCCESS" && entry.actorKind === "ANONYMOUS",
    ]);
  }

  await ownerContext.close();
  await viewerContext.close();
});
