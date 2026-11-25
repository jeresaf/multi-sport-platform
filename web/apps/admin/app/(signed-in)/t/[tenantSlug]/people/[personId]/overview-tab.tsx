"use client";

import { StatusHistory } from "@/components/registry/status-history";
import { Facts, ViewerSection } from "@/components/viewer/facts";
import { humanizeCode } from "@/lib/codes";
import { formatDay } from "@/lib/entitlements";
import { useCountries } from "@/lib/reference-data";
import { usePersonStatusChanges, type Person, type PersonPrivate } from "../people-data";

const notRecorded = "Not recorded";

/**
 * Overview: the biodata, then the status history. The date of birth is `PRIVATE` (S06 D3): only with
 * `person.read_private` (`privateDetails`); everyone else sees the birth year.
 */
export function OverviewTab({ person, privateDetails }: Readonly<{ person: Person; privateDetails: PersonPrivate | undefined }>) {
  const countries = useCountries();
  const history = usePersonStatusChanges(person.id);
  const country = (code: string | null) =>
    code ? (countries.data?.items.find((item) => item.code === code)?.name ?? code) : notRecorded;
  const born = privateDetails
    ? [["Date of birth", privateDetails.dateOfBirth ? formatDay(privateDetails.dateOfBirth) : notRecorded] as const]
    : [["Born", person.birthYear?.toString() ?? notRecorded] as const];

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
        <StatusHistory history={history} />
      </ViewerSection>
    </>
  );
}
