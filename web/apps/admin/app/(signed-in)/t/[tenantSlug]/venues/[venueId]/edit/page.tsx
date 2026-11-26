"use client";

import { useMutation, useQueryClient } from "@tanstack/react-query";
import { useParams, useRouter } from "next/navigation";
import { useState } from "react";
import { Loading } from "@/components/loading";
import { ProblemAlert } from "@/components/problem-alert";
import { ForbiddenView, NotFoundView } from "@/components/status-page";
import { UnavailableNote } from "@/components/unavailable-note";
import { BackLink } from "@/components/viewer/facts";
import { actionAccess } from "@/lib/action-access";
import { call, isApiProblem } from "@/lib/api";
import { ifMatch } from "@/lib/concurrency";
import { useTenantWorkspace } from "@/lib/tenant-workspace";
import { tenantPath } from "@/lib/workspace";
import { VenueEditor, type VenueSave } from "../../venue-editor";
import { fromVenue, toUpdateRequest } from "../../venue-form";
import { editVenueActions, useKeepVenue, useVenue, venueQueryKey, type Venue } from "../../venues-data";

/**
 * Registry → Venues → a venue → Edit details (S07 D11, D23): every detail, the web address included, in any status (a
 * correction). The `PUT` replaces them all from the version the page opened on (`If-Match`); a change to the name,
 * the city or the coordinates runs the duplicate warning again. Statuses, aliases and identifiers have their own
 * actions.
 */
export default function EditVenuePage() {
  const { venueId } = useParams<{ venueId: string }>();
  const { tenant, can, unavailable } = useTenantWorkspace();
  const { allowed, reason } = actionAccess(editVenueActions, can, unavailable);
  const queryClient = useQueryClient();
  const venue = useVenue(venueId, true);
  // The form starts once the venue has been read since the page opened (a cached one may be older than its version)
  // and never resets what is being typed; only Reload starts it over (as People's Edit details, review #113 R3).
  const [started, setStarted] = useState(false);
  const [reloading, setReloading] = useState(false);
  if (!started && !reloading && venue.isFetchedAfterMount && venue.isSuccess && !venue.isFetching) {
    setStarted(true);
  }
  const reload = async () => {
    setStarted(false);
    setReloading(true);
    try {
      await queryClient.refetchQueries({ queryKey: venueQueryKey(tenant.tenantId, venueId) });
    } finally {
      setReloading(false);
    }
  };

  if (!can("venue.read")) {
    return <ForbiddenView />;
  }
  if (isApiProblem(venue.error, "VENUE_NOT_FOUND")) {
    return <NotFoundView />;
  }
  const venueHref = `${tenantPath(tenant.slug)}/venues/${venueId}`;
  const back = <BackLink href={venueHref}>{venue.data?.name ?? "Back"}</BackLink>;
  if (!allowed) {
    return reason ? (
      <section className="grid gap-4">
        {back}
        <h1 className="text-2xl font-semibold">Edit details</h1>
        <UnavailableNote id="edit-venue-unavailable" reason={reason} />
      </section>
    ) : (
      <ForbiddenView />
    );
  }
  if (!started || !venue.data) {
    const error = reloading ? null : venue.error;
    return (
      <section className="grid gap-4">
        {back}
        {error ? <ProblemAlert error={error} /> : <Loading />}
      </section>
    );
  }
  return <EditDetails venue={venue.data} venueHref={venueHref} onReload={() => void reload()} />;
}

function EditDetails({
  venue,
  venueHref,
  onReload,
}: Readonly<{ venue: Venue; venueHref: string; onReload: () => void }>) {
  const { client } = useTenantWorkspace();
  const router = useRouter();
  const keep = useKeepVenue();
  // The venue as the page opened on it: the change's basis (#110 R1) and what the warning compares with.
  const [opened] = useState(() => ({ version: venue.version, values: fromVenue(venue) }));
  const save = useMutation({
    mutationFn: ({ values, decision }: VenueSave) =>
      call(
        client.PUT("/api/admin/v1/venues/{venueId}", {
          params: { path: { venueId: venue.id }, header: ifMatch(opened.version) },
          body: toUpdateRequest(values, decision),
        }),
      ),
    onSuccess: (saved) => {
      keep(saved);
      router.push(venueHref);
    },
  });

  return (
    <section className="grid grid-cols-[minmax(0,1fr)] gap-6">
      <BackLink href={venueHref}>{venue.name}</BackLink>
      <header className="grid gap-1">
        <p className="text-sm text-muted-foreground break-words">{venue.name}</p>
        <h1 className="text-2xl font-semibold">Edit details</h1>
        <p className="text-muted-foreground">
          A change to the name, the city or the coordinates is checked against the venues you already have. The status,
          aliases and identifiers are changed on the venue&apos;s page.
        </p>
      </header>
      <VenueEditor
        initial={opened.values}
        venue={venue}
        save={save}
        submitLabel="Save"
        pendingLabel="Saving…"
        cancelHref={venueHref}
        onReload={onReload}
      />
    </section>
  );
}
