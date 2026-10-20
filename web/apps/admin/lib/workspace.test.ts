import { afterEach, describe, expect, it, vi } from "vitest";
import { landingPath, readLastWorkspace, rememberWorkspace, type Me, type MeTenant } from "./workspace";

const tenant = (slug: string, status: MeTenant["status"]): MeTenant => ({
  tenantId: `0199a3f0-0000-7000-8000-${slug.padStart(12, "0")}`,
  slug,
  displayName: slug.toUpperCase(),
  status,
  roles: ["TENANT_OWNER"],
});

const me = (tenants: MeTenant[], platformRoles: string[] = []): Me => ({
  id: "0199a3f0-0000-7000-8000-000000000001",
  email: "grace.nakato@kmsa.example",
  platformRoles,
  tenants,
});

afterEach(() => {
  vi.unstubAllGlobals();
  localStorage.clear();
});

describe("landingPath", () => {
  it("opens the last-used workspace while it is still the user's", () => {
    const user = me([tenant("elssc", "ACTIVE"), tenant("kmsa", "ACTIVE")], ["PLATFORM_ADMIN"]);
    expect(landingPath(user, "/t/kmsa")).toBe("/t/kmsa");
    expect(landingPath(user, "/platform")).toBe("/platform");
  });

  it("ignores a last-used workspace the user has lost, and falls back to the first active tenant", () => {
    const user = me([tenant("elssc", "SUSPENDED"), tenant("kmsa", "ACTIVE")]);
    expect(landingPath(user, "/t/gone")).toBe("/t/kmsa");
    expect(landingPath(user, "/platform")).toBe("/t/kmsa");
  });

  it("opens the platform console for staff without an active tenant", () => {
    expect(landingPath(me([tenant("kmsa", "SUSPENDED")], ["PLATFORM_SUPPORT"]), null)).toBe("/platform");
  });

  it("opens a blocked tenant (which says why) when it is all the user has, and nothing when they have none", () => {
    expect(landingPath(me([tenant("kmsa", "ARCHIVED")]), null)).toBe("/t/kmsa");
    expect(landingPath(me([]), null)).toBeNull();
  });
});

describe("last-used workspace", () => {
  it("is remembered in this browser", () => {
    rememberWorkspace("/t/kmsa");
    expect(readLastWorkspace()).toBe("/t/kmsa");
  });

  it("is simply forgotten when storage refuses", () => {
    const refusing = { getItem: () => { throw new Error("denied"); }, setItem: () => { throw new Error("denied"); } };
    vi.stubGlobal("localStorage", refusing);
    expect(() => rememberWorkspace("/t/kmsa")).not.toThrow();
    expect(readLastWorkspace()).toBeNull();
  });
});
