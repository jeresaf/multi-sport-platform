"use client";

import { NamesAndIdsTab } from "@/components/registry/names-and-ids/names-and-ids-tab";
import type { NamesAndIdsConfig } from "@/components/registry/names-and-ids/names-and-ids-data";
import { humanizeCode } from "@/lib/codes";
import { limits } from "../organisation-form";
import { acceptsNewChildren, useOrganisationNamesApi, type Organisation } from "../organisations-data";

const actions = {
  addIdentifier: "organisation.external_id.add",
  endIdentifier: "organisation.external_id.end",
  addAlias: "organisation.alias.add",
  removeAlias: "organisation.alias.remove",
} as const;

/**
 * Names & IDs (S07 D10, D23): the organisation's aliases (any kind, names up to a display name's length) and its
 * external identifiers. A dissolved or archived organisation takes no new one (`ORGANISATION_DISSOLVED`,
 * `ORGANISATION_ARCHIVED`); its aliases can still be removed and its identifiers ended.
 */
export function NamesTab({ organisation }: Readonly<{ organisation: Organisation }>) {
  const api = useOrganisationNamesApi(organisation.id);
  const config: NamesAndIdsConfig = {
    name: organisation.displayName,
    noun: "organisation",
    actions,
    closedNote: acceptsNewChildren(organisation.status)
      ? null
      : `${organisation.displayName} is ${humanizeCode(organisation.status).toLowerCase()}: no new one can be added.`,
    aliasKinds: ["FORMER_NAME", "NICKNAME", "SPELLING"],
    aliasNameMaxLength: limits.displayName,
    authorityExamples: "KMSA_CLUB_NO or COMPANY_REG_NO",
  };
  return <NamesAndIdsTab record={organisation} config={config} api={api} />;
}
