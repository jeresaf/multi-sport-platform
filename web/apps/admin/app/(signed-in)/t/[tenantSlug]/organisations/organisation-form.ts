// Add organisation's and Edit details' form (S07 D4, D9, D13): the values as typed, their checks (the server's limits,
// which stay authoritative) and the requests the duplicate check, the create and the update send. Empty optional fields
// are sent as null. Every code here is generic (AR-11).
import { z } from "zod";
import type { DuplicateDecision } from "@/components/registry/duplicate-panel";
import type { ApiFields } from "@/lib/form-errors";
import { slugSchema } from "../venues/venue-form";
import {
  organisationTypes,
  type CheckOrganisationDuplicatesRequest,
  type CreateOrganisationRequest,
  type Organisation,
  type OrganisationType,
  type UpdateOrganisationRequest,
} from "./organisations-data";

// OrganisationDetails on the server.
export const limits = {
  displayName: 120,
  legalName: 200,
  shortName: 40,
  city: 100,
  website: 200,
  email: 200,
  phone: 30,
  description: 2000,
  foundedYear: { min: 1800 },
} as const;

/** A name the server can key: at least one letter or digit, in any script. */
const keyable = /[\p{L}\p{N}]/u;

const text = (max: number, label: string) =>
  z.string().refine((value) => value.trim().length <= max, `Keep ${label} to ${max} characters.`);

const isWebAddress = (value: string) => {
  try {
    const url = new URL(value);
    return url.protocol === "https:" || url.protocol === "http:";
  } catch {
    return false;
  }
};

/**
 * The form's checks: Add's (`edit` false, no slug) or Edit details' (with the slug). `thisYear` is the tenant's year,
 * the latest founding year (the server checks it on its own date).
 */
export const organisationSchema = (edit: boolean, thisYear: number) =>
  z.object({
    displayName: text(limits.displayName, "the name")
      .refine((value) => value.trim().length > 0, "Enter the name.")
      .refine((value) => !value.trim() || keyable.test(value), "The name needs at least one letter or digit."),
    legalName: text(limits.legalName, "the legal name"),
    shortName: text(limits.shortName, "the short name"),
    type: z
      .string()
      .refine((value) => (organisationTypes as readonly string[]).includes(value), "Choose what kind of body it is."),
    country: z.string(),
    city: text(limits.city, "the city"),
    foundedYear: z.string().refine((value) => {
      const trimmed = value.trim();
      if (!trimmed) {
        return true;
      }
      const year = Number(trimmed);
      return Number.isInteger(year) && year >= limits.foundedYear.min && year <= thisYear;
    }, `Enter a year from ${limits.foundedYear.min} to ${thisYear}.`),
    website: text(limits.website, "the website").refine(
      (value) => !value.trim() || isWebAddress(value.trim()),
      "Enter the full address, starting with https:// or http://.",
    ),
    email: text(limits.email, "the email address").refine(
      (value) => !value.trim() || z.email().safeParse(value.trim()).success,
      "Enter an email address such as office@example.org.",
    ),
    phone: text(limits.phone, "the phone number"),
    description: text(limits.description, "the description"),
    primaryVenueId: z.string(),
    slug: edit ? slugSchema : z.string(),
  });

export type OrganisationValues = z.infer<ReturnType<typeof organisationSchema>>;

export function emptyOrganisation(): OrganisationValues {
  return {
    displayName: "",
    legalName: "",
    shortName: "",
    type: "",
    country: "",
    city: "",
    foundedYear: "",
    website: "",
    email: "",
    phone: "",
    description: "",
    primaryVenueId: "",
    slug: "",
  };
}

/** The organisation's saved details as the form shows them. */
export function fromOrganisation(organisation: Organisation): OrganisationValues {
  return {
    displayName: organisation.displayName,
    legalName: organisation.legalName ?? "",
    shortName: organisation.shortName ?? "",
    type: organisation.type,
    country: organisation.country,
    city: organisation.city ?? "",
    foundedYear: organisation.foundedYear === null ? "" : String(organisation.foundedYear),
    website: organisation.website ?? "",
    email: organisation.email ?? "",
    phone: organisation.phone ?? "",
    description: organisation.description ?? "",
    primaryVenueId: organisation.primaryVenue?.id ?? "",
    slug: organisation.slug,
  };
}

