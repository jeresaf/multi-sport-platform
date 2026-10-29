import type { components } from "@msp/api-client";
import { TriangleAlert } from "lucide-react";
import { formatQuantity } from "@/lib/entitlements";

export type QuotaUsageValue = components["schemas"]["QuotaUsageResponse"];

/**
 * A quota's usage (S04 D15): "Used: 4 of 5 users", with "Near the limit" from 80 % and "At the limit" once it is
 * reached, or "not measured yet" for a quota no module counts (AR-30).
 */
export function QuotaUsage({ usage, unit }: Readonly<{ usage: QuotaUsageValue; unit: string | null | undefined }>) {
  if (!usage.measured || usage.used === null) {
    return <p className="text-sm text-muted-foreground">Usage: not measured yet</p>;
  }
  return (
    <p className="flex flex-wrap items-center gap-1 text-sm">
      Used:{" "}
      {usage.limit === null
        ? `${formatQuantity(usage.used, unit)} (unlimited)`
        : `${usage.used.toLocaleString("en-GB")} of ${formatQuantity(usage.limit, unit)}`}
      {usage.warning ? (
        <span className="inline-flex items-center gap-1">
          <TriangleAlert aria-hidden="true" className="size-4 text-warning" />
          {usage.limit !== null && usage.used >= usage.limit ? "At the limit" : "Near the limit"}
        </span>
      ) : null}
    </p>
  );
}
