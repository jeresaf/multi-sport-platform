import { expect, test } from "@playwright/test";
import { acceptByApi, inviteMember, inviteOwner, newPassword, personEmail, signIn, signInByApi, myTenantId } from "./support/accounts";

// Tenant workspace → Sports (S05 D17) against the real API and the seeded definitions: every tenant role reads them,
// so a scorer opens the list, a variant and each of its tabs. Labels come from the seeded definition; the test reads
// the variant's name from the list instead of naming a sport.

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
