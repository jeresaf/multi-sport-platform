import { describe, expect, it } from "vitest";
import { roleDescription, rolesAssignableAt } from "./roles";

describe("rolesAssignableAt", () => {
  it("offers the tenant roles in catalogue order, without team-only and platform roles", () => {
    const codes = rolesAssignableAt("TENANT").map((role) => role.code);

    expect(codes[0]).toBe("TENANT_OWNER");
    expect(codes).toContain("VIEWER");
    expect(codes).not.toContain("TEAM_MANAGER");
    expect(codes).not.toContain("PLATFORM_ADMIN");
  });

  it("describes a role from the catalogue", () => {
    expect(roleDescription("VIEWER")).toBeTruthy();
    expect(roleDescription("NOT_A_ROLE")).toBeUndefined();
  });
});