const orNull = (value: string) => value.trim() || null;

/** The details both the create and the update send. */
function details(values: OrganisationValues) {
  return {
    type: values.type as OrganisationType,
    displayName: values.displayName.trim(),
    legalName: orNull(values.legalName),
    shortName: orNull(values.shortName),
    city: orNull(values.city),
    foundedYear: values.foundedYear.trim() ? Number(values.foundedYear.trim()) : null,
    website: orNull(values.website),
    email: orNull(values.email),
    phone: orNull(values.phone),
    // Plain text (D4): its line breaks are kept, only the ends trimmed.
    description: orNull(values.description),
    primaryVenueId: values.primaryVenueId || null,
  };
}

const reviewFields = ({ reviewed, reason }: DuplicateDecision) => ({
  duplicatesReviewed: reviewed,
  duplicateReason: reviewed ? orNull(reason) : null,
});

/**
 * Add organisation's request. No slug: the server proposes one from the display name (D9). No country unless one was
 * chosen: the server uses the tenant's.
 */
export function toCreateRequest(values: OrganisationValues, review: DuplicateDecision): CreateOrganisationRequest {
  return { ...details(values), country: orNull(values.country), ...reviewFields(review) };
}

/** Edit details' request: the country and the slug are always sent (a PUT without a country gives the tenant's). */
export function toUpdateRequest(values: OrganisationValues, review: DuplicateDecision): UpdateOrganisationRequest {
  return { ...details(values), country: values.country, slug: values.slug.trim(), ...reviewFields(review) };
}

/** What D13 compares: the display, legal and short names, the type and the city. Needs a type and a keyable name. */
export function toCheckRequest(values: OrganisationValues): CheckOrganisationDuplicatesRequest | null {
  const displayName = values.displayName.trim();
  if (!keyable.test(displayName) || !(organisationTypes as readonly string[]).includes(values.type)) {
    return null;
  }
  return {
    type: values.type as OrganisationType,
    displayName,
    legalName: orNull(values.legalName),
    shortName: orNull(values.shortName),
    city: orNull(values.city),
  };
}

/** The check's input as one key, so the live warning asks again only when one of them changes. */
export function checkKey(request: CheckOrganisationDuplicatesRequest | null): string {
  return request
    ? JSON.stringify([request.type, request.displayName, request.legalName, request.shortName, request.city])
    : "";
}

/**
 * Whether an edit changes what D13 compares (a name, the type or the city), loosely: the server decides by the name and
 * city keys, so a change of case or spacing may still be checked here and pass there.
 */
export function changesComparedFields(opened: OrganisationValues, values: OrganisationValues): boolean {
  const loose = (value: string) => value.trim().replace(/\s+/g, " ").toLowerCase();
  return (
    opened.type !== values.type ||
    (["displayName", "legalName", "shortName", "city"] as const).some(
      (field) => loose(opened[field]) !== loose(values[field]),
    )
  );
}

/**
 * The API's names for the form's fields (`errors[].field`). Add has no web address field, so its `slug` refusal goes to
 * the form's alert instead of a field nobody sees.
 */
export function organisationApiFields(edit: boolean): ApiFields<OrganisationValues> {
  if (edit) {
    return allApiFields;
  }
  return Object.fromEntries(Object.entries(allApiFields).filter(([api]) => api !== "slug"));
}

const allApiFields: ApiFields<OrganisationValues> = {
  type: "type",
  displayName: "displayName",
  legalName: "legalName",
  shortName: "shortName",
  country: "country",
  city: "city",
  foundedYear: "foundedYear",
  website: "website",
  email: "email",
  phone: "phone",
  description: "description",
  primaryVenueId: "primaryVenueId",
  slug: "slug",
};
