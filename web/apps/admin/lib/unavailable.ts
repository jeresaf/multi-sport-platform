// Why a control the user's roles allow can't be used now (S04 D14, D19): the capabilities' `unavailable` names the
// action and the step that refuses it; the screens show the control disabled with this reason. Controls the roles
// don't allow aren't listed there and stay hidden.
import type { components } from "@msp/api-client";
import type { CapabilityAccess } from "./access";

export type UnavailableAction = components["schemas"]["UnavailableActionResponse"];

/** "Read-only: the subscription has ended", "Requires PRO plan"; null when nothing refuses the action. */
export function unavailableReason(
  action: string,
  unavailable: readonly UnavailableAction[],
  access: CapabilityAccess,
): string | null {
  const entry = unavailable.find((item) => item.action === action);
  if (!entry) {
    return null;
  }
  if (entry.code === "TENANT_READ_ONLY") {
    const cause = readOnlyCause(access.subscriptionStatus);
    return cause ? `Read-only: ${cause}` : "Read-only";
  }
  if (entry.code === "ENTITLEMENT_REQUIRED") {
    // The plans come in rank order: the lowest that includes it is the one to ask for.
    const plan = entry.plans?.[0];
    return plan ? `Requires ${plan} plan` : "Not included in your plan";
  }
  return "Not available now";
}

/** Why the workspace is read-only, or null for a status that doesn't make it so (none does today). */
function readOnlyCause(status: CapabilityAccess["subscriptionStatus"]): string | null {
  switch (status) {
    case "EXPIRED":
      return "the subscription has ended";
    case "CANCELLED":
      return "the subscription was cancelled";
    case "SCHEDULED":
      return "the subscription hasn't started yet";
    case null:
      return "the organisation has no subscription";
    default:
      return null;
  }
}
