// The tenant's access as its shell says it (S04 D19): a banner while the subscription is ending or in grace, and while
// the workspace is read-only (why, and since or until when), from the capabilities' `access` (D14).
import type { components } from "@msp/api-client";
import { formatDay } from "./entitlements";

export type CapabilityAccess = components["schemas"]["CapabilityAccessResponse"];

export type AccessNotice = {
  /** `warning` while full access lasts (expiring, grace); `readOnly` once writes are refused. */
  tone: "warning" | "readOnly";
  title: string;
  message: string;
};

// `access` doesn't say whether a next period is scheduled, so the banner doesn't claim it isn't.
const contact = "If it hasn't been renewed, contact the platform team.";

/** The banner for the tenant's access today, or null when there's nothing to say (full access, or still being set up). */
export function accessNotice(access: CapabilityAccess): AccessNotice | null {
  const { mode, subscriptionStatus: status, startsOn, endsOn, graceEndsOn, cancelledOn } = access;

  if (mode === "READ_ONLY") {
    return {
      tone: "readOnly",
      title: "This workspace is read-only",
      message: `${readOnlyReason(status, startsOn, endsOn, graceEndsOn, cancelledOn)} You can still see everything, but nothing can be changed. To restore full access, contact the platform team.`,
    };
  }
  if (mode !== "FULL" || !endsOn) {
    return null;
  }
  if (status === "EXPIRING") {
    return { tone: "warning", title: "The subscription ends soon", message: `It ends on ${formatDay(endsOn)}. ${contact}` };
  }
  if (status === "GRACE" && graceEndsOn) {
    return {
      tone: "warning",
      title: "The subscription has ended",
      message: `It ended on ${formatDay(endsOn)}. Full access continues until ${formatDay(graceEndsOn)}; after that the workspace is read-only. ${contact}`,
    };
  }
  return null;
}

function readOnlyReason(
  status: CapabilityAccess["subscriptionStatus"],
  startsOn: string | null,
  endsOn: string | null,
  graceEndsOn: string | null,
  cancelledOn: string | null,
): string {
  switch (status) {
    case "EXPIRED":
      return graceEndsOn && endsOn
        ? `Since ${formatDay(nextDay(graceEndsOn))}: the subscription ended on ${formatDay(endsOn)}.`
        : "The subscription has ended.";
    case "CANCELLED":
      // A period cancelled before it started became current only once its first day came: the workspace was
      // read-only before it was cancelled, so the cancellation doesn't say since when.
      return cancelledOn && startsOn && cancelledOn >= startsOn
        ? `Since ${formatDay(cancelledOn)}: the subscription was cancelled.`
        : "The subscription was cancelled.";
    case "SCHEDULED":
      return startsOn ? `Until ${formatDay(startsOn)}: the subscription hasn't started yet.` : "The subscription hasn't started yet.";
    default:
      return "The organisation has no subscription.";
  }
}

/** The day after a tenant's date (`2026-10-31` → `2026-11-01`). */
export function nextDay(day: string): string {
  const date = new Date(`${day}T00:00:00Z`);
  date.setUTCDate(date.getUTCDate() + 1);
  return date.toISOString().slice(0, 10);
}
