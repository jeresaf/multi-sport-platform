"use client";

import { NamesAndIdsTab } from "@/components/registry/names-and-ids/names-and-ids-tab";
import type { NamesAndIdsConfig } from "@/components/registry/names-and-ids/names-and-ids-data";
import { limits } from "../team-form";
import { acceptsNewChildren, useTeamNamesApi, type Team } from "../teams-data";
import { closedNote } from "./image-controls";

const actions = {
  addIdentifier: "team.external_id.add",
  endIdentifier: "team.external_id.end",
  addAlias: "team.alias.add",
  removeAlias: "team.alias.remove",
} as const;

/**
 * Names & IDs (S07 D10, D23): the team's nicknames and other spellings (names up to a team name's length; its former
 * names come from a rename and are in History) and its external identifiers. A disbanded or archived team takes no new
 * one (`TEAM_DISBANDED`, `TEAM_ARCHIVED`); its aliases can still be removed and its identifiers ended.
 */
export function NamesTab({ team }: Readonly<{ team: Team }>) {
  const api = useTeamNamesApi(team.id);
  const config: NamesAndIdsConfig = {
    name: team.name,
    noun: "team",
    actions,
    closedNote: acceptsNewChildren(team.status) ? null : closedNote(team, "one"),
    aliasKinds: ["NICKNAME", "SPELLING"],
    aliasNameMaxLength: limits.name,
    authorityExamples: "KMSA_TEAM_NO or LEAGUE_REG_NO",
  };
  return <NamesAndIdsTab record={team} config={config} api={api} />;
}
