// Venue test data (S07 T6a): KMSA's venues from pilot-customer.md, supporting the test-only sport (sport-definitions.ts),
// and the answers the venue screens need. Each test file mocks `next/navigation` itself.
import type { components } from "@msp/api-client";
import { tenantCapabilities } from "./capabilities";
import type { Answer, Answers } from "./render";
import { ringballVariant } from "./sport-definitions";

type Schemas = components["schemas"];
type Venue = Schemas["VenueResponse"];
type VenueListItem = Schemas["VenueListItem"];
type TenantCapabilities = Schemas["TenantCapabilitiesResponse"];

export const tenantId = "0199a3f0-0000-7000-8000-00000000000a";

export const me: Answer = {
  status: 200,
  body: {
    id: "0199a3f0-0000-7000-8000-000000000001",
    email: "brian.ssemakula@kmsa.example",
    platformRoles: [],
    tenants: [
      { tenantId, slug: "kmsa", displayName: "Kampala Metro Sports Association", status: "ACTIVE", roles: ["REGISTRAR"] },
    ],
  },
};

const settings: Answer = {
  status: 200,
  body: {
    timeZone: "Africa/Kampala",
    currency: "UGX",
    languageCode: "en",
    startingSoonMinutes: 30,
    requireGuardianConsentForMinors: true,
    publicEnabled: true,
    version: "1",
  },
};

export function venue(overrides: Partial<Venue> = {}): Venue {
  return {
    id: "0199a3f0-0000-7000-8000-0000000000b1",
    name: "Bukoto Community Stadium",
    shortName: "Bukoto Stadium",
    slug: "bukoto-community-stadium",
    type: "STADIUM",
    addressLine: "Plot 12, Bukoto Street",
    city: "Kampala",
    region: "Central",
    country: "UG",
    latitude: 0.3476,
    longitude: 32.5925,
    timeZone: "Africa/Kampala",
    capacity: 3000,
    surfaces: ["NATURAL_GRASS"],
    supportedVariants: ["RINGBALL_SEVENS"],
    status: "ACTIVE",
    aliases: [],
    externalIdentifiers: [],
    version: 4,
    ...overrides,
  };
}

export const bukoto = venue();
export const kisaasi = venue({
  id: "0199a3f0-0000-7000-8000-0000000000b2",
  name: "Kisaasi Recreation Ground",
  shortName: null,
  slug: "kisaasi-recreation-ground",
  type: "SPORTS_GROUND",
  addressLine: null,
  region: null,
  latitude: null,
  longitude: null,
  capacity: null,
  surfaces: [],
  supportedVariants: [],
});
export const metroArena = venue({
  id: "0199a3f0-0000-7000-8000-0000000000b3",
  name: "Metro Indoor Arena",
  slug: "metro-indoor-arena",
  type: "ARENA",
  city: "Naguru",
  status: "TEMPORARILY_UNAVAILABLE",
});

export function listItem(shown: Venue): VenueListItem {
  const { id, name, shortName, slug, type, city, country, supportedVariants, status } = shown;
  return { id, name, shortName, slug, type, city, country, supportedVariants, status };
}

export const listPage = (items: unknown[], nextCursor: string | null = null) => ({
  status: 200,
  body: { items, page: { nextCursor, hasMore: nextCursor !== null } },
});

/** The answers every venue screen needs, with these capabilities (an action list, or the whole answer). */
export function venueAnswers(capabilities: string[] | TenantCapabilities): Answers {
  return {
    "GET /api/account/v1/me": me,
    "GET /api/admin/v1/me/capabilities": {
      status: 200,
      body: Array.isArray(capabilities) ? tenantCapabilities(capabilities) : capabilities,
    },
    "GET /api/admin/v1/tenant/settings": settings,
    "GET /api/public/v1/reference/countries": listPage([{ code: "UG", alpha3Code: "UGA", numericCode: "800", name: "Uganda" }]),
    "GET /api/admin/v1/sports": { status: 200, body: { items: [ringballVariant] } },
  };
}
