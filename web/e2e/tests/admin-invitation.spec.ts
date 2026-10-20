import { expect, test } from "@playwright/test";
import { inviteOwner, newPassword, personEmail, signIn } from "./support/accounts";

test("an invited owner creates their account from the email and signs in", async ({ page, request }) => {
  const email = personEmail("brian.ssemakula", "kmsa");
  const link = await inviteOwner(request, "kmsa", email);

  const response = await page.goto(link);

  // The token leaves no trace: no Referer from the page, and gone from the address bar once read (#44 M3).
  expect(response?.headers()["referrer-policy"]).toBe("no-referrer");
  await expect(page.getByRole("heading", { level: 1, name: "Join Kampala Metro Sports Association" })).toBeVisible();
  await expect(page).toHaveURL("/accept-invitation");
  await expect(page.getByText(`You're invited as Tenant owner for ${email}.`)).toBeVisible();

  const password = newPassword();
  await page.getByLabel("Choose a password").fill(password);
  await page.getByLabel("Type it again").fill(password);
  await page.getByRole("button", { name: "Create account and accept" }).click();

  await expect(page.getByRole("heading", { level: 1, name: "You've joined Kampala Metro Sports Association" })).toBeVisible();
  await page.getByRole("link", { name: "Sign in" }).click();
  await signIn(page, email, password);

  await expect(page).toHaveURL("/t/kmsa");
  await expect(page.getByRole("heading", { level: 1, name: "Kampala Metro Sports Association" })).toBeVisible();
  await expect(page.getByText("Your roles: Tenant owner")).toBeVisible();

  // The link works once.
  await page.goto(link);
  await expect(page.getByText("The invitation has been accepted already. Sign in instead.")).toBeVisible();
});

test("a tenant owner's direct call to a platform endpoint is refused by the server", async ({ page, request }) => {
  const email = personEmail("esther.nabirye", "kmsa");
  const link = await inviteOwner(request, "kmsa", email);
  const password = newPassword();
  await page.goto(link);
  await page.getByLabel("Choose a password").fill(password);
  await page.getByLabel("Type it again").fill(password);
  await page.getByRole("button", { name: "Create account and accept" }).click();
  await expect(page.getByRole("heading", { level: 1, name: /You've joined/ })).toBeVisible();
  await signIn(page, email, password);
  await expect(page).toHaveURL("/t/kmsa");

  // With the owner's own session cookie, through the admin app's rewrite.
  const tenants = await page.request.get("/api/platform/v1/tenants");

  expect(tenants.status()).toBe(403);
  expect(await tenants.json()).toMatchObject({ code: "AUTH_FORBIDDEN" });
});
