import { expect, type Page } from "@playwright/test";

/**
 * Opens a page and waits until React has hydrated it (the admin app marks `<html data-hydrated>`): typing into a form
 * before that and pressing its button submits it natively, a reload that loses the input.
 */
export async function open(page: Page, path: string) {
  await page.goto(path);
  await expect(page.locator("html[data-hydrated]")).toBeAttached();
}
