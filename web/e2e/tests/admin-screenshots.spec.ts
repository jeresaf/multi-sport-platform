import { expect, test, type Page } from "@playwright/test";
import { inviteOwner, personEmail, platformAdmin, signIn } from "./support/accounts";

// Screenshots for the PR (S03-T6: tablet and desktop, light and dark). CI uploads e2e/screenshots as an artifact.
// The signed-out pages are also shot at phone width: their links come by email and are usually opened on a phone
// (#56 review, owner). The signed-in pages get phone width with the shell (T6b2).
const phone = { name: "phone", width: 375, height: 812 } as const;
const widths = [
  { name: "tablet", width: 820, height: 1180 },
  { name: "desktop", width: 1280, height: 800 },
] as const;

async function shoot(page: Page, name: string, sizes: readonly { name: string; width: number; height: number }[] = widths) {
  for (const size of sizes) {
    await page.setViewportSize({ width: size.width, height: size.height });
    for (const colorScheme of ["light", "dark"] as const) {
      await page.emulateMedia({ colorScheme });
      // `animations: "disabled"` ends colour transitions at once: switching the scheme animates every shadcn control.
      await page.screenshot({
        path: `screenshots/admin-${name}-${size.name}-${colorScheme}.png`,
        fullPage: true,
        animations: "disabled",
      });
    }
  }
}

test("screenshots of the signed-out pages", async ({ page, request }) => {
  await page.goto("/sign-in");
  await page.getByRole("button", { name: "Sign in" }).click();
  await expect(page.getByText("Enter your email address.")).toBeVisible();
  await shoot(page, "sign-in-errors", [phone, ...widths]);

  await page.goto("/forgot-password");
  await expect(page.getByRole("heading", { level: 1, name: "Forgot your password?" })).toBeVisible();
  await shoot(page, "forgot-password", [phone, ...widths]);

  await page.goto(await inviteOwner(request, "elssc", personEmail("daniel.kato", "elssc")));
  await expect(page.getByLabel("Choose a password")).toBeVisible();
  await shoot(page, "accept-invitation", [phone, ...widths]);
});

test("screenshots of the signed-in home", async ({ page }) => {
  await signIn(page, platformAdmin.email, platformAdmin.password);
  await expect(page.getByText(`Signed in as ${platformAdmin.email}`)).toBeVisible();
  await shoot(page, "home");
});
