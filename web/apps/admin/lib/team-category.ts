// A team's category as people read it (S07 D7, Q53): the age group, then the gender, then the level unless it is the
// first team — "Senior Men", "Under-17 Boys", "Senior Women · Reserve". `MEN` and `WOMEN` read "Boys" and "Girls" in
// the age groups U6 to U17. Codes only: no sport word.
import type { components } from "@msp/api-client";

type Schemas = components["schemas"];
export type GenderCategory = Schemas["GenderCategory"];
export type AgeGroup = Schemas["AgeGroup"];
export type TeamLevel = Schemas["TeamLevel"];

const youthGroups = new Set(["U6", "U7", "U8", "U9", "U10", "U11", "U12", "U13", "U14", "U15", "U16", "U17"]);

export function ageGroupLabel(ageGroup: AgeGroup): string {
  switch (ageGroup) {
    case "SENIOR":
      return "Senior";
    case "VETERANS":
      return "Veterans";
    default:
      return `Under-${ageGroup.slice(1)}`;
  }
}

export function genderCategoryLabel(gender: GenderCategory, ageGroup: AgeGroup): string {
  const youth = youthGroups.has(ageGroup);
  switch (gender) {
    case "MEN":
      return youth ? "Boys" : "Men";
    case "WOMEN":
      return youth ? "Girls" : "Women";
    case "MIXED":
      return "Mixed";
  }
}

export function teamLevelLabel(level: TeamLevel): string {
  switch (level) {
    case "FIRST":
      return "First team";
    case "RESERVE":
      return "Reserve";
    case "DEVELOPMENT":
      return "Development";
    case "ACADEMY":
      return "Academy";
  }
}

export function teamCategoryLabel(team: { genderCategory: GenderCategory; ageGroup: AgeGroup; level: TeamLevel }): string {
  const category = `${ageGroupLabel(team.ageGroup)} ${genderCategoryLabel(team.genderCategory, team.ageGroup)}`;
  return team.level === "FIRST" ? category : `${category} · ${teamLevelLabel(team.level)}`;
}
