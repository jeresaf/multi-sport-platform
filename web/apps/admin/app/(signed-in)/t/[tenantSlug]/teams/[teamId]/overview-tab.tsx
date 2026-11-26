"use client";

import Link from "next/link";
import { Facts, ViewerSection } from "@/components/viewer/facts";
import { humanizeCode } from "@/lib/codes";
import { formatDay } from "@/lib/entitlements";
import { ageGroupLabel, genderCategoryLabel, teamLevelLabel } from "@/lib/team-category";
import { useTenantWorkspace } from "@/lib/tenant-workspace";
import { tenantPath } from "@/lib/workspace";
import { useSportVariants } from "../../sports/sports-data";
import type { Team } from "../teams-data";

const notRecorded = "Not recorded";
const linkClass = "break-words text-primary underline-offset-4 hover:underline";
/** The API's colour form (#RRGGBB, `TeamDetailsRequest`): only such a value is painted. */
const hexColour = /^#[0-9A-Fa-f]{6}$/;

/** A colour as a swatch with its hex beside it: the hex says it to a screen reader and to anyone the swatch fails. */
export function ColourSwatch({ colour }: Readonly<{ colour: string }>) {
  return (
    <span className="flex items-center gap-2">
      {hexColour.test(colour) ? (
        <span
          aria-hidden="true"
          className="inline-block size-5 shrink-0 rounded-sm border"
          // The team's own colour is data, not a design token.
          style={{ backgroundColor: colour }}
        />
      ) : null}
      <span className="font-mono text-sm">{colour.toUpperCase()}</span>
    </span>
  );
}

/**
 * Overview (S07 D23): what the team is (sport named from its definition, category, code, motto, founded, since when it
 * has its name), where it belongs (its organisation, or independent; its home venue) and its colours as swatches with
 * their hex. The logo and badge controls come with T6c-2b.
 */
export function OverviewTab({ team }: Readonly<{ team: Team }>) {
  const { tenant, can } = useTenantWorkspace();
  const sports = useSportVariants();
  const root = tenantPath(tenant.slug);
  const sport = sports.data?.items.find((item) => item.variant === team.variant)?.name ?? humanizeCode(team.variant);
  const colours = [
    ["Primary colour", team.primaryColour],
    ["Secondary colour", team.secondaryColour],
    ["Third colour", team.tertiaryColour],
  ] as const;

  return (
    <>
      <ViewerSection title="Team">
        <Facts
          items={[
            ["Name", team.name],
            ["Short name", team.shortName ?? "None"],
            ["Name since", formatDay(team.nameSince)],
            ["Sport", sport],
            ["Age group", ageGroupLabel(team.ageGroup)],
            ["Gender", genderCategoryLabel(team.genderCategory, team.ageGroup)],
            ["Level", teamLevelLabel(team.level)],
            ["Code", team.code ?? "None"],
            ["Motto", team.motto ?? "None"],
            ["Founded", team.foundedYear === null ? notRecorded : String(team.foundedYear)],
          ]}
        />
      </ViewerSection>
      <ViewerSection title="Organisation and ground">
        <Facts
          items={[
            [
              "Organisation",
              team.organisation === null ? (
                "Independent"
              ) : can("organisation.read") ? (
                <Link href={`${root}/organisations/${team.organisation.id}`} className={linkClass}>
                  {team.organisation.displayName}
                </Link>
              ) : (
                team.organisation.displayName
              ),
            ],
            [
              "Home venue",
              team.homeVenue === null ? (
                "None"
              ) : can("venue.read") ? (
                <Link href={`${root}/venues/${team.homeVenue.id}`} className={linkClass}>
                  {team.homeVenue.name}
                </Link>
              ) : (
                team.homeVenue.name
              ),
            ],
          ]}
        />
      </ViewerSection>
      <ViewerSection title="Colours">
        <Facts items={colours.map(([label, colour]) => [label, colour ? <ColourSwatch colour={colour} /> : "None"] as const)} />
      </ViewerSection>
    </>
  );
}
