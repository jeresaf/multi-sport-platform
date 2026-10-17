'use client';

import Link from 'next/link';
import { Activity, ArrowRight, Search, ShieldCheck, Trophy } from 'lucide-react';
import { useEffect, useMemo, useState } from 'react';
import {
  formatSport,
  publicApi,
  type PublicCompetition,
  resolveTenantSlug,
  tenantPath,
} from '@/lib/public-api';

export default function CompetitionsPage() {
  const [competitions, setCompetitions] = useState<PublicCompetition[]>([]);
  const [search, setSearch] = useState('');
  const [status, setStatus] = useState('');
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [tenantSlug] = useState(resolveTenantSlug);

  useEffect(() => {
    publicApi<PublicCompetition[]>('/competitions', undefined, tenantSlug)
      .then(setCompetitions)
      .catch((reason: Error) => setError(reason.message))
      .finally(() => setLoading(false));
  }, [tenantSlug]);

  const statuses = useMemo(
    () => [...new Set(competitions.map(item => item.status).filter(Boolean))].sort(),
    [competitions],
  );
  const sports = useMemo(
    () => new Set(competitions.map(item => item.sportId)).size,
    [competitions],
  );
  const filtered = useMemo(() => {
    const query = search.trim().toLowerCase();
    return competitions.filter(competition => {
      const matchesSearch = !query
        || competition.name.toLowerCase().includes(query)
        || (competition.description ?? '').toLowerCase().includes(query)
        || (competition.sportName || formatSport(competition.sportId))
          .toLowerCase().includes(query);
      return matchesSearch && (!status || competition.status === status);
    });
  }, [competitions, search, status]);

  return (
    <div className="app-container py-5 sm:py-7">
      <header className="overflow-hidden rounded-[28px] border border-[var(--color-border)] bg-[var(--color-surface)] shadow-[var(--shadow-card)]">
        <div className="bg-gradient-to-br from-slate-950 via-slate-900 to-slate-800 px-5 py-7 text-white sm:px-7 sm:py-8">
          <span className="inline-flex h-8 items-center gap-2 rounded-full border border-white/10 bg-white/10 px-3 text-xs font-bold uppercase tracking-[0.18em] text-white/80">
            <Trophy className="h-3.5 w-3.5" />
            Competition directory
          </span>
          <h1 className="mt-4 text-3xl font-black tracking-tight sm:text-5xl">
            Published competitions
          </h1>
          <p className="mt-3 max-w-2xl text-sm leading-6 text-white/70 sm:text-base">
            Explore every published league, tournament, series and event for this organisation.
          </p>
        </div>
        <div className="grid gap-px bg-[var(--color-border)] sm:grid-cols-3">
          <Metric icon={<Trophy />} label="Competitions" value={competitions.length} />
          <Metric icon={<Activity />} label="Sports covered" value={sports} />
          <Metric icon={<ShieldCheck />} label="Visible now" value={filtered.length} />
        </div>
      </header>

      <section className="surface mt-6 grid gap-3 rounded-2xl border border-[var(--color-border)] p-4 shadow-[var(--shadow-card)] md:grid-cols-[minmax(0,1fr)_220px]">
        <label className="relative">
          <span className="sr-only">Search competitions</span>
          <Search className="absolute left-3.5 top-3.5 h-4 w-4 muted" />
          <input
            value={search}
            onChange={event => setSearch(event.target.value)}
            placeholder="Search by competition or sport"
            className="w-full rounded-xl border border-[var(--color-border)] bg-[var(--color-surface)] py-3 pl-10 pr-3 text-sm outline-none focus:border-[var(--brand-primary)]"
          />
        </label>
        <select
          value={status}
          onChange={event => setStatus(event.target.value)}
          className="rounded-xl border border-[var(--color-border)] bg-[var(--color-surface)] px-4 py-3 text-sm font-semibold"
          aria-label="Filter competitions by status"
        >
          <option value="">All statuses</option>
          {statuses.map(item => <option key={item} value={item}>{readable(item)}</option>)}
        </select>
      </section>

      {loading ? (
        <CompetitionSkeleton />
      ) : error ? (
        <Message tone="error">Could not load competitions: {error}</Message>
      ) : filtered.length === 0 ? (
        <Message>No published competitions match those filters.</Message>
      ) : (
        <div className="mt-6 grid gap-5 md:grid-cols-2 xl:grid-cols-3" data-testid="competition-directory">
          {filtered.map(competition => (
            <Link
              key={competition.id}
              href={tenantPath(`/competition/${competition.id}`, tenantSlug)}
              className="surface group flex min-h-56 flex-col rounded-2xl border border-[var(--color-border)] p-5 shadow-[var(--shadow-card)] transition hover:-translate-y-0.5 hover:shadow-lg"
            >
              <div className="flex items-start justify-between gap-3">
                <span className="flex h-11 w-11 items-center justify-center rounded-2xl bg-[var(--color-surface-muted)] text-[var(--brand-primary)]">
                  <Trophy className="h-5 w-5" />
                </span>
                <span className="rounded-full bg-[var(--color-surface-muted)] px-3 py-1.5 text-xs font-bold text-[var(--brand-primary)]">
                  {readable(competition.status)}
                </span>
              </div>
              <p className="mt-5 text-xs font-bold uppercase tracking-[0.14em] text-[var(--brand-primary)]">
                {competition.sportName || formatSport(competition.sportId)}
              </p>
              <h2 className="mt-1 text-xl font-black tracking-tight">{competition.name}</h2>
              <p className="mt-3 line-clamp-3 flex-1 text-sm leading-6 muted">
                {competition.description || 'Official schedules, results, standings and competition statistics.'}
              </p>
              <span className="mt-5 inline-flex items-center gap-2 text-sm font-bold text-[var(--brand-primary)]">
                Open competition
                <ArrowRight className="h-4 w-4 transition group-hover:translate-x-1" />
              </span>
            </Link>
          ))}
        </div>
      )}
    </div>
  );
}

