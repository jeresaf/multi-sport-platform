// The duplicate check's answer in words (S06 D5): how sure it is, and why each person matched. The evidence never
// carries the other person's date of birth: `SAME_DATE_OF_BIRTH` only confirms the date the registrar typed.
import type { components } from "@msp/api-client";

type Schemas = components["schemas"];
export type DuplicateConfidence = Schemas["DuplicateConfidence"];
export type DuplicateEvidence = Schemas["DuplicateEvidenceResponse"];

export const confidenceLabels: Readonly<Record<DuplicateConfidence, string>> = {
  EXACT_IDENTIFIER: "Same identifier",
  LIKELY: "Likely the same person",
  POSSIBLE: "Possibly the same person",
};

/** One sentence per piece of evidence, e.g. "Names 86% alike" or "Holds the same KMSA_REGISTRATION identifier". */
export function evidenceText({ code, value }: DuplicateEvidence): string {
  switch (code) {
    case "SAME_EXTERNAL_ID":
      return value ? `Holds the same ${value} identifier` : "Holds the same identifier";
    case "SAME_DATE_OF_BIRTH":
      return "Same date of birth";
    case "BIRTH_YEAR_WITHIN_ONE":
      return "Born within a year of each other";
    case "SAME_NAME_KEY":
      return "Same names, in any order";
    case "NAME_SIMILARITY":
      return value ? `Names ${value}% alike` : "Similar names";
    case "SAME_LAST_NAME":
      return "Same last name";
    case "SAME_NATIONALITY":
      return "Same nationality";
    case "ALIAS_MATCH":
      return "Matches one of their other names (aliases)";
  }
}
