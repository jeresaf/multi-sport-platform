import { expect, test } from "@playwright/test";

test("public home shows the coming-soon page", async ({ page }) => {
  await page.goto("/");

  await expect(page).toHaveTitle("MSP");
  await expect(page.getByRole("heading", { level: 1, name: "Coming soon" })).toBeVisible();
});
