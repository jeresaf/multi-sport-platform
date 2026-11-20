"use client";

import { Button } from "@msp/ui/components/button";
import { Input } from "@msp/ui/components/input";
import { Label } from "@msp/ui/components/label";
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from "@msp/ui/components/table";
import Link from "next/link";
import { usePathname, useRouter, useSearchParams } from "next/navigation";
import { useEffect, useMemo, useState } from "react";
import { EmptyState } from "@/components/empty-state";
import { Loading } from "@/components/loading";
import { AgeBadge } from "@/components/people/person-badges";
import { ProblemAlert } from "@/components/problem-alert";
import { SelectField } from "@/components/select-field";
import { StatusBadge } from "@/components/status-badge";
import { ForbiddenView } from "@/components/status-page";
import { RowName, wide } from "@/components/viewer/facts";
import { humanizeCode } from "@/lib/codes";
import { useCountries } from "@/lib/reference-data";
import { useTenantWorkspace } from "@/lib/tenant-workspace";
import { tenantPath } from "@/lib/workspace";
import { useTenantSports } from "../settings/sports/sports-settings-data";
import { ageStatuses, peopleFilters, personStatuses, profileKinds, usePeople, type PeopleFilters } from "./people-data";

/** How long the search waits after the last key before it asks. */
const searchDelay = 300;

const ageLabels = { ADULT: "Adult", MINOR: "Minor", UNKNOWN: "Age unknown" } as const;

/**
 * Registry → People (S06 T7, admin-app.md): the tenant's people sorted by last name, searched by name, alias or
 * external identifier and filtered by status, age, profile and sport. The search and filters are in the address, so
 * going back from a person keeps them. Read-only until Add person (T7b).
 */
