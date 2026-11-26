"use client";

import { Button } from "@msp/ui/components/button";
import { Input } from "@msp/ui/components/input";
import { Label } from "@msp/ui/components/label";
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from "@msp/ui/components/table";
import { Plus } from "lucide-react";
import Link from "next/link";
import { usePathname, useRouter, useSearchParams } from "next/navigation";
import { useEffect, useMemo, useRef, useState } from "react";
import { EmptyState } from "@/components/empty-state";
import { Loading } from "@/components/loading";
import { ProblemAlert } from "@/components/problem-alert";
import { SelectField } from "@/components/select-field";
import { StatusBadge } from "@/components/status-badge";
import { ForbiddenView } from "@/components/status-page";
import { UnavailableNote } from "@/components/unavailable-note";
import { RowName, orNone, wide } from "@/components/viewer/facts";
import { actionAccess } from "@/lib/action-access";
import { humanizeCode } from "@/lib/codes";
import { useCountries } from "@/lib/reference-data";
import { useTenantWorkspace } from "@/lib/tenant-workspace";
import { tenantPath } from "@/lib/workspace";
import { useSportVariants } from "../sports/sports-data";
import { addVenueActions, useVenues, venueFilters, venueStatuses, type VenueFilters } from "./venues-data";

/** How long the search and the city wait after the last key before they ask. */
const searchDelay = 300;

type Typed = { q: string; city: string };

/**
 * Registry → Venues (S07 D23): the tenant's venues sorted by name, searched by name, alias or external identifier and
 * filtered by city, status and sport. The search and filters are in the address, so going back from a venue keeps
 * them. The sport filter lists every published variant: a ground can host a sport the tenant doesn't run yet (D3).
 */
