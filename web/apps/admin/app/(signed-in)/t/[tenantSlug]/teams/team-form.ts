// Add team's, Edit details' and Rename's forms (S07 D6, D7, D8, D9, D13): the values as typed, their checks (the
// server's limits, which stay authoritative) and the requests the duplicate check, the create, the update and the
// rename send. Empty optional fields are sent as null. Every code here is generic (AR-11): the sport is a variant code.
import { z } from "zod";
import { hexColour } from "@/components/registry/colour-swatch";
import type { DuplicateDecision } from "@/components/registry/duplicate-panel";
import type { ApiFields } from "@/lib/form-errors";
import {
  ageGroups,
  genderCategories,
  teamLevels,
  type AgeGroup,
  type GenderCategory,
  type TeamLevel,
} from "@/lib/team-category";
import { slugSchema } from "../venues/venue-form";
import type {
  CheckTeamDuplicatesRequest,
  CreateTeamRequest,
  RenameTeamRequest,
  Team,
  UpdateTeamRequest,
} from "./teams-data";

// TeamDetails on the server.
export const limits = {
  name: 120,
  shortName: 30,
  motto: 120,
  foundedYear: { min: 1800 },
} as const;

/** A name the server can key: at least one letter or digit, in any script. */
const keyable = /[\p{L}\p{N}]/u;
/** The code's letters, typed in either case and sent in capitals (`^[A-Z]{3}$`). */
const codeLetters = /^[A-Za-z]{3}$/;
const isoDay = /^\d{4}-\d{2}-\d{2}$/;

const text = (max: number, label: string) =>
  z.string().refine((value) => value.trim().length <= max, `Keep ${label} to ${max} characters.`);

const name = text(limits.name, "the name")
  .refine((value) => value.trim().length > 0, "Enter the name.")
  .refine((value) => !value.trim() || keyable.test(value), "The name needs at least one letter or digit.");

const code = z
  .string()
  .refine((value) => !value.trim() || codeLetters.test(value.trim()), "Use three letters, such as NTS.");

const colour = z
  .string()
  .refine(
    (value) => !value.trim() || hexColour.test(value.trim()),
    // An example of the form a team's own colour (data, not a design token) is typed in.
    // eslint-disable-next-line no-restricted-syntax
    "Enter a colour as # and six digits or letters A–F, such as #1A3D7C.",
  );

const oneOf = <T extends string>(codes: readonly T[], message: string) =>
  z.string().refine((value) => (codes as readonly string[]).includes(value), message);

/**
 * The form's checks: Add's (`edit` false: the sport and the name's first day, no slug) or Edit details' (with the
 * slug; the sport and the name aren't changed there). `today` is the tenant's date (YYYY-MM-DD), the latest founding
 * year's and the name's first day's limit (the server checks them on its own date).
 */
export const teamSchema = (edit: boolean, today: string) => {
  const thisYear = Number(today.slice(0, 4));
  return z
    .object({
      variant: edit ? z.string() : z.string().refine((value) => value !== "", "Choose the sport."),
      name: edit ? z.string() : name,
      shortName: text(limits.shortName, "the short name"),
      genderCategory: oneOf(genderCategories, "Choose the gender."),
      ageGroup: oneOf(ageGroups, "Choose the age group."),
      level: oneOf(teamLevels, "Choose the level."),
      code,
      motto: text(limits.motto, "the motto"),
      primaryColour: colour,
      secondaryColour: colour,
      tertiaryColour: colour,
      foundedYear: z.string().refine((value) => {
        const trimmed = value.trim();
        if (!trimmed) {
          return true;
        }
        const year = Number(trimmed);
        return Number.isInteger(year) && year >= limits.foundedYear.min && year <= thisYear;
      }, `Enter a year from ${limits.foundedYear.min} to ${thisYear}.`),
      nameSince: z
        .string()
        .refine((value) => !value || (isoDay.test(value) && value <= today), "Choose a day no later than today."),
      organisationId: z.string(),
      homeVenueId: z.string(),
      slug: edit ? slugSchema : z.string(),
    })
    .superRefine((values, context) => {
      // D6: a secondary colour needs a primary one, a third colour a secondary one (`TEAM_COLOURS_INVALID`).
      if (values.secondaryColour.trim() && !values.primaryColour.trim()) {
        context.addIssue({
          code: "custom",
          path: ["secondaryColour"],
          message: "Enter the primary colour first.",
        });
      }
      if (values.tertiaryColour.trim() && !values.secondaryColour.trim()) {
        context.addIssue({
          code: "custom",
          path: ["tertiaryColour"],
          message: "Enter the secondary colour first.",
        });
      }
    });
};

