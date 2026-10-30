// Test data for `GET /api/admin/v1/me/capabilities` (S04 D14): the actions the user may perform, nothing unavailable,
// and a tenant with an active subscription unless a test says otherwise.
import type { components } from "@msp/api-client";

type TenantCapabilities = components["schemas"]["TenantCapabilitiesResponse"];
type Access = TenantCapabilities["access"];

/** KMSA's pilot period (pilot-customer.md), active: no banner. */
export const fullAccess: Access = {
  mode: "FULL",
  subscriptionStatus: "ACTIVE",
  startsOn: "2026-10-01",
  endsOn: "2027-03-31",
  graceEndsOn: "2027-04-14",
  cancelledOn: null,
  timeZone: "Africa/Kampala",
};

export function tenantCapabilities(
  actions: string[],
  overrides: Partial<Omit<TenantCapabilities, "actions">> = {},
): TenantCapabilities {
  return { actions, unavailable: [], access: fullAccess, scope: null, ...overrides };
}