export default function PeoplePage() {
  const { tenant, can } = useTenantWorkspace();
  const searchParams = useSearchParams();
  const pathname = usePathname();
  const router = useRouter();
  const filters = useMemo(() => peopleFilters(new URLSearchParams(searchParams.toString())), [searchParams]);
  const people = usePeople(filters);
  const sports = useTenantSports();
  const countries = useCountries();
  const [search, setSearch] = useState(filters.q ?? "");

  const change = (next: PeopleFilters) => {
    const params = new URLSearchParams();
    for (const [key, value] of Object.entries({ ...filters, ...next })) {
      if (value) {
        params.set(key, value);
      }
    }
    const query = params.toString();
    router.replace(query ? `${pathname}?${query}` : pathname, { scroll: false });
  };

  // The search follows the typing once it pauses; the filters change at once.
  const typed = search.trim();
  useEffect(() => {
    if (typed === (filters.q ?? "")) {
      return;
    }
    const timer = setTimeout(() => change({ q: typed || undefined }), searchDelay);
    return () => clearTimeout(timer);
    // `change` is rebuilt each render from the same filters; the typed text is what starts the wait.
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [typed, filters.q]);

  if (!can("person.read")) {
    return <ForbiddenView />;
  }

  const variants = (sports.data?.variants ?? []).filter((variant) => variant.enabled);
  const countryName = (code: string | null) =>
    code ? (countries.data?.items.find((country) => country.code === code)?.name ?? code) : "Not recorded";
  const items = people.data?.pages.flatMap((page) => page.items) ?? [];
  const filtered = Boolean(filters.q || filters.status || filters.ageStatus || filters.profile || filters.variant);
  const personHref = (id: string) => `${tenantPath(tenant.slug)}/people/${id}`;

  return (
    <section className="grid grid-cols-[minmax(0,1fr)] gap-6">
      <header className="grid gap-1">
        <h1 className="text-2xl font-semibold">People</h1>
        <p className="text-muted-foreground">
          Everyone your organisation keeps a record of: players, staff and officials, one record per person.
        </p>
      </header>

      <form
        role="search"
        aria-label="People"
        className="grid gap-4 tablet:grid-cols-2 desktop:grid-cols-[minmax(0,2fr)_repeat(4,minmax(0,1fr))]"
        onSubmit={(event) => {
          event.preventDefault();
          change({ q: typed || undefined });
        }}
      >
        <div className="grid gap-2 tablet:col-span-2 desktop:col-span-1">
          <Label htmlFor="people-search">Search</Label>
          <Input
            id="people-search"
            type="search"
            value={search}
            onChange={(event) => setSearch(event.target.value)}
            placeholder="Name, alias or identifier"
            autoComplete="off"
          />
        </div>
        <SelectField
          id="people-status"
          label="Status"
          value={filters.status ?? ""}
          onChange={(event) => change({ status: (event.target.value || undefined) as PeopleFilters["status"] })}
        >
          <option value="">All but archived</option>
          {personStatuses.map((status) => (
            <option key={status} value={status}>
              {humanizeCode(status)}
            </option>
          ))}
        </SelectField>
        <SelectField
          id="people-age"
          label="Age"
          value={filters.ageStatus ?? ""}
          onChange={(event) => change({ ageStatus: (event.target.value || undefined) as PeopleFilters["ageStatus"] })}
        >
          <option value="">Any age</option>
          {ageStatuses.map((age) => (
            <option key={age} value={age}>
              {ageLabels[age]}
            </option>
          ))}
        </SelectField>
        <SelectField
          id="people-profile"
          label="Role"
          value={filters.profile ?? ""}
          onChange={(event) => change({ profile: (event.target.value || undefined) as PeopleFilters["profile"] })}
        >
          <option value="">Any role</option>
          {profileKinds.map((kind) => (
            <option key={kind} value={kind}>
              {humanizeCode(kind)}
            </option>
          ))}
        </SelectField>
        {variants.length > 0 || filters.variant ? (
          <SelectField
            id="people-variant"
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

      {people.isPending ? <Loading /> : null}
      {people.isError ? <ProblemAlert error={people.error} /> : null}
      {people.isSuccess && items.length === 0 ? (
        filtered ? (
          <EmptyState title="No one matches">Try another spelling, an alias or fewer filters.</EmptyState>
        ) : (
          <EmptyState title="No people yet">The people your organisation registers are listed here.</EmptyState>
        )
      ) : null}
      {items.length > 0 ? (
        <Table>
          <TableHeader>
            <TableRow>
              <TableHead>Name</TableHead>
              <TableHead className={wide}>Born</TableHead>
              <TableHead className={wide}>Nationality</TableHead>
              <TableHead className={wide}>Status</TableHead>
            </TableRow>
          </TableHeader>
          <TableBody>
            {items.map((person) => (
              <TableRow key={person.id}>
                <TableHead scope="row" className="h-auto max-w-64 py-2 align-top whitespace-normal tablet:max-w-80">
                  <RowName
                    name={
                      <span className="flex flex-wrap items-center gap-2">
                        <Link
                          href={personHref(person.id)}
                          className="break-words text-primary underline-offset-4 hover:underline"
                        >
                          {person.displayName}
                        </Link>
                        <AgeBadge ageStatus={person.ageStatus} />
                      </span>
                    }
                    details={[
                      ["Born", person.birthYear ?? "Not recorded"],
                      ["Nationality", countryName(person.nationality)],
                      ["Status", humanizeCode(person.status)],
                    ]}
                  />
                </TableHead>
                <TableCell className={wide}>{person.birthYear ?? "Not recorded"}</TableCell>
                <TableCell className={wide}>{countryName(person.nationality)}</TableCell>
                <TableCell className={wide}>
                  <StatusBadge status={person.status} />
                </TableCell>
              </TableRow>
            ))}
          </TableBody>
        </Table>
      ) : null}
      {people.hasNextPage ? (
        <Button
          variant="outline"
          className="justify-self-start"
          disabled={people.isFetchingNextPage}
          onClick={() => void people.fetchNextPage()}
        >
          {people.isFetchingNextPage ? "Loading…" : "Show more"}
        </Button>
      ) : null}
    </section>
  );
}
