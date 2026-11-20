"use client";

import type { components } from "@msp/api-client";
import { Download } from "lucide-react";
import { StatusBadge } from "@/components/status-badge";
import { Facts, ViewerSection } from "@/components/viewer/facts";
import { humanizeCode } from "@/lib/codes";
import { formatDay } from "@/lib/entitlements";
import { formatDateTime, useTenantTimeZone } from "@/lib/tenant-settings";
import type { Person, PersonPrivate } from "../people-data";

type ConsentScope = components["schemas"]["ConsentScope"];

const scopeLabels: Readonly<Record<ConsentScope, string>> = {
  DATA_PROCESSING: "Data processing",
  PUBLIC_PROFILE: "Public profile",
  PHOTO: "Photo",
};

export const scopeList = (scopes: readonly ConsentScope[]) =>
  scopes.length > 0 ? scopes.map((scope) => scopeLabels[scope]).join(", ") : "None";

/** The Consent tab is shown for someone who isn't known to be an adult, or who has a consent on record (S06 T7). */
export function showsConsent(person: Person): boolean {
  return person.ageStatus !== "ADULT" || person.consents.length > 0;
}

/**
 * Consent: what the guardians have agreed to (S06 D14), active consents first. Everyone with `person.read` sees the
 * scopes, dates and status; the guardian (name, relationship, contact) and the evidence are `PRIVATE` and shown only
 * from the private read (`privateDetails`, `person.read_private`).
 */
export function ConsentTab({ person, privateDetails }: Readonly<{ person: Person; privateDetails: PersonPrivate | undefined }>) {
  const timeZone = useTenantTimeZone();
  const consents = [...person.consents].sort(
    (a, b) => Number(b.status === "ACTIVE") - Number(a.status === "ACTIVE") || b.recordedAt.localeCompare(a.recordedAt),
  );

  return (
    <>
      <ViewerSection title="What is agreed now">
        <p>{scopeList(person.activeConsentScopes)}</p>
        {person.ageStatus !== "ADULT" ? (
          <p className="text-sm text-muted-foreground">
            {person.ageStatus === "MINOR"
              ? "A minor's public profile and photo are shown only with their guardian's consent."
              : "Without a date of birth this person is protected as a minor: their public profile and photo are shown only with a guardian's consent."}
          </p>
        ) : null}
      </ViewerSection>

      <ViewerSection title="Consent records">
        {consents.length === 0 ? <p className="text-muted-foreground">No consent recorded yet.</p> : null}
        {consents.map((consent) => {
          const details = privateDetails?.consents.find((item) => item.id === consent.id);
          const evidence = details?.evidence ? (
            <a
              href={details.evidence.url}
              download
              className="inline-flex min-h-11 items-center gap-1 text-primary underline-offset-4 hover:underline"
            >
              <Download aria-hidden="true" className="size-4" />
              Download the signed form
            </a>
          ) : consent.hasEvidence ? (
            "On file"
          ) : (
            "None"
          );
          return (
            <article key={consent.id} className="grid gap-3 rounded-lg border p-4">
              <header className="flex flex-wrap items-center gap-2">
                <h4 className="font-semibold">Given on {formatDay(consent.givenOn)}</h4>
                <StatusBadge status={consent.status} />
              </header>
              <Facts
                items={[
                  ["Agreed to", scopeList(consent.scopes)],
                  ...(details
                    ? ([
                        ["Guardian", details.guardianName],
                        ["Relationship", details.relationship ? humanizeCode(details.relationship) : "Not recorded"],
                        ["Contact", details.contact],
                      ] as const)
                    : []),
                  ["Evidence", evidence],
                  ["Recorded", formatDateTime(consent.recordedAt, timeZone)],
                  ...(consent.withdrawnAt
                    ? ([
                        ["Withdrawn", formatDateTime(consent.withdrawnAt, timeZone)],
                        ["Reason", consent.withdrawalReason ?? "No reason given"],
                      ] as const)
                    : []),
                ]}
              />
            </article>
          );
        })}
      </ViewerSection>
    </>
  );
}
