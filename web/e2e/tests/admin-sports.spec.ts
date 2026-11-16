import { expect, test } from "@playwright/test";
import { acceptByApi, inviteMember, inviteOwner, newPassword, personEmail, signIn, signInByApi, myTenantId } from "./support/accounts";

// Tenant workspace → Sports (S05 D17) against the real API and the seeded definitions: every tenant role except Team
// manager (no actions yet) reads them, so a scorer opens the list, a variant and each of its tabs. Labels come from
// the seeded definition; the test reads the variant's name from the list instead of naming a sport.

test("a scorer reads a sport's definition, tab by tab", async ({ page, request }) => {
  const owner = { email: personEmail("ruth.akello", "kmsa"), password: newPassword() };
  await acceptByApi(request, await inviteOwner(request, "kmsa", owner.email), owner.password);
  await signInByApi(request, owner.email, owner.password);
  const tenantId = await myTenantId(request, "kmsa");

  const scorer = { email: personEmail("isaac.okello", "kmsa"), password: newPassword() };
  await acceptByApi(request, await inviteMember(request, { ...owner, tenantId }, scorer.email, ["SCORER"]), scorer.password);
  await signIn(page, scorer.email, scorer.password);
  await expect(page.getByRole("heading", { level: 1, name: "Kampala Metro Sports Association" })).toBeVisible();

  await page.getByRole("navigation").getByRole("link", { name: "Sports" }).first().click();
  await expect(page.getByRole("heading", { level: 1, name: "Sports" })).toBeVisible();
  const first = page.getByRole("table").getByRole("link").first();
  const name = (await first.textContent())!;
  await first.click();

  await expect(page.getByRole("heading", { level: 1, name })).toBeVisible();
  await expect(page.getByText(/version 1 · published/)).toBeVisible();
  await expect(page.getByRole("tab", { name: "Overview", selected: true })).toBeVisible();
  await expect(page.getByRole("heading", { name: "Terminology" })).toBeVisible();

  for (const [tab, value] of [
    ["Periods", "periods"],
    ["Events", "events"],
    ["Positions & roles", "positions"],
    ["Statistics", "statistics"],
    ["Scorer pad", "scorer-pad"],
  ] as const) {
    await page.getByRole("tab", { name: tab }).click();
    await expect(page.getByRole("tab", { name: tab, selected: true })).toBeVisible();
    await expect(page).toHaveURL(new RegExp(`[?&]tab=${value}`));
  }

  // The pad is drawn from the definition: groups with labelled buttons, nothing to press.
  const panel = page.getByRole("tabpanel");
  await expect(panel.getByRole("region").first()).toBeVisible();
  expect(await panel.getByRole("listitem").count()).toBeGreaterThan(0);
  await expect(panel.getByRole("button")).toHaveCount(0);

  // A linked tab opens as linked.
  await page.reload();
  await expect(page.getByRole("tab", { name: "Scorer pad", selected: true })).toBeVisible();

  await page.goto("/t/kmsa/sports/NO_SUCH_VARIANT");
  await expect(page.getByRole("heading", { level: 1, name: "Page not found" })).toBeVisible();
});

