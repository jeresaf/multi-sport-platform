import { describe, expect, it } from "vitest";
import { currentItem, platformNavigation, tenantNavigation, visibleNavigation } from "./navigation";

const labels = (items: { label: string }[]) => items.map((item) => item.label);

describe("tenant navigation", () => {
  const items = tenantNavigation("kmsa");

  it("shows an owner every entry", () => {
    const owner = [
      "tenant.settings.read",
      "tenant.settings.update",
      "membership.read",
      "membership.invite",
      "tenant.subscription.read",
      "sport.read",
      "format.read",
      "ruleset.read",
      "tenant.sport.read",
    ];
    expect(labels(visibleNavigation(items, owner))).toEqual([
      "Overview",
      "Sports",
      "Formats",
      "Rules templates",
      "Organisation",
      "Members & roles",
      "Enabled sports",
      "Subscription",
    ]);
  });

  it("shows a viewer only what their actions allow", () => {
    expect(labels(visibleNavigation(items, ["tenant.settings.read"]))).toEqual(["Overview", "Organisation"]);
    expect(labels(visibleNavigation(items, ["tenant.subscription.read"]))).toEqual(["Overview", "Subscription"]);
    expect(labels(visibleNavigation(items, ["sport.read", "tenant.settings.read"]))).toEqual(["Overview", "Sports", "Organisation"]);
    expect(labels(visibleNavigation(items, ["format.read"]))).toEqual(["Overview", "Formats"]);
    expect(labels(visibleNavigation(items, ["ruleset.read"]))).toEqual(["Overview", "Rules templates"]);
    expect(labels(visibleNavigation(items, ["tenant.sport.read"]))).toEqual(["Overview", "Enabled sports"]);
    expect(labels(visibleNavigation(items, []))).toEqual(["Overview"]);
  });

  it("finds the entry a path belongs to; the overview only on its own path", () => {
    expect(currentItem(items, "/t/kmsa")?.label).toBe("Overview");
    expect(currentItem(items, "/t/kmsa/settings/members")?.label).toBe("Members & roles");
    expect(currentItem(items, "/t/kmsa/settings/members/0199a3f0")?.label).toBe("Members & roles");
    expect(currentItem(items, "/t/kmsa/sports/A_VARIANT")?.label).toBe("Sports");
    expect(currentItem(items, "/t/kmsa/settings/sports")?.label).toBe("Enabled sports");
    expect(currentItem(items, "/t/kmsa/formats/A_FORMAT")?.label).toBe("Formats");
    expect(currentItem(items, "/t/kmsa/rules-templates/A_TEMPLATE")?.label).toBe("Rules templates");
    expect(currentItem(items, "/t/kmsa/settings/membership")).toBeUndefined();
    expect(currentItem(items, "/t/kmsa/elsewhere")).toBeUndefined();
  });
});

describe("platform navigation", () => {
  it("shows support staff the read screens they hold", () => {
    const support = ["tenant.read", "plan.read", "platform.audit.read"];
    expect(labels(visibleNavigation(platformNavigation(), support))).toEqual(["Overview", "Tenants", "Plans", "Audit"]);
  });
});
