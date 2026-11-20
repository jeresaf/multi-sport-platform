import { expect, test } from "@playwright/test";
import { acceptByApi, inviteMember, inviteOwnerOf, newPassword, ownTenant, personEmail, signIn, signInByApi } from "./support/accounts";
import { createPerson } from "./support/people";

// Registry → People → Add person (S06 D5, D6) against the real API: a registrar types a name that is already
// registered, is blocked by the identifier the existing person holds, then adds a different person of the same name
// and date of birth with a reason. A tenant of its own (S06-T7a), so its members don't count against KMSA's staff limit.
test("a registrar adds a person past the duplicate review", async ({ page, request }) => {
  const tenant = await ownTenant(request, "crane-netball", "Crane Netball Club");
  const owner = { email: personEmail("agnes.nansubuga", tenant.slug), password: newPassword() };
  await acceptByApi(request, await inviteOwnerOf(request, tenant.id, owner.email), owner.password);
  await signInByApi(request, owner.email, owner.password);
  const tenantId = tenant.id;

  // Already registered: Grace Nabirye, born 10 April 2001, with the club's registration number.
  const existing = await createPerson(request, { tenantId }, {
    firstName: "Grace",
    lastName: "Nabirye",
    dateOfBirth: "2001-04-10",
    nationality: "UG",
    externalIdentifiers: [{ authority: "CRANE_REG_NO", value: "CN-2026-0014" }],
  });

  const registrar = { email: personEmail("joan.atim", tenant.slug), password: newPassword() };
  await acceptByApi(request, await inviteMember(request, { ...owner, tenantId }, registrar.email, ["REGISTRAR"]), registrar.password);
  await signIn(page, registrar.email, registrar.password);
  await expect(page.getByRole("heading", { level: 1, name: tenant.name })).toBeVisible();

  await page.getByRole("navigation").getByRole("link", { name: "People" }).first().click();
  await page.getByRole("link", { name: "Add person" }).click();
  await expect(page.getByRole("heading", { level: 1, name: "Who are you adding?" })).toBeVisible();

  // The name first: the person already registered is listed.
  await page.getByLabel("First name").fill("Grace");
  await page.getByLabel("Last name").fill("Nabirye");
  await expect(page.getByRole("link", { name: "Grace Nabirye" })).toHaveAttribute("href", `/t/${tenant.slug}/people/${existing.id}`);
  await page.getByRole("button", { name: "Continue" }).click();

  // The same identifier blocks the save.
  await expect(page.getByRole("heading", { level: 1, name: "Their details" })).toBeVisible();
  await page.getByLabel("Date of birth (optional)").fill("2001-04-10");
  await page.getByLabel("Nationality", { exact: true }).selectOption("UG");
  await page.getByRole("button", { name: "Add an identifier" }).click();
  await page.getByLabel("Issued by (code)").fill("CRANE_REG_NO");
  await page.getByLabel("Identifier", { exact: true }).fill("CN-2026-0014");
  await page.getByRole("button", { name: "Check and add" }).click();
  await expect(page.getByText("This identifier is already in use")).toBeVisible();
  await expect(page.getByText("Holds the same CRANE_REG_NO identifier")).toBeVisible();
  await expect(page.getByRole("button", { name: "Add as a new person" })).toHaveCount(0);

  // Without it, the review asks for a reason; the existing person shows a birth year, never the date.
  await page.getByRole("button", { name: "Change the details" }).click();
  await page.getByRole("button", { name: "Remove" }).click();
  await page.getByRole("button", { name: "Check and add" }).click();
  const candidates = page.getByRole("list", { name: "People who may match" });
  await expect(candidates.getByText("Likely the same person")).toBeVisible();
  await expect(candidates.getByText("Same date of birth")).toBeVisible();
  await expect(candidates.getByText("2001", { exact: true })).toBeVisible();
  await expect(candidates.getByText("10 April 2001")).toHaveCount(1); // the new person's own date only

  await page.getByRole("button", { name: "Add as a new person" }).click();
  await expect(page.getByText("Say why this is a different person.")).toBeVisible();
  await page.getByLabel("Why is Grace Nabirye a different person?").fill("A twin sister; checked with the club secretary.");
  await page.getByRole("button", { name: "Add as a new person" }).click();

  // The new person's page, and both in the directory.
  await expect(page.getByRole("heading", { level: 1, name: "Grace Nabirye" })).toBeVisible();
  expect(page.url()).not.toContain(existing.id);
  await page.getByRole("link", { name: "People" }).first().click();
  await expect(page.getByRole("table").getByRole("link", { name: "Grace Nabirye" })).toHaveCount(2);
});
