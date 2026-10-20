// The system roles from contracts/roles.json (docs/generated/roles.md): which ones a screen may offer at a scope.
import catalogue from "./generated/roles.json";

export type RoleDefinition = { code: string; description: string; assignableAt: readonly string[] };

const roles: readonly RoleDefinition[] = catalogue.roles;

/** The roles that can be assigned at a scope (`TENANT`, `COMPETITION`, …), in the catalogue's order. */
export function rolesAssignableAt(scopeType: string): RoleDefinition[] {
  return roles.filter((role) => role.assignableAt.includes(scopeType));
}

export function roleDescription(code: string): string | undefined {
  return roles.find((role) => role.code === code)?.description;
}
