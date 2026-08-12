'use client';

import Link from 'next/link';
import { Calendar, ChevronLeft, ChevronRight, MapPin, Search, Trophy, UserRound, Users } from 'lucide-react';
import { useEffect, useState } from 'react';
import { formatSport, publicApi, resolveTenantSlug, TENANT_SLUG, tenantPath } from '@/lib/public-api';

type SearchItem = {
  id: string;
  type: 'Competition' | 'Team' | 'Athlete' | 'Contest' | 'Venue';
  title: string;
  subtitle?: string | null;
  sportId?: string | null;
  date?: string | null;
  status?: string | null;
  competitionId?: string | null;
  venue?: string | null;
  href?: string | null;
};
type Option = { id: string; name: string };
type SearchResponse = {
  query: string;
  page: number;
  pageSize: number;
  total: number;
  totalPages: number;
  items: SearchItem[];
  filters: { sports: Option[]; competitions: Option[]; statuses: string[] };
};
type Filters = {
  q: string;
  type: string;
  sportId: string;
  dateFrom: string;
  dateTo: string;
  status: string;
  competitionId: string;
};

const initialFilters: Filters = {
  q: '', type: 'all', sportId: '', dateFrom: '', dateTo: '', status: '', competitionId: '',
};

export default function SearchPage() {
  const [filters, setFilters] = useState(initialFilters);
  const [page, setPage] = useState(1);
  const [tenantSlug, setTenantSlug] = useState(TENANT_SLUG);
  const [response, setResponse] = useState<SearchResponse | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    const slug = resolveTenantSlug();
    let disposed = false;
    queueMicrotask(() => !disposed && setTenantSlug(slug));
    const timeout = window.setTimeout(async () => {
      setLoading(true);
      setError(null);
      const query = new URLSearchParams({ page: String(page), pageSize: '12' });
      Object.entries(filters).forEach(([key, value]) => value && query.set(key, value));
      try {
        const result = await publicApi<SearchResponse>(`/search?${query}`, undefined, slug);
        if (!disposed) setResponse(result);
      } catch (reason) {
        if (!disposed) setError(reason instanceof Error ? reason.message : 'Search failed.');
      } finally {
        if (!disposed) setLoading(false);
      }
    }, 250);
    return () => {
      disposed = true;
      window.clearTimeout(timeout);
    };
  }, [filters, page]);

  function update<K extends keyof Filters>(key: K, value: Filters[K]) {
    setFilters(current => ({ ...current, [key]: value }));
    setPage(1);
  }

  return <div className="container mx-auto max-w-6xl px-4 py-10">
    <p className="text-sm font-semibold uppercase tracking-wider text-primary">Public directory</p>
    <h1 className="mt-2 text-3xl font-bold">Search</h1>
    <p className="mt-2 text-gray-600">Find competitions, teams, athletes, contests, and venues published by this tenant.</p>

    <section aria-label="Search filters" className="mt-8 rounded-2xl border bg-gray-50 p-4">
      <label className="relative block">
        <span className="sr-only">Search public sport</span>
        <Search className="absolute left-3 top-3.5 h-5 w-5 text-gray-400" />
        <input value={filters.q} onChange={event => update('q', event.target.value)} placeholder="Search names, contests, or venues" className="w-full rounded-xl border bg-white py-3 pl-11 pr-4 text-base" />
      </label>
      <div className="mt-4 grid grid-cols-1 gap-3 sm:grid-cols-2 lg:grid-cols-3">
        <Select label="Result type" value={filters.type} onChange={value => update('type', value)} options={[['all', 'All result types'], ['competition', 'Competitions'], ['team', 'Teams'], ['athlete', 'Athletes'], ['contest', 'Contests'], ['venue', 'Venues']]} />
        <Select label="Sport" value={filters.sportId} onChange={value => update('sportId', value)} options={[['', 'All sports'], ...(response?.filters.sports.map(item => [item.id, item.name] as [string, string]) ?? [])]} />
        <Select label="Status" value={filters.status} onChange={value => update('status', value)} options={[['', 'All statuses'], ...(response?.filters.statuses.map(value => [value, splitLabel(value)] as [string, string]) ?? [])]} />
        <Select label="Competition" value={filters.competitionId} onChange={value => update('competitionId', value)} options={[['', 'All competitions'], ...(response?.filters.competitions.map(item => [item.id, item.name] as [string, string]) ?? [])]} />
        <label className="text-sm font-medium text-gray-700">From date<input aria-label="From date" type="date" value={filters.dateFrom} onChange={event => update('dateFrom', event.target.value)} className="mt-1 block w-full rounded-lg border bg-white px-3 py-2" /></label>
        <label className="text-sm font-medium text-gray-700">To date<input aria-label="To date" type="date" value={filters.dateTo} onChange={event => update('dateTo', event.target.value)} className="mt-1 block w-full rounded-lg border bg-white px-3 py-2" /></label>
      </div>
      {Object.values(filters).some(value => value && value !== 'all') && <button type="button" onClick={() => { setFilters(initialFilters); setPage(1); }} className="mt-4 text-sm font-semibold text-primary hover:underline">Clear all filters</button>}
    </section>

    <div className="mt-8 flex items-center justify-between gap-3"><p className="text-sm text-gray-500">{loading ? 'Searching…' : `${response?.total ?? 0} result${response?.total === 1 ? '' : 's'}`}</p>{response && response.totalPages > 0 && <p className="text-sm text-gray-500">Page {response.page} of {response.totalPages}</p>}</div>
    {error && <Message>Search unavailable: {error}</Message>}
    {!loading && !error && response?.items.length === 0 && <Message>No public results match these filters.</Message>}
    <div className="mt-4 grid gap-4 md:grid-cols-2 lg:grid-cols-3" data-testid="search-results">
      {response?.items.map(item => <ResultCard key={`${item.type}-${item.id}`} item={item} tenantSlug={tenantSlug} />)}
    </div>

    {response && response.totalPages > 1 && <nav aria-label="Search result pages" className="mt-8 flex items-center justify-center gap-3"><button type="button" disabled={page <= 1 || loading} onClick={() => setPage(current => Math.max(1, current - 1))} className="inline-flex items-center gap-1 rounded-lg border px-4 py-2 disabled:opacity-40"><ChevronLeft className="h-4 w-4" /> Previous</button><button type="button" disabled={page >= response.totalPages || loading} onClick={() => setPage(current => current + 1)} className="inline-flex items-center gap-1 rounded-lg border px-4 py-2 disabled:opacity-40">Next <ChevronRight className="h-4 w-4" /></button></nav>}
  </div>;
}

