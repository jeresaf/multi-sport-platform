// Test data for Registry → People (S06): people from the pilot customer's registry (pilot-customer.md), as the admin
// reads answer them. Positions and roles are the test-only ringball definition's (test/sport-definitions.ts).
import type { components } from "@msp/api-client";

type Schemas = components["schemas"];
type Person = Schemas["PersonResponse"];
type PersonListItem = Schemas["PersonListItem"];

export const brendaId = "0199a3f0-0000-7000-8000-0000000000b1";
export const denisId = "0199a3f0-0000-7000-8000-0000000000d1";
export const nakatoId = "0199a3f0-0000-7000-8000-0000000000e1";
export const johnId = "0199a3f0-0000-7000-8000-0000000000f1";

const basePerson: Person = {
  id: johnId,
  firstName: "John",
  middleName: null,
  lastName: "Ssekandi",
  otherNames: null,
  displayName: "John Ssekandi",
  gender: "MALE",
  birthYear: 1998,
  isMinor: false,
  ageStatus: "ADULT",
  nationality: "UG",
  secondaryNationality: null,
  heightCm: 178,
  weightKg: 72.5,
  publicProfileEnabled: false,
  status: "ACTIVE",
  source: "MANUAL",
  aliases: [],
  externalIdentifiers: [],
  athleteProfiles: [],
  staffProfiles: [],
  officialProfiles: [],
  photo: null,
  activeConsentScopes: [],
  consents: [],
  version: 7,
};

export function person(overrides: Partial<Person> = {}): Person {
  return { ...basePerson, ...overrides };
}

/** A 15-year-old under-17 player with her mother's consent (Data processing, Public profile) and a scanned form. */
export const brenda = person({
  id: brendaId,
  firstName: "Brenda",
  lastName: "Akello",
  displayName: "Brenda Akello",
  gender: "FEMALE",
  birthYear: 2011,
  isMinor: true,
  ageStatus: "MINOR",
  heightCm: null,
  weightKg: null,
  photo: {
    thumbnailUrl: "/api/public/v1/media/thumb-token",
    mediumUrl: "/api/public/v1/media/medium-token",
    largeUrl: "/api/public/v1/media/large-token",
    expiresAt: "2026-11-20T10:10:00Z",
  },
  activeConsentScopes: ["DATA_PROCESSING", "PUBLIC_PROFILE"],
  consents: [
    {
      id: "0199a3f0-0000-7000-8000-0000000000c2",
      scopes: ["DATA_PROCESSING", "PUBLIC_PROFILE"],
      givenOn: "2026-11-02",
      status: "ACTIVE",
      hasEvidence: true,
      recordedAt: "2026-11-03T07:00:00Z",
      withdrawnAt: null,
      withdrawalReason: null,
      replacesConsentId: "0199a3f0-0000-7000-8000-0000000000c1",
    },
    {
      id: "0199a3f0-0000-7000-8000-0000000000c1",
      scopes: ["DATA_PROCESSING", "PUBLIC_PROFILE", "PHOTO"],
      givenOn: "2026-11-02",
      status: "WITHDRAWN",
      hasEvidence: true,
      recordedAt: "2026-11-02T07:00:00Z",
      withdrawnAt: "2026-11-03T07:00:00Z",
      withdrawalReason: "Her mother asked for the photo to come down",
      replacesConsentId: null,
    },
  ],
});

export const brendaPrivate: Schemas["PersonPrivateResponse"] = {
  id: brendaId,
  dateOfBirth: "2011-03-14",
  consents: [
    {
      id: "0199a3f0-0000-7000-8000-0000000000c2",
      guardianName: "Grace Akello",
      relationship: "MOTHER",
      contact: "+256 772 000 147",
      evidence: { url: "/api/public/v1/media/evidence-token", expiresAt: "2026-11-20T10:10:00Z" },
    },
    {
      id: "0199a3f0-0000-7000-8000-0000000000c1",
      guardianName: "Grace Akello",
      relationship: "MOTHER",
      contact: "+256 772 000 147",
      evidence: { url: "/api/public/v1/media/evidence-token", expiresAt: "2026-11-20T10:10:00Z" },
    },
  ],
};

/** Athlete, assistant coach and referee at once (the slice demo's Denis Opoka), in the test sport's words. */
export const denis = person({
  id: denisId,
  firstName: "Denis",
  lastName: "Opoka",
  displayName: "Denis Opoka",
  birthYear: 1991,
  aliases: [{ id: "0199a3f0-0000-7000-8000-0000000000a1", name: "Opoka D.", kind: "SPELLING" }],
  externalIdentifiers: [
    {
      id: "0199a3f0-0000-7000-8000-0000000000a2",
      authority: "KMSA_REGISTRATION",
      value: "KM-2026-0147",
      validFrom: "2026-01-15",
      validTo: null,
    },
    {
      id: "0199a3f0-0000-7000-8000-0000000000a3",
      authority: "KMSA_REGISTRATION",
      value: "KM-2019-0032",
      validFrom: "2019-02-01",
      validTo: "2025-12-31",
    },
  ],
  athleteProfiles: [
    {
      id: "0199a3f0-0000-7000-8000-0000000000a4",
      variant: "RINGBALL_SEVENS",
      primaryPosition: "WING_RINGER",
      otherPositions: ["HOOP_GUARD"],
      dominantSide: "LEFT",
      publicName: null,
      status: "ACTIVE",
    },
  ],
  staffProfiles: [
    {
      id: "0199a3f0-0000-7000-8000-0000000000a5",
      capabilities: [{ variant: "RINGBALL_SEVENS", role: "HEAD_COACH", qualification: "Level 1 coaching certificate" }],
      status: "ACTIVE",
    },
  ],
  officialProfiles: [
    {
      id: "0199a3f0-0000-7000-8000-0000000000a6",
      capabilities: [
        {
          variant: "RINGBALL_SEVENS",
          role: "RING_JUDGE",
          grade: "Class 2",
          licenceNumber: "RJ-0193",
          licenceValidUntil: "2026-06-30",
          licenceExpired: true,
        },
      ],
      status: "INACTIVE",
    },
  ],
});

/** Four name parts, no date of birth (so "Age unknown"), no photo, no profile. */
export const nakato = person({
  id: nakatoId,
  firstName: "Nakato",
  middleName: "Irene",
  lastName: "Namaganda",
  otherNames: "Nalubega",
  displayName: "Nakato Irene Nalubega Namaganda",
  gender: "FEMALE",
  birthYear: null,
  isMinor: true,
  ageStatus: "UNKNOWN",
  nationality: null,
});

export function listItem(from: Person): PersonListItem {
  const { id, displayName, firstName, lastName, status, birthYear, isMinor, ageStatus, nationality } = from;
  return { id, displayName, firstName, lastName, status, birthYear, isMinor, ageStatus, nationality };
}

export const john = person();
