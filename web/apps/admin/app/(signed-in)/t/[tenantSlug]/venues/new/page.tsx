"use client";

import { useMutation } from "@tanstack/react-query";
import { useRouter } from "next/navigation";
import { Loading } from "@/components/loading";
import { ForbiddenView } from "@/components/status-page";
import { UnavailableNote } from "@/components/unavailable-note";
import { BackLink } from "@/components/viewer/facts";
import { actionAccess } from "@/lib/action-access";
import { call } from "@/lib/api";
import { useTenantTimeZoneOnceRead } from "@/lib/tenant-settings";
import { useTenantWorkspace } from "@/lib/tenant-workspace";
import { tenantPath } from "@/lib/workspace";
import { VenueEditor, type VenueSave } from "../venue-editor";
import { emptyVenue, toCreateRequest } from "../venue-form";
import { addVenueActions, useKeepVenue } from "../venues-data";

/**
 * Registry → Venues → Add venue (S07 D3, D13, D23): the venue's details with the live duplicate warning; the time zone
 * starts as the organisation's, the country is the organisation's unless one is chosen, and the web address is
 * proposed by the server from the name (D9). Saved, it opens the new venue.
 */
export default function AddVenuePage() {
  const { tenant, client, can, unavailable } = useTenantWorkspace();
  const router = useRouter();
  const keep = useKeepVenue();
  const zone = useTenantTimeZoneOnceRead();
  const { allowed, reason } = actionAccess(addVenueActions, can, unavailable);
  const venuesHref = `${tenantPath(tenant.slug)}/venues`;
  const save = useMutation({
    mutationFn: ({ values, decision }: VenueSave) =>
      call(client.POST("/api/admin/v1/venues", { body: toCreateRequest(values, decision) })),
    onSuccess: (venue) => {
      keep(venue);
      router.push(`${venuesHref}/${venue.id}`);
    },
  });

  if (!allowed && !reason) {
    return <ForbiddenView />;
  }
  return (
    <section className="grid grid-cols-[minmax(0,1fr)] gap-6">
      <BackLink href={venuesHref}>Venues</BackLink>
      <header className="grid gap-1">
        <h1 className="text-2xl font-semibold">Add venue</h1>
        <p className="text-muted-foreground">
          A place where contests are played. Its name and city are checked against the venues you already have.
        </p>
      </header>
      {!allowed ? (
        <UnavailableNote id="add-venue-unavailable" reason={reason!} />
      ) : zone.isLoading ? (
        <Loading />
      ) : (
        <VenueEditor
          initial={emptyVenue(zone.timeZone)}
          save={save}
          submitLabel="Add venue"
          pendingLabel="Adding…"
          cancelHref={venuesHref}
        />
      )}
    </section>
  );
}