function ResultCard({ item, tenantSlug }: { item: SearchItem; tenantSlug: string }) {
  const content = <><div className="flex items-start justify-between gap-3"><span className="flex h-10 w-10 items-center justify-center rounded-lg bg-primary/10 text-primary"><TypeIcon type={item.type} /></span>{item.status && <span className="rounded-full bg-gray-100 px-2 py-1 text-xs">{splitLabel(item.status)}</span>}</div><p className="mt-4 text-xs font-semibold uppercase tracking-wide text-primary">{item.type}{item.sportId ? ` · ${formatSport(item.sportId)}` : ''}</p><h2 className="mt-1 text-lg font-bold">{item.title}</h2>{item.subtitle && <p className="mt-2 line-clamp-2 text-sm text-gray-600">{item.subtitle}</p>}<div className="mt-3 flex flex-wrap gap-3 text-xs text-gray-500">{item.date && <span className="inline-flex items-center gap-1"><Calendar className="h-3 w-3" />{new Date(item.date).toLocaleDateString()}</span>}{item.venue && <span className="inline-flex items-center gap-1"><MapPin className="h-3 w-3" />{item.venue}</span>}</div></>;
  const className = 'rounded-xl border bg-white p-5 transition hover:shadow-md';
  return item.href ? <Link className={className} href={tenantPath(item.href, tenantSlug)}>{content}</Link> : <article className={className}>{content}</article>;
}

function TypeIcon({ type }: { type: SearchItem['type'] }) {
  if (type === 'Competition') return <Trophy className="h-5 w-5" />;
  if (type === 'Team') return <Users className="h-5 w-5" />;
  if (type === 'Athlete') return <UserRound className="h-5 w-5" />;
  if (type === 'Venue') return <MapPin className="h-5 w-5" />;
  return <Calendar className="h-5 w-5" />;
}

function Select({ label, value, onChange, options }: { label: string; value: string; onChange: (value: string) => void; options: Array<[string, string]> }) { return <label className="text-sm font-medium text-gray-700">{label}<select aria-label={label} value={value} onChange={event => onChange(event.target.value)} className="mt-1 block w-full rounded-lg border bg-white px-3 py-2">{options.map(([optionValue, optionLabel]) => <option key={optionValue} value={optionValue}>{optionLabel}</option>)}</select></label>; }
function Message({ children }: { children: React.ReactNode }) { return <div className="mt-5 rounded-xl border border-dashed bg-gray-50 p-8 text-center text-gray-500">{children}</div>; }
function splitLabel(value: string) { return value.replace(/([a-z])([A-Z])/g, '$1 $2'); }
