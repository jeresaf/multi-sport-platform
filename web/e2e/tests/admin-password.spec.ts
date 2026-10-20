import { expect, test } from "@playwright/test";
import { acceptByApi, inviteOwner, newPassword, personEmail, signIn } from "./support/accounts";
import { linkFromEmail } from "./support/mailpit";

test("a member who forgot their password resets it from the email", async ({ page, request }) => {
  const email = personEmail("agnes.namutebi", "elssc");
  await acceptByApi(request, await inviteOwner(request, "elssc", email), newPassword());

  await page.goto("/sign-in");
  await page.getByRole("link", { name: "Forgot your password?" }).click();
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
