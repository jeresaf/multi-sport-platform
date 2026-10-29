// Entitlement values as people read them (S04 D1): an on/off feature, a limit with its unit, or a level. The API sends
// a value by its type: `{ enabled }`, `{ limit }` (null = unlimited) or `{ level }` (null = none).
import type { components } from "@msp/api-client";
import { humanizeCode } from "./codes";

export type EntitlementValue = components["schemas"]["EntitlementValue"];
export type EntitlementDefinition = components["schemas"]["PlanEntitlementResponse"];

/** "5 users", "1 user", "Unlimited", "Included", "Basic", "None". */
export function formatEntitlementValue(value: EntitlementValue, unit?: string | null): string {
  if ("enabled" in value) {
    return value.enabled ? "Included" : "Not included";
  }
  if ("limit" in value) {
    return value.limit === null || value.limit === undefined ? "Unlimited" : formatQuantity(value.limit, unit);
  }
  return value.level ? humanizeCode(value.level) : "None";
}

/** "5 users", "1 user", "20 GB": a unit is plural (catalogue), so one item drops its "s". */
export function formatQuantity(quantity: number, unit?: string | null): string {
  const number = quantity.toLocaleString("en-GB");
  if (!unit) {
    return number;
  }
  return `${number} ${quantity === 1 && unit.endsWith("s") ? unit.slice(0, -1) : unit}`;
}

/** A tenant's date (`2026-10-23`, no time) as people read it: "23 Oct 2026". */
export function formatDay(day: string): string {
  return new Intl.DateTimeFormat("en-GB", { day: "numeric", month: "short", year: "numeric", timeZone: "UTC" }).format(
    new Date(`${day}T00:00:00Z`),
  );
}