export type TeamValues = z.infer<ReturnType<typeof teamSchema>>;

/** Add team's start: the first team (D7's default level), no sport chosen unless only one is enabled. */
export function emptyTeam(overrides: Partial<TeamValues> = {}): TeamValues {
  return {
    variant: "",
    name: "",
    shortName: "",
    genderCategory: "",
    ageGroup: "",
    level: "FIRST",
    code: "",
    motto: "",
    primaryColour: "",
    secondaryColour: "",
    tertiaryColour: "",
    foundedYear: "",
    nameSince: "",
    organisationId: "",
    homeVenueId: "",
    slug: "",
    ...overrides,
  };
}

/** The team's saved details as the form shows them. */
export function fromTeam(team: Team): TeamValues {
  return {
    variant: team.variant,
    name: team.name,
    shortName: team.shortName ?? "",
    genderCategory: team.genderCategory,
    ageGroup: team.ageGroup,
    level: team.level,
    code: team.code ?? "",
    motto: team.motto ?? "",
    primaryColour: team.primaryColour ?? "",
    secondaryColour: team.secondaryColour ?? "",
    tertiaryColour: team.tertiaryColour ?? "",
    foundedYear: team.foundedYear === null ? "" : String(team.foundedYear),
    nameSince: team.nameSince,
    organisationId: team.organisation?.id ?? "",
    homeVenueId: team.homeVenue?.id ?? "",
    slug: team.slug,
  };
}

const orNull = (value: string) => value.trim() || null;
const codeOrNull = (value: string) => value.trim().toUpperCase() || null;
const colourOrNull = (value: string) => value.trim().toUpperCase() || null;

/** The details the create and the update both send. */
function details(values: TeamValues) {
  return {
    genderCategory: values.genderCategory as GenderCategory,
    ageGroup: values.ageGroup as AgeGroup,
    level: values.level as TeamLevel,
    shortName: orNull(values.shortName),
    organisationId: values.organisationId || null,
    code: codeOrNull(values.code),
    motto: orNull(values.motto),
    primaryColour: colourOrNull(values.primaryColour),
    secondaryColour: colourOrNull(values.secondaryColour),
    tertiaryColour: colourOrNull(values.tertiaryColour),
    homeVenueId: values.homeVenueId || null,
    foundedYear: values.foundedYear.trim() ? Number(values.foundedYear.trim()) : null,
  };
}

const reviewFields = ({ reviewed, reason }: DuplicateDecision) => ({
  duplicatesReviewed: reviewed,
  duplicateReason: reviewed ? orNull(reason) : null,
});

/**
 * Add team's request. No slug: the server proposes one from the name and category (D9). No `nameSince` unless a day
 * was chosen: the server uses the tenant's today.
 */
export function toCreateRequest(values: TeamValues, review: DuplicateDecision): CreateTeamRequest {
  return {
    variant: values.variant,
    name: values.name.trim(),
    ...details(values),
    nameSince: values.nameSince || null,
    ...reviewFields(review),
  };
}

/** Edit details' request: the current name (never another: that is a rename, D8) and the slug, always sent. */
export function toUpdateRequest(team: Team, values: TeamValues, review: DuplicateDecision): UpdateTeamRequest {
  return {
    name: team.name,
    ...details(values),
    slug: values.slug.trim(),
    ...reviewFields(review),
  };
}

/** What D13 compares for Add team: the names, the sport, the category and the organisation. */
export function toCheckRequest(values: TeamValues): CheckTeamDuplicatesRequest | null {
  const teamName = values.name.trim();
  if (
    !keyable.test(teamName) ||
    !values.variant ||
    !(genderCategories as readonly string[]).includes(values.genderCategory) ||
    !(ageGroups as readonly string[]).includes(values.ageGroup)
  ) {
    return null;
  }
  return {
    variant: values.variant,
    name: teamName,
    genderCategory: values.genderCategory as GenderCategory,
    ageGroup: values.ageGroup as AgeGroup,
    level: values.level as TeamLevel,
    shortName: orNull(values.shortName),
    organisationId: values.organisationId || null,
  };
}

