"use client";

import { Button } from "@msp/ui/components/button";
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from "@msp/ui/components/table";
import Link from "next/link";
import { Loading } from "@/components/loading";
import { ProblemAlert } from "@/components/problem-alert";
import { StatusBadge } from "@/components/status-badge";
import { RowName, wide } from "@/components/viewer/facts";
import { humanizeCode } from "@/lib/codes";
import { teamCategoryLabel } from "@/lib/team-category";
import { useTenantWorkspace } from "@/lib/tenant-workspace";
import { tenantPath } from "@/lib/workspace";
import { useSportVariants } from "../../sports/sports-data";
import { useHomeTeams, type Venue } from "../venues-data";

/**
 * Teams that call it home (S07 D23): the teams whose home venue this is, archived ones left out, in the team
 * directory's order. A team's name opens its page.
 */
export function TeamsTab({ venue }: Readonly<{ venue: Venue }>) {
  const { tenant, can } = useTenantWorkspace();
  const teams = useHomeTeams(venue.id);
  const sports = useSportVariants();
  if (!can("team.read")) {
    return <p className="text-muted-foreground">Your role doesn&apos;t include viewing teams.</p>;
  }

  const sportName = (code: string) => sports.data?.items.find((item) => item.variant === code)?.name ?? humanizeCode(code);
  const items = teams.data?.pages.flatMap((page) => page.items) ?? [];
  return (
    <>
      {teams.isPending ? <Loading /> : null}
      {teams.isError ? <ProblemAlert error={teams.error} /> : null}
      {teams.isSuccess && items.length === 0 ? (
        <p className="text-muted-foreground">No team calls {venue.name} home.</p>
      ) : null}
      {items.length > 0 ? (
        <Table>
          <TableHeader>
            <TableRow>
              <TableHead>Team</TableHead>
              <TableHead className={wide}>Category</TableHead>
              <TableHead className={wide}>Sport</TableHead>
              <TableHead className={wide}>Organisation</TableHead>
              <TableHead className={wide}>Status</TableHead>
            </TableRow>
          </TableHeader>
          <TableBody>
            {items.map((team) => {
              const category = teamCategoryLabel(team);
              const sport = sportName(team.variant);
              const organisation = team.organisation?.displayName ?? "Independent";
              return (
                <TableRow key={team.id}>
                  <TableHead scope="row" className="h-auto max-w-64 py-2 align-top whitespace-normal tablet:max-w-80">
                    <RowName
                      name={
                        <Link
                          href={`${tenantPath(tenant.slug)}/teams/${team.id}`}
                          className="break-words text-primary underline-offset-4 hover:underline"
                        >
                          {team.name}
                        </Link>
                      }
                      details={[
                        ["Category", category],
                        ["Sport", sport],
                        ["Organisation", organisation],
                        ["Status", humanizeCode(team.status)],
                      ]}
                    />
                  </TableHead>
                  <TableCell className={wide}>{category}</TableCell>
                  <TableCell className={wide}>{sport}</TableCell>
                  <TableCell className={`${wide} whitespace-normal`}>{organisation}</TableCell>
                  <TableCell className={wide}>
                    <StatusBadge status={team.status} />
                  </TableCell>
                </TableRow>
              );
            })}
          </TableBody>
        </Table>
      ) : null}
      {teams.hasNextPage ? (
        <Button
          variant="outline"
          className="justify-self-start"
          disabled={teams.isFetchingNextPage}
          onClick={() => void teams.fetchNextPage()}
        >
          {teams.isFetchingNextPage ? "Loading…" : "Show more"}
        </Button>
      ) : null}
    </>
  );
}
