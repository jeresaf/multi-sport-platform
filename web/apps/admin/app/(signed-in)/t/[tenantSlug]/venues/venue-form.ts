// Add venue's and Edit details' form (S07 D3, D9, D13): the values as typed, their checks (the server's limits, which
// stay authoritative) and the requests the duplicate check, the create and the update send. Empty optional fields are
// sent as null. Every code here is generic (AR-11); sports are variant codes from their definitions.
import { z } from "zod";
import type { ApiFields } from "@/lib/form-errors";
import {
  venueSurfaces,
  venueTypes,
  type CheckVenueDuplicatesRequest,
  type CreateVenueRequest,
  type UpdateVenueRequest,
  type Venue,
  type VenueSurface,
  type VenueType,
} from "./venues-data";

// VenueDetails, Slug and IVenueChangeRequest on the server.
export const limits = {
  name: 120,
  shortName: 40,
  addressLine: 200,
  city: 100,
  region: 100,
  capacity: 200_000,
  surfaces: 5,
  supportedVariants: 20,
  slug: { min: 2, max: 80 },
  duplicateReason: 500,
} as const;

/** A slug (D9): lower-case letters and digits in words joined by single hyphens. */
export const slugPattern = /^[a-z0-9]+(-[a-z0-9]+)*$/;
const uuidPattern = /^[0-9a-f]{8}-?[0-9a-f]{4}-?[0-9a-f]{4}-?[0-9a-f]{4}-?[0-9a-f]{12}$/i;
/** A name the server can key: at least one letter or digit, in any script. */
const keyable = /[\p{L}\p{N}]/u;

const text = (max: number, label: string) =>
  z.string().refine((value) => value.trim().length <= max, `Keep ${label} to ${max} characters.`);
const required = (max: number, label: string) =>
  text(max, label).refine((value) => value.trim().length > 0, `Enter the ${label}.`);

/** A number within a range with at most `decimals` decimal places; empty is allowed (the field is optional). */
function inRange(value: string, min: number, max: number, decimals: number): boolean {
  const trimmed = value.trim();
  if (!trimmed) {
    return true;
  }
  const number = Number(trimmed);
  return Number.isFinite(number) && number >= min && number <= max && Number(number.toFixed(decimals)) === number;
}

const coordinate = (max: number, label: string) =>
  z.string().refine((value) => inRange(value, -max, max, 6), `Enter the ${label} from -${max} to ${max}, with at most six decimals.`);

/** Edit details' slug check: Add has no slug field (the server proposes one, owner, T6a-2 plan). */
export const slugSchema = z
  .string()
  .trim()
  .min(limits.slug.min, `Use ${limits.slug.min} to ${limits.slug.max} characters.`)
  .max(limits.slug.max, `Use ${limits.slug.min} to ${limits.slug.max} characters.`)
  .refine((value) => slugPattern.test(value), "Use lower-case letters and digits, with single hyphens between words.")
  .refine((value) => !uuidPattern.test(value), "A web address can't look like a record's ID.");

/** The form's checks: Add's (`edit` false, no slug) or Edit details' (with the slug). */
export const venueSchema = (edit: boolean) =>
  z
  .object({
    name: required(limits.name, "name").refine(
      (value) => !value.trim() || keyable.test(value),
      "The name needs at least one letter or digit.",
    ),
    shortName: text(limits.shortName, "the short name"),
    type: z
      .string()
      .refine((value) => (venueTypes as readonly string[]).includes(value), "Choose what kind of place it is."),
    addressLine: text(limits.addressLine, "the address"),
    city: required(limits.city, "city"),
    region: text(limits.region, "the region"),
    country: z.string(),
    timeZone: z.string().min(1, "Choose the venue's time zone."),
    latitude: coordinate(90, "latitude"),
    longitude: coordinate(180, "longitude"),
    capacity: z
      .string()
      .refine(
        (value) => inRange(value, 0, limits.capacity, 0),
        `Enter whole seats from 0 to ${limits.capacity.toLocaleString("en-GB")}.`,
      ),
    surfaces: z.array(z.enum(venueSurfaces)).max(limits.surfaces, `Choose at most ${limits.surfaces} surfaces.`),
    supportedVariants: z
      .array(z.string())
      .max(limits.supportedVariants, `Choose at most ${limits.supportedVariants} sports.`),
    slug: edit ? slugSchema : z.string(),
  })
  .superRefine((values, context) => {
    const latitude = values.latitude.trim();
    const longitude = values.longitude.trim();
    if (latitude && !longitude) {
      context.addIssue({ code: "custom", path: ["longitude"], message: "Enter the longitude too, or neither." });
    } else if (longitude && !latitude) {
      context.addIssue({ code: "custom", path: ["latitude"], message: "Enter the latitude too, or neither." });
    }
  });

export type VenueValues = z.infer<ReturnType<typeof venueSchema>>;

export function emptyVenue(timeZone: string): VenueValues {
  return {
    name: "",
    shortName: "",
    type: "",
    addressLine: "",
    city: "",
    region: "",
    country: "",
    timeZone,
    latitude: "",
    longitude: "",
    capacity: "",
    surfaces: [],
    supportedVariants: [],
    slug: "",
  };
}

