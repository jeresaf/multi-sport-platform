import { expect, test, type Page } from "@playwright/test";
import {
  acceptByApi,
  createTenant,
  inviteMember,
  inviteOwnerOf,
  newPassword,
  personEmail,
  signIn,
} from "./support/accounts";
import { linkFromEmail } from "./support/mailpit";
import { open } from "./support/pages";
import { phone, shoot, widths } from "./support/screenshots";

// Tenant settings through the screens (S03-T6c1): an owner invites a viewer and manages them; the viewer sees the
// settings read-only, and a change they send to the API anyway is refused.

const run = process.env.MSP_E2E_RUN!;

const row = (page: Page, email: string) => page.getByRole("row").filter({ hasText: email });

test("an owner manages members and settings; a viewer only reads them, and the API refuses their change", async ({
  page,
  browser,
  request,
}) => {
  const slug = `jacaranda-games-${run}`;
  const name = `Jacaranda Corporate Games ${run}`;
  const tenantId = await createTenant(request, slug, name);
  const owner = { email: personEmail("sarah.achieng", slug), password: newPassword() };
  await acceptByApi(request, await inviteOwnerOf(request, tenantId, owner.email), owner.password);
  const viewer = { email: personEmail("ruth.atim", slug), password: newPassword() };
  const leaving = personEmail("patrick.opio", slug);

  // The owner changes a setting.
  await signIn(page, owner.email, owner.password);
  await expect(page).toHaveURL(`/t/${slug}`);
  await open(page, `/t/${slug}/settings/organisation`);
  const minutes = page.getByLabel(/Starting soon/);
  await expect(minutes).toHaveValue("30");
  await minutes.fill("45");
  await page.getByRole("button", { name: "Save changes" }).click();
  await expect(page.getByText("Saved.")).toBeVisible();

  // The owner invites a viewer through the screen.
  await open(page, `/t/${slug}/settings/members`);
  await expect(row(page, owner.email)).toContainText("Tenant owner");
  await page.getByRole("button", { name: "Invite someone" }).click();
  const invite = page.getByRole("dialog");
  await invite.getByLabel("Email").fill(viewer.email);
  await invite.getByRole("checkbox", { name: "Viewer" }).click();
  await shoot(page, "invite-dialog", [phone, ...widths]);
  await invite.getByRole("button", { name: "Send invitation" }).click();
  await expect(invite).toBeHidden();
  await expect(row(page, viewer.email)).toContainText("Pending");

  // An invitation sent by mistake is revoked.
  await page.getByRole("button", { name: "Invite someone" }).click();
  await invite.getByLabel("Email").fill(leaving);
  await invite.getByRole("checkbox", { name: "Registrar" }).click();
  await invite.getByRole("button", { name: "Send invitation" }).click();
  await expect(invite).toBeHidden();
  // Sending the email changes the invitation moments after it is created (PROGRESS tech debt): a revoke saved while
  // that send is saved meets a concurrency conflict. Wait, on the list itself, until the email has gone out.
  await expect(async () => {
    await page.reload();
    await expect(row(page, leaving)).toBeVisible();
    await expect(row(page, leaving)).not.toContainText("Not sent yet", { timeout: 1_000 });
  }).toPass({ timeout: 20_000 });
  await page.getByRole("button", { name: `Revoke the invitation to ${leaving}` }).click();
  await page.getByRole("dialog").getByRole("button", { name: "Revoke" }).click();
  await expect(page.getByRole("dialog")).toBeHidden();
  await expect(row(page, leaving)).toHaveCount(0);
  await page.getByLabel("Invitations to show").selectOption("REVOKED");
  await expect(row(page, leaving)).toContainText("Revoked");

  // The viewer accepts and signs in, in a browser of their own.
  await acceptByApi(request, await linkFromEmail(request, viewer.email, "/accept-invitation"), viewer.password);
  const viewerContext = await browser.newContext();
  const viewerPage = await viewerContext.newPage();
  await signIn(viewerPage, viewer.email, viewer.password);
  await expect(viewerPage).toHaveURL(`/t/${slug}`);

  await open(viewerPage, `/t/${slug}/settings/organisation`);
  await expect(viewerPage.getByText("You can see these settings but not change them.")).toBeVisible();
  await expect(viewerPage.getByLabel(/Starting soon/)).toHaveValue("45");
  await expect(viewerPage.getByLabel(/Starting soon/)).toBeDisabled();
  await expect(viewerPage.getByRole("button", { name: "Save changes" })).toHaveCount(0);
  await shoot(viewerPage, "organisation-read-only", widths);

  await open(viewerPage, `/t/${slug}/settings/members`);
  await expect(row(viewerPage, owner.email)).toBeVisible();
  await expect(viewerPage.getByRole("button", { name: /^Change / })).toHaveCount(0);
  await expect(viewerPage.getByRole("button", { name: "Invite someone" })).toHaveCount(0);

  // The server refuses what the screen doesn't offer (the viewer's own cookie).
  const changed = await viewerPage.request.patch("/api/admin/v1/tenant/settings", {
    data: { startingSoonMinutes: 60 },
    headers: { "X-CSRF": "1", "X-Tenant-Id": tenantId },
  });
  expect(changed.status()).toBe(403);
  expect(await changed.json()).toMatchObject({ code: "AUTH_FORBIDDEN" });

  // The owner gives the viewer a second role, then suspends and restores them.
  await open(page, `/t/${slug}/settings/members`);
  await page.getByRole("button", { name: `Change ${viewer.email}` }).click();
  await page.getByRole("menuitem", { name: "Change roles" }).click();
  const roles = page.getByRole("dialog");
  await roles.getByRole("checkbox", { name: "Registrar" }).click();
  await shoot(page, "roles-dialog", widths);
  await roles.getByRole("button", { name: "Save roles" }).click();
  await expect(roles).toBeHidden();
  await expect(row(page, viewer.email)).toContainText("Registrar");

  await page.getByRole("button", { name: `Change ${viewer.email}` }).click();
  await page.getByRole("menuitem", { name: "Suspend" }).click();
  const suspend = page.getByRole("dialog");
  await suspend.getByLabel("Reason").fill("On leave until the new season");
  await shoot(page, "reason-dialog", [phone, ...widths]);
  await suspend.getByRole("button", { name: "Suspend" }).click();
  await expect(suspend).toBeHidden();
  await expect(row(page, viewer.email)).toHaveCount(0);

  await page.getByLabel("Members to show").selectOption("SUSPENDED");
  await expect(row(page, viewer.email)).toContainText("On leave until the new season");
  await page.getByRole("button", { name: `Change ${viewer.email}` }).click();
  await page.getByRole("menuitem", { name: "Restore" }).click();
  await page.getByRole("dialog").getByRole("button", { name: "Restore" }).click();
  await expect(page.getByRole("dialog")).toBeHidden();
  await expect(row(page, viewer.email)).toHaveCount(0);
  await page.getByLabel("Members to show").selectOption("ACTIVE");
  await expect(row(page, viewer.email)).toContainText("Active");
  await shoot(page, "members-owner", [phone, ...widths]);

  await viewerContext.close();
});

