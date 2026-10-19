import { expect, test, type Page } from "@playwright/test";

// The DEMO tenants (backend seeder `tenancy.demo.tenants`), visited by their platform subdomains.
const tenants = [
  { slug: "kmsa", name: "Kampala Metro Sports Association" },
  { slug: "elssc", name: "Eastern Lakes Schools Sports Council" },
] as const;

const siteUrl = (host: string) => `http://${host}.localhost:3000/`;

async function expectTenantHome(page: Page, name: string) {
  await expect(page).toHaveTitle(name);
  await expect(page.getByRole("banner")).toHaveText(name);
  await expect(page.getByRole("heading", { level: 1, name: "Coming soon" })).toBeVisible();
  await expect(page.getByRole("contentinfo")).toHaveText(`${name} · Runs on MSP`);
}

for (const tenant of tenants) {
  test(`${tenant.slug}.localhost shows ${tenant.name}`, async ({ page }) => {
    const response = await page.goto(siteUrl(tenant.slug));

    expect(response?.status()).toBe(200);
    await expectTenantHome(page, tenant.name);
  });
}

test("an unknown host shows the not-found page", async ({ page }) => {
  const response = await page.goto(siteUrl("no-such-site"));

  expect(response?.status()).toBe(404);
  await expect(page).toHaveTitle("Page not found");
  await expect(page.getByRole("heading", { level: 1, name: "We can't find this page" })).toBeVisible();
  await expect(page.getByText("Kampala Metro Sports Association")).toHaveCount(0);
});

// Screenshots for the PR (web.md: phone, tablet, desktop). CI uploads e2e/screenshots as an artifact.
const widths = [
  { name: "phone", width: 375, height: 812 },
  { name: "tablet", width: 820, height: 1180 },
  { name: "desktop", width: 1280, height: 800 },
] as const;

for (const size of widths) {
  test(`screenshots at ${size.name} width`, async ({ page }) => {
    await page.setViewportSize({ width: size.width, height: size.height });

    for (const tenant of tenants) {
      await page.goto(siteUrl(tenant.slug));
      await expectTenantHome(page, tenant.name);
      await page.screenshot({ path: `screenshots/${tenant.slug}-${size.name}.png`, fullPage: true });
    }

    await page.goto(siteUrl("no-such-site"));
    await expect(page.getByRole("heading", { level: 1, name: "We can't find this page" })).toBeVisible();
    await page.screenshot({ path: `screenshots/not-found-${size.name}.png`, fullPage: true });
  });
}
