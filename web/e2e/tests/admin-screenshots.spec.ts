import { expect, test, type Page } from "@playwright/test";
import { inviteOwner, personEmail, platformAdmin, signIn } from "./support/accounts";

// Screenshots for the PR (S03-T6: tablet and desktop, light and dark). CI uploads e2e/screenshots as an artifact.
const widths = [
  { name: "tablet", width: 820, height: 1180 },
  { name: "desktop", width: 1280, height: 800 },
] as const;

async function shoot(page: Page, name: string) {
  for (const size of widths) {
    await page.setViewportSize({ width: size.width, height: size.height });
    for (const colorScheme of ["light", "dark"] as const) {
      await page.emulateMedia({ colorScheme });
      await page.screenshot({ path: `screenshots/admin-${name}-${size.name}-${colorScheme}.png`, fullPage: true });
    }
  }
}

test("screenshots of the signed-out pages", async ({ page, request }) => {
  await page.goto("/sign-in");
  await page.getByRole("button", { name: "Sign in" }).click();
  await expect(page.getByText("Enter your email address.")).toBeVisible();
  await shoot(page, "sign-in-errors");

  await page.goto("/forgot-password");
  await expect(page.getByRole("heading", { level: 1, name: "Forgot your password?" })).toBeVisible();
  await shoot(page, "forgot-password");

  await page.goto(await inviteOwner(request, "elssc", personEmail("daniel.kato", "elssc")));
  await expect(page.getByLabel("Choose a password")).toBeVisible();
  await shoot(page, "accept-invitation");
});

test("screenshots of the signed-in home", async ({ page }) => {
  await signIn(page, platformAdmin.email, platformAdmin.password);
  await expect(page.getByText(`Signed in as ${platformAdmin.email}`)).toBeVisible();
  await shoot(page, "home");
});
