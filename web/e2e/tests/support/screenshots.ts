// Screenshots for the PR (S03-T6: tablet and desktop, light and dark). CI uploads e2e/screenshots as an artifact.
// Pages opened from an email, and the shell, are also shot at phone width (#56 review, owner; web.md).
import type { Page } from "@playwright/test";

export type Size = { name: string; width: number; height: number };

export const phone: Size = { name: "phone", width: 375, height: 812 };
export const widths: readonly Size[] = [
  { name: "tablet", width: 820, height: 1180 },
  { name: "desktop", width: 1280, height: 800 },
];

export async function shoot(page: Page, name: string, sizes: readonly Size[] = widths) {
  // With a dialog open, the viewport is what the person sees: a full-page shot would show the page past the overlay
  // (which covers the viewport only) as if it weren't dimmed.
  const fullPage = (await page.getByRole("dialog").count()) === 0;
  for (const size of sizes) {
    await page.setViewportSize({ width: size.width, height: size.height });
    for (const colorScheme of ["light", "dark"] as const) {
      await page.emulateMedia({ colorScheme });
      // `animations: "disabled"` ends colour transitions at once: switching the scheme animates every shadcn control.
      await page.screenshot({
        path: `screenshots/admin-${name}-${size.name}-${colorScheme}.png`,
        fullPage,
        animations: "disabled",
      });
    }
  }
}
