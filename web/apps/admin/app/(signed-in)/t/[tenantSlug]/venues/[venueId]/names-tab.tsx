"use client";

import { NamesAndIdsTab } from "@/components/registry/names-and-ids/names-and-ids-tab";
import type { NamesAndIdsConfig } from "@/components/registry/names-and-ids/names-and-ids-data";
import { limits } from "../venue-form";
import { useVenueNamesApi, type Venue } from "../venues-data";

const actions = {
  addIdentifier: "venue.external_id.add",
  endIdentifier: "venue.external_id.end",
  addAlias: "venue.alias.add",
  removeAlias: "venue.alias.remove",
} as const;

/**
 * Names & IDs (S07 D10, D23): the venue's aliases (any kind, names up to a venue's name's length) and its external
 * identifiers. A closed or archived venue takes no new one (`VENUE_CLOSED`, `VENUE_ARCHIVED`); a temporarily
 * unavailable one does.
 */
export function NamesTab({ venue }: Readonly<{ venue: Venue }>) {
  const api = useVenueNamesApi(venue.id);
  const config: NamesAndIdsConfig = {
    name: venue.name,
    noun: "venue",
    actions,
    closedNote:
      venue.status === "CLOSED" || venue.status === "ARCHIVED"
        ? `${venue.name} is ${venue.status === "CLOSED" ? "closed" : "archived"}: no new one can be added.`
        : null,
    aliasKinds: ["FORMER_NAME", "NICKNAME", "SPELLING"],
    aliasNameMaxLength: limits.name,
    authorityExamples: "KMSA_GROUND_NO or COUNCIL_FACILITY_NO",
  };
  return <NamesAndIdsTab record={venue} config={config} api={api} />;
}
