import { expect, test, type APIRequestContext } from "@playwright/test";
import { auditEntries, type AuditEntry } from "./support/audit";
import {
  acceptByApi,
  inviteMember,
  inviteOwnerOf,
  kampalaDate,
  newPassword,
  ownTenant,
  personEmail,
  platformAdmin,
  signIn,
  signInByApi,
} from "./support/accounts";
import { open } from "./support/pages";
import { changePerson, createPerson, enableFirstSport, placeholderPng, setPhoto, signedForm } from "./support/people";
import { phone, shoot, widths } from "./support/screenshots";

// The S06 slice demo (docs/build/slices/S06-people-registry.md, "Demo") through the screens against the real API, on
// tenants of this run's own (KMSA and ELSSC stay the manual demo, seeded by registry.demo.people): Coucal's registrar
// finds a likely match and opens it instead, adds a twin past the review with a reason (not warned again), gives her an
// athlete profile and a photo; records a minor's consent with the form, turns her public profile on, and the public read
// shows only her name and photo until the photo scope is withdrawn; one person holds three profiles; an archived person
// takes no new one. Francolin's owner sees none of Coucal's people. The audit trail keeps the reasons. Positions and
// roles are the first enabled sport's own, so the test names no sport.

const json = (member: { tenantId: string }) => ({ "X-CSRF": "1", "X-Tenant-Id": member.tenantId });

/** The public read of a person on the tenant's site: in Development the `tenant` query names the site (S02 D6). */
async function publicPerson(request: APIRequestContext, slug: string, personId: string) {
  const read = await request.get(`/api/public/v1/people/${personId}`, { params: { tenant: slug } });
  expect(read.status(), `public read: ${await read.text()}`).toBe(200);
  return (await read.json()) as Record<string, unknown>;
}

