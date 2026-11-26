"use client";

import { Button } from "@msp/ui/components/button";
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from "@msp/ui/components/table";
import { Plus } from "lucide-react";
import Link from "next/link";
import { StatusBadge } from "@/components/status-badge";
import { RowName, wide } from "@/components/viewer/facts";
import { humanizeCode } from "@/lib/codes";
import { teamCategoryLabel } from "@/lib/team-category";
import { useTenantWorkspace } from "@/lib/tenant-workspace";
import { tenantPath } from "@/lib/workspace";
import { useSportVariants } from "../../sports/sports-data";
import { addTeamActions } from "../../teams/teams-data";
import { acceptsNewChildren, type Organisation } from "../organisations-data";

/**
 * Teams (S07 D23): the organisation's teams, archived ones left out, in the team directory's order (the read's
 * `teams`, not paged), with their sport named from its definition (AR-29). An organisation's status never changes its
 * teams' (pack RULE-ORG-005). A team's name opens its page when the user may read teams. **Add team** opens Add
 * team with the organisation chosen (owner, T6c-2a plan), while it can take a team (not dissolved or archived, D6).
 */
export function TeamsTab({ organisation }: Readonly<{ organisation: Organisation }>) {
  const { tenant, can } = useTenantWorkspace();
  const sports = useSportVariants();
  const sportName = (code: string) =>
    sports.data?.items.find((item) => item.variant === code)?.name ?? humanizeCode(code);
  // Offered only when it would work: the add page says why otherwise (a read-only tenant, say).
  const add =
    acceptsNewChildren(organisation.status) && addTeamActions.every((action) => can(action)) ? (
      <div>
        <Button asChild variant="outline">
          <Link href={`${tenantPath(tenant.slug)}/teams/new?organisationId=${organisation.id}`}>
            <Plus aria-hidden="true" />
            Add team
          </Link>
        </Button>
      </div>
    ) : null;
  if (organisation.teams.length === 0) {
    return (
      <>
        <p className="text-muted-foreground">{organisation.displayName} has no teams yet.</p>
        {add}
      </>
    );
  }
  return (
    <>
      {add}
      <Table>
        <TableHeader>
          <TableRow>
            <TableHead>Team</TableHead>
            <TableHead className={wide}>Category</TableHead>
            <TableHead className={wide}>Sport</TableHead>
            <TableHead className={wide}>Status</TableHead>
          </TableRow>
        </TableHeader>
        <TableBody>
          {organisation.teams.map((team) => {
            const category = teamCategoryLabel(team);
            const sport = sportName(team.variant);
            return (
              <TableRow key={team.id}>
                <TableHead scope="row" className="h-auto max-w-64 py-2 align-top whitespace-normal tablet:max-w-80">
                  <RowName
                    name={
                      can("team.read") ? (
                        <Link
                          href={`${tenantPath(tenant.slug)}/teams/${team.id}`}
                          className="break-words text-primary underline-offset-4 hover:underline"
                        >
                          {team.name}
                        </Link>
                      ) : (
                        team.name
                      )
                    }
                    details={[
                      ["Category", category],
                      ["Sport", sport],
                      ["Status", humanizeCode(team.status)],
                    ]}
                  />
                </TableHead>
                <TableCell className={wide}>{category}</TableCell>
                <TableCell className={wide}>{sport}</TableCell>
                <TableCell className={wide}>
                  <StatusBadge status={team.status} />
                </TableCell>
              </TableRow>
            );
          })}
        </TableBody>
      </Table>
    </>
  );
}
