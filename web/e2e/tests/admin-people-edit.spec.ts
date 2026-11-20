import { expect, test } from "@playwright/test";
import { acceptByApi, inviteMember, inviteOwnerOf, newPassword, ownTenant, personEmail, signIn, signInByApi } from "./support/accounts";
import { createPerson } from "./support/people";

// Registry → People → a person → Edit details and the status moves (S06 D8, D9, T7c-2b) against the real API: a
// registrar corrects a mistyped name and date of birth, which the duplicate check matches with the person's twin, saves
// past him with a reason, then moves the status through every move D9 allows. A tenant of its own (S06-T7a).
test("a registrar edits a person's details and changes their status", async ({ page, request }) => {
  const tenant = await ownTenant(request, "barbet-sports", "Barbet Sports Club");
  const owner = { email: personEmail("agnes.nansubuga", tenant.slug), password: newPassword() };
  await acceptByApi(request, await inviteOwnerOf(request, tenant.id, owner.email), owner.password);
  await signInByApi(request, owner.email, owner.password);
  const member = { tenantId: tenant.id };
  await createPerson(request, member, {
    firstName: "Moses",
    lastName: "Kato",
    gender: "MALE",
    dateOfBirth: "1996-08-14",
    nationality: "UG",
  });
  const aron = await createPerson(request, member, {
    firstName: "Aron",
    lastName: "Kato",
    gender: "MALE",
    dateOfBirth: "1996-08-15",
    nationality: "UG",
  });

  const registrar = { email: personEmail("brian.ssemakula", tenant.slug), password: newPassword() };
  await acceptByApi(request, await inviteMember(request, { ...owner, tenantId: tenant.id }, registrar.email, ["REGISTRAR"]), registrar.password);
  await signIn(page, registrar.email, registrar.password);
  await expect(page.getByRole("heading", { level: 1, name: tenant.name })).toBeVisible();

  // Edit details: the first name and the date of birth were mistyped. The check finds his twin, Moses.
  await page.goto(`/t/${tenant.slug}/people/${aron.id}`);
  await page.getByRole("link", { name: "Edit details" }).click();
  await expect(page.getByRole("heading", { level: 1, name: "Edit details" })).toBeVisible();
  await expect(page.getByLabel("Date of birth (optional)")).toHaveValue("1996-08-15");
  await page.getByLabel("First name").fill("Aaron");
  await page.getByLabel("Date of birth (optional)").fill("1996-08-14");
  await page.getByLabel("Height in cm (optional)").fill("181");
  await page.getByRole("button", { name: "Save", exact: true }).click();

  await expect(page.getByRole("heading", { level: 1, name: "Check for duplicates" })).toBeVisible();
  const candidates = page.getByRole("list", { name: "People who may match" });
  await expect(candidates.getByText("Moses Kato")).toBeVisible();
  await expect(candidates.getByText("Same date of birth")).toBeVisible();
  await page.getByLabel("Why is Aaron Kato a different person?").fill("Twin brothers; checked with the club.");
  await page.getByRole("button", { name: "Save the changes" }).click();

  await expect(page.getByRole("heading", { level: 1, name: "Aaron Kato" })).toBeVisible();
  await expect(page.getByText("14 Aug 1996")).toBeVisible();
  await expect(page.getByText("181 cm")).toBeVisible();

  // A change that the check doesn't compare saves at once; a decided pair isn't shown again.
  await page.getByRole("link", { name: "Edit details" }).click();
  await page.getByLabel("Other names (optional)").fill("Wasswa");
  await page.getByRole("button", { name: "Save", exact: true }).click();
  await expect(page.getByRole("heading", { level: 1, name: "Aaron Kato" })).toBeVisible();
  await expect(page.getByText("Wasswa")).toBeVisible();

  // The status moves, each in the status history.
  const status = page.locator("#person-status");
  const move = async (label: string, reason: string | null, after: string) => {
    await page.getByRole("button", { name: label, exact: true }).click();
    const dialog = page.getByRole("dialog");
    if (reason) {
      await dialog.getByLabel(/^Reason/).fill(reason);
    }
    await dialog.getByRole("button", { name: label, exact: true }).click();
    await expect(dialog).toBeHidden();
    await expect(status).toHaveText(after);
    await expect(status).toBeFocused();
  };
  await move("Deactivate", null, "Inactive");
  await move("Reactivate", "Back for the new season", "Active");
  await move("Archive", "Left the club", "Archived");
  await expect(page.getByRole("button", { name: "Mark as deceased" })).toHaveCount(0);
  await move("Unarchive", "Rejoined the club", "Active");
  await move("Mark as deceased", "Reported by the club secretary", "Deceased");
  await expect(page.getByRole("button", { name: /^(Deactivate|Reactivate|Archive|Unarchive|Mark as deceased)$/ })).toHaveCount(0);
  await expect(page.getByRole("cell", { name: "Reported by the club secretary" })).toBeVisible();
  // A deceased person's details can still be corrected.
  await expect(page.getByRole("link", { name: "Edit details" })).toBeVisible();
});
