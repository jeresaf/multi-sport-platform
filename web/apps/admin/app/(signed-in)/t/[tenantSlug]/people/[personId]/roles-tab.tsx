"use client";

import { Badge } from "@msp/ui/components/badge";
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from "@msp/ui/components/table";
import type { ReactNode } from "react";
import { EmptyState } from "@/components/empty-state";
import { StatusBadge } from "@/components/status-badge";
import { Facts, RowName, ViewerSection, wide } from "@/components/viewer/facts";
import { humanizeCode } from "@/lib/codes";
import { formatDay } from "@/lib/entitlements";
import type { Person } from "../people-data";
import { useSportLabels } from "./sport-labels";

/** A profile's card: its title and status, then what it holds. */
function ProfileCard({ title, status, children }: Readonly<{ title: string; status: string; children: ReactNode }>) {
  return (
    <article className="grid gap-3 rounded-lg border p-4">
      <header className="flex flex-wrap items-center gap-2">
        <h4 className="font-semibold break-words">{title}</h4>
        <StatusBadge status={status} />
      </header>
      {children}
    </article>
  );
}

/**
 * Roles: the person's athlete profiles (one per sport), staff profile and official profile (S06 D12), archived ones
 * included, each with its status. Sports, positions and roles are named by the definitions.
 */
export function RolesTab({ person }: Readonly<{ person: Person }>) {
  const { athleteProfiles, staffProfiles, officialProfiles } = person;
  const labels = useSportLabels([
    ...athleteProfiles.map((profile) => profile.variant),
    ...staffProfiles.flatMap((profile) => profile.capabilities.map((capability) => capability.variant)),
    ...officialProfiles.flatMap((profile) => profile.capabilities.map((capability) => capability.variant)),
  ]);

  if (athleteProfiles.length + staffProfiles.length + officialProfiles.length === 0) {
    return (
      <EmptyState title="No roles yet">
        {person.displayName} has no athlete, staff or official profile.
      </EmptyState>
    );
  }

  return (
    <>
      {athleteProfiles.length > 0 ? (
        <ViewerSection title="Athlete">
          {athleteProfiles.map((profile) => (
            <ProfileCard key={profile.id} title={labels.variant(profile.variant)} status={profile.status}>
              <Facts
                items={[
                  [
                    "Primary position",
                    profile.primaryPosition ? labels.position(profile.variant, profile.primaryPosition) : "None",
                  ],
                  [
                    "Other positions",
                    profile.otherPositions.length > 0
                      ? profile.otherPositions.map((code) => labels.position(profile.variant, code)).join(", ")
                      : "None",
                  ],
                  ["Dominant side", profile.dominantSide ? humanizeCode(profile.dominantSide) : "Not recorded"],
                  ["Public name", profile.publicName ?? `${person.displayName} (the display name)`],
                ]}
              />
            </ProfileCard>
          ))}
        </ViewerSection>
      ) : null}

      {staffProfiles.length > 0 ? (
        <ViewerSection title="Staff">
          {staffProfiles.map((profile) => (
            <ProfileCard key={profile.id} title="Staff profile" status={profile.status}>
              <Table>
                <TableHeader>
                  <TableRow>
                    <TableHead>Role</TableHead>
                    <TableHead className={wide}>Sport</TableHead>
                    <TableHead className={wide}>Qualification</TableHead>
                  </TableRow>
                </TableHeader>
                <TableBody>
                  {profile.capabilities.map((capability) => {
                    const sport = labels.variant(capability.variant);
                    const qualification = capability.qualification ?? "None";
                    return (
                      <TableRow key={`${capability.variant} ${capability.role}`}>
                        <TableHead scope="row" className="h-auto py-2 align-top whitespace-normal">
                          <RowName
                            name={labels.staffRole(capability.variant, capability.role)}
                            details={[["Sport", sport], ["Qualification", qualification]]}
                          />
                        </TableHead>
                        <TableCell className={wide}>{sport}</TableCell>
                        <TableCell className={`${wide} whitespace-normal`}>{qualification}</TableCell>
                      </TableRow>
                    );
                  })}
                </TableBody>
              </Table>
            </ProfileCard>
          ))}
        </ViewerSection>
      ) : null}

      {officialProfiles.length > 0 ? (
        <ViewerSection title="Official">
          {officialProfiles.map((profile) => (
            <ProfileCard key={profile.id} title="Official profile" status={profile.status}>
              <Table>
                <TableHeader>
                  <TableRow>
                    <TableHead>Role</TableHead>
                    <TableHead className={wide}>Sport</TableHead>
                    <TableHead className={wide}>Grade</TableHead>
                    <TableHead className={wide}>Licence</TableHead>
                  </TableRow>
                </TableHeader>
                <TableBody>
                  {profile.capabilities.map((capability) => {
                    const sport = labels.variant(capability.variant);
                    const grade = capability.grade ?? "None";
                    const licence = (
                      <>
                        {capability.licenceNumber ?? "No licence number"}
                        {capability.licenceValidUntil ? `, valid until ${formatDay(capability.licenceValidUntil)}` : null}
                        {capability.licenceExpired ? (
                          <Badge variant="outline" className="ml-2">
                            Expired
                          </Badge>
                        ) : null}
                      </>
                    );
                    return (
                      <TableRow key={`${capability.variant} ${capability.role}`}>
                        <TableHead scope="row" className="h-auto py-2 align-top whitespace-normal">
                          <RowName
                            name={labels.officialRole(capability.variant, capability.role)}
                            details={[["Sport", sport], ["Grade", grade], ["Licence", licence]]}
                          />
                        </TableHead>
                        <TableCell className={wide}>{sport}</TableCell>
                        <TableCell className={`${wide} whitespace-normal`}>{grade}</TableCell>
                        <TableCell className={`${wide} whitespace-normal`}>{licence}</TableCell>
                      </TableRow>
                    );
                  })}
                </TableBody>
              </Table>
            </ProfileCard>
          ))}
        </ViewerSection>
      ) : null}
    </>
  );
}