export default function VenuesPage() {
  const { tenant, can, unavailable } = useTenantWorkspace();
  const add = actionAccess(addVenueActions, can, unavailable);
  const searchParams = useSearchParams();
  const pathname = usePathname();
  const router = useRouter();
  const filters = useMemo(() => venueFilters(new URLSearchParams(searchParams.toString())), [searchParams]);
  const venues = useVenues(filters);
  const sports = useSportVariants();
  const countries = useCountries();
  const [typed, setTyped] = useState<Typed>({ q: filters.q ?? "", city: filters.city ?? "" });
  // The text the page last put in the address: different text came from elsewhere (the navigation's entry, going
  // back), so the boxes show it rather than writing their own back.
  const written = useRef<Typed>({ q: filters.q ?? "", city: filters.city ?? "" });

  const change = (next: VenueFilters) => {
    const merged = { ...filters, ...next };
    written.current = { q: merged.q ?? "", city: merged.city ?? "" };
    const params = new URLSearchParams();
    for (const [key, value] of Object.entries(merged)) {
      if (value) {
        params.set(key, value);
      }
    }
    const query = params.toString();
    router.replace(query ? `${pathname}?${query}` : pathname, { scroll: false });
  };

  useEffect(() => {
    const fromAddress = { q: filters.q ?? "", city: filters.city ?? "" };
    if (fromAddress.q !== written.current.q || fromAddress.city !== written.current.city) {
      written.current = fromAddress;
      setTyped(fromAddress);
    }
  }, [filters.q, filters.city]);

  // The text follows the typing once it pauses; the selects change at once.
  const q = typed.q.trim();
  const city = typed.city.trim();
  useEffect(() => {
    if (q === (filters.q ?? "") && city === (filters.city ?? "")) {
      return;
    }
    const timer = setTimeout(() => change({ q: q || undefined, city: city || undefined }), searchDelay);
    return () => clearTimeout(timer);
    // `change` is rebuilt each render from these same filters.
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [q, city, filters]);

  if (!can("venue.read")) {
    return <ForbiddenView />;
  }

  const variants = sports.data?.items ?? [];
  const sportName = (code: string) => variants.find((variant) => variant.variant === code)?.name ?? humanizeCode(code);
  const countryName = (code: string) => countries.data?.items.find((country) => country.code === code)?.name ?? code;
  const items = venues.data?.pages.flatMap((page) => page.items) ?? [];
  const filtered = Boolean(filters.q || filters.city || filters.status || filters.variant);
  const venueHref = (id: string) => `${tenantPath(tenant.slug)}/venues/${id}`;

  return (
    <section className="grid grid-cols-[minmax(0,1fr)] gap-6">
      <header className="flex flex-wrap items-end justify-between gap-3">
        <div className="grid gap-1">
          <h1 className="text-2xl font-semibold">Venues</h1>
          <p className="text-muted-foreground">The grounds, halls and courts where your organisation&apos;s contests are played.</p>
        </div>
        {add.allowed ? (
          <Button asChild>
            <Link href={`${tenantPath(tenant.slug)}/venues/new`}>
              <Plus aria-hidden="true" />
              Add venue
            </Link>
          </Button>
        ) : add.reason ? (
          <Button disabled aria-describedby="add-venue-unavailable">
            <Plus aria-hidden="true" />
            Add venue
          </Button>
        ) : null}
      </header>
      {add.reason ? <UnavailableNote id="add-venue-unavailable" reason={add.reason} /> : null}

      <form
        role="search"
        aria-label="Venues"
        className="grid gap-4 tablet:grid-cols-2 desktop:grid-cols-[minmax(0,2fr)_repeat(3,minmax(0,1fr))]"
        onSubmit={(event) => {
          event.preventDefault();
          change({ q: q || undefined, city: city || undefined });
        }}
      >
        <div className="grid gap-2 tablet:col-span-2 desktop:col-span-1">
          <Label htmlFor="venues-search">Search</Label>
          <Input
            id="venues-search"
            type="search"
            value={typed.q}
            onChange={(event) => setTyped({ ...typed, q: event.target.value })}
            placeholder="Name, alias or identifier"
            autoComplete="off"
          />
        </div>
        <div className="grid gap-2">
          <Label htmlFor="venues-city">City</Label>
          <Input
            id="venues-city"
            value={typed.city}
            onChange={(event) => setTyped({ ...typed, city: event.target.value })}
            autoComplete="off"
          />
        </div>
        <SelectField
          id="venues-status"
          label="Status"
          value={filters.status ?? ""}
          onChange={(event) => change({ status: (event.target.value || undefined) as VenueFilters["status"] })}
        >
          <option value="">All but archived</option>
          {venueStatuses.map((status) => (
            <option key={status} value={status}>
              {humanizeCode(status)}
            </option>
          ))}
        </SelectField>
        {variants.length > 0 || filters.variant ? (
          <SelectField
            id="venues-variant"
            label="Sport"
            value={filters.variant ?? ""}
            onChange={(event) => change({ variant: event.target.value || undefined })}
          >
            <option value="">Any sport</option>
            {variants.map((variant) => (
              <option key={variant.variant} value={variant.variant}>
                {variant.name}
              </option>
            ))}
            {filters.variant && !variants.some((variant) => variant.variant === filters.variant) ? (
              <option value={filters.variant}>{humanizeCode(filters.variant)}</option>
            ) : null}
          </SelectField>
        ) : null}
      </form>

      {venues.isPending ? <Loading /> : null}
      {venues.isError ? <ProblemAlert error={venues.error} /> : null}
      {venues.isSuccess && items.length === 0 ? (
        filtered ? (
          <EmptyState title="No venue matches">Try another spelling, an alias or fewer filters.</EmptyState>
        ) : (
          <EmptyState title="No venues yet">The venues your organisation registers are listed here.</EmptyState>
        )
      ) : null}
      {items.length > 0 ? (
        <Table>
          <TableHeader>
            <TableRow>
              <TableHead>Name</TableHead>
              <TableHead className={wide}>Type</TableHead>
              <TableHead className={wide}>City</TableHead>
              <TableHead className={wide}>Sports</TableHead>
              <TableHead className={wide}>Status</TableHead>
            </TableRow>
          </TableHeader>
          <TableBody>
            {items.map((venue) => {
              const place = `${venue.city}, ${countryName(venue.country)}`;
              const supported = orNone(venue.supportedVariants.map(sportName));
              return (
                <TableRow key={venue.id}>
                  <TableHead scope="row" className="h-auto max-w-64 py-2 align-top whitespace-normal tablet:max-w-80">
                    <RowName
                      name={
                        <Link href={venueHref(venue.id)} className="break-words text-primary underline-offset-4 hover:underline">
                          {venue.name}
                        </Link>
                      }
                      details={[
                        ["Type", humanizeCode(venue.type)],
                        ["City", place],
                        ["Sports", supported],
                        ["Status", humanizeCode(venue.status)],
                      ]}
                    />
                  </TableHead>
                  <TableCell className={wide}>{humanizeCode(venue.type)}</TableCell>
                  <TableCell className={wide}>{place}</TableCell>
                  <TableCell className={`${wide} max-w-64 whitespace-normal`}>{supported}</TableCell>
                  <TableCell className={wide}>
                    <StatusBadge status={venue.status} />
                  </TableCell>
                </TableRow>
              );
            })}
          </TableBody>
        </Table>
      ) : null}
      {venues.hasNextPage ? (
        <Button
          variant="outline"
          className="justify-self-start"
          disabled={venues.isFetchingNextPage}
          onClick={() => void venues.fetchNextPage()}
        >
          {venues.isFetchingNextPage ? "Loading…" : "Show more"}
        </Button>
      ) : null}
    </section>
  );
}