// Tenant workspace → Formats (S05 D17) against the seeded formats: a registrar (every tenant role except Team
// manager reads them) opens the list and a format with its stages and parameters. Formats are generic, so the test
// names the seeded ones.
test("a registrar reads the formats and a format's parameters", async ({ page, request }) => {
  const owner = { email: personEmail("peter.lubega", "kmsa"), password: newPassword() };
  await acceptByApi(request, await inviteOwner(request, "kmsa", owner.email), owner.password);
  await signInByApi(request, owner.email, owner.password);
  const tenantId = await myTenantId(request, "kmsa");

  const registrar = { email: personEmail("moses.kato", "kmsa"), password: newPassword() };
  await acceptByApi(request, await inviteMember(request, { ...owner, tenantId }, registrar.email, ["REGISTRAR"]), registrar.password);
  await signIn(page, registrar.email, registrar.password);
  await expect(page.getByRole("heading", { level: 1, name: "Kampala Metro Sports Association" })).toBeVisible();

  await page.getByRole("navigation").getByRole("link", { name: "Formats" }).first().click();
  await expect(page.getByRole("heading", { level: 1, name: "Formats" })).toBeVisible();
  for (const name of ["Single contest", "League (round robin)", "Knockout"]) {
    await expect(page.getByRole("table").getByRole("link", { name })).toBeVisible();
  }

  await page.getByRole("table").getByRole("link", { name: "League (round robin)" }).click();
  await expect(page.getByRole("heading", { level: 1, name: "League (round robin)" })).toBeVisible();
  await expect(page.getByText(/Version 1 · published/)).toBeVisible();
  await expect(page.getByRole("list", { name: "Stages" }).getByRole("listitem")).toHaveText([
    "League: league stage, drawn by the round robin generator",
  ]);
  const cycles = page.getByRole("row").filter({ has: page.getByRole("rowheader", { name: /^Cycles/ }) });
  await expect(cycles.getByRole("cell")).toHaveText(["Whole number", "1", "1 to 4"]);

  await page.getByRole("main").getByRole("link", { name: "Formats" }).click();
  await expect(page.getByRole("heading", { level: 1, name: "Formats" })).toBeVisible();

  await page.goto("/t/kmsa/formats/NO_SUCH_FORMAT");
  await expect(page.getByRole("heading", { level: 1, name: "Page not found" })).toBeVisible();
});

// Tenant workspace → Rules templates (S05 D17) against the seeded templates: a viewer (every tenant role except Team
// manager reads them) opens the list, a template's parameters, rules and tiebreakers, and its pinned sport version.
// The test reads the template's name from the list instead of naming a sport.
test("a viewer reads a rules template and opens its sport", async ({ page, request }) => {
  const owner = { email: personEmail("sarah.nabirye", "kmsa"), password: newPassword() };
  await acceptByApi(request, await inviteOwner(request, "kmsa", owner.email), owner.password);
  await signInByApi(request, owner.email, owner.password);
  const tenantId = await myTenantId(request, "kmsa");

  const viewer = { email: personEmail("daniel.ssempijja", "kmsa"), password: newPassword() };
  await acceptByApi(request, await inviteMember(request, { ...owner, tenantId }, viewer.email, ["VIEWER"]), viewer.password);
  await signIn(page, viewer.email, viewer.password);
  await expect(page.getByRole("heading", { level: 1, name: "Kampala Metro Sports Association" })).toBeVisible();

  await page.getByRole("navigation").getByRole("link", { name: "Rules templates" }).first().click();
  await expect(page.getByRole("heading", { level: 1, name: "Rules templates" })).toBeVisible();
  const first = page.getByRole("table").getByRole("link").first();
  const name = (await first.textContent())!;
  await first.click();

  await expect(page.getByRole("heading", { level: 1, name })).toBeVisible();
  await expect(page.getByText(/Version 1 · published/)).toBeVisible();
  for (const heading of ["Overview", "Parameters", "Rules", "Tiebreakers"]) {
    await expect(page.getByRole("heading", { level: 2, name: heading })).toBeVisible();
  }
  await expect(page.getByRole("row").filter({ has: page.getByRole("rowheader", { name: /^Tiebreakers/ }) })).toBeVisible();
  expect(await page.getByRole("list", { name: "Period lengths" }).getByRole("listitem").count()).toBeGreaterThan(0);
  expect(await page.getByRole("list", { name: "Tiebreakers in order" }).getByRole("listitem").count()).toBeGreaterThan(0);

  // The template pins its sport definition's version; the link opens that version.
  await page.getByRole("link", { name: /, version 1$/ }).click();
  await expect(page).toHaveURL(/\/t\/kmsa\/sports\/[A-Z_]+\?version=1$/);
  await expect(page.getByRole("tab", { name: "Overview", selected: true })).toBeVisible();

  await page.goto("/t/kmsa/rules-templates/NO_SUCH_TEMPLATE");
  await expect(page.getByRole("heading", { level: 1, name: "Page not found" })).toBeVisible();
});
