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
import {
  ageGroupLabel,
  ageGroups,
  genderCategories,
  genderLabel,
  teamCategoryLabel,
  teamLevelLabel,
  teamLevels,
} from "@/lib/team-category";
import { useTenantWorkspace } from "@/lib/tenant-workspace";
import { tenantPath } from "@/lib/workspace";
import { useSportVariants } from "../sports/sports-data";
import { addTeamActions, teamFilters, teamStatuses, useTeams, type TeamFilters } from "./teams-data";

/** How long the search waits after the last key before it asks. */
const searchDelay = 300;
const linkClass = "break-words text-primary underline-offset-4 hover:underline";

/**
 * Registry → Teams (S07 D23): the tenant's teams sorted by name and category, searched by name, alias or external
 * identifier and filtered by sport, category and status. The search and filters are in the address, so going back
 * from a team keeps them. The sport filter lists every published variant, named from its definition (AR-29): a team
 * stays listed under a sport the tenant has since disabled. **Add team** needs the create and its live check.
 */
export default function TeamsPage() {
  const { tenant, can, unavailable } = useTenantWorkspace();
  const add = actionAccess(addTeamActions, can, unavailable);
  const searchParams = useSearchParams();
  const pathname = usePathname();
  const router = useRouter();
  const filters = useMemo(() => teamFilters(new URLSearchParams(searchParams.toString())), [searchParams]);
  const teams = useTeams(filters);
  const sports = useSportVariants();
  const [typed, setTyped] = useState(filters.q ?? "");
  // The text the page last put in the address: different text came from elsewhere (the navigation's entry, going
  // back), so the box shows it rather than writing its own back.
  const written = useRef(filters.q ?? "");

  const change = (next: TeamFilters) => {
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

  if (!can("team.read")) {
    return <ForbiddenView />;
  }

  const root = tenantPath(tenant.slug);
  const variants = sports.data?.items ?? [];
  const sportName = (code: string) => variants.find((variant) => variant.variant === code)?.name ?? humanizeCode(code);
  const items = teams.data?.pages.flatMap((page) => page.items) ?? [];
  const filtered = Object.values(filters).some(Boolean);
  // The organisation's link needs its own read; without it the name alone.
  const organisationName = (organisation: { id: string; displayName: string } | null) =>
    organisation === null ? (
      "Independent"
    ) : can("organisation.read") ? (
      <Link href={`${root}/organisations/${organisation.id}`} className={linkClass}>
        {organisation.displayName}
      </Link>
    ) : (
      organisation.displayName
    );

  return (
    <section className="grid grid-cols-[minmax(0,1fr)] gap-6">
      <header className="flex flex-wrap items-end justify-between gap-3">
        <div className="grid gap-1">
          <h1 className="text-2xl font-semibold">Teams</h1>
          <p className="text-muted-foreground">The teams your organisation registers, in every sport and category.</p>
        </div>
        {add.allowed ? (
          <Button asChild>
            <Link href={`${root}/teams/new`}>
              <Plus aria-hidden="true" />
              Add team
            </Link>
          </Button>
        ) : add.reason ? (
          <Button disabled aria-describedby="add-team-unavailable">
            <Plus aria-hidden="true" />
            Add team
          </Button>
        ) : null}
      </header>
      {add.reason ? <UnavailableNote id="add-team-unavailable" reason={add.reason} /> : null}

      <form
        role="search"
        aria-label="Teams"
        className="grid gap-4 tablet:grid-cols-2 desktop:grid-cols-3"
        onSubmit={(event) => {
          event.preventDefault();
          change({ q: q || undefined });
        }}
      >
        <div className="grid gap-2 tablet:col-span-2 desktop:col-span-3">
          <Label htmlFor="teams-search">Search</Label>
          <Input
            id="teams-search"
            type="search"
            value={typed}
            onChange={(event) => setTyped(event.target.value)}
            placeholder="Name, alias or identifier"
            autoComplete="off"
          />
        </div>
        {variants.length > 0 || filters.variant ? (
          <SelectField
            id="teams-variant"
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
        <SelectField
          id="teams-gender"
          label="Gender"
          value={filters.genderCategory ?? ""}
          onChange={(event) =>
            change({ genderCategory: (event.target.value || undefined) as TeamFilters["genderCategory"] })
          }
        >
          <option value="">Any gender</option>
          {genderCategories.map((code) => (
            <option key={code} value={code}>
              {genderLabel(code)}
            </option>
          ))}
        </SelectField>
        <SelectField
          id="teams-age-group"
          label="Age group"
          value={filters.ageGroup ?? ""}
          onChange={(event) => change({ ageGroup: (event.target.value || undefined) as TeamFilters["ageGroup"] })}
        >
          <option value="">Any age group</option>
          {ageGroups.map((code) => (
            <option key={code} value={code}>
              {ageGroupLabel(code)}
            </option>
          ))}
        </SelectField>
        <SelectField
          id="teams-level"
          label="Level"
          value={filters.level ?? ""}
          onChange={(event) => change({ level: (event.target.value || undefined) as TeamFilters["level"] })}
        >
          <option value="">Any level</option>
          {teamLevels.map((code) => (
            <option key={code} value={code}>
              {teamLevelLabel(code)}
            </option>
          ))}
        </SelectField>
        <SelectField
          id="teams-status"
          label="Status"
          value={filters.status ?? ""}
          onChange={(event) => change({ status: (event.target.value || undefined) as TeamFilters["status"] })}
        >
          <option value="">All but archived</option>
          {teamStatuses.map((status) => (
            <option key={status} value={status}>
              {humanizeCode(status)}
            </option>
          ))}
        </SelectField>
      </form>

      {teams.isPending ? <Loading /> : null}
      {teams.isError ? <ProblemAlert error={teams.error} /> : null}
      {teams.isSuccess && items.length === 0 ? (
        filtered ? (
          <EmptyState title="No team matches">Try another spelling, an alias or fewer filters.</EmptyState>
        ) : (
          <EmptyState title="No teams yet">The teams your organisation registers are listed here.</EmptyState>
        )
      ) : null}
      {items.length > 0 ? (
        <Table>
          <TableHeader>
            <TableRow>
              <TableHead>Name</TableHead>
              <TableHead className={wide}>Category</TableHead>
              <TableHead className={wide}>Sport</TableHead>
              <TableHead className={wide}>Organisation</TableHead>
              <TableHead className={wide}>Status</TableHead>
            </TableRow>
          </TableHeader>
          <TableBody>
            {items.map((team) => {
              const category = teamCategoryLabel(team);
              const sport = sportName(team.variant);
              const organisation = organisationName(team.organisation);
              return (
                <TableRow key={team.id}>
                  <TableHead scope="row" className="h-auto max-w-64 py-2 align-top whitespace-normal tablet:max-w-80">
                    <RowName
                      name={
                        <span className="grid">
                          <Link href={`${root}/teams/${team.id}`} className={linkClass}>
                            {team.name}
                          </Link>
                          {team.shortName ? (
                            <span className="text-sm font-normal text-muted-foreground">{team.shortName}</span>
                          ) : null}
                        </span>
                      }
                      details={[
                        ["Category", category],
                        ["Sport", sport],
                        ["Organisation", organisation],
                        ["Status", humanizeCode(team.status)],
                      ]}
                    />
                  </TableHead>
                  <TableCell className={wide}>{category}</TableCell>
                  <TableCell className={wide}>{sport}</TableCell>
                  <TableCell className={`${wide} whitespace-normal`}>{organisation}</TableCell>
                  <TableCell className={wide}>
                    <StatusBadge status={team.status} />
                  </TableCell>
                </TableRow>
              );
            })}
          </TableBody>
        </Table>
      ) : null}
      {teams.hasNextPage ? (
        <Button
          variant="outline"
          className="justify-self-start"
          disabled={teams.isFetchingNextPage}
          onClick={() => void teams.fetchNextPage()}
        >
          {teams.isFetchingNextPage ? "Loading…" : "Show more"}
        </Button>
      ) : null}
    </section>
  );
}
