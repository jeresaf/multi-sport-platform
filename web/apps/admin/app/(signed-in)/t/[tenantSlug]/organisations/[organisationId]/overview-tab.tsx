"use client";

import { Button } from "@msp/ui/components/button";
import Link from "next/link";
import { useState } from "react";
import { StatusBadge } from "@/components/status-badge";
import { UnavailableNote } from "@/components/unavailable-note";
import { Facts, ViewerSection } from "@/components/viewer/facts";
import { humanizeCode } from "@/lib/codes";
import { useCountries } from "@/lib/reference-data";
import { useTenantWorkspace } from "@/lib/tenant-workspace";
import { tenantPath } from "@/lib/workspace";
import { acceptsNewChildren, type Organisation } from "../organisations-data";
import { ParentDialog, parentAction } from "./parent-dialog";

const notRecorded = "Not recorded";
const linkClass = "break-words text-primary underline-offset-4 hover:underline";
/** Only a web address is a link (never `javascript:` or another scheme a stored value could carry). */
const webLink = /^https?:\/\//i;
const parentNoteId = "organisation-parent-note";

/** A record's link with its status beside it when it isn't active (a suspended parent, a dissolved child). */
function RecordLink({ href, name, status }: Readonly<{ href: string; name: string; status: string }>) {
  return (
    <span className="flex flex-wrap items-center gap-2">
      <Link href={href} className={linkClass}>
        {name}
      </Link>
      {status === "ACTIVE" ? null : <StatusBadge status={status} />}
    </span>
  );
}

/**
 * Overview (S07 D23): what the organisation is (names, type, founded), how to reach it (city, country, website,
 * email, phone, primary venue), and where it sits: its parent, with Change parent, and its children (archived ones
 * left out).
 */
export function OverviewTab({ organisation }: Readonly<{ organisation: Organisation }>) {
  const { tenant, can, unavailable } = useTenantWorkspace();
  const countries = useCountries();
  const [changing, setChanging] = useState(false);
  const root = tenantPath(tenant.slug);
  const country = countries.data?.items.find((item) => item.code === organisation.country)?.name ?? organisation.country;
  const organisationHref = (id: string) => `${root}/organisations/${id}`;
  // A viewer holds neither the action nor a reason it is unavailable: no control at all.
  const parentRefusal = can(parentAction) ? null : unavailable(parentAction);
  const parentOffered = can(parentAction) || parentRefusal !== null;
  // A dissolved or archived organisation takes no new parent; one it has can still be cleared (D5).
  const closed = acceptsNewChildren(organisation.status)
    ? null
    : `${humanizeCode(organisation.status)}: it can't take a parent.`;
  const parentNote = parentRefusal ?? (organisation.parent ? null : closed);

  return (
    <>
      <ViewerSection title="Organisation">
        <Facts
          items={[
            ["Name", organisation.displayName],
            ["Legal name", organisation.legalName ?? notRecorded],
            ["Short name", organisation.shortName ?? "None"],
            ["Type", humanizeCode(organisation.type)],
            ["Founded", organisation.foundedYear === null ? notRecorded : String(organisation.foundedYear)],
            ["Description", organisation.description ?? "None"],
          ]}
        />
      </ViewerSection>
      <ViewerSection title="Contact">
        <Facts
          items={[
            ["City", organisation.city ?? notRecorded],
            ["Country", country],
            [
              "Website",
              organisation.website && webLink.test(organisation.website) ? (
                <a href={organisation.website} target="_blank" rel="noopener noreferrer" className={linkClass}>
                  {organisation.website}
                  <span className="sr-only"> (opens in a new tab)</span>
                </a>
              ) : (
                (organisation.website ?? notRecorded)
              ),
            ],
            ["Email", organisation.email ?? notRecorded],
            ["Phone", organisation.phone ?? notRecorded],
            [
              "Primary venue",
              organisation.primaryVenue && !can("venue.read") ? (
                organisation.primaryVenue.name
              ) : organisation.primaryVenue ? (
                <Link href={`${root}/venues/${organisation.primaryVenue.id}`} className={linkClass}>
                  {organisation.primaryVenue.name}
                </Link>
              ) : (
                "None"
              ),
            ],
          ]}
        />
      </ViewerSection>
      <ViewerSection title="Structure">
        <Facts
          items={[
            [
              "Parent",
              organisation.parent ? (
                <RecordLink
                  href={organisationHref(organisation.parent.id)}
                  name={organisation.parent.displayName}
                  status={organisation.parent.status}
                />
              ) : (
                "None"
              ),
            ],
            [
              "Member organisations",
              organisation.children.length > 0 ? (
                <ul aria-label={`Organisations under ${organisation.displayName}`} className="grid gap-1">
                  {organisation.children.map((child) => (
                    <li key={child.id}>
                      <RecordLink href={organisationHref(child.id)} name={child.displayName} status={child.status} />
                    </li>
                  ))}
                </ul>
              ) : (
                "None"
              ),
            ],
          ]}
        />
        {parentOffered ? (
          <div className="grid gap-2 justify-self-start">
            <Button
              variant="outline"
              onClick={() => setChanging(true)}
              {...(parentNote ? { disabled: true, "aria-describedby": parentNoteId } : {})}
            >
              {organisation.parent ? (closed ? "Remove parent" : "Change parent") : "Set parent"}
            </Button>
            {parentNote ? <UnavailableNote id={parentNoteId} reason={parentNote} /> : null}
          </div>
        ) : null}
      </ViewerSection>
      {changing ? <ParentDialog organisation={organisation} onClose={() => setChanging(false)} /> : null}
    </>
  );
}
