"use client";

import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from "@msp/ui/components/table";
import { StatusHistory } from "@/components/registry/status-history";
import { RowName, ViewerSection, wide } from "@/components/viewer/facts";
import { formatDay } from "@/lib/entitlements";
import { useTeamStatusChanges, type Team, type TeamFormerName } from "../teams-data";

/** A former name's dates: "1 Jan 2018 to 31 Dec 2025" (the last day it was used). */
export function formerNamePeriod(name: Pick<TeamFormerName, "validFrom" | "validTo">): string {
  return `${formatDay(name.validFrom)} to ${formatDay(name.validTo)}`;
}

/**
 * History (S07 D23): the status changes, newest first (as the other registries'), then the former names (D9) with the
 * dates each was used, newest first, and the short name and code the team had with it. A former name is never removed.
 */
export function HistoryTab({ team }: Readonly<{ team: Team }>) {
  const statusChanges = useTeamStatusChanges(team.id);
  return (
    <>
      <ViewerSection title="Status">
        <StatusHistory history={statusChanges} />
      </ViewerSection>
      <ViewerSection title="Former names">
        {team.formerNames.length === 0 ? (
          <p className="text-muted-foreground">{team.name} has had no other name.</p>
        ) : (
          <Table>
            <TableHeader>
              <TableRow>
                <TableHead>Name</TableHead>
                <TableHead className={wide}>Used</TableHead>
                <TableHead className={wide}>Short name</TableHead>
                <TableHead className={wide}>Code</TableHead>
              </TableRow>
            </TableHeader>
            <TableBody>
              {team.formerNames.map((former) => {
                const period = formerNamePeriod(former);
                const shortName = former.shortName ?? "None";
                const code = former.code ?? "None";
                return (
                  <TableRow key={former.id}>
                    <TableHead scope="row" className="h-auto py-2 align-top whitespace-normal">
                      <RowName
                        name={former.name}
                        details={[
                          ["Used", period],
                          ["Short name", shortName],
                          ["Code", code],
                        ]}
                      />
                    </TableHead>
                    <TableCell className={wide}>{period}</TableCell>
                    <TableCell className={`${wide} whitespace-normal`}>{shortName}</TableCell>
                    <TableCell className={wide}>{code}</TableCell>
                  </TableRow>
                );
              })}
            </TableBody>
          </Table>
        )}
      </ViewerSection>
    </>
  );
}
