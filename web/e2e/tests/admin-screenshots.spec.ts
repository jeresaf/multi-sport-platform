import { expect, test, type Page } from "@playwright/test";
import {
  acceptByApi,
  createTenant,
  inviteOwnerOf,
  newPassword,
  ownTenant,
  personEmail,
  platformAdmin,
  signIn,
  signInByApi,
  suspendTenant,
  tenantId,
} from "./support/accounts";
import { changePerson, createPerson, enableFirstSport, placeholderPng, setPhoto } from "./support/people";
import { open } from "./support/pages";
import { phone, shoot, widths } from "./support/screenshots";

// A screen still reading its data (`Loading`, a polite status).
const loading = (page: Page) => page.getByRole("status").filter({ hasText: "Loading" });

test("screenshots of the signed-out pages", async ({ page, request }) => {
  await open(page, "/sign-in");
  await page.getByRole("button", { name: "Sign in" }).click();
  await expect(page.getByText("Enter your email address.")).toBeVisible();
  await shoot(page, "sign-in-errors", [phone, ...widths]);

  await open(page, "/forgot-password");
  await expect(page.getByRole("heading", { level: 1, name: "Forgot your password?" })).toBeVisible();
  await shoot(page, "forgot-password", [phone, ...widths]);

  // A tenant of this run's own (S06-T7a): a pending invitation takes a staff seat.
  const tenant = await ownTenant(request, "musizi-athletics", "Musizi Athletics Club");
  await page.goto(await inviteOwnerOf(request, tenant.id, personEmail("daniel.kato", tenant.slug)));
  await expect(page.getByLabel("Choose a password")).toBeVisible();
  await shoot(page, "accept-invitation", [phone, ...widths]);
});

test("screenshots of the platform console", async ({ page }) => {
  await signIn(page, platformAdmin.email, platformAdmin.password);
  await expect(page.getByRole("heading", { level: 1, name: "Platform console" })).toBeVisible();
  await shoot(page, "platform-console", [phone, ...widths]);

  await page.getByRole("button", { name: /^Account menu: / }).click();
  await expect(page.getByRole("menuitem", { name: "Sign out" })).toBeVisible();
  await shoot(page, "user-menu", widths);
  await page.keyboard.press("Escape");

  await page.goto("/platform/plans");
  await expect(page.getByRole("columnheader", { name: "Pro" })).toBeVisible();
  await shoot(page, "plans", [phone, ...widths]);

  // Screens that come later answer "not available yet".
  await page.goto("/platform/diagnostics");
  await expect(page.getByText("This screen isn't available yet.")).toBeVisible();
  await shoot(page, "not-available", widths);
});

test("screenshots of the platform console's sport configuration", async ({ page }) => {
  await signIn(page, platformAdmin.email, platformAdmin.password);
  await expect(page.getByRole("heading", { level: 1, name: "Platform console" })).toBeVisible();
  // The console's own entries (S05 D17), below Plans; the pages link within the console.
  await page.getByRole("link", { name: "Sports" }).first().click();
  await expect(page).toHaveURL(/\/platform\/sports$/);
  await expect(page.getByRole("heading", { level: 1, name: "Sports" })).toBeVisible();
  await expect(loading(page)).toHaveCount(0);
  await shoot(page, "platform-sports", [phone, ...widths]);

  await page.getByRole("table").getByRole("link").first().click();
  await expect(page).toHaveURL(/\/platform\/sports\/[^/]+$/);
  await expect(page.getByRole("tab", { name: "Overview", selected: true })).toBeVisible();
  // The console has no time zone: its dates are in UTC and say so.
  await expect(page.getByText(/published .* \(UTC\)/)).toBeVisible();
  await shoot(page, "platform-sport-overview", [phone, ...widths]);
  await page.getByRole("tab", { name: "Scorer pad" }).click();
  await expect(page.getByRole("tab", { name: "Scorer pad", selected: true })).toBeVisible();
  await shoot(page, "platform-sport-scorer-pad", [phone, ...widths]);

  await page.getByRole("link", { name: "Formats" }).first().click();
  await expect(page.getByRole("heading", { level: 1, name: "Formats" })).toBeVisible();
  await expect(loading(page)).toHaveCount(0);
  await shoot(page, "platform-formats", [phone, ...widths]);

  await page.getByRole("table").getByRole("link", { name: "League (round robin)" }).click();
  await expect(page).toHaveURL(/\/platform\/formats\/[^/]+$/);
  await expect(page.getByRole("heading", { level: 2, name: "Parameters" })).toBeVisible();
  await shoot(page, "platform-format", [phone, ...widths]);

  await page.getByRole("link", { name: "Rules templates" }).first().click();
  await expect(page.getByRole("heading", { level: 1, name: "Rules templates" })).toBeVisible();
  await expect(loading(page)).toHaveCount(0);
  await shoot(page, "platform-rules-templates", [phone, ...widths]);

  await page.getByRole("table").getByRole("link").first().click();
  await expect(page).toHaveURL(/\/platform\/rules-templates\/[^/]+$/);
  await expect(page.getByRole("heading", { level: 2, name: "Tiebreakers" })).toBeVisible();
  await expect(loading(page)).toHaveCount(0);
  await shoot(page, "platform-rules-template", [phone, ...widths]);
});

