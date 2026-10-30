// Why a control the roles allow is disabled (S04 D19): the read-only cause from the subscription's status, the plan
// that includes an entitlement, nothing for an action nothing refuses.
import { describe, expect, it } from "vitest";
import { cancelledAccess, fullAccess } from "@/test/capabilities";
import { unavailableReason, type UnavailableAction } from "./unavailable";

const readOnly = (action: string): UnavailableAction => ({ action, code: "TENANT_READ_ONLY", entitlement: null, plans: null });

describe("unavailableReason", () => {
  it("is null for an action nothing refuses", () => {
    expect(unavailableReason("membership.invite", [readOnly("tenant.settings.update")], cancelledAccess)).toBeNull();
  });

  it.each([
    ["EXPIRED", "Read-only: the subscription has ended"],
    ["CANCELLED", "Read-only: the subscription was cancelled"],
    ["SCHEDULED", "Read-only: the subscription hasn't started yet"],
    [null, "Read-only: the organisation has no subscription"],
  ] as const)("says why the workspace is read-only (%s)", (status, reason) => {
    const access = { ...cancelledAccess, subscriptionStatus: status };
    expect(unavailableReason("membership.invite", [readOnly("membership.invite")], access)).toBe(reason);
  });

  it("names the lowest plan that includes the entitlement", () => {
    const entry: UnavailableAction = {
      action: "tenant.domain.add",
      code: "ENTITLEMENT_REQUIRED",
      entitlement: "CUSTOM_DOMAIN",
      plans: ["PRO"],
    };
    expect(unavailableReason("tenant.domain.add", [entry], fullAccess)).toBe("Requires PRO plan");
    // Only an override could allow it: no plan to name.
    expect(unavailableReason("tenant.domain.add", [{ ...entry, plans: [] }], fullAccess)).toBe("Not included in your plan");
  });
});
