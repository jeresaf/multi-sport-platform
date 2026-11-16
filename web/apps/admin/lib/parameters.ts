// A format's or a ruleset template's parameters as the screens show them (S05 D17): the type, the default and what a
// competition may choose, in words. Choice and list values are codes; the caller may label them (a template labels its
// tiebreakers from the catalogue), else they are written as words from the code.
import type { components } from "@msp/api-client";
import { codeLabel } from "./sport-definition";

type Schemas = components["schemas"];
export type ParameterValue = Schemas["ParameterValue"];
export type Parameter = Schemas["FormatParameterResponse"] | Schemas["RulesetParameterResponse"];
export type ParameterType = Parameter["type"];
type ValueLabel = (code: string) => string;

const typeLabels: Record<ParameterType, string> = {
  NUMBER: "Whole number",
  FLAG: "Yes or no",
  CHOICE: "One of a list",
  LIST: "Ordered list",
};

export const parameterTypeLabel = (type: ParameterType) => typeLabels[type] ?? codeLabel(type);

/** A value in words: "3", "Yes", "Random", or a list in its order ("Points, then Wins"); "None" for an empty list. */
export function parameterValueLabel(value: ParameterValue, label: ValueLabel = codeLabel): string {
  if (typeof value === "boolean") return value ? "Yes" : "No";
  if (typeof value === "number") return String(value);
  if (typeof value === "string") return label(value);
  return value.length > 0 ? value.map(label).join(", then ") : "None";
}

/** What a competition may choose: a number's range, a choice's or a list's values; "Yes or no" for a flag. */
export function parameterAllowed(parameter: Parameter, label: ValueLabel = codeLabel): string {
  switch (parameter.type) {
    case "NUMBER":
      return numberRange(parameter.min, parameter.max);
    case "FLAG":
      return "Yes or no";
    case "CHOICE":
      return `One of: ${parameter.values.map(label).join(", ")}`;
    case "LIST":
      return `Any of, each once, in order: ${parameter.values.map(label).join(", ")}`;
  }
}

/** "1 to 4", "At least 1", "At most 4", or "Any whole number". */
export function numberRange(min: number | null, max: number | null): string {
  if (min !== null && max !== null) return min === max ? `Only ${min}` : `${min} to ${max}`;
  if (min !== null) return `At least ${min}`;
  if (max !== null) return `At most ${max}`;
  return "Any whole number";
}