test("screenshots of the platform console's tenants", async ({ page, request }) => {
  await signInByApi(request, platformAdmin.email, platformAdmin.password);
  const kmsa = await tenantId(request, "kmsa");
  await signIn(page, platformAdmin.email, platformAdmin.password);
  await expect(page.getByRole("heading", { level: 1, name: "Platform console" })).toBeVisible();

  await page.goto("/platform/tenants");
  await expect(page.getByRole("heading", { level: 1, name: "Tenants" })).toBeVisible();
  await expect(page.getByRole("row").nth(1)).toBeVisible();
  await expect(loading(page)).toHaveCount(0);
  await shoot(page, "tenants", [phone, ...widths]);

  await page.getByRole("button", { name: "New tenant" }).click();
  const create = page.getByRole("dialog");
  await expect(create.getByRole("option", { name: "Uganda", exact: true })).toBeAttached();
  await create.getByRole("button", { name: "Create tenant" }).click();
  await expect(create.getByText("Choose a country.")).toBeVisible();
  await shoot(page, "tenant-create", [phone, ...widths]);
  await page.keyboard.press("Escape");

  await page.goto(`/platform/tenants/${kmsa}`);
  await expect(page.getByRole("heading", { level: 1, name: "Kampala Metro Sports Association" })).toBeVisible();
  await expect(page.getByText("Uganda")).toBeVisible();
  // With its Subscription panel (S04-T4a): the demo period on STANDARD.
  await expect(page.getByRole("heading", { name: "Current period" })).toBeVisible();
  await shoot(page, "tenant-detail", [phone, ...widths]);

  // The dialogs only: the demo tenant stays active for the other tests.
  await page.getByRole("button", { name: "Suspend" }).click();
  await expect(page.getByRole("dialog").getByLabel("Reason")).toBeVisible();
  await shoot(page, "tenant-reason-dialog", [phone, ...widths]);
  await page.keyboard.press("Escape");

  await page.getByRole("button", { name: "Invite an owner" }).click();
  await expect(page.getByRole("dialog").getByLabel("Email")).toBeVisible();
  await shoot(page, "owner-invite-dialog", [phone, ...widths]);
  await page.keyboard.press("Escape");

  await page.getByRole("button", { name: "Assign a period" }).click();
  await expect(page.getByRole("dialog").getByLabel("Last day")).toBeVisible();
  await shoot(page, "subscription-assign-dialog", [phone, ...widths]);
  await page.keyboard.press("Escape");

  await page.getByRole("button", { name: "Set an override" }).click();
  await page.getByRole("dialog").getByLabel("Entitlement").selectOption("MAX_STAFF_USERS");
  await expect(page.getByRole("dialog").getByLabel("Limit (users)")).toBeVisible();
  await shoot(page, "subscription-override-dialog", [phone, ...widths]);
  await page.keyboard.press("Escape");
});

