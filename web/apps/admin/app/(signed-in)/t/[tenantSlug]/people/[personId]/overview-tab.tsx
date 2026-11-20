"use client";

import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from "@msp/ui/components/table";
import { Loading } from "@/components/loading";
import { ProblemAlert } from "@/components/problem-alert";
import { Facts, RowName, ViewerSection, wide } from "@/components/viewer/facts";
import { humanizeCode } from "@/lib/codes";
import { formatDay } from "@/lib/entitlements";
import { useCountries } from "@/lib/reference-data";
import { formatDateTime, useTenantTimeZone } from "@/lib/tenant-settings";
import { usePersonStatusChanges, type Person, type PersonPrivate } from "../people-data";

const notRecorded = "Not recorded";

/**
 * Overview: the biodata, then the status history. The date of birth is `PRIVATE` (S06 D3): only with
 * `person.read_private` (`privateDetails`); everyone else sees the birth year.
 */
export function OverviewTab({ person, privateDetails }: Readonly<{ person: Person; privateDetails: PersonPrivate | undefined }>) {
  const countries = useCountries();
  const timeZone = useTenantTimeZone();
  const history = usePersonStatusChanges(person.id);
  const country = (code: string | null) =>
    code ? (countries.data?.items.find((item) => item.code === code)?.name ?? code) : notRecorded;
  const born = privateDetails
    ? [["Date of birth", privateDetails.dateOfBirth ? formatDay(privateDetails.dateOfBirth) : notRecorded] as const]
    : [["Born", person.birthYear?.toString() ?? notRecorded] as const];

  const changes = history.data?.items ?? [];
  return (
    <>
      <ViewerSection title="Biodata">
        <Facts
          items={[
            ["First name", person.firstName],
            ["Middle name", person.middleName ?? "None"],
            ["Last name", person.lastName],
            ["Other names", person.otherNames ?? "None"],
            ["Display name", person.displayName],
            ["Gender", person.gender ? humanizeCode(person.gender) : notRecorded],
            ...born,
            ["Nationality", country(person.nationality)],
            ["Second nationality", person.secondaryNationality ? country(person.secondaryNationality) : "None"],
            ["Height", person.heightCm === null ? notRecorded : `${person.heightCm} cm`],
            ["Weight", person.weightKg === null ? notRecorded : `${person.weightKg} kg`],
            ["Public profile", person.publicProfileEnabled ? "On" : "Off"],
          ]}
        />
      </ViewerSection>

      <ViewerSection title="Status history">
        {history.isPending ? <Loading /> : null}
        {history.isError ? <ProblemAlert error={history.error} /> : null}
        {history.isSuccess && changes.length === 0 ? <p className="text-muted-foreground">No changes recorded.</p> : null}
        {changes.length > 0 ? (
          <Table>
            <TableHeader>
              <TableRow>
                <TableHead>Change</TableHead>
                <TableHead className={wide}>When</TableHead>
                <TableHead className={wide}>Reason</TableHead>
              </TableRow>
            </TableHeader>
            <TableBody>
              {changes.map((change) => {
                const label = change.fromStatus
                  ? `${humanizeCode(change.fromStatus)} → ${humanizeCode(change.toStatus)}`
                  : `Recorded as ${humanizeCode(change.toStatus).toLowerCase()}`;
                const when = formatDateTime(change.occurredAt, timeZone);
                const reason = change.reason ?? "No reason given";
                return (
                  <TableRow key={change.id}>
                    <TableHead scope="row" className="h-auto py-2 align-top whitespace-normal">
                      <RowName name={label} details={[["When", when], ["Reason", reason]]} />
                    </TableHead>
                    <TableCell className={wide}>{when}</TableCell>
                    <TableCell className={`${wide} max-w-96 break-words whitespace-normal`}>{reason}</TableCell>
                  </TableRow>
                );
              })}
            </TableBody>
          </Table>
        ) : null}
      </ViewerSection>
    </>
  );
}
