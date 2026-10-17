'use client';

import Link from 'next/link';
import {
  Calendar,
  ChevronLeft,
  ChevronRight,
  MapPin,
  Medal,
  Search,
  Trophy,
  UserRound,
  Users,
} from 'lucide-react';
import { useEffect, useState } from 'react';
import {
  formatSport,
  publicApi,
  resolveTenantSlug,
  tenantPath,
} from '@/lib/public-api';

type SearchItem = {
  id: string;
  type: 'Competition' | 'Team' | 'Athlete' | 'Contest' | 'Venue' | 'Sport';
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
  q: '',
  type: 'all',
  sportId: '',
  dateFrom: '',
  dateTo: '',
  status: '',
  competitionId: '',
};
const typeFilters = [
  ['all', 'All', Search],
  ['competition', 'Competitions', Trophy],
  ['team', 'Teams', Users],
  ['athlete', 'Athletes', UserRound],
  ['contest', 'Contests', Calendar],
  ['venue', 'Venues', MapPin],
] as const;

export default function SearchPage() {
  const [filters, setFilters] = useState(initialFilters);
  const [page, setPage] = useState(1);
  const [tenantSlug] = useState(resolveTenantSlug);
  const [response, setResponse] = useState<SearchResponse | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    let disposed = false;
    const timeout = window.setTimeout(async () => {
      setLoading(true);
      setError(null);
      const query = new URLSearchParams({ page: String(page), pageSize: '12' });
      Object.entries(filters).forEach(([key, value]) => value && query.set(key, value));
      try {
        const result = await publicApi<SearchResponse>(`/search?${query}`, undefined, tenantSlug);
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
  }, [filters, page, tenantSlug]);

  function update<K extends keyof Filters>(key: K, value: Filters[K]) {
    setFilters(current => ({ ...current, [key]: value }));
    setPage(1);
  }

  const hasFilters = Object.values(filters).some(value => value && value !== 'all');

  return (
    <div className="app-container py-5 sm:py-7">
      <header className="overflow-hidden rounded-[28px] border border-[var(--color-border)] bg-[var(--color-surface)] shadow-[var(--shadow-card)]">
        <div className="bg-gradient-to-br from-slate-950 via-slate-900 to-slate-800 px-5 py-7 text-white sm:px-7 sm:py-8">
          <span className="inline-flex h-8 items-center gap-2 rounded-full border border-white/10 bg-white/10 px-3 text-xs font-bold uppercase tracking-[0.18em] text-white/80">
            <Search className="h-3.5 w-3.5" />
            Public discovery
          </span>
          <h1 className="mt-4 max-w-3xl text-3xl font-black tracking-tight sm:text-5xl">
            Find teams, athletes and competitions
          </h1>
          <p className="mt-3 max-w-2xl text-sm leading-6 text-white/70 sm:text-base">
            Search every published sport entity, fixture, venue and competition from one place.
          </p>
        </div>
      </header>

      <section aria-label="Search filters" className="surface mt-6 rounded-2xl border border-[var(--color-border)] p-4 shadow-[var(--shadow-card)] sm:p-5">
        <label className="relative block">
          <span className="sr-only">Search public sport</span>
          <Search className="absolute left-4 top-4 h-5 w-5 muted" />
          <input
            value={filters.q}
            onChange={event => update('q', event.target.value)}
            placeholder="Search names, contests, competitions or venues"
            className="w-full rounded-xl border border-[var(--color-border)] bg-[var(--color-surface)] py-3.5 pl-12 pr-4 text-base outline-none focus:border-[var(--brand-primary)]"
            autoFocus
          />
        </label>

        <div className="mt-4 flex gap-2 overflow-x-auto pb-1" aria-label="Result type">
          {typeFilters.map(([value, label, Icon]) => (
            <button
              key={value}
              type="button"
              onClick={() => update('type', value)}
              aria-pressed={filters.type === value}
              className={`inline-flex shrink-0 items-center gap-2 rounded-full border px-3.5 py-2 text-sm font-bold transition ${
                filters.type === value
                  ? 'border-[var(--brand-primary)] bg-[var(--brand-primary)] text-white'
                  : 'border-[var(--color-border)] hover:bg-[var(--color-surface-muted)]'
              }`}
            >
              <Icon className="h-4 w-4" />
              {label}
            </button>
          ))}
        </div>

        <details className="mt-4 border-t border-[var(--color-border)] pt-4">
          <summary className="cursor-pointer text-sm font-bold text-[var(--brand-primary)]">
            Advanced filters
          </summary>
          <div className="mt-4 grid grid-cols-1 gap-3 sm:grid-cols-2 lg:grid-cols-3">
            <Select
              label="Sport"
              value={filters.sportId}
              onChange={value => update('sportId', value)}
              options={[
                ['', 'All sports'],
                ...(response?.filters.sports.map(item => [item.id, item.name] as [string, string]) ?? []),
              ]}
            />
            <Select
              label="Status"
              value={filters.status}
              onChange={value => update('status', value)}
              options={[
                ['', 'All statuses'],
                ...(response?.filters.statuses.map(value => [value, splitLabel(value)] as [string, string]) ?? []),
              ]}
            />
            <Select
              label="Competition"
              value={filters.competitionId}
              onChange={value => update('competitionId', value)}
              options={[
                ['', 'All competitions'],
                ...(response?.filters.competitions.map(item => [item.id, item.name] as [string, string]) ?? []),
              ]}
            />
            <DateField label="From date" value={filters.dateFrom} onChange={value => update('dateFrom', value)} />
            <DateField label="To date" value={filters.dateTo} onChange={value => update('dateTo', value)} />
          </div>
        </details>

        {hasFilters && (
          <button
            type="button"
            onClick={() => {
              setFilters(initialFilters);
              setPage(1);
            }}
            className="mt-4 text-sm font-bold text-[var(--brand-primary)] hover:underline"
          >
            Clear all filters
          </button>
        )}
      </section>

      <div className="mt-7 flex items-center justify-between gap-3">
        <p className="text-sm muted">
          {loading ? 'Searching…' : `${response?.total ?? 0} result${response?.total === 1 ? '' : 's'}`}
        </p>
        {response && response.totalPages > 0 && (
          <p className="text-sm muted">Page {response.page} of {response.totalPages}</p>
        )}
      </div>

      {error && <Message>Search unavailable: {error}</Message>}
      {!loading && !error && response?.items.length === 0 && (
        <Message>No public results match these filters.</Message>
      )}

      <div className="mt-4 grid gap-4 md:grid-cols-2 xl:grid-cols-3" data-testid="search-results">
        {response?.items.map(item => (
          <ResultCard key={`${item.type}-${item.id}`} item={item} tenantSlug={tenantSlug} />
        ))}
      </div>

      {response && response.totalPages > 1 && (
        <nav aria-label="Search result pages" className="mt-8 flex items-center justify-center gap-3">
          <PageButton
            disabled={page <= 1 || loading}
            onClick={() => setPage(current => Math.max(1, current - 1))}
          >
            <ChevronLeft className="h-4 w-4" /> Previous
          </PageButton>
          <PageButton
            disabled={page >= response.totalPages || loading}
            onClick={() => setPage(current => current + 1)}
          >
            Next <ChevronRight className="h-4 w-4" />
          </PageButton>
        </nav>
      )}
    </div>
  );
}

function ResultCard({ item, tenantSlug }: { item: SearchItem; tenantSlug: string }) {
  const content = (
    <>
      <div className="flex items-start justify-between gap-3">
        <span className="flex h-11 w-11 items-center justify-center rounded-xl bg-[var(--color-surface-muted)] text-[var(--brand-primary)]">
          <TypeIcon type={item.type} />
        </span>
        {item.status && (
          <span className="rounded-full bg-[var(--color-surface-muted)] px-2.5 py-1 text-xs font-bold">
            {splitLabel(item.status)}
          </span>
        )}
      </div>
      <p className="mt-4 text-xs font-bold uppercase tracking-[0.14em] text-[var(--brand-primary)]">
        {item.type}{item.sportId ? ` · ${formatSport(item.sportId)}` : ''}
      </p>
      <h2 className="mt-1 text-lg font-black">{item.title}</h2>
      {item.subtitle && <p className="mt-2 line-clamp-2 text-sm leading-6 muted">{item.subtitle}</p>}
      <div className="mt-4 flex flex-wrap gap-3 text-xs muted">
        {item.date && (
          <span className="inline-flex items-center gap-1">
            <Calendar className="h-3 w-3" />
            {new Date(item.date).toLocaleDateString()}
          </span>
        )}
        {item.venue && (
          <span className="inline-flex items-center gap-1">
            <MapPin className="h-3 w-3" />
            {item.venue}
          </span>
        )}
      </div>
    </>
  );
  const className = 'surface rounded-2xl border border-[var(--color-border)] p-5 shadow-[var(--shadow-card)] transition hover:-translate-y-0.5 hover:shadow-lg';
  return item.href
    ? <Link className={className} href={tenantPath(item.href, tenantSlug)}>{content}</Link>
    : <article className={className}>{content}</article>;
}

function TypeIcon({ type }: { type: SearchItem['type'] }) {
  if (type === 'Competition') return <Trophy className="h-5 w-5" />;
  if (type === 'Team') return <Users className="h-5 w-5" />;
  if (type === 'Athlete') return <UserRound className="h-5 w-5" />;
  if (type === 'Venue') return <MapPin className="h-5 w-5" />;
  if (type === 'Sport') return <Medal className="h-5 w-5" />;
  return <Calendar className="h-5 w-5" />;
}

function Select({ label, value, onChange, options }: {
  label: string;
  value: string;
  onChange: (value: string) => void;
  options: Array<[string, string]>;
}) {
  return (
    <label className="text-sm font-semibold">
      {label}
      <select
        aria-label={label}
        value={value}
        onChange={event => onChange(event.target.value)}
        className="mt-1 block w-full rounded-xl border border-[var(--color-border)] bg-[var(--color-surface)] px-3 py-2.5"
      >
        {options.map(([optionValue, optionLabel]) => (
          <option key={optionValue} value={optionValue}>{optionLabel}</option>
        ))}
      </select>
    </label>
  );
}

function DateField({ label, value, onChange }: {
  label: string;
  value: string;
  onChange: (value: string) => void;
}) {
  return (
    <label className="text-sm font-semibold">
      {label}
      <input
        aria-label={label}
        type="date"
        value={value}
        onChange={event => onChange(event.target.value)}
        className="mt-1 block w-full rounded-xl border border-[var(--color-border)] bg-[var(--color-surface)] px-3 py-2.5"
      />
    </label>
  );
}

function PageButton({ children, disabled, onClick }: {
  children: React.ReactNode;
  disabled: boolean;
  onClick: () => void;
}) {
  return (
    <button
      type="button"
      disabled={disabled}
      onClick={onClick}
      className="inline-flex items-center gap-1 rounded-xl border border-[var(--color-border)] px-4 py-2.5 font-semibold hover:bg-[var(--color-surface-muted)] disabled:opacity-40"
    >
      {children}
    </button>
  );
}

function Message({ children }: { children: React.ReactNode }) {
  return (
    <div className="surface mt-5 rounded-2xl border border-dashed border-[var(--color-border)] p-10 text-center muted">
      {children}
    </div>
  );
}

function splitLabel(value: string) {
  return value.replace(/([a-z])([A-Z])/g, '$1 $2');
}
