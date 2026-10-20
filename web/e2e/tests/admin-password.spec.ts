import { expect, test } from "@playwright/test";
import { acceptByApi, inviteOwner, newPassword, personEmail, signIn } from "./support/accounts";
import { linkFromEmail } from "./support/mailpit";
import { open } from "./support/pages";
import { phone, shoot, widths } from "./support/screenshots";

test("a member who forgot their password resets it from the email", async ({ page, request }) => {
  const email = personEmail("agnes.namutebi", "elssc");
  await acceptByApi(request, await inviteOwner(request, "elssc", email), newPassword());

  await open(page, "/sign-in");
  await page.getByRole("link", { name: "Forgot your password?" }).click();
  await expect(page.getByRole("heading", { level: 1, name: "Forgot your password?" })).toBeVisible();
  await page.getByLabel("Email").fill(email);
  await page.getByRole("button", { name: "Send the link" }).click();
  await expect(page.getByRole("heading", { level: 1, name: "Check your email" })).toBeVisible();

  const link = await linkFromEmail(request, email, "/reset-password");
  const response = await page.goto(link);
  expect(response?.headers()["referrer-policy"]).toBe("no-referrer");
  await expect(page).toHaveURL("/reset-password");

  const password = newPassword();
  await page.getByLabel("New password").fill(password);
  await page.getByLabel("Type it again").fill(password);
  await page.getByRole("button", { name: "Save the new password" }).click();
  await expect(page.getByRole("heading", { level: 1, name: "Password changed" })).toBeVisible();

  await signIn(page, email, password);
  await expect(page).toHaveURL("/t/elssc");
});

test("a too-short password is refused before it is sent", async ({ page }) => {
  await page.goto("/reset-password?token=not-a-real-token");
  await page.getByLabel("New password").fill("short");
  await page.getByLabel("Type it again").fill("short");
  await page.getByRole("button", { name: "Save the new password" }).click();

  await expect(page.getByText("Use at least 12 characters.")).toBeVisible();
});

test("a used or made-up reset link offers a new one", async ({ page }) => {
  await page.goto("/reset-password?token=not-a-real-token");
  const password = newPassword();
  await page.getByLabel("New password").fill(password);
  await page.getByLabel("Type it again").fill(password);
  await page.getByRole("button", { name: "Save the new password" }).click();

  await expect(page.getByText("The reset link is invalid or has expired. Ask for a new one.")).toBeVisible();
  await expect(page.getByRole("link", { name: "Ask for a new link" })).toHaveAttribute("href", "/forgot-password");
});

test("a signed-in member changes their password, stays signed in, and signs in with the new one", async ({
  page,
  request,
}) => {
  const email = personEmail("ruth.atim", "elssc");
  const password = newPassword();
  await acceptByApi(request, await inviteOwner(request, "elssc", email), password);

  await signIn(page, email, password);
  await expect(page).toHaveURL("/t/elssc");
  await page.getByRole("button", { name: /^Account menu: / }).click();
  await page.getByRole("menuitem", { name: "Change password" }).click();
  await expect(page).toHaveURL("/account/password");
  await expect(page.locator("html[data-hydrated]")).toBeAttached();

  // A wrong current password is refused under its field.
  const changed = newPassword();
  await page.getByLabel("Current password").fill(`${password} typo`);
  await page.getByLabel("New password").fill(changed);
  await page.getByLabel("Type it again").fill(changed);
  await page.getByRole("button", { name: "Change password" }).click();
  await expect(page.getByLabel("Current password")).toHaveAttribute("aria-invalid", "true");
  await shoot(page, "change-password", [phone, ...widths]);

  await page.getByLabel("Current password").fill(password);
  await page.getByRole("button", { name: "Change password" }).click();
  await expect(page.getByText("Your password has been changed.")).toBeVisible();

  // This session stays signed in (the response renews its cookie through the rewrite).
  await open(page, "/");
  await expect(page).toHaveURL("/t/elssc");
  // `/` sends the user on once `me` is read: the menu opened before the workspace has rendered is lost.
  await expect(page.getByText("Your roles: Tenant owner")).toBeVisible();

  await page.getByRole("button", { name: /^Account menu: / }).click();
  await page.getByRole("menuitem", { name: "Sign out" }).click();
  await expect(page).toHaveURL("/sign-in");
  await signIn(page, email, changed);
  await expect(page).toHaveURL("/t/elssc");
});
