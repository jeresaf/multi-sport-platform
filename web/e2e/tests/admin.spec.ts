import { expect, test } from "@playwright/test";

test("admin home shows the app name", async ({ page }) => {
  await page.goto("/");

  await expect(page).toHaveTitle("MSP Admin");
  await expect(page.getByRole("heading", { level: 1, name: "MSP Admin" })).toBeVisible();
});
