// Team test data (S07 T6c): KMSA's teams from pilot-customer.md, playing the test-only sport (sport-definitions.ts),
// and the answers the team screens need. Each test file mocks `next/navigation` itself.
import type { components } from "@msp/api-client";
import { listPage, me, venueAnswers } from "./venues";

type Schemas = components["schemas"];
type Team = Schemas["TeamResponse"];
type TeamListItem = Schemas["TeamListItem"];

export { listPage, me };

const actor = { actorKind: "USER", actorId: "0199a3f0-0000-7000-8000-000000000001" } as const;

export function team(overrides: Partial<Team> = {}): Team {
  return {
    id: "0199a3f0-0000-7000-8000-0000000000f1",
    variant: "RINGBALL_SEVENS",
    genderCategory: "MEN",
    ageGroup: "SENIOR",
    level: "FIRST",
    name: "Mutundwe Meteors FC",
    shortName: "Meteors",
    nameSince: "2026-01-01",
    slug: "mutundwe-meteors-fc",
    code: "MUT",
    motto: "Higher together",
    // A team's own colours are data the API stores (#RRGGBB), not design tokens; the second in lower case, as typed.
    // eslint-disable-next-line no-restricted-syntax
    primaryColour: "#1A3D7C",
    // eslint-disable-next-line no-restricted-syntax
    secondaryColour: "#f2c230",
    tertiaryColour: null,
    foundedYear: 2018,
    organisation: { id: "0199a3f0-0000-7000-8000-0000000000e5", displayName: "Mutundwe Meteors SC", slug: "mutundwe-meteors-sc" },
    homeVenue: { id: "0199a3f0-0000-7000-8000-0000000000b1", name: "Bukoto Community Stadium", slug: "bukoto-community-stadium" },
    status: "ACTIVE",
    logo: null,
    badge: null,
    formerNames: [
      {
        id: "0199a3f0-0000-7000-8000-0000000000f9",
        name: "Mutundwe Weavers FC",
        shortName: "Weavers",
        code: "MWV",
        validFrom: "2018-01-01",
        validTo: "2025-12-31",
      },
    ],
    aliases: [],
    externalIdentifiers: [],
    version: 5,
    ...overrides,
  };
}

export const meteors = team();
export const sparrows = team({
  id: "0199a3f0-0000-7000-8000-0000000000f2",
  name: "Seeta Sparrows FC Under-17",
  shortName: null,
  nameSince: "2020-02-01",
  slug: "seeta-sparrows-fc-under-17",
  ageGroup: "U17",
  code: null,
  motto: null,
  primaryColour: null,
  secondaryColour: null,
  foundedYear: null,
  organisation: { id: "0199a3f0-0000-7000-8000-0000000000e4", displayName: "Seeta Sparrows FC", slug: "seeta-sparrows-fc" },
  homeVenue: null,
  formerNames: [],
  version: 2,
});
export const kingfishers = team({
  id: "0199a3f0-0000-7000-8000-0000000000f3",
  name: "Kireka Kingfishers FC Reserves",
  shortName: null,
  slug: "kireka-kingfishers-fc-reserves",
  level: "RESERVE",
  organisation: null,
  homeVenue: null,
  formerNames: [],
  status: "DISBANDED",
});

export function listItem(shown: Team): TeamListItem {
  const { id, name, shortName, slug, variant, genderCategory, ageGroup, level, organisation, status } = shown;
  return { id, name, shortName, slug, variant, genderCategory, ageGroup, level, organisation, status };
}

/** The first row of every team's status history: the team as created. */
export const created = {
  id: "0199a3f0-0000-7000-8000-0000000000c1",
  fromStatus: null,
  toStatus: "ACTIVE",
  reason: null,
  occurredAt: "2026-11-02T07:30:00Z",
  ...actor,
};

/** A later status change, newest first in a history. */
export function statusChange(id: string, fromStatus: string, toStatus: string, reason: string, occurredAt: string) {
  return { id, fromStatus, toStatus, reason, occurredAt, ...actor };
}

/** The answers every team screen needs, with these capabilities. */
export const teamAnswers = venueAnswers;