test("screenshots of a tenant workspace", async ({ page, request }) => {
  // A tenant of this run's own (S06-T7a), so the owner added doesn't count against KMSA's staff limit.
  const tenant = await ownTenant(request, "cassia-games", "Cassia Community Games");
  const email = personEmail("grace.nakato", tenant.slug);
  const password = newPassword();
  await acceptByApi(request, await inviteOwnerOf(request, tenant.id, email), password);
  await signIn(page, email, password);
  await expect(page.getByRole("heading", { level: 1, name: tenant.name })).toBeVisible();
  await shoot(page, "tenant-workspace", [phone, ...widths]);

  await page.getByRole("button", { name: `Workspace: ${tenant.name}` }).click();
  await expect(page.getByRole("menu")).toBeVisible();
  await shoot(page, "workspace-switcher", widths);
  await page.keyboard.press("Escape");

  await page.setViewportSize(phone);
  await page.getByRole("button", { name: "Open navigation" }).click();
  await expect(page.getByRole("dialog")).toBeVisible();
  await shoot(page, "navigation-sheet", [phone]);
  await page.keyboard.press("Escape");

  await page.goto(`/t/${tenant.slug}/settings/organisation`);
  await expect(page.getByRole("heading", { level: 1, name: "Organisation" })).toBeVisible();
  await expect(page.getByLabel("Time zone")).toHaveValue("Africa/Kampala");
  await expect(loading(page)).toHaveCount(0);
  await shoot(page, "organisation", [phone, ...widths]);

  await page.goto(`/t/${tenant.slug}/settings/members`);
  await expect(page.getByRole("heading", { level: 1, name: "Members & roles" })).toBeVisible();
  await expect(page.getByRole("row").filter({ hasText: email })).toBeVisible();
  await expect(loading(page)).toHaveCount(0);
  await shoot(page, "members", [phone, ...widths]);

  await page.goto(`/t/${tenant.slug}/sports`);
  await expect(page.getByRole("heading", { level: 1, name: "Sports" })).toBeVisible();
  await expect(loading(page)).toHaveCount(0);
  await shoot(page, "sports", [phone, ...widths]);

  await page.getByRole("table").getByRole("link").first().click();
  await expect(page.getByRole("tab", { name: "Overview", selected: true })).toBeVisible();
  await shoot(page, "sport-overview", [phone, ...widths]);
  for (const [tab, name] of [
    ["Periods", "sport-periods"],
    ["Events", "sport-events"],
    ["Positions & roles", "sport-positions"],
    ["Statistics", "sport-statistics"],
    ["Scorer pad", "sport-scorer-pad"],
  ] as const) {
    await page.getByRole("tab", { name: tab }).click();
    await expect(page.getByRole("tab", { name: tab, selected: true })).toBeVisible();
    await shoot(page, name, [phone, ...widths]);
  }

  await page.goto(`/t/${tenant.slug}/formats`);
  await expect(page.getByRole("heading", { level: 1, name: "Formats" })).toBeVisible();
  await expect(loading(page)).toHaveCount(0);
  await shoot(page, "formats", [phone, ...widths]);

  await page.getByRole("table").getByRole("link", { name: "League (round robin)" }).click();
  await expect(page.getByRole("heading", { level: 2, name: "Parameters" })).toBeVisible();
  await shoot(page, "format", [phone, ...widths]);

  await page.goto(`/t/${tenant.slug}/rules-templates`);
  await expect(page.getByRole("heading", { level: 1, name: "Rules templates" })).toBeVisible();
  await expect(loading(page)).toHaveCount(0);
  await shoot(page, "rules-templates", [phone, ...widths]);

  await page.getByRole("table").getByRole("link").first().click();
  await expect(page.getByRole("heading", { level: 2, name: "Tiebreakers" })).toBeVisible();
  await expect(loading(page)).toHaveCount(0);
  await shoot(page, "rules-template", [phone, ...widths]);

  await page.goto("/platform");
  await expect(page.getByRole("heading", { level: 1, name: "You can't open this page" })).toBeVisible();
  await shoot(page, "forbidden", [phone, ...widths]);

  await page.goto("/t/no-such-tenant");
  await expect(page.getByRole("heading", { level: 1, name: "Page not found" })).toBeVisible();
  await shoot(page, "not-found", [phone, ...widths]);
});

test("screenshots of a suspended tenant", async ({ page, request }) => {
  const run = process.env.MSP_E2E_RUN!;
  const slug = `acacia-schools-${run}`;
  const id = await createTenant(request, slug, `Acacia Schools Netball Circle ${run}`);
  const email = personEmail("harriet.akello", slug);
  const password = newPassword();
  await acceptByApi(request, await inviteOwnerOf(request, id, email), password);
  await suspendTenant(request, id, "Subscription unpaid since September");

  await signIn(page, email, password);
  await expect(page.getByText("Subscription unpaid since September")).toBeVisible();
  await shoot(page, "tenant-suspended", [phone, ...widths]);
});

