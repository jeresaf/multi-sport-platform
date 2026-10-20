import { expect, test } from "@playwright/test";
import { platformAdmin, signIn } from "./support/accounts";
import { open } from "./support/pages";

test("a signed-out visitor is sent to sign in", async ({ page }) => {
  await page.goto("/");

  await expect(page).toHaveURL("/sign-in");
  await expect(page).toHaveTitle("Sign in · MSP Admin");
  await expect(page.getByRole("heading", { level: 1, name: "Sign in" })).toBeVisible();
});

test("a wrong password is refused with the catalogue's message", async ({ page }) => {
  await signIn(page, platformAdmin.email, "not the password at all");

  await expect(
    page.getByRole("alert").getByText("The email or password is incorrect, or the account can't sign in right now."),
  ).toBeVisible();
  await expect(page).toHaveURL("/sign-in");
});

test("the platform administrator signs in through the rewrite and signs out", async ({ page, context }) => {
  // A return path that isn't the fallback ("/"), so the test fails if sign-in ignores it.
  await open(page, "/sign-in?returnTo=%2Fplatform%2Faudit%3Ffrom%3Dlink");
  await page.getByLabel("Email").fill(platformAdmin.email);
  await page.getByLabel("Password").fill(platformAdmin.password);
  await page.getByRole("button", { name: "Sign in" }).click();

  await expect(page).toHaveURL("/platform/audit?from=link");
  await page.getByRole("button", { name: /^Account menu: / }).click();
  await expect(page.getByRole("menu").getByText(platformAdmin.email)).toBeVisible();
  await expect(page.getByRole("menu").getByText("Platform admin")).toBeVisible();
  // The session cookie is the admin app's own (first-party, HttpOnly, SameSite=Strict; `msp_session` in Development).
  const session = (await context.cookies()).find((cookie) => cookie.name === "msp_session");
  expect(session).toMatchObject({ domain: "localhost", httpOnly: true, sameSite: "Strict" });

  await page.getByRole("menuitem", { name: "Sign out" }).click();

  await expect(page).toHaveURL("/sign-in");
  await page.goto("/");
  await expect(page).toHaveURL("/sign-in");
});

test("a sign-in link can't send someone to another site", async ({ page }) => {
  await open(page, "/sign-in?returnTo=https%3A%2F%2Fevil.example%2F");
  await page.getByLabel("Email").fill(platformAdmin.email);
  await page.getByLabel("Password").fill(platformAdmin.password);
  await page.getByRole("button", { name: "Sign in" }).click();

  // The fallback: `/`, which opens the administrator's workspace (the platform console; they belong to no tenant).
  await expect(page).toHaveURL("/platform");
});
