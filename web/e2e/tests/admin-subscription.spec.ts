import { expect, test, type APIRequestContext, type Page } from "@playwright/test";
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
  signInByApi,
} from "./support/accounts";
import { phone, shoot, widths } from "./support/screenshots";

// The tenant's side of its subscription (S04 D19): Settings → Subscription, and the shell's banner while the
// subscription is ending, in grace and once the workspace is read-only, on a tenant of this run's own. The platform
// administrator changes the periods through the API; the console's panel has its own spec (admin-platform).

const run = process.env.MSP_E2E_RUN!;
const csrf = { "X-CSRF": "1" };

/** A tenant's date as the admin app shows it: "8 Nov 2026". */
const shown = (day: string) =>
  new Intl.DateTimeFormat("en-GB", { day: "numeric", month: "short", year: "numeric", timeZone: "UTC" }).format(
    new Date(`${day}T00:00:00Z`),
  );

/** Cancels the tenant's subscription, then (unless `period` is null) assigns it a STANDARD period on those days. */
async function replacePeriod(request: APIRequestContext, id: string, period: { startsOn: string; endsOn: string } | null) {
  await signInByApi(request, platformAdmin.email, platformAdmin.password);
  const cancelled = await request.post(`/api/platform/v1/tenants/${id}/subscription/cancel`, {
    data: { reason: "Subscription e2e: next state" },
    headers: csrf,
  });
  expect(cancelled.status(), "cancel subscription").toBe(200);
  if (period) {
    const assigned = await request.post(`/api/platform/v1/tenants/${id}/subscription/periods`, {
      data: { planCode: "STANDARD", kind: "PAID", ...period },
      headers: csrf,
    });
    expect(assigned.status(), "assign period").toBe(201);
  }
}

const banner = (page: Page) => page.getByRole("region", { name: "Subscription" });

test("a tenant's members see its subscription, and the banner while it ends, in grace and read-only", async ({
  page,
  browser,
  request,
}) => {
  const slug = `nsambya-netball-${run}`;
  const name = `Nsambya Community Netball League ${run}`;
  const id = await createTenant(request, slug, name);
  const owner = { email: personEmail("ruth.nakimuli", slug), password: newPassword() };
  await acceptByApi(request, await inviteOwnerOf(request, id, owner.email), owner.password);
  const viewer = { email: personEmail("joseph.ssali", slug), password: newPassword() };
  await acceptByApi(request, await inviteMember(request, { ...owner, tenantId: id }, viewer.email, ["VIEWER"]), viewer.password);

  // Ending in 10 days: inside the 30-day expiring window, full access with a warning.
  const endsOn = kampalaDate(10);
  await replacePeriod(request, id, { startsOn: kampalaDate(-1), endsOn });
  await signIn(page, owner.email, owner.password);
  await expect(page).toHaveURL(`/t/${slug}`);
  await expect(banner(page)).toContainText("The subscription ends soon");
  await expect(banner(page)).toContainText(`It ends on ${shown(endsOn)}.`);

  await page.getByRole("navigation", { name: "Workspace" }).first().getByRole("link", { name: "Subscription" }).click();
  await expect(page).toHaveURL(`/t/${slug}/settings/subscription`);
  await expect(page.getByRole("heading", { level: 2, name: "Standard plan" })).toBeVisible();
  const staff = page.getByRole("listitem").filter({ hasText: "Staff users" });
  await expect(staff).toContainText("Used: 2 of");
  await expect(page.getByRole("listitem").filter({ hasText: "Enabled sports" })).toContainText("Usage: not measured yet");
  await shoot(page, "tenant-subscription", [phone, ...widths]);

  // Ended yesterday: 14 days of grace, still full access.
  await replacePeriod(request, id, { startsOn: kampalaDate(-30), endsOn: kampalaDate(-1) });
  await page.reload();
  await expect(banner(page)).toContainText("The subscription has ended");
  await expect(banner(page)).toContainText(`Full access continues until ${shown(kampalaDate(13))}`);
  await shoot(page, "tenant-banner-grace", [phone, ...widths]);

  // Cancelled: read-only since today.
  await replacePeriod(request, id, null);
  await page.reload();
  await expect(banner(page)).toContainText("This workspace is read-only");
  await expect(banner(page)).toContainText(`Since ${shown(kampalaDate(0))}: the subscription was cancelled.`);
  await expect(page.getByText("The workspace is read-only until the platform team renews the subscription.")).toBeVisible();
  await shoot(page, "tenant-banner-read-only", [phone, ...widths]);

  // Every member sees the banner: a viewer too, on the workspace's overview.
  const viewerContext = await browser.newContext();
  const viewerPage = await viewerContext.newPage();
  await signIn(viewerPage, viewer.email, viewer.password);
  await expect(viewerPage).toHaveURL(`/t/${slug}`);
  await expect(banner(viewerPage)).toContainText("This workspace is read-only");
  await viewerContext.close();
});
