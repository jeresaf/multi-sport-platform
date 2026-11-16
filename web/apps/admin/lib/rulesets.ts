// A ruleset template's codes as the screens show them (S05 D17). The template pins a sport definition version, whose
// period, position and official codes it uses: their labels come from that definition when it could be read, else the
// codes are written as words. Tiebreakers are labelled from the catalogue entries the template carries; the closed
// vocabularies (tie resolutions, coverage, readiness checks, lock policy) are words from the code.
import type { components } from "@msp/api-client";
import { numberRange } from "./parameters";
import { codeLabel, type SportDefinition } from "./sport-definition";

export type RulesetTemplate = components["schemas"]["RulesetTemplateResponse"];

/** A period's label ("First half"), or its code as words. */
export function periodLabel(definition: SportDefinition | undefined, code: string): string {
  return definition?.periods.find((period) => period.code === code)?.label ?? codeLabel(code);
}

/**
 * A lineup position rule's subject: a position's label ("Goalkeeper"), or a group of positions ("Any of: Wing ringer,
 * Centre ringer"); the code as words when the definition can't say which.
 */
export function positionLabel(definition: SportDefinition | undefined, code: string): string {
  const position = definition?.positions.find((item) => item.code === code);
  if (position) return position.label;
  const group = definition?.positions.filter((item) => item.group === code) ?? [];
  return group.length > 0 ? `Any of: ${group.map((item) => item.label).join(", ")}` : codeLabel(code);
}

/** An official role's label ("Referee"), or its code as words. */
export function officialLabel(definition: SportDefinition | undefined, code: string): string {
  return definition?.officialRoles.find((role) => role.code === code)?.label ?? codeLabel(code);
}

/** A code of the template's choices or lists: a tiebreaker's catalogue label, else the code as words. */
export function rulesetValueLabel(template: RulesetTemplate, code: string): string {
  return template.tiebreakers.find((tiebreaker) => tiebreaker.code === code)?.label ?? codeLabel(code);
}

/** Age bounds in words: "No limit", "16 to 19", "At least 16", "At most 19". */
export function ageLabel(min: number | null, max: number | null): string {
  return min === null && max === null ? "No limit" : numberRange(min, max);
}

/** Names by code from a list read (formats, sport variants), for labelling codes; empty while it isn't read. */
export function namesByCode<T>(items: readonly T[] | undefined, code: (item: T) => string, name: (item: T) => string) {
  return Object.fromEntries((items ?? []).map((item) => [code(item), name(item)])) as Record<string, string>;
}