test("screenshots of the people screens", async ({ page, request }) => {
  // Four people made through the API on a tenant of this run's own (the demo data is T7d's): one with a photo and
  // all three profiles, a minor with her mother's consent, four name parts without a date of birth or a photo, and
  // an adult with nothing more.
  test.slow();
  const tenant = await ownTenant(request, "bee-eater-sports", "Bee-eater Sports Club");
  const owner = { email: personEmail("brian.ssemakula", tenant.slug), password: newPassword() };
  await acceptByApi(request, await inviteOwnerOf(request, tenant.id, owner.email), owner.password);
  await signInByApi(request, owner.email, owner.password);
  const member = { tenantId: tenant.id };
  const sport = await enableFirstSport(request, member);

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
    otherPositions: sport.positions.slice(1, 2),
    dominantSide: "RIGHT",
  });
  denis = await changePerson(request, member, denis, "staff-profiles", {
    capabilities: [{ variant: sport.variant, role: sport.staffRole, qualification: "Level 1 coaching certificate" }],
  });
  denis = await changePerson(request, member, denis, "official-profiles", {
    capabilities: [
      { variant: sport.variant, role: sport.officialRole, grade: "Class 2", licenceNumber: "BSC-R-0193", licenceValidUntil: "2027-06-30" },
    ],
  });
  denis = await changePerson(request, member, denis, "external-identifiers", {
    authority: "BEE_EATER_REGISTRATION",
    value: `KM-${process.env.MSP_E2E_RUN}-0147`,
    validFrom: "2026-01-15",
  });
  denis = await changePerson(request, member, denis, "aliases", { name: "Opoka Denis Okumu", kind: "FORMER_NAME" });
  await setPhoto(request, member, denis);

  const brenda = await createPerson(request, member, {
    firstName: "Brenda",
    lastName: "Akello",
    gender: "FEMALE",
    dateOfBirth: "2011-03-14",
    nationality: "UG",
  });
  await changePerson(request, member, brenda, "consents", {
    guardianName: "Grace Akello",
    relationship: "MOTHER",
    contact: "+256 772 000 147",
    scopes: ["DATA_PROCESSING", "PUBLIC_PROFILE", "PHOTO"],
    givenOn: "2026-11-02",
  });
  const nakato = await createPerson(request, member, {
    firstName: "Nakato",
    middleName: "Irene",
    otherNames: "Nalubega",
    lastName: "Namaganda",
    displayName: "Nakato Irene Nalubega Namaganda",
    gender: "FEMALE",
  });
  const john = await createPerson(request, member, { firstName: "John", lastName: "Ssekandi", gender: "MALE", dateOfBirth: "1998-08-21" });

  await signIn(page, owner.email, owner.password);
  await expect(page.getByRole("heading", { level: 1, name: tenant.name })).toBeVisible();
  await page.getByRole("navigation", { name: "Workspace" }).getByRole("link", { name: "People" }).click();
  await expect(page.getByRole("heading", { level: 1, name: "People" })).toBeVisible();
  await expect(page.getByRole("link", { name: "Nakato Irene Nalubega Namaganda" })).toBeVisible();
  await expect(loading(page)).toHaveCount(0);
  await shoot(page, "people", [phone, ...widths]);

  await page.getByLabel("Search").fill("Akello");
  await expect(page).toHaveURL(/\?q=Akello$/);
  await expect(page.getByRole("link", { name: "John Ssekandi" })).toHaveCount(0);
  await shoot(page, "people-search", [phone]);

  await page.getByRole("link", { name: "Brenda Akello" }).click();
  await expect(page.getByRole("heading", { level: 1, name: "Brenda Akello" })).toBeVisible();
  // The owner holds person.read_private: the date of birth, not the year.
  await expect(page.getByText("14 Mar 2011")).toBeVisible();
  await expect(loading(page)).toHaveCount(0);
  await shoot(page, "person-minor", [phone, ...widths]);
  await page.getByRole("tab", { name: "Consent" }).click();
  await expect(page.getByText("Grace Akello")).toBeVisible();
  await shoot(page, "person-consent", [phone, ...widths]);

  await page.goto(`/t/${tenant.slug}/people/${denis.id}`);
  await expect(page.getByRole("img", { name: "Photo of Denis Opoka" })).toBeVisible();
  await expect(loading(page)).toHaveCount(0);
  await shoot(page, "person", [phone, ...widths]);
  await page.getByRole("tab", { name: "Roles" }).click();
  await expect(page.getByRole("heading", { level: 4, name: "Staff profile" })).toBeVisible();
  await shoot(page, "person-roles", [phone, ...widths]);
  await page.getByRole("tab", { name: "External IDs & aliases" }).click();
  await expect(page.getByText("Opoka Denis Okumu").first()).toBeVisible();
  await shoot(page, "person-identifiers", [phone, ...widths]);

  await page.goto(`/t/${tenant.slug}/people/${nakato.id}?tab=roles`);
  await expect(page.getByText("No roles yet")).toBeVisible();
  await expect(page.getByRole("img", { name: "No photo of Nakato Irene Nalubega Namaganda" })).toBeVisible();
  await shoot(page, "person-no-roles", [phone, ...widths]);

  // Add person (T7b): the name with the person it finds, the details, and the review with Denis as a candidate.
  await page.goto(`/t/${tenant.slug}/people/new`);
  await page.getByLabel("First name").fill("Denis");
  await page.getByLabel("Last name").fill("Opoka");
  await expect(page.getByRole("link", { name: "Denis Opoka" })).toBeVisible();
  await shoot(page, "add-person-name", [phone, ...widths]);
  await page.getByRole("button", { name: "Continue" }).click();
  await page.getByLabel("Date of birth (optional)").fill("1991-05-02");
  await page.getByRole("button", { name: "Add an identifier" }).click();
  await expect(loading(page)).toHaveCount(0);
  await shoot(page, "add-person-details", [phone, ...widths]);
  await page.getByRole("button", { name: "Remove" }).click();
  await page.getByRole("button", { name: "Check and add" }).click();
  await expect(page.getByRole("list", { name: "People who may match" }).getByText("Same date of birth")).toBeVisible();
  await expect(page.getByText("No sport recorded")).toHaveCount(0);
  await shoot(page, "add-person-review", [phone, ...widths]);

  // Roles (T7c-1a): adding an athlete and an official profile, changing a staff profile, and a status move's reason.
  await page.goto(`/t/${tenant.slug}/people/${nakato.id}?tab=roles`);
  await page.getByRole("button", { name: "Add athlete profile" }).click();
  await expect(page.getByRole("dialog").getByRole("checkbox").first()).toBeVisible();
  await shoot(page, "athlete-profile-dialog", [phone, ...widths]);
  await page.getByRole("dialog").getByRole("button", { name: "Cancel" }).click();
  await page.getByRole("button", { name: "Add official profile" }).click();
  await expect(page.getByRole("dialog").getByLabel("Licence valid until (optional)")).toBeVisible();
  await shoot(page, "official-profile-dialog", [phone, ...widths]);
  await page.getByRole("dialog").getByRole("button", { name: "Cancel" }).click();
  await page.goto(`/t/${tenant.slug}/people/${denis.id}?tab=roles`);
  await page.getByRole("button", { name: "Edit: Staff profile" }).click();
  await page.getByRole("dialog").getByRole("button", { name: "Add another role" }).click();
  await shoot(page, "staff-profile-dialog", [phone, ...widths]);
  await page.getByRole("dialog").getByRole("button", { name: "Cancel" }).click();
  await page.getByRole("button", { name: /^Retire: .* athlete profile$/ }).click();
  await expect(page.getByRole("dialog").getByLabel("Reason")).toBeVisible();
  await shoot(page, "profile-status-dialog", [phone, ...widths]);
  await page.getByRole("dialog").getByRole("button", { name: "Cancel" }).click();

  // Consent (T7c-1b): recording a guardian's consent (Nakato's age is unknown), withdrawing part of Brenda's.
  await page.goto(`/t/${tenant.slug}/people/${nakato.id}?tab=consent`);
  await page.getByRole("button", { name: "Record consent" }).click();
  await expect(page.getByRole("dialog").getByText(/Placeholder wording/)).toBeVisible();
  await shoot(page, "consent-dialog", [phone, ...widths]);
  await page.getByRole("dialog").getByRole("button", { name: "Cancel" }).click();
  await page.goto(`/t/${tenant.slug}/people/${brenda.id}?tab=consent`);
  await page.getByRole("button", { name: /^Withdraw: / }).click();
  await expect(page.getByRole("dialog").getByLabel("Reason")).toBeVisible();
  await shoot(page, "withdraw-consent-dialog", [phone, ...widths]);
  await page.getByRole("dialog").getByRole("button", { name: "Cancel" }).click();

  // Photo, identifiers and aliases (T7c-2a): adding a photo for the long name without one (its preview shown), adding
  // an identifier and an alias to Denis.
  await page.goto(`/t/${tenant.slug}/people/${nakato.id}`);
  await page.getByRole("button", { name: "Add photo" }).click();
  await page
    .getByRole("dialog")
    .getByLabel("Photo")
    .setInputFiles({ name: "nakato.png", mimeType: "image/png", buffer: placeholderPng(320) });
  await expect(page.getByRole("dialog").getByRole("img", { name: "The chosen photo" })).toBeVisible();
  await shoot(page, "photo-dialog", [phone, ...widths]);
  await page.getByRole("dialog").getByRole("button", { name: "Cancel" }).click();
  await page.goto(`/t/${tenant.slug}/people/${denis.id}?tab=identifiers`);
  await page.getByRole("button", { name: "Add identifier" }).click();
  await expect(page.getByRole("dialog").getByLabel("Valid from (optional)")).toBeEnabled();
  await shoot(page, "identifier-dialog", [phone, ...widths]);
  await page.getByRole("dialog").getByRole("button", { name: "Cancel" }).click();
  await page.getByRole("button", { name: "Add alias" }).click();
  await expect(page.getByRole("dialog").getByLabel("Kind")).toBeVisible();
  await shoot(page, "alias-dialog", [phone, ...widths]);
  await page.getByRole("dialog").getByRole("button", { name: "Cancel" }).click();

  // Edit details and the status moves (T7c-2b): the long name's form, a change that the check finds Denis for, and
  // the archive dialog's reason.
  await page.goto(`/t/${tenant.slug}/people/${nakato.id}/edit`);
  await expect(page.getByLabel("Other names (optional)")).toHaveValue("Nalubega");
  await expect(loading(page)).toHaveCount(0);
  await shoot(page, "edit-person", [phone, ...widths]);
  await page.goto(`/t/${tenant.slug}/people/${john.id}/edit`);
  await page.getByLabel("First name").fill("Denis");
  await page.getByLabel("Last name").fill("Opoka");
  await page.getByLabel("Date of birth (optional)").fill("1991-05-02");
  await page.getByRole("button", { name: "Save", exact: true }).click();
  await expect(page.getByRole("list", { name: "People who may match" }).getByText("Same date of birth")).toBeVisible();
  await shoot(page, "edit-person-review", [phone, ...widths]);
  await page.goto(`/t/${tenant.slug}/people/${nakato.id}`);
  await page.getByRole("button", { name: "Archive", exact: true }).click();
  await expect(page.getByRole("dialog").getByLabel("Reason")).toBeVisible();
  await shoot(page, "person-status-dialog", [phone, ...widths]);
});