test("slice demo: duplicate review, a twin past it, a minor's consent and public read, profiles, isolation", async ({
  page,
  browser,
  request,
}) => {
  // Two tenants, a dozen people, the screens' steps and the audit trail's polls.
  test.slow();
  const coucal = await ownTenant(request, "coucal-sports", "Coucal Sports Club");
  const owner = { email: personEmail("agnes.nansubuga", coucal.slug), password: newPassword() };
  await acceptByApi(request, await inviteOwnerOf(request, coucal.id, owner.email), owner.password);
  await signInByApi(request, owner.email, owner.password);
  const member = { tenantId: coucal.id };
  const sport = await enableFirstSport(request, member);

  // Already registered (as registry.demo.people seeds KMSA): John Ssekandi, Mariam Nankya, Brenda Akello (a minor,
  // 14 or 15 on the day the test runs, with a photo and no consent), Denis Opoka with three profiles, Ivan Mukasa
  // archived.
  const thisYear = Number(kampalaDate(0).slice(0, 4));
  const john = await createPerson(request, member, {
    firstName: "John",
    lastName: "Ssekandi",
    gender: "MALE",
    dateOfBirth: "1998-08-21",
    nationality: "UG",
  });
  const mariam = await createPerson(request, member, {
    firstName: "Mariam",
    lastName: "Nankya",
    gender: "FEMALE",
    dateOfBirth: "2003-07-19",
    nationality: "UG",
  });
  let brenda = await createPerson(request, member, {
    firstName: "Brenda",
    lastName: "Akello",
    gender: "FEMALE",
    dateOfBirth: `${thisYear - 15}-03-14`,
    nationality: "UG",
  });
  brenda = await setPhoto(request, member, brenda);
  let denis = await createPerson(request, member, {
    firstName: "Denis",
    lastName: "Opoka",
    gender: "MALE",
    dateOfBirth: "1991-05-02",
    nationality: "UG",
  });
  denis = await changePerson(request, member, denis, "athlete-profiles", {
    variant: sport.variant,
    primaryPosition: sport.positions[0] ?? null,
  });
  denis = await changePerson(request, member, denis, "staff-profiles", {
    capabilities: [{ variant: sport.variant, role: sport.staffRole }],
  });
  denis = await changePerson(request, member, denis, "official-profiles", {
    capabilities: [
      { variant: sport.variant, role: sport.officialRole, licenceNumber: "CSC-R-0193", licenceValidUntil: "2027-06-30" },
    ],
  });
  let ivan = await createPerson(request, member, {
    firstName: "Ivan",
    lastName: "Mukasa",
    gender: "MALE",
    dateOfBirth: "1996-11-03",
    nationality: "UG",
  });
  ivan = await changePerson(request, member, ivan, "archive", { reason: "Left the association" });

  const registrar = { email: personEmail("brian.ssemakula", coucal.slug), password: newPassword() };
  await acceptByApi(request, await inviteMember(request, { ...owner, tenantId: coucal.id }, registrar.email, ["REGISTRAR"]), registrar.password);
  await signIn(page, registrar.email, registrar.password);
  await expect(page.getByRole("heading", { level: 1, name: coucal.name })).toBeVisible();

  // People → search "Ssekandi": John is there.
  await page.getByRole("navigation").getByRole("link", { name: "People" }).first().click();
  await expect(page.getByRole("heading", { level: 1, name: "People" })).toBeVisible();
  await page.getByLabel("Search").fill("Ssekandi");
  await expect(page).toHaveURL(/\?q=Ssekandi$/);
  await expect(page.getByRole("table").getByRole("link", { name: "John Ssekandi" })).toBeVisible();
  await expect(page.getByRole("table").getByRole("link", { name: "Mariam Nankya" })).toHaveCount(0);

  // Add person "Ssekandi John" on John's date of birth: likely the same person, so the registrar opens him instead.
  await page.getByRole("link", { name: "Add person" }).click();
  await expect(page.getByRole("heading", { level: 1, name: "Who are you adding?" })).toBeVisible();
  await page.getByLabel("First name").fill("Ssekandi");
  await page.getByLabel("Last name").fill("John");
  await page.getByRole("button", { name: "Continue" }).click();
  await expect(page.getByRole("heading", { level: 1, name: "Their details" })).toBeVisible();
  await page.getByLabel("Date of birth (optional)").fill("1998-08-21");
  await page.getByRole("button", { name: "Check and add" }).click();
  const candidates = page.getByRole("list", { name: "People who may match" });
  await expect(candidates.getByText("Likely the same person")).toBeVisible();
  await expect(candidates.getByText("Same date of birth")).toBeVisible();
  await expect(candidates.getByText("Same names, in any order")).toBeVisible();
  await shoot(page, "add-person-likely-match", [phone, ...widths]);
  await candidates.getByRole("link", { name: "Open John Ssekandi" }).click();
  await expect(page).toHaveURL(new RegExp(`/people/${john.id}`));
  await expect(page.getByRole("heading", { level: 1, name: "John Ssekandi" })).toBeVisible();

  // Shamim Nankya, Mariam's twin: possibly the same person; added with a reason.
  await open(page, `/t/${coucal.slug}/people/new`);
  await page.getByLabel("First name").fill("Shamim");
  await page.getByLabel("Last name").fill("Nankya");
  await page.getByRole("button", { name: "Continue" }).click();
  await page.getByLabel("Date of birth (optional)").fill("2003-07-19");
  await page.getByLabel("Nationality", { exact: true }).selectOption("UG");
  await page.getByRole("button", { name: "Check and add" }).click();
  await expect(candidates.getByText("Possibly the same person")).toBeVisible();
  await expect(candidates.getByRole("link", { name: "Open Mariam Nankya" })).toBeVisible();
  await page.getByRole("button", { name: "Add as a new person" }).click();
  await page.getByLabel("Why is Shamim Nankya a different person?").fill("Twin sister, different first name");
  await page.getByRole("button", { name: "Add as a new person" }).click();
  await expect(page.getByRole("heading", { level: 1, name: "Shamim Nankya" })).toBeVisible();
  const shamimId = /\/people\/([0-9a-f-]{36})/.exec(page.url())![1]!;

  // The pair is decided: checking Shamim again doesn't warn about Mariam.
  await signInByApi(request, registrar.email, registrar.password);
  const recheck = await request.post("/api/admin/v1/people/duplicate-check", {
    data: { firstName: "Shamim", lastName: "Nankya", dateOfBirth: "2003-07-19", nationality: "UG", personId: shamimId },
    headers: json(member),
  });
  expect(recheck.status()).toBe(200);
  const { candidates: again } = (await recheck.json()) as { candidates: { personId: string }[] };
  expect(again.map((candidate) => candidate.personId)).not.toContain(mariam.id);

  // Her athlete profile (the sport's first position) and a photo.
  await page.getByRole("tab", { name: "Roles" }).click();
  await page.getByRole("button", { name: "Add athlete profile" }).click();
  const athleteDialog = page.getByRole("dialog");
  await athleteDialog.getByLabel("Primary position (optional)").selectOption(sport.positions[0]!);
  await athleteDialog.getByRole("button", { name: "Add profile" }).click();
  await expect(athleteDialog).toBeHidden();
  await expect(page.getByRole("tabpanel")).toContainText(sport.labels.position);
  await page.getByRole("button", { name: "Add photo" }).click();
  const photo = page.getByRole("dialog");
  await photo.getByLabel("Photo").setInputFiles({ name: "shamim.png", mimeType: "image/png", buffer: placeholderPng(320) });
  await photo.getByRole("button", { name: "Add photo" }).click();
  await expect(photo).toBeHidden();
  await expect(page.getByRole("img", { name: "Photo of Shamim Nankya" })).toBeVisible();

  // Brenda Akello: a minor, public profile off; her mother's consent with the signed form; the public profile on.
  await open(page, `/t/${coucal.slug}/people/${brenda.id}?tab=consent`);
  await expect(page.getByText("Minor", { exact: true }).first()).toBeVisible();
  await expect(page.getByText("Public profile off", { exact: true })).toBeVisible();
  await page.getByRole("button", { name: "Record consent" }).click();
  const record = page.getByRole("dialog");
  await record.getByLabel("Guardian's name").fill("Grace Akello");
  await record.getByLabel("Relationship").selectOption("MOTHER");
  await record.getByLabel("Contact", { exact: true }).fill("+256 772 000 147");
  for (const scope of ["Data processing", "Public profile", "Photo"]) {
    await record.getByRole("checkbox", { name: scope }).check();
  }
  await record.getByLabel("Evidence (optional)").setInputFiles(signedForm);
  await record.getByRole("button", { name: "Record consent" }).click();
  await expect(record).toBeHidden();
  const consent = page.getByRole("article").filter({ has: page.getByText("Active", { exact: true }) });
  await expect(consent.getByRole("link", { name: "Download the signed form" })).toBeVisible();
  await page.getByRole("button", { name: "Turn on public profile" }).click();
  await page.getByRole("dialog").getByRole("button", { name: "Turn on" }).click();
  await expect(page.getByText("Public profile on", { exact: true })).toBeVisible();

  // The public read shows a minor's display name and photo only: no nationality, no profiles.
  const shown = await publicPerson(request, coucal.slug, brenda.id);
  expect(shown).toMatchObject({ id: brenda.id, displayName: "Brenda Akello", nationality: null, athleteProfiles: [] });
  expect(shown.photo).not.toBeNull();

  // The photo scope withdrawn with a reason: the public read drops the photo.
  await consent.getByRole("button", { name: /^Withdraw: / }).click();
  const withdraw = page.getByRole("dialog");
  await withdraw.getByRole("checkbox", { name: "Photo" }).check();
  await withdraw.getByLabel("Reason").fill("Her mother asked for the photo to come down");
  await withdraw.getByRole("button", { name: "Withdraw" }).click();
  await expect(withdraw).toBeHidden();
  await expect(page.getByRole("article").filter({ hasText: "Her mother asked for the photo to come down" })).toBeVisible();
  expect(await publicPerson(request, coucal.slug, brenda.id)).toMatchObject({ displayName: "Brenda Akello", photo: null });

  // Denis Opoka: athlete, staff and official profiles on one person.
  await open(page, `/t/${coucal.slug}/people/${denis.id}?tab=roles`);
  const roles = page.getByRole("tabpanel");
  await expect(roles.getByRole("heading", { level: 4, name: "Staff profile" })).toBeVisible();
  await expect(roles.getByRole("heading", { level: 4, name: "Official profile" })).toBeVisible();
  await expect(roles).toContainText(sport.labels.position);
  await expect(roles).toContainText(sport.labels.staffRole);
  await expect(roles).toContainText(sport.labels.officialRole);
  await expect(roles.getByRole("cell", { name: "CSC-R-0193, valid until 30 Jun 2027" }).first()).toBeVisible();

  // Ivan Mukasa, archived: no new profile, on the screen or through the API.
  await open(page, `/t/${coucal.slug}/people/${ivan.id}?tab=roles`);
  await expect(page.getByText("Ivan Mukasa is archived: no new role can be added.")).toBeVisible();
  await expect(page.getByRole("button", { name: /^Add (athlete|staff|official) profile$/ })).toHaveCount(0);
  await shoot(page, "person-archived-roles", [phone, ...widths]);
  const refused = await request.post(`/api/admin/v1/people/${ivan.id}/athlete-profiles`, {
    data: { variant: sport.variant, version: ivan.version },
    headers: json(member),
  });
  expect(refused.status()).toBe(409);
  expect(await refused.json()).toMatchObject({ code: "PERSON_ARCHIVED" });

  // Francolin Schools' owner sees none of Coucal's people: not listed, not found by search, not found by ID.
  const francolin = await ownTenant(request, "francolin-schools", "Francolin Schools Sports Circle");
  const francolinOwner = { email: personEmail("agnes.namutebi", francolin.slug), password: newPassword() };
  await acceptByApi(request, await inviteOwnerOf(request, francolin.id, francolinOwner.email), francolinOwner.password);
  const francolinContext = await browser.newContext();
  const francolinPage = await francolinContext.newPage();
  await signIn(francolinPage, francolinOwner.email, francolinOwner.password);
  await expect(francolinPage.getByRole("heading", { level: 1, name: francolin.name })).toBeVisible();
  await open(francolinPage, `/t/${francolin.slug}/people`);
  await expect(francolinPage.getByText("No people yet")).toBeVisible();
  await open(francolinPage, `/t/${francolin.slug}/people?q=Ssekandi`);
  await expect(francolinPage.getByText("No one matches")).toBeVisible();
  const notFound = await francolinContext.request.get(`/api/admin/v1/people/${john.id}`, {
    headers: { "X-Tenant-Id": francolin.id },
  });
  expect(notFound.status()).toBe(404);
  expect(await notFound.json()).toMatchObject({ code: "PERSON_NOT_FOUND" });
  await francolinContext.close();

  // The audit trail, read as the platform administrator: Shamim's create keeps the reason to proceed, the withdrawal
  // its own, the archive Ivan's.
  await signInByApi(request, platformAdmin.email, platformAdmin.password);
  const success = (actionCode: string, entityId: string, reason: string) => (entry: AuditEntry) =>
    entry.actionCode === actionCode && entry.outcome === "SUCCESS" && entry.entityId === entityId && entry.reason === reason;
  await auditEntries(request, { tenantId: coucal.id, action: "person.create" }, [
    success("person.create", shamimId, "Twin sister, different first name"),
  ]);
  await auditEntries(request, { tenantId: coucal.id, action: "person.consent.withdraw" }, [
    (entry) => entry.outcome === "SUCCESS" && entry.reason === "Her mother asked for the photo to come down",
  ]);
  await auditEntries(request, { tenantId: coucal.id, action: "person.archive" }, [
    success("person.archive", ivan.id, "Left the association"),
  ]);
});
