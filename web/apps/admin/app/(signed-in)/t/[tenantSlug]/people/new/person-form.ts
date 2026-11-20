// Add person's form (S06 D2, D10): the values as typed, their checks (the server's limits, which stay authoritative),
// and the requests the duplicate check and the create send. Empty optional fields are sent as null.
import type { Path } from "react-hook-form";
import { z } from "zod";
import type { ApiFields } from "@/lib/form-errors";
import type { CheckDuplicatesRequest, CreatePersonRequest } from "../people-data";

// PersonBiodata and ExternalIdentifier on the server.
export const limits = {
  name: 100,
  otherNames: 200,
  displayName: 200,
  height: { min: 50, max: 260 },
  weight: { min: 10, max: 300 },
  identifierValue: 100,
  identifiers: 10,
} as const;

/** An issuer's code (S06 D10): 2–40 capital letters, digits or underscores, starting with a letter. */
export const authorityPattern = /^[A-Z][A-Z0-9_]{1,39}$/;
export const authorityMessage =
  "Use 2 to 40 capital letters, digits or underscores, starting with a letter, such as KMSA_REG_NO.";
/**
 * Said for a start after the tenant's today: the API accepts one, but the identifier then can't be ended before that
 * day, so the page refuses it (owner, Q46, S06-T7c-2a).
 */
export const validFromLaterMessage = "An identifier can't start later than today.";

const text = (max: number, label: string) =>
  z.string().refine((value) => value.trim().length <= max, `Keep ${label} to ${max} characters.`);
const required = (max: number, label: string) =>
  text(max, label).refine((value) => value.trim().length > 0, `Enter the ${label}.`);

/** An identifier row; `today` is the tenant's (YYYY-MM-DD), or empty until its time zone is read (no cap then). */
const identifier = (today: string) =>
  z.object({ authority: z.string(), value: z.string(), validFrom: z.string() }).superRefine((row, context) => {
    const authority = row.authority.trim().toUpperCase();
    const value = row.value.trim();
    if (!authority && !value && !row.validFrom) {
      return;
    }
    if (!authorityPattern.test(authority)) {
      context.addIssue({ code: "custom", path: ["authority"], message: authorityMessage });
    }
    if (!value) {
      context.addIssue({ code: "custom", path: ["value"], message: "Enter the identifier." });
    } else if (value.length > limits.identifierValue) {
      context.addIssue({
        code: "custom",
        path: ["value"],
        message: `Keep the identifier to ${limits.identifierValue} characters.`,
      });
    }
    if (today && row.validFrom > today) {
      context.addIssue({ code: "custom", path: ["validFrom"], message: validFromLaterMessage });
    }
  });

function inRange(value: string, min: number, max: number, decimals: number): boolean {
  const number = Number(value);
  return Number.isFinite(number) && number >= min && number <= max && Number(number.toFixed(decimals)) === number;
}

/** The form's checks; `today` is the tenant's date, or empty until it is known. */
export const personSchema = (today: string) =>
  z.object({
    firstName: required(limits.name, "first name"),
    middleName: text(limits.name, "the middle name"),
    lastName: required(limits.name, "last name"),
    otherNames: text(limits.otherNames, "other names"),
    displayName: text(limits.displayName, "the display name"),
    gender: z.enum(["", "FEMALE", "MALE"]),
    dateOfBirth: z.string(),
    nationality: z.string(),
    secondaryNationality: z.string(),
    heightCm: z
      .string()
      .refine(
        (value) => !value.trim() || inRange(value, limits.height.min, limits.height.max, 0),
        `Enter whole centimetres from ${limits.height.min} to ${limits.height.max}.`,
      ),
    weightKg: z
      .string()
      .refine(
        (value) => !value.trim() || inRange(value, limits.weight.min, limits.weight.max, 1),
        `Enter kilograms from ${limits.weight.min} to ${limits.weight.max}, with at most one decimal.`,
      ),
    externalIdentifiers: z.array(identifier(today)).max(limits.identifiers),
  });
export type Values = z.infer<ReturnType<typeof personSchema>>;

export const emptyIdentifier = { authority: "", value: "", validFrom: "" };

export const defaultValues: Values = {
  firstName: "",
  middleName: "",
  lastName: "",
  otherNames: "",
  displayName: "",
  gender: "",
  dateOfBirth: "",
  nationality: "",
  secondaryNationality: "",
  heightCm: "",
  weightKg: "",
  externalIdentifiers: [],
};

/** The fields Add person's first step asks for; every other field is on the second. */
export const nameFields = ["firstName", "middleName", "lastName", "otherNames"] as const;

/** The API's names for the form's fields (`errors[].field`), identifiers by their position. */
export const apiFields: ApiFields<Values> = {
  firstName: "firstName",
  middleName: "middleName",
  lastName: "lastName",
  otherNames: "otherNames",
  displayName: "displayName",
  gender: "gender",
  dateOfBirth: "dateOfBirth",
  nationality: "nationality",
  secondaryNationality: "secondaryNationality",
  heightCm: "heightCm",
  weightKg: "weightKg",
  ...Object.fromEntries(
    Array.from({ length: limits.identifiers }, (_, index) =>
      (["authority", "value", "validFrom"] as const).map((part) => [
        `externalIdentifiers[${index}].${part}`,
        `externalIdentifiers.${index}.${part}` as Path<Values>,
      ]),
    ).flat(),
  ),
};

const optional = (value: string) => value.trim() || null;

/** The name as it is shown before a display name is chosen: "first last", as the server defaults it (S06 D2). */
export function displayNameOf(values: Pick<Values, "firstName" | "lastName" | "displayName">): string {
  return values.displayName.trim() || `${values.firstName.trim()} ${values.lastName.trim()}`.trim();
}

function identifiers(values: Values) {
  return values.externalIdentifiers
    .filter((row) => row.authority.trim() || row.value.trim() || row.validFrom)
    .map((row) => ({
      authority: row.authority.trim().toUpperCase(),
      value: row.value.trim(),
      validFrom: row.validFrom || null,
    }));
}

/** What the duplicate check compares (S06 D5): the names, date of birth, nationalities and identifiers. */
export function toCheckRequest(values: Values): CheckDuplicatesRequest {
  return {
    firstName: values.firstName.trim(),
    middleName: optional(values.middleName),
    lastName: values.lastName.trim(),
    otherNames: optional(values.otherNames),
    dateOfBirth: values.dateOfBirth || null,
    nationality: values.nationality || null,
    secondaryNationality: values.secondaryNationality || null,
    externalIdentifiers: identifiers(values).map(({ authority, value }) => ({ authority, value })),
  };
}

/** The create (S06 D6): the same details as the check, the rest of the biodata, the token and any reason. */
export function toCreateRequest(
  values: Values,
  checkToken: string,
  overrideReason: string | null,
): CreatePersonRequest {
  return {
    ...toCheckRequest(values),
    displayName: optional(values.displayName),
    gender: values.gender || null,
    heightCm: values.heightCm.trim() ? Number(values.heightCm) : null,
    weightKg: values.weightKg.trim() ? Number(values.weightKg) : null,
    externalIdentifiers: identifiers(values),
    checkToken,
    overrideReason,
  };
}
