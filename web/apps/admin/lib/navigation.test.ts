import { describe, expect, it } from "vitest";
import { currentItem, platformNavigation, tenantNavigation, visibleNavigation } from "./navigation";

const labels = (items: { label: string }[]) => items.map((item) => item.label);

describe("tenant navigation", () => {
  const items = tenantNavigation("kmsa");

  it("shows an owner every entry", () => {
    const owner = ["tenant.settings.read", "tenant.settings.update", "membership.read", "membership.invite"];
    expect(labels(visibleNavigation(items, owner))).toEqual(["Overview", "Organisation", "Members & roles"]);
  });

  it("shows a viewer only what their actions allow", () => {
    expect(labels(visibleNavigation(items, ["tenant.settings.read"]))).toEqual(["Overview", "Organisation"]);
    expect(labels(visibleNavigation(items, []))).toEqual(["Overview"]);
  });

  it("finds the entry a path belongs to; the overview only on its own path", () => {
    expect(currentItem(items, "/t/kmsa")?.label).toBe("Overview");
    expect(currentItem(items, "/t/kmsa/settings/members")?.label).toBe("Members & roles");
    expect(currentItem(items, "/t/kmsa/settings/members/0199a3f0")?.label).toBe("Members & roles");
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
