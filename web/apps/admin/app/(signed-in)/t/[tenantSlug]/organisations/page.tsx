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
import { RowName, wide } from "@/components/viewer/facts";
import { actionAccess } from "@/lib/action-access";
import { humanizeCode } from "@/lib/codes";
import { useCountries } from "@/lib/reference-data";
import { useTenantWorkspace } from "@/lib/tenant-workspace";
import { tenantPath } from "@/lib/workspace";
import {
  addOrganisationActions,
  organisationFilters,
  organisationPlace,
  organisationStatuses,
  organisationTypes,
  useOrganisations,
  type OrganisationFilters,
} from "./organisations-data";

/** How long the search waits after the last key before it asks. */
const searchDelay = 300;

/**
 * Registry → Organisations (S07 D23): the tenant's clubs, schools, federations and other bodies sorted by name,
 * searched by name, alias or external identifier and filtered by type and status. The search and filters are in the
 * address, so going back from an organisation keeps them. **Add organisation** needs the create and its live check.
 */
export default function OrganisationsPage() {
  const { tenant, can, unavailable } = useTenantWorkspace();
  const add = actionAccess(addOrganisationActions, can, unavailable);
  const searchParams = useSearchParams();
  const pathname = usePathname();
  const router = useRouter();
  const filters = useMemo(() => organisationFilters(new URLSearchParams(searchParams.toString())), [searchParams]);
  const organisations = useOrganisations(filters);
  const countries = useCountries();
  const [typed, setTyped] = useState(filters.q ?? "");
  // The text the page last put in the address: different text came from elsewhere (the navigation's entry, going
  // back), so the box shows it rather than writing its own back.
  const written = useRef(filters.q ?? "");

  const change = (next: OrganisationFilters) => {
    const merged = { ...filters, ...next };
    written.current = merged.q ?? "";
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
    const fromAddress = filters.q ?? "";
    if (fromAddress !== written.current) {
      written.current = fromAddress;
      setTyped(fromAddress);
    }
  }, [filters.q]);

  // The text follows the typing once it pauses; the selects change at once.
  const q = typed.trim();
  useEffect(() => {
    if (q === (filters.q ?? "")) {
      return;
    }
    const timer = setTimeout(() => change({ q: q || undefined }), searchDelay);
    return () => clearTimeout(timer);
    // `change` is rebuilt each render from these same filters.
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [q, filters]);

  if (!can("organisation.read")) {
    return <ForbiddenView />;
  }

  const countryName = (code: string) => countries.data?.items.find((country) => country.code === code)?.name ?? code;
  const items = organisations.data?.pages.flatMap((page) => page.items) ?? [];
  const filtered = Boolean(filters.q || filters.type || filters.status);
  const organisationHref = (id: string) => `${tenantPath(tenant.slug)}/organisations/${id}`;

  return (
    <section className="grid grid-cols-[minmax(0,1fr)] gap-6">
      <header className="flex flex-wrap items-end justify-between gap-3">
        <div className="grid gap-1">
          <h1 className="text-2xl font-semibold">Organisations</h1>
          <p className="text-muted-foreground">The clubs, schools and other bodies whose teams take part in your competitions.</p>
        </div>
        {add.allowed ? (
          <Button asChild>
            <Link href={`${tenantPath(tenant.slug)}/organisations/new`}>
              <Plus aria-hidden="true" />
              Add organisation
            </Link>
          </Button>
        ) : add.reason ? (
          <Button disabled aria-describedby="add-organisation-unavailable">
            <Plus aria-hidden="true" />
            Add organisation
          </Button>
        ) : null}
      </header>
      {add.reason ? <UnavailableNote id="add-organisation-unavailable" reason={add.reason} /> : null}

      <form
        role="search"
        aria-label="Organisations"
        className="grid gap-4 tablet:grid-cols-2 desktop:grid-cols-[minmax(0,2fr)_repeat(2,minmax(0,1fr))]"
        onSubmit={(event) => {
          event.preventDefault();
          change({ q: q || undefined });
        }}
      >
        <div className="grid gap-2 tablet:col-span-2 desktop:col-span-1">
          <Label htmlFor="organisations-search">Search</Label>
          <Input
            id="organisations-search"
            type="search"
            value={typed}
            onChange={(event) => setTyped(event.target.value)}
            placeholder="Name, alias or identifier"
            autoComplete="off"
          />
        </div>
        <SelectField
          id="organisations-type"
          label="Type"
          value={filters.type ?? ""}
          onChange={(event) => change({ type: (event.target.value || undefined) as OrganisationFilters["type"] })}
        >
          <option value="">Any type</option>
          {organisationTypes.map((type) => (
            <option key={type} value={type}>
              {humanizeCode(type)}
            </option>
          ))}
        </SelectField>
        <SelectField
          id="organisations-status"
          label="Status"
          value={filters.status ?? ""}
          onChange={(event) => change({ status: (event.target.value || undefined) as OrganisationFilters["status"] })}
        >
          <option value="">All but archived</option>
          {organisationStatuses.map((status) => (
            <option key={status} value={status}>
              {humanizeCode(status)}
            </option>
          ))}
        </SelectField>
      </form>

      {organisations.isPending ? <Loading /> : null}
      {organisations.isError ? <ProblemAlert error={organisations.error} /> : null}
      {organisations.isSuccess && items.length === 0 ? (
        filtered ? (
          <EmptyState title="No organisation matches">Try another spelling, an alias or fewer filters.</EmptyState>
        ) : (
          <EmptyState title="No organisations yet">The clubs, schools and bodies your organisation registers are listed here.</EmptyState>
        )
      ) : null}
      {items.length > 0 ? (
        <Table>
          <TableHeader>
            <TableRow>
              <TableHead>Name</TableHead>
              <TableHead className={wide}>Type</TableHead>
              <TableHead className={wide}>Place</TableHead>
              <TableHead className={wide}>Status</TableHead>
            </TableRow>
          </TableHeader>
          <TableBody>
            {items.map((organisation) => {
              const place = organisationPlace(organisation, countryName);
              return (
                <TableRow key={organisation.id}>
                  <TableHead scope="row" className="h-auto max-w-64 py-2 align-top whitespace-normal tablet:max-w-80">
                    <RowName
                      name={
                        <span className="grid">
                          <Link
                            href={organisationHref(organisation.id)}
                            className="break-words text-primary underline-offset-4 hover:underline"
                          >
                            {organisation.displayName}
                          </Link>
                          {organisation.shortName ? (
                            <span className="text-sm font-normal text-muted-foreground">{organisation.shortName}</span>
                          ) : null}
                        </span>
                      }
                      details={[
                        ["Type", humanizeCode(organisation.type)],
                        ["Place", place],
                        ["Status", humanizeCode(organisation.status)],
                      ]}
                    />
                  </TableHead>
                  <TableCell className={wide}>{humanizeCode(organisation.type)}</TableCell>
                  <TableCell className={wide}>{place}</TableCell>
                  <TableCell className={wide}>
                    <StatusBadge status={organisation.status} />
                  </TableCell>
                </TableRow>
              );
            })}
          </TableBody>
        </Table>
      ) : null}
      {organisations.hasNextPage ? (
        <Button
          variant="outline"
          className="justify-self-start"
          disabled={organisations.isFetchingNextPage}
          onClick={() => void organisations.fetchNextPage()}
        >
          {organisations.isFetchingNextPage ? "Loading…" : "Show more"}
        </Button>
      ) : null}
    </section>
  );
}