/** The check's input as one key, so the live warning asks again only when one of them changes. */
export function checkKey(request: CheckTeamDuplicatesRequest | null): string {
  return request
    ? JSON.stringify([
        request.variant,
        request.name,
        request.shortName,
        request.genderCategory,
        request.ageGroup,
        request.level,
        request.organisationId,
      ])
    : "";
}

const loose = (value: string) => value.trim().replace(/\s+/g, " ").toLowerCase();

/**
 * Whether an edit changes what the server compares on an update (`Team.ChangesComparedFields`): the short name, the
 * category or the organisation. Loosely: the server compares the short name's key, so a change of case or spacing may
 * still be checked here and pass there.
 */
export function changesComparedFields(opened: TeamValues, values: TeamValues): boolean {
  return (
    loose(opened.shortName) !== loose(values.shortName) ||
    opened.genderCategory !== values.genderCategory ||
    opened.ageGroup !== values.ageGroup ||
    opened.level !== values.level ||
    opened.organisationId !== values.organisationId
  );
}

/** The API's names for the details' fields (`errors[].field`). Add has no web address field: its `slug` refusal goes to the alert. */
export function teamApiFields(edit: boolean): ApiFields<TeamValues> {
  const fields: ApiFields<TeamValues> = {
    genderCategory: "genderCategory",
    ageGroup: "ageGroup",
    level: "level",
    shortName: "shortName",
    organisationId: "organisationId",
    code: "code",
    motto: "motto",
    primaryColour: "primaryColour",
    secondaryColour: "secondaryColour",
    tertiaryColour: "tertiaryColour",
    homeVenueId: "homeVenueId",
    foundedYear: "foundedYear",
  };
  return edit ? { ...fields, slug: "slug" } : { ...fields, variant: "variant", name: "name", nameSince: "nameSince" };
}

/**
 * Rename's checks (D8): the new name, and the short name and code from the new name's first day (`effectiveOn`): after
 * the current name's first day (`nameSince`) and not after the tenant's today.
 */
export const renameSchema = (nameSince: string, today: string) =>
  z.object({
    name,
    shortName: text(limits.shortName, "the short name"),
    code,
    effectiveOn: z
      .string()
      .refine((value) => isoDay.test(value), "Choose the new name's first day.")
      .refine((value) => !isoDay.test(value) || value > nameSince, "Choose a day after the current name's first day.")
      .refine((value) => !isoDay.test(value) || value <= today, "Choose a day no later than today."),
  });

export type RenameValues = z.infer<ReturnType<typeof renameSchema>>;

/** Rename's start: the current short name and code (kept unless changed), the new name's first day today. */
export function renameStart(team: Team, today: string): RenameValues {
  return {
    name: "",
    shortName: team.shortName ?? "",
    code: team.code ?? "",
    effectiveOn: today,
  };
}

/** Rename's request: the short name and code are always sent (a cleared one is `null`, the server's "clear"). */
export function toRenameRequest(values: RenameValues, review: DuplicateDecision): RenameTeamRequest {
  return {
    name: values.name.trim(),
    shortName: orNull(values.shortName),
    code: codeOrNull(values.code),
    effectiveOn: values.effectiveOn,
    ...reviewFields(review),
  };
}

/**
 * What D13 compares for a rename: the new names with the team's sport, category and organisation; nothing while the
 * names are the team's own (loosely: the server decides by the name keys).
 */
export function toRenameCheckRequest(team: Team, values: RenameValues): CheckTeamDuplicatesRequest | null {
  const teamName = values.name.trim();
  if (!keyable.test(teamName)) {
    return null;
  }
  if (loose(teamName) === loose(team.name) && loose(values.shortName) === loose(team.shortName ?? "")) {
    return null;
  }
  return {
    variant: team.variant,
    name: teamName,
    genderCategory: team.genderCategory,
    ageGroup: team.ageGroup,
    level: team.level,
    shortName: orNull(values.shortName),
    organisationId: team.organisation?.id ?? null,
  };
}

export const renameApiFields: ApiFields<RenameValues> = {
  name: "name",
  shortName: "shortName",
  code: "code",
  effectiveOn: "effectiveOn",
};
