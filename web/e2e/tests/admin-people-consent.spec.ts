import { expect, test } from "@playwright/test";
import { acceptByApi, inviteMember, inviteOwnerOf, newPassword, ownTenant, personEmail, signIn, signInByApi } from "./support/accounts";
import { createPerson } from "./support/people";

/** A small PDF: the API checks the file's first bytes, not its name or the type sent. */
export const signedForm = {
  name: "consent-form.pdf",
  mimeType: "application/pdf",
  buffer: Buffer.from("%PDF-1.4\n1 0 obj << /Type /Catalog >> endobj\ntrailer << /Root 1 0 R >>\n%%EOF\n"),
};

// Registry → People → a person → Consent and the public profile (S06 D14, D15, T7c-1b) against the real API: a
// registrar records a minor's guardian consent with the signed form, turns the public profile on, withdraws the
// public-profile scope (the profile stays on but is no longer shown), then turns it off. A tenant of its own (S06-T7a).
test("a registrar records consent, turns the public profile on, withdraws and turns it off", async ({ page, request }) => {
  const tenant = await ownTenant(request, "wagtail-juniors", "Wagtail Junior Sports Club");
  const owner = { email: personEmail("agnes.nansubuga", tenant.slug), password: newPassword() };
  await acceptByApi(request, await inviteOwnerOf(request, tenant.id, owner.email), owner.password);
  await signInByApi(request, owner.email, owner.password);
  const sharon = await createPerson(request, { tenantId: tenant.id }, {
    firstName: "Sharon",
    lastName: "Atim",
    gender: "FEMALE",
    dateOfBirth: "2013-05-02",
    nationality: "UG",
  });

  const registrar = { email: personEmail("brian.ssemakula", tenant.slug), password: newPassword() };
  await acceptByApi(request, await inviteMember(request, { ...owner, tenantId: tenant.id }, registrar.email, ["REGISTRAR"]), registrar.password);
  await signIn(page, registrar.email, registrar.password);
  await expect(page.getByRole("heading", { level: 1, name: tenant.name })).toBeVisible();

  await page.goto(`/t/${tenant.slug}/people/${sharon.id}?tab=consent`);
  await expect(page.getByText("No consent recorded yet.")).toBeVisible();
  // A minor without consent to the public profile: the switch says why it can't be used.
  await expect(page.getByRole("button", { name: "Turn on public profile" })).toBeDisabled();

  // Her mother's consent, with the signed form.
  await page.getByRole("button", { name: "Record consent" }).click();
  const record = page.getByRole("dialog");
  await record.getByLabel("Guardian's name").fill("Florence Atim");
  await record.getByLabel("Relationship").selectOption("MOTHER");
  await record.getByLabel("Contact", { exact: true }).fill("+256 772 000 318");
  await record.getByRole("checkbox", { name: "Data processing" }).check();
  await record.getByRole("checkbox", { name: "Public profile" }).check();
  await record.getByLabel("Evidence (optional)").setInputFiles(signedForm);
  await record.getByRole("button", { name: "Record consent" }).click();
  await expect(record).toBeHidden();
  const consent = page.getByRole("article").filter({ has: page.getByText("Active", { exact: true }) });
  await expect(consent.getByText("Florence Atim")).toBeVisible();
  await expect(consent.getByRole("link", { name: "Download the signed form" })).toBeVisible();

  // The public profile, now that the guardian agreed to it.
  await page.getByRole("button", { name: "Turn on public profile" }).click();
  await page.getByRole("dialog").getByRole("button", { name: "Turn on" }).click();
  await expect(page.getByText("Public profile on", { exact: true })).toBeVisible();

  // Withdrawing the public-profile scope: the data-processing scope goes on in a new record; the profile is hidden.
  await consent.getByRole("button", { name: /^Withdraw: / }).click();
  const withdraw = page.getByRole("dialog");
  await withdraw.getByRole("checkbox", { name: "Public profile" }).check();
  await withdraw.getByLabel("Reason").fill("The guardian asked by phone");
  await withdraw.getByRole("button", { name: "Withdraw" }).click();
  await expect(withdraw).toBeHidden();
  // The closed record keeps its reason; the new one carries data processing alone.
  await expect(page.getByRole("article").filter({ hasText: "The guardian asked by phone" })).toBeVisible();
  await expect(page.getByRole("article").filter({ hasText: "Florence Atim" })).toHaveCount(2);
  await expect(page.getByText("Public profile on (not shown without a guardian's consent to the public profile)")).toBeVisible();

  await page.getByRole("button", { name: "Turn off public profile" }).click();
  await page.getByRole("dialog").getByRole("button", { name: "Turn off" }).click();
  await expect(page.getByText("Public profile off", { exact: true })).toBeVisible();
});
