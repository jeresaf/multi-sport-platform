import { expect, test, type Page } from "@playwright/test";
import {
  acceptByApi,
  createTenant,
  inviteOwner,
  inviteOwnerOf,
  newPassword,
  personEmail,
  platformAdmin,
  signIn,
  suspendTenant,
} from "./support/accounts";
import { open } from "./support/pages";

// Screenshots for the PR (S03-T6: tablet and desktop, light and dark). CI uploads e2e/screenshots as an artifact.
// The signed-out pages are also shot at phone width: their links come by email and are usually opened on a phone
// (#56 review, owner); so is the shell (web.md: phone, tablet and desktop).
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
  await open(page, "/sign-in");
  await page.getByRole("button", { name: "Sign in" }).click();
  await expect(page.getByText("Enter your email address.")).toBeVisible();
  await shoot(page, "sign-in-errors", [phone, ...widths]);

  await open(page, "/forgot-password");
  await expect(page.getByRole("heading", { level: 1, name: "Forgot your password?" })).toBeVisible();
  await shoot(page, "forgot-password", [phone, ...widths]);

  await page.goto(await inviteOwner(request, "elssc", personEmail("daniel.kato", "elssc")));
  await expect(page.getByLabel("Choose a password")).toBeVisible();
  await shoot(page, "accept-invitation", [phone, ...widths]);
});

test("screenshots of the platform console", async ({ page }) => {
  await signIn(page, platformAdmin.email, platformAdmin.password);
  await expect(page.getByRole("heading", { level: 1, name: "Platform console" })).toBeVisible();
  await shoot(page, "platform-console", [phone, ...widths]);

  await page.getByRole("button", { name: /^Account menu: / }).click();
  await expect(page.getByRole("menuitem", { name: "Sign out" })).toBeVisible();
  await shoot(page, "user-menu", widths);
});

test("screenshots of a tenant workspace", async ({ page, request }) => {
  const email = personEmail("grace.nakato", "kmsa");
  const password = newPassword();
  await acceptByApi(request, await inviteOwner(request, "kmsa", email), password);
  await signIn(page, email, password);
  await expect(page.getByRole("heading", { level: 1, name: "Kampala Metro Sports Association" })).toBeVisible();
  await shoot(page, "tenant-workspace", [phone, ...widths]);

  await page.getByRole("button", { name: "Workspace: Kampala Metro Sports Association" }).click();
  await expect(page.getByRole("menu")).toBeVisible();
  await shoot(page, "workspace-switcher", widths);
  await page.keyboard.press("Escape");

  await page.setViewportSize(phone);
  await page.getByRole("button", { name: "Open navigation" }).click();
  await expect(page.getByRole("dialog")).toBeVisible();
  await shoot(page, "navigation-sheet", [phone]);
  await page.keyboard.press("Escape");

  await page.goto("/t/kmsa/settings/organisation");
  await expect(page.getByText("This screen isn't available yet.")).toBeVisible();
  await shoot(page, "not-available", widths);

  await page.goto("/platform");
  await expect(page.getByRole("heading", { level: 1, name: "You can't open this page" })).toBeVisible();
  await shoot(page, "forbidden", [phone, ...widths]);

  await page.goto("/t/no-such-tenant");
  await expect(page.getByRole("heading", { level: 1, name: "Page not found" })).toBeVisible();
  await shoot(page, "not-found", [phone, ...widths]);
});

test("screenshots of a suspended tenant", async ({ page, request }) => {
  const run = process.env.MSP_E2E_RUN!;
  const slug = `acacia-schools-${run}`;
  const id = await createTenant(request, slug, `Acacia Schools Netball Circle ${run}`);
  const email = personEmail("harriet.akello", slug);
  const password = newPassword();
  await acceptByApi(request, await inviteOwnerOf(request, id, email), password);
  await suspendTenant(request, id, "Subscription unpaid since September");

  await signIn(page, email, password);
  await expect(page.getByText("Subscription unpaid since September")).toBeVisible();
  await shoot(page, "tenant-suspended", [phone, ...widths]);
});
