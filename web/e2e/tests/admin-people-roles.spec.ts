import { expect, test } from "@playwright/test";
import { acceptByApi, inviteMember, inviteOwnerOf, newPassword, ownTenant, personEmail, signIn, signInByApi } from "./support/accounts";
import { createPerson, enableFirstSport } from "./support/people";

// Registry → People → a person → Roles (S06 D12, T7c) against the real API: a registrar adds an athlete profile and an
// official profile with a licence, then retires the athlete profile with a reason. Positions and roles are the first
// enabled sport's own codes, so no sport word is the test's. A tenant of its own (S06-T7a).
test("a registrar adds, changes and retires role profiles", async ({ page, request }) => {
  const tenant = await ownTenant(request, "hammerkop-sports", "Hammerkop Sports Club");
  const owner = { email: personEmail("agnes.nansubuga", tenant.slug), password: newPassword() };
  await acceptByApi(request, await inviteOwnerOf(request, tenant.id, owner.email), owner.password);
  await signInByApi(request, owner.email, owner.password);
  const member = { tenantId: tenant.id };
  const sport = await enableFirstSport(request, member);
  const shamim = await createPerson(request, member, {
    firstName: "Shamim",
    lastName: "Nankya",
    gender: "FEMALE",
    dateOfBirth: "2003-07-19",
    nationality: "UG",
  });

  const registrar = { email: personEmail("brian.ssemakula", tenant.slug), password: newPassword() };
  await acceptByApi(request, await inviteMember(request, { ...owner, tenantId: tenant.id }, registrar.email, ["REGISTRAR"]), registrar.password);
  await signIn(page, registrar.email, registrar.password);
  await expect(page.getByRole("heading", { level: 1, name: tenant.name })).toBeVisible();

  await page.goto(`/t/${tenant.slug}/people/${shamim.id}?tab=roles`);
  await expect(page.getByText("No roles yet")).toBeVisible();

  // An athlete profile: the primary position and the name shown publicly.
  await page.getByRole("button", { name: "Add athlete profile" }).click();
  const athleteDialog = page.getByRole("dialog");
  await athleteDialog.getByLabel("Primary position (optional)").selectOption(sport.positions[0]!);
  await athleteDialog.getByLabel("Public name (optional)").fill("S. Nankya");
  await athleteDialog.getByRole("button", { name: "Add profile" }).click();
  await expect(athleteDialog).toBeHidden();
  await expect(page.getByText("S. Nankya")).toBeVisible();
  await expect(page.getByText("No roles yet")).toBeHidden();
  // One sport enabled, and it is taken: no second athlete profile is offered.
  await expect(page.getByRole("button", { name: "Add athlete profile" })).toHaveCount(0);

  // An official profile with a licence.
  await page.getByRole("button", { name: "Add official profile" }).click();
  const officialDialog = page.getByRole("dialog");
  await officialDialog.getByLabel("Role", { exact: true }).selectOption(sport.officialRole);
  await officialDialog.getByLabel("Licence number (optional)").fill("HK-REF-0031");
  await officialDialog.getByLabel("Licence valid until (optional)").fill("2027-06-30");
  await officialDialog.getByRole("button", { name: "Add profile" }).click();
  await expect(officialDialog).toBeHidden();
  const official = page.getByRole("article", { name: "Official profile" });
  await expect(official.getByText("HK-REF-0031, valid until 30 Jun 2027").first()).toBeVisible();

  // Retiring the athlete profile needs a reason; it can then only be made active again or archived.
  const athlete = page.getByRole("article").filter({ hasText: "S. Nankya" });
  await athlete.getByRole("button", { name: /^Retire/ }).click();
  const retire = page.getByRole("dialog");
  await retire.getByLabel("Reason").fill("Stopped playing after the 2026 season");
  await retire.getByRole("button", { name: "Retire" }).click();
  await expect(retire).toBeHidden();
  await expect(athlete.getByText("Retired")).toBeVisible();
  await expect(athlete.getByRole("button", { name: /^Make active/ })).toBeVisible();
  await expect(athlete.getByRole("button", { name: /^Retire/ })).toHaveCount(0);
});