test("screenshots of the venue screens", async ({ page, request }) => {
  // Two venues and a home team made through the API on a tenant of this run's own (the demo data is S07-T7's): a
  // stadium with coordinates and the tenant's first sport, a ground with only what is required.
  test.slow();
  const tenant = await ownTenant(request, "hoopoe-sports", "Hoopoe Sports Club");
  const owner = { email: personEmail("brian.ssemakula", tenant.slug), password: newPassword() };
  await acceptByApi(request, await inviteOwnerOf(request, tenant.id, owner.email), owner.password);
  await signInByApi(request, owner.email, owner.password);
  const headers = { "X-CSRF": "1", "X-Tenant-Id": tenant.id };
  const sport = await enableFirstSport(request, { tenantId: tenant.id });
  const post = async (path: string, data: Record<string, unknown>) => {
    const created = await request.post(path, { data, headers });
    expect(created.status(), `${path}: ${await created.text()}`).toBe(201);
    return (await created.json()) as { id: string };
  };
  const stadium = await post("/api/admin/v1/venues", {
    name: "Bukoto Community Stadium",
    shortName: "Bukoto Stadium",
    type: "STADIUM",
    addressLine: "Plot 12, Bukoto Street",
    city: "Kampala",
    region: "Central",
    latitude: 0.3476,
    longitude: 32.5925,
    timeZone: "Africa/Kampala",
    capacity: 3000,
    surfaces: ["NATURAL_GRASS"],
    supportedVariants: [sport.variant],
  });
  await post("/api/admin/v1/venues", { name: "Kisaasi Recreation Ground", type: "SPORTS_GROUND", city: "Kampala", timeZone: "Africa/Kampala" });
  for (const [path, data] of [
    [`/api/admin/v1/venues/${stadium.id}/aliases`, { name: "Bukoto Grounds", kind: "FORMER_NAME" }],
    [`/api/admin/v1/venues/${stadium.id}/external-identifiers`, { authority: "METRO_COUNCIL_NO", value: "MC-0042" }],
  ] as const) {
    const added = await request.post(path, { data, headers });
    expect(added.status(), `${path}: ${await added.text()}`).toBe(200);
  }
  await post("/api/admin/v1/teams", {
    variant: sport.variant,
    name: "Bukoto Comets FC",
    genderCategory: "WOMEN",
    ageGroup: "U17",
    homeVenueId: stadium.id,
  });

  await signIn(page, owner.email, owner.password);
  await expect(page.getByRole("heading", { level: 1, name: tenant.name })).toBeVisible();
  await page.getByRole("navigation", { name: "Workspace" }).getByRole("link", { name: "Venues" }).click();
  await expect(page.getByRole("heading", { level: 1, name: "Venues" })).toBeVisible();
  await expect(page.getByRole("link", { name: "Kisaasi Recreation Ground" })).toBeVisible();
  await expect(loading(page)).toHaveCount(0);
  await shoot(page, "venues", [phone, ...widths]);

  await page.getByRole("link", { name: "Bukoto Community Stadium" }).click();
  await expect(page.getByRole("link", { name: /Open in a map/ })).toBeVisible();
  await expect(loading(page)).toHaveCount(0);
  await shoot(page, "venue-overview", [phone, ...widths]);
  await page.getByRole("tab", { name: "Teams" }).click();
  await expect(page.getByText("Bukoto Comets FC")).toBeVisible();
  await shoot(page, "venue-teams", [phone, ...widths]);
  await page.getByRole("tab", { name: "History" }).click();
  await expect(page.getByText("Recorded as active")).toBeVisible();
  await shoot(page, "venue-history", [phone, ...widths]);
  await page.getByRole("button", { name: "Close venue" }).click();
  await expect(page.getByRole("dialog").getByLabel("Reason")).toBeVisible();
  await shoot(page, "venue-status-dialog", [phone, ...widths]);
  await page.keyboard.press("Escape");
  await expect(page.getByRole("dialog")).toHaveCount(0);

  await page.getByRole("tab", { name: "Names & IDs" }).click();
  await expect(page.getByText("MC-0042").first()).toBeVisible();
  await shoot(page, "venue-names", [phone, ...widths]);
  await page.getByRole("button", { name: "Add identifier" }).click();
  await expect(page.getByRole("dialog").getByLabel("Issued by (code)")).toBeVisible();
  await shoot(page, "venue-identifier-dialog", [phone, ...widths]);
  await page.keyboard.press("Escape");

  await page.getByRole("link", { name: "Edit details" }).click();
  await expect(page.getByRole("heading", { level: 1, name: "Edit details" })).toBeVisible();
  await expect(page.getByLabel("Web address")).toHaveValue("bukoto-community-stadium");
  await shoot(page, "venue-edit", [phone, ...widths]);

  // Add venue with a name the tenant already has: the live warning lists it.
  await page.goto(`/t/${tenant.slug}/venues/new`);
  await page.getByLabel("Name", { exact: true }).fill("Kisaasi Recreation Ground");
  await page.getByLabel("City or town").fill("Kampala");
  const warning = page.getByRole("region", { name: "Is this a venue you already have?" });
  await expect(warning.getByRole("link", { name: "Open Kisaasi Recreation Ground" })).toBeVisible();
  await warning.scrollIntoViewIfNeeded();
  await shoot(page, "add-venue-duplicates", [phone, ...widths]);
});
