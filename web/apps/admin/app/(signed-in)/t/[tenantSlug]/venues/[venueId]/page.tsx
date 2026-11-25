"use client";

import { Tabs, TabsContent, TabsList, TabsTrigger } from "@msp/ui/components/tabs";
import { useParams, usePathname, useRouter, useSearchParams } from "next/navigation";
import type { ReactNode } from "react";
import { Loading } from "@/components/loading";
import { ProblemAlert } from "@/components/problem-alert";
import { StatusHistory } from "@/components/registry/status-history";
import { StatusBadge } from "@/components/status-badge";
import { ForbiddenView, NotFoundView } from "@/components/status-page";
import { BackLink } from "@/components/viewer/facts";
import { isApiProblem } from "@/lib/api";
import { humanizeCode } from "@/lib/codes";
import { useTenantWorkspace } from "@/lib/tenant-workspace";
import { tenantPath } from "@/lib/workspace";
import { useVenue, useVenueStatusChanges, type Venue } from "../venues-data";
import { OverviewTab } from "./overview-tab";
import { TeamsTab } from "./teams-tab";
import { VenueActions, venueStatusId } from "./venue-actions";

function HistoryTab({ venue }: Readonly<{ venue: Venue }>) {
  return <StatusHistory history={useVenueStatusChanges(venue.id)} />;
}

const venueTabs = [
  { value: "overview", label: "Overview", content: OverviewTab },
  { value: "teams", label: "Teams", content: TeamsTab },
  { value: "history", label: "History", content: HistoryTab },
] as const satisfies ReadonlyArray<{ value: string; label: string; content: (props: { venue: Venue }) => ReactNode }>;

type VenueTab = (typeof venueTabs)[number]["value"];

/**
 * Registry → Venues → a venue (S07 D23): the header (name, status, type and city, the status moves), then the tabs
 * Overview, Teams (the teams that call it home) and History. The tab is in the address (`?tab=`). Names & IDs and Edit
 * details come with T6a-2.
 */
export default function VenuePage() {
  const { venueId } = useParams<{ venueId: string }>();
  const searchParams = useSearchParams();
  const pathname = usePathname();
  const router = useRouter();
  const { tenant, can } = useTenantWorkspace();
  const venue = useVenue(venueId);

  if (!can("venue.read")) {
    return <ForbiddenView />;
  }
  if (isApiProblem(venue.error, "VENUE_NOT_FOUND")) {
    return <NotFoundView />;
  }

  const back = <BackLink href={`${tenantPath(tenant.slug)}/venues`}>Venues</BackLink>;
  if (!venue.data) {
    return (
      <section className="grid gap-4">
        {back}
        {venue.isError ? <ProblemAlert error={venue.error} /> : <Loading />}
      </section>
    );
  }

  const data = venue.data;
  const tab: VenueTab = venueTabs.find((item) => item.value === searchParams.get("tab"))?.value ?? "overview";
  const changeTab = (value: string) => {
    const next = new URLSearchParams(searchParams.toString());
    next.set("tab", value);
    router.replace(`${pathname}?${next.toString()}`, { scroll: false });
  };

  return (
    // `minmax(0, 1fr)`: grid tracks would otherwise grow to the tab list's or a table's width and widen the page on a phone.
    <section className="grid grid-cols-[minmax(0,1fr)] gap-6">
      <header className="grid gap-3">
        {back}
        <div className="grid min-w-0 gap-2">
          <h1 className="text-2xl font-semibold break-words">{data.name}</h1>
          <div className="flex flex-wrap items-center gap-2">
            <span id={venueStatusId} tabIndex={-1} className="rounded-md outline-none focus-visible:ring-2 focus-visible:ring-ring">
              <StatusBadge status={data.status} />
            </span>
            <span className="text-muted-foreground">
              {humanizeCode(data.type)} · {data.city}
            </span>
          </div>
        </div>
        <VenueActions venue={data} />
      </header>

      <Tabs value={tab} onValueChange={changeTab} className="min-w-0">
        <TabsList aria-label="Venue">
          {venueTabs.map(({ value, label }) => (
            <TabsTrigger key={value} value={value}>
              {label}
            </TabsTrigger>
          ))}
        </TabsList>
        {venueTabs.map(({ value, label, content: Content }) => (
          <TabsContent key={value} value={value} className="grid grid-cols-[minmax(0,1fr)] gap-6">
            <h2 className="sr-only">{label}</h2>
            <Content venue={data} />
          </TabsContent>
        ))}
      </Tabs>
    </section>
  );
}