function Metric({ icon, label, value }: { icon: React.ReactNode; label: string; value: number }) {
  return (
    <div className="bg-[var(--color-surface)] px-5 py-4">
      <div className="flex items-center justify-between gap-3">
        <span className="flex h-9 w-9 items-center justify-center rounded-xl bg-[var(--color-surface-muted)] text-[var(--brand-primary)] [&_svg]:h-4 [&_svg]:w-4">
          {icon}
        </span>
        <strong className="text-2xl font-black tracking-tight">{value}</strong>
      </div>
      <p className="mt-2 text-xs font-bold uppercase tracking-wider muted">{label}</p>
    </div>
  );
}

function CompetitionSkeleton() {
  return (
    <div className="mt-6 grid gap-5 md:grid-cols-2 xl:grid-cols-3" aria-label="Loading competitions">
      {[0, 1, 2, 3, 4, 5].map(item => (
        <div key={item} className="surface h-56 animate-pulse rounded-2xl border border-[var(--color-border)] p-5">
          <div className="h-11 w-11 rounded-2xl bg-[var(--color-surface-muted)]" />
          <div className="mt-5 h-3 w-24 rounded bg-[var(--color-surface-muted)]" />
          <div className="mt-3 h-6 w-3/4 rounded bg-[var(--color-surface-muted)]" />
          <div className="mt-4 h-16 rounded bg-[var(--color-surface-muted)]" />
        </div>
      ))}
    </div>
  );
}

function Message({ children, tone = 'neutral' }: { children: React.ReactNode; tone?: 'neutral' | 'error' }) {
  return (
    <div className={`surface mt-6 rounded-2xl border border-dashed px-6 py-14 text-center ${
      tone === 'error' ? 'border-red-300 text-red-700' : 'border-[var(--color-border)] muted'
    }`}>
      {children}
    </div>
  );
}

function readable(value: string) {
  return value.replaceAll(/([a-z])([A-Z])/g, '$1 $2').replaceAll('_', ' ');
}