test("an administrator who suspends themselves loses the workspace at once", async ({ page, request }) => {
  const slug = `weaver-athletics-${run}`;
  const tenantId = await createTenant(request, slug, `Weaver Bird Athletics Club ${run}`);
  const owner = { email: personEmail("grace.nakato", slug), password: newPassword(), tenantId };
  await acceptByApi(request, await inviteOwnerOf(request, tenantId, owner.email), owner.password);
  const admin = { email: personEmail("daniel.kato", slug), password: newPassword() };
  await acceptByApi(request, await inviteMember(request, owner, admin.email, ["TENANT_ADMIN"]), admin.password);

  await signIn(page, admin.email, admin.password);
  await expect(page).toHaveURL(`/t/${slug}`);
  await open(page, `/t/${slug}/settings/members`);
  await page.getByRole("button", { name: `Change ${admin.email}` }).click();
  await page.getByRole("menuitem", { name: "Suspend" }).click();
  const suspend = page.getByRole("dialog");
  await expect(suspend).toContainText("This is you:");
  await suspend.getByLabel("Reason").fill("Stepping down as administrator");
  await suspend.getByRole("button", { name: "Suspend" }).click();

  // `me` lists active memberships only, so the workspace is no longer theirs: the page says so (#58 re-review Q1).
  await expect(page.getByRole("heading", { level: 1, name: "Page not found" })).toBeVisible();
  await expect(page).toHaveURL(`/t/${slug}/settings/members`);

  // … and the API refuses them, with their own session: no active membership (multi-tenancy.md).
  const settings = await page.request.get("/api/admin/v1/tenant/settings", { headers: { "X-Tenant-Id": tenantId } });
  expect(settings.status()).toBe(403);
  expect(await settings.json()).toMatchObject({ code: "AUTH_TENANT_FORBIDDEN" });
});
