// Organisation test data (S07 T6b): KMSA's clubs from pilot-customer.md and the answers the organisation screens need.
// Each test file mocks `next/navigation` itself.
import type { components } from "@msp/api-client";
import { listPage, me, venueAnswers } from "./venues";

type Schemas = components["schemas"];
type Organisation = Schemas["OrganisationResponse"];
type OrganisationListItem = Schemas["OrganisationListItem"];

export { listPage, me };

export function organisation(overrides: Partial<Organisation> = {}): Organisation {
  return {
    id: "0199a3f0-0000-7000-8000-0000000000e1",
    type: "CLUB",
    displayName: "Ntinda Swifts SC",
    legalName: "Ntinda Swifts Sports Club Limited",
    shortName: "Ntinda Swifts",
    slug: "ntinda-swifts-sc",
    country: "UG",
    city: "Kampala",
    foundedYear: 1998,
    website: "https://ntinda-swifts.example",
    email: "secretary@ntinda-swifts.example",
    phone: "+256 700 000 101",
    description: "A multi-sport community club in Ntinda.",
    primaryVenue: { id: "0199a3f0-0000-7000-8000-0000000000b1", name: "Bukoto Community Stadium", slug: "bukoto-community-stadium" },
    parent: null,
    children: [],
    teams: [],
    status: "ACTIVE",
    logo: null,
    aliases: [],
    externalIdentifiers: [],
    version: 3,
    ...overrides,
  };
}

export const ntinda = organisation();
export const kisaasi = organisation({
  id: "0199a3f0-0000-7000-8000-0000000000e2",
  displayName: "Kisaasi Kestrels SC",
  legalName: null,
  shortName: null,
  slug: "kisaasi-kestrels-sc",
  city: null,
  foundedYear: null,
  website: null,
  email: null,
  phone: null,
  description: null,
  primaryVenue: null,
});
export const academy = organisation({
  id: "0199a3f0-0000-7000-8000-0000000000e3",
  type: "ACADEMY",
  displayName: "Ntinda Swifts Academy",
  legalName: null,
  shortName: null,
  slug: "ntinda-swifts-academy",
  status: "SUSPENDED",
});
export const seeta = organisation({
  id: "0199a3f0-0000-7000-8000-0000000000e4",
  displayName: "Seeta Sparrows FC",
  slug: "seeta-sparrows-fc",
  city: "Mukono",
  status: "DISSOLVED",
});

export const summary = ({ id, displayName, slug, status }: Organisation) => ({ id, displayName, slug, status });

export function listItem(shown: Organisation): OrganisationListItem {
  const { id, displayName, shortName, slug, type, city, country, status } = shown;
  return { id, displayName, shortName, slug, type, city, country, status };
}

/** The answers every organisation screen needs, with these capabilities. */
export const organisationAnswers = venueAnswers;

/** An organisation the duplicate check answers (D13), from the organisation it names. */
export function candidate(
  shown: Organisation,
  confidence: "LIKELY" | "POSSIBLE" = "LIKELY",
  evidence: Array<{ code: string; value: string | null }> = [
    { code: "SAME_NAME_KEY", value: null },
    { code: "SAME_TYPE", value: null },
  ],
) {
  const { id: organisationId, displayName, type, city, status } = shown;
  return { organisationId, displayName, type, city, status, confidence, evidence };
}