/** The venue's saved details as the form shows them. */
export function fromVenue(venue: Venue): VenueValues {
  return {
    name: venue.name,
    shortName: venue.shortName ?? "",
    type: venue.type,
    addressLine: venue.addressLine ?? "",
    city: venue.city,
    region: venue.region ?? "",
    country: venue.country,
    timeZone: venue.timeZone,
    latitude: venue.latitude === null ? "" : String(venue.latitude),
    longitude: venue.longitude === null ? "" : String(venue.longitude),
    capacity: venue.capacity === null ? "" : String(venue.capacity),
    surfaces: [...venue.surfaces],
    supportedVariants: [...venue.supportedVariants],
    slug: venue.slug,
  };
}

const orNull = (value: string) => value.trim() || null;
const numberOrNull = (value: string) => (value.trim() ? Number(value.trim()) : null);

/** The details both the create and the update send. */
function details(values: VenueValues) {
  return {
    name: values.name.trim(),
    shortName: orNull(values.shortName),
    type: values.type as VenueType,
    addressLine: orNull(values.addressLine),
    city: values.city.trim(),
    region: orNull(values.region),
    timeZone: values.timeZone,
    latitude: numberOrNull(values.latitude),
    longitude: numberOrNull(values.longitude),
    capacity: numberOrNull(values.capacity),
    surfaces: values.surfaces as VenueSurface[],
    supportedVariants: values.supportedVariants,
  };
}

/** What the venues that may match were reviewed with: "This is a different one" and its optional reason. */
export type DuplicateReview = { reviewed: boolean; reason: string };

const reviewFields = ({ reviewed, reason }: DuplicateReview) => ({
  duplicatesReviewed: reviewed,
  duplicateReason: reviewed ? orNull(reason) : null,
});

/**
 * Add venue's request. No slug: the server proposes one from the name (D9). No country unless one was chosen: the
 * server uses the organisation's.
 */
export function toCreateRequest(values: VenueValues, review: DuplicateReview): CreateVenueRequest {
  return { ...details(values), country: orNull(values.country), ...reviewFields(review) };
}

/** Edit details' request: the venue's country is always sent (a PUT without one gives it the tenant's, #118 Q1). */
export function toUpdateRequest(values: VenueValues, review: DuplicateReview): UpdateVenueRequest {
  return { ...details(values), country: values.country, slug: values.slug.trim(), ...reviewFields(review) };
}

/** What D13 compares: the name, the city and the coordinates (only when both are numbers in range). */
export function toCheckRequest(values: VenueValues): CheckVenueDuplicatesRequest | null {
  const name = values.name.trim();
  const city = values.city.trim();
  if (!keyable.test(name) || !keyable.test(city)) {
    return null;
  }
  const latitude = numberOrNull(values.latitude);
  const longitude = numberOrNull(values.longitude);
  const located =
    latitude !== null &&
    longitude !== null &&
    inRange(values.latitude, -90, 90, 6) &&
    inRange(values.longitude, -180, 180, 6);
  return { name, city, latitude: located ? latitude : null, longitude: located ? longitude : null };
}

/** The check's input as one key, so the live warning asks again only when one of them changes. */
export function checkKey(request: CheckVenueDuplicatesRequest | null): string {
  return request ? JSON.stringify([request.name, request.city, request.latitude, request.longitude]) : "";
}

/**
 * Whether an edit changes what D13 compares (the name, the city or the coordinates), loosely: the server decides by the
 * name and city keys, so a change of case or spacing may still be checked here and pass there.
 */
export function changesComparedFields(opened: VenueValues, values: VenueValues): boolean {
  const loose = (value: string) => value.trim().replace(/\s+/g, " ").toLowerCase();
  return (
    loose(opened.name) !== loose(values.name) ||
    loose(opened.city) !== loose(values.city) ||
    numberOrNull(opened.latitude) !== numberOrNull(values.latitude) ||
    numberOrNull(opened.longitude) !== numberOrNull(values.longitude)
  );
}

/**
 * The API's names for the form's fields (`errors[].field`); a refused sport or surface is named by its index. Add has no
 * web address field, so its `slug` refusal goes to the form's alert instead of a field nobody sees (#138 R2).
 */
export function venueApiFields(edit: boolean): ApiFields<VenueValues> {
  if (edit) {
    return allVenueApiFields;
  }
  return Object.fromEntries(Object.entries(allVenueApiFields).filter(([api]) => api !== "slug"));
}

const allVenueApiFields: ApiFields<VenueValues> = {
  name: "name",
  shortName: "shortName",
  type: "type",
  addressLine: "addressLine",
  city: "city",
  region: "region",
  country: "country",
  timeZone: "timeZone",
  latitude: "latitude",
  longitude: "longitude",
  capacity: "capacity",
  surfaces: "surfaces",
  supportedVariants: "supportedVariants",
  slug: "slug",
  ...Object.fromEntries([
    ...Array.from({ length: limits.supportedVariants }, (_, index) => [
      `supportedVariants[${index}]`,
      "supportedVariants",
    ]),
    ...Array.from({ length: limits.surfaces }, (_, index) => [`surfaces[${index}]`, "surfaces"]),
  ]),
};
