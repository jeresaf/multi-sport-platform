// A sport definition as the viewer shows it (S05 D17): the API's types and the generic words for its closed
// vocabularies. Every sport word comes from the definition itself (its labels and terminology, AR-29); codes the
// definition gives no label for (actor roles, scopes, groups) are written as words from the code, as the backend does
// for generic event types.
import type { components } from "@msp/api-client";

type Schemas = components["schemas"];
export type SportDefinition = Schemas["SportDefinitionResponse"];
export type SportVariant = Schemas["SportVariantResponse"];
export type SportEventType = Schemas["SportEventTypeResponse"];
export type SportStatistic = Schemas["SportStatisticResponse"];
export type SportScorerPad = Schemas["SportScorerPadResponse"];
export type PhaseKind = Schemas["PhaseKind"];

/** "PENALTY_GOAL" → "Penalty goal"; "U19" stays "U19". */
export function codeLabel(code: string): string {
  const words = code.toLowerCase().split("_").filter(Boolean).join(" ");
  return words.charAt(0).toUpperCase() + words.slice(1);
}

const clockLabels: Record<Schemas["PeriodClock"], string> = {
  COUNT_UP: "Counts up",
  COUNT_DOWN: "Counts down",
  NONE: "No clock",
};

const poolLabels: Record<Schemas["ActorPool"], string> = {
  ON_FIELD: "On the field",
  BENCH: "On the bench",
  SQUAD: "In the squad",
  STAFF: "Staff",
  SQUAD_OR_STAFF: "Squad or staff",
};

const sideLabels: Record<Schemas["ActorSide"], string> = { OWN: "Own side", OPPONENT: "Opponent" };

const aggregationLabels: Record<Schemas["StatisticAggregation"], string> = {
  SUM: "Sum",
  MAX: "Highest",
  MIN: "Lowest",
  RATIO: "Ratio",
  BUILTIN: "Built in",
};

export const clockLabel = (clock: Schemas["PeriodClock"]) => clockLabels[clock] ?? codeLabel(clock);
export const poolLabel = (pool: Schemas["ActorPool"]) => poolLabels[pool] ?? codeLabel(pool);
export const sideLabel = (side: Schemas["ActorSide"]) => sideLabels[side] ?? codeLabel(side);
export const aggregationLabel = (aggregation: Schemas["StatisticAggregation"]) =>
  aggregationLabels[aggregation] ?? codeLabel(aggregation);

/** A length in seconds as people read it: "45 min", "10 min 30 s", "40 s". */
export function formatSeconds(seconds: number): string {
  const minutes = Math.floor(seconds / 60);
  const rest = seconds % 60;
  if (minutes === 0) return `${rest} s`;
  return rest === 0 ? `${minutes} min` : `${minutes} min ${rest} s`;
}

/** An event's label from the definition (its own or a generic event's); the code as words if neither has it. */
export function eventLabel(definition: SportDefinition, code: string): string {
  return (
    definition.eventTypes.find((event) => event.code === code)?.label ??
    definition.genericEventTypes.find((event) => event.code === code)?.label ??
    codeLabel(code)
  );
}

/** The labels of the definition's periods of a kind ("Penalty shoot-out"), or the kind as words if it has none. */
export function phaseLabel(definition: SportDefinition, kind: PhaseKind): string {
  const labels = definition.periods.filter((period) => period.kind === kind).map((period) => period.label);
  return labels.length > 0 ? listLabel(labels) : codeLabel(kind);
}

/** "A", "A or B", "A, B or C" (`joiner` "and" for all of them). */
export function listLabel(items: readonly string[], joiner: "or" | "and" = "or"): string {
  if (items.length <= 1) return items.join("");
  return `${items.slice(0, -1).join(", ")} ${joiner} ${items[items.length - 1]}`;
}

/** How a statistic is derived, in words from the definition's labels: "Counts Goal events by Scorer". */
export function derivationLabel(definition: SportDefinition, statistic: SportStatistic): string {
  const derive = statistic.derive;
  const events = (codes: readonly string[]) => listLabel(codes.map((code) => eventLabel(definition, code)));
  const statisticLabel = (code: string) => definition.statistics.find((s) => s.code === code)?.label ?? codeLabel(code);

  if (derive.manual) return "Entered by hand";
  // A ratio is `builtin: RATIO` with its numerator and denominator (the definition validator's rule).
  if (derive.numerator && derive.denominator) {
    return `${statisticLabel(derive.numerator)} divided by ${statisticLabel(derive.denominator)}`;
  }
  if (derive.builtin) return `Built in: ${codeLabel(derive.builtin)}`;

  const parts: string[] = [];
  if (derive.countEvents.length > 0) parts.push(`Counts ${events(derive.countEvents)} events`);
  if (derive.sumPoints.length > 0) parts.push(`Adds up the points of ${events(derive.sumPoints)} events`);
  if (derive.actorRoles.length > 0) parts.push(`by ${listLabel(derive.actorRoles.map(codeLabel))}`);
  // A flag's condition is written "true" or "false" in the file.
  const value = (written: string) => (written === "true" ? "yes" : written === "false" ? "no" : codeLabel(written));
  const conditions = Object.entries(derive.where).map(([field, written]) => `${codeLabel(field).toLowerCase()} is ${value(written)}`);
  if (conditions.length > 0) parts.push(`where ${listLabel(conditions, "and")}`);
  return parts.length > 0 ? parts.join(" ") : "Not described";
}
