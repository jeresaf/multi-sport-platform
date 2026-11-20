"use client";

import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from "@msp/ui/components/table";
import { NoneOr, RowName, ViewerSection, wide } from "@/components/viewer/facts";
import { humanizeCode } from "@/lib/codes";
import { formatDay } from "@/lib/entitlements";
import type { Person } from "../people-data";

/**
 * External IDs & aliases (S06 D10): the identifiers other bodies gave the person (ended ones included, an identifier
 * is never reused) and the other names they are known by (removed ones are not listed).
 */
export function IdentifiersTab({ person }: Readonly<{ person: Person }>) {
  return (
    <>
      <ViewerSection title="External identifiers">
        <NoneOr empty={person.externalIdentifiers.length === 0}>
          <Table>
            <TableHeader>
              <TableRow>
                <TableHead>Identifier</TableHead>
                <TableHead className={wide}>Authority</TableHead>
                <TableHead className={wide}>Valid</TableHead>
              </TableRow>
            </TableHeader>
            <TableBody>
              {person.externalIdentifiers.map((identifier) => {
                const valid = validity(identifier.validFrom, identifier.validTo);
                return (
                  <TableRow key={identifier.id}>
                    <TableHead scope="row" className="h-auto py-2 align-top whitespace-normal">
                      <RowName
                        name={<span className="font-mono break-all">{identifier.value}</span>}
                        details={[["Authority", identifier.authority], ["Valid", valid]]}
                      />
                    </TableHead>
                    <TableCell className={`${wide} break-all whitespace-normal`}>{identifier.authority}</TableCell>
                    <TableCell className={wide}>{valid}</TableCell>
                  </TableRow>
                );
              })}
            </TableBody>
          </Table>
        </NoneOr>
      </ViewerSection>

      <ViewerSection title="Aliases">
        <NoneOr empty={person.aliases.length === 0}>
          <Table>
            <TableHeader>
              <TableRow>
                <TableHead>Name</TableHead>
                <TableHead className={wide}>Kind</TableHead>
              </TableRow>
            </TableHeader>
            <TableBody>
              {person.aliases.map((alias) => (
                <TableRow key={alias.id}>
                  <TableHead scope="row" className="h-auto py-2 align-top whitespace-normal">
                    <RowName
                      name={<span className="break-words">{alias.name}</span>}
                      details={[["Kind", humanizeCode(alias.kind)]]}
                    />
                  </TableHead>
                  <TableCell className={wide}>{humanizeCode(alias.kind)}</TableCell>
                </TableRow>
              ))}
            </TableBody>
          </Table>
        </NoneOr>
      </ViewerSection>
    </>
  );
}

/** "From 1 Jan 2026", "1 Jan 2026 to 30 Jun 2026 (ended)", "Current". */
export function validity(validFrom: string | null, validTo: string | null): string {
  if (validTo) {
    return `${validFrom ? `${formatDay(validFrom)} to ` : "Until "}${formatDay(validTo)} (ended)`;
  }
  return validFrom ? `From ${formatDay(validFrom)}` : "Current";
}
