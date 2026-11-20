import { expect, test } from "@playwright/test";
import { acceptByApi, inviteMember, inviteOwnerOf, newPassword, ownTenant, personEmail, signIn, signInByApi } from "./support/accounts";
import { createPerson, placeholderPng } from "./support/people";

// Registry → People → a person → the photo, external identifiers and aliases (S06 D10, D19, T7c-2a) against the real
// API: a registrar adds a photo (the API checks its bytes and keeps resized copies), adds an identifier and ends it,
// adds an alias and removes it, then removes the photo. A tenant of its own (S06-T7a).
test("a registrar changes a person's photo, identifiers and aliases", async ({ page, request }) => {
  const tenant = await ownTenant(request, "bulbul-sports", "Bulbul Sports Club");
  const owner = { email: personEmail("agnes.nansubuga", tenant.slug), password: newPassword() };
  await acceptByApi(request, await inviteOwnerOf(request, tenant.id, owner.email), owner.password);
  await signInByApi(request, owner.email, owner.password);
  const moses = await createPerson(request, { tenantId: tenant.id }, {
    firstName: "Moses",
    lastName: "Kato",
    gender: "MALE",
    dateOfBirth: "1996-08-14",
    nationality: "UG",
  });

  const registrar = { email: personEmail("brian.ssemakula", tenant.slug), password: newPassword() };
  await acceptByApi(request, await inviteMember(request, { ...owner, tenantId: tenant.id }, registrar.email, ["REGISTRAR"]), registrar.password);
  await signIn(page, registrar.email, registrar.password);
  await expect(page.getByRole("heading", { level: 1, name: tenant.name })).toBeVisible();

  await page.goto(`/t/${tenant.slug}/people/${moses.id}?tab=identifiers`);
  await expect(page.getByRole("img", { name: "No photo of Moses Kato" })).toBeVisible();

  // The photo: chosen, previewed, stored.
  await page.getByRole("button", { name: "Add photo" }).click();
  const photo = page.getByRole("dialog");
  await photo.getByLabel("Photo").setInputFiles({ name: "moses.png", mimeType: "image/png", buffer: placeholderPng(320) });
  await expect(photo.getByRole("img", { name: "The chosen photo" })).toBeVisible();
  await photo.getByRole("button", { name: "Add photo" }).click();
  await expect(photo).toBeHidden();
  await expect(page.getByRole("img", { name: "Photo of Moses Kato" })).toBeVisible();
  await expect(page.getByRole("button", { name: "Change photo" })).toBeFocused();

  // An identifier: the issuer's code is upper-cased; then it is ended and stays listed.
  await page.getByRole("button", { name: "Add identifier" }).click();
  const identifier = page.getByRole("dialog");
  await identifier.getByLabel("Issued by (code)").fill("bulbul_reg_no");
  await identifier.getByLabel("Identifier").fill("BB-2026-0012");
  await identifier.getByLabel("Valid from (optional)").fill("2026-01-10");
  await identifier.getByRole("button", { name: "Add identifier" }).click();
  await expect(identifier).toBeHidden();
  await expect(page.getByRole("rowheader").filter({ hasText: "BB-2026-0012" })).toBeVisible();
  await expect(page.getByRole("cell", { name: "BULBUL_REG_NO", exact: true })).toBeVisible();

  await page.getByRole("button", { name: "End BULBUL_REG_NO BB-2026-0012" }).click();
  const end = page.getByRole("dialog");
  await end.getByLabel("Reason (optional)").fill("Re-registered under a new number");
  await end.getByRole("button", { name: "End identifier" }).click();
  await expect(end).toBeHidden();
  await expect(page.getByRole("cell", { name: /^10 Jan 2026 to .* \(ended\)$/ })).toBeVisible();
  await expect(page.getByRole("button", { name: /^End / })).toHaveCount(0);

  // An alias: added, then removed with a reason.
  await page.getByRole("button", { name: "Add alias" }).click();
  const alias = page.getByRole("dialog");
  await alias.getByLabel("Name").fill("Mozzy Kato");
  await alias.getByLabel("Kind").selectOption("NICKNAME");
  await alias.getByRole("button", { name: "Add alias" }).click();
  await expect(alias).toBeHidden();
  await expect(page.getByRole("cell", { name: "Nickname", exact: true })).toBeVisible();

  await page.getByRole("button", { name: "Remove the alias Mozzy Kato" }).click();
  const remove = page.getByRole("dialog");
  await remove.getByLabel("Reason").fill("Entered by mistake");
  await remove.getByRole("button", { name: "Remove alias" }).click();
  await expect(remove).toBeHidden();
  await expect(page.getByText("Mozzy Kato")).toHaveCount(0);

  // The photo comes down again.
  await page.getByRole("button", { name: "Remove photo" }).click();
  await page.getByRole("dialog").getByRole("button", { name: "Remove photo" }).click();
  await expect(page.getByRole("img", { name: "No photo of Moses Kato" })).toBeVisible();
});
