"use client";

// The organisation's primary venue (S07 D4): optional, found by a search of the venue directory (`venue.read`). A
// closed venue is shown but can't be chosen (`ORGANISATION_PRIMARY_VENUE_UNAVAILABLE`); archived ones aren't listed.
// The venue the organisation already has stays chosen whatever its status (the update keeps it), until it is removed
// or another is chosen; removed, it can be chosen again from the search, closed or not (#140 Q1).
import { RecordSearchField } from "@/components/registry/record-search-field";
import { humanizeCode } from "@/lib/codes";
import { useTenantWorkspace } from "@/lib/tenant-workspace";
import { useVenueChoices } from "./organisations-data";

/** Whether a venue may become an organisation's primary venue or a team's home venue: not closed or archived (D4, D6). */
export const choosableVenue = (status: string) => status !== "CLOSED" && status !== "ARCHIVED";

export function PrimaryVenueField({
  value,
  onChange,
  saved,
  error,
}: Readonly<{
  /** The chosen venue's ID ("" for none). */
  value: string;
  onChange: (venueId: string) => void;
  /** The venue the organisation has now (Edit), kept as a choice whatever its status. */
  saved?: { id: string; name: string } | null;
  error?: string;
}>) {
  const { can } = useTenantWorkspace();
  return (
    <RecordSearchField
      id="organisation-primary-venue"
      noun="venue"
      legend="Primary venue (optional)"
      hint={`Where it is based or usually plays. Closed venues can't be chosen${saved ? ", except the one it already has." : "."}`}
      value={value}
      onChange={(venueId) => onChange(venueId)}
      saved={saved}
      error={error}
      canSearch={can("venue.read")}
      noAccess="Choosing a venue needs access to the venues."
      useChoices={useVenueChoices}
      nameOf={(item) => item.name}
      describe={(item) => ({
        facts: [humanizeCode(item.type), item.city].filter(Boolean).join(" · "),
        unavailable: choosableVenue(item.status) ? null : "Closed: it can't be a primary venue.",
      })}
      clearLabel="No primary venue"
      chosenMessage={(name) => `${name} chosen as the primary venue.`}
    />
  );
}
