"use client";

import { RecordImageControl, type RecordImageConfig } from "@/components/registry/image-control";
import { humanizeCode } from "@/lib/codes";
import { acceptsNewChildren, useTeamImageApi, type Team, type TeamImage } from "../teams-data";

/** Why a disbanded or archived team takes no new child ("Bunga Lakeside FC is disbanded: no new badge can be added."). */
export function closedNote(team: Pick<Team, "name" | "status">, what: string): string {
  return `${team.name} is ${humanizeCode(team.status).toLowerCase()}: no new ${what} can be added.`;
}

function TeamImageControl({ team, image }: Readonly<{ team: Team; image: TeamImage }>) {
  const api = useTeamImageApi(team.id, image);
  const config: RecordImageConfig = {
    id: `team-${image}`,
    image,
    name: team.name,
    actions: { set: `team.${image}.set`, remove: `team.${image}.remove` },
    hasImage: team[image] !== null,
    closedNote: acceptsNewChildren(team.status) ? null : closedNote(team, image),
  };
  return <RecordImageControl record={team} config={config} api={api} />;
}

/**
 * The team's logo and badge (S07 D15, T4b), in the header under the actions as the organisation's logo (owner, plan
 * 2026-11-27): `team.logo.set`/`team.badge.set` add or replace one, `.remove` takes it down. A disbanded or archived
 * team gets no new image; removing works in any status.
 */
export function TeamImageControls({ team }: Readonly<{ team: Team }>) {
  return (
    <div className="flex flex-wrap gap-4">
      <TeamImageControl team={team} image="logo" />
      <TeamImageControl team={team} image="badge" />
    </div>
  );
}
