'use client';

import Link from 'next/link';
import { Activity, BarChart3, Medal, Radio, Target, Trophy } from 'lucide-react';
import { useEffect, useMemo, useState } from 'react';
import {
  formatSport,
  publicApi,
  type PublicHome,
  resolveTenantSlug,
  tenantPath,
} from '@/lib/public-api';

type Leader = {
  participantId: string;
  name: string;
  appearances: number;
  wins: number;
  podiums: number;
};

export default function StatisticsPage() {
  const [home, setHome] = useState<PublicHome | null>(null);
  const [tenantSlug] = useState(resolveTenantSlug);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    publicApi<PublicHome>('/home', undefined, tenantSlug)
      .then(setHome)
      .catch((reason: Error) => setError(reason.message))
      .finally(() => setLoading(false));
  }, [tenantSlug]);

  const leaders = useMemo(() => {
    const grouped = new Map<string, Leader>();
    for (const result of home?.recentResults ?? []) {
      const current = grouped.get(result.participantId) ?? {
        participantId: result.participantId,
        name: result.participantName,
        appearances: 0,
        wins: 0,
        podiums: 0,
      };
      current.appearances += 1;
      if (result.rank === 1) current.wins += 1;
      if (result.rank != null && result.rank <= 3) current.podiums += 1;
      grouped.set(result.participantId, current);
    }
    return [...grouped.values()]
      .sort((left, right) =>
        right.wins - left.wins
        || right.podiums - left.podiums
        || right.appearances - left.appearances
        || left.name.localeCompare(right.name))
      .slice(0, 8);
  }, [home]);

  const sportCoverage = useMemo(() => {
    const grouped = new Map<string, { sportId: string; name: string; total: number }>();
    for (const competition of home?.competitions ?? []) {
      const current = grouped.get(competition.sportId) ?? {
        sportId: competition.sportId,
        name: competition.sportName || formatSport(competition.sportId),
        total: 0,
      };
      current.total += 1;
      grouped.set(competition.sportId, current);
    }
    return [...grouped.values()].sort((left, right) => right.total - left.total);
  }, [home]);

  if (loading) return <PageState message="Loading published statistics…" />;
  if (error) return <PageState message={`Statistics could not be loaded: ${error}`} tone="error" />;
  if (!home) return <PageState message="No published statistics are available." />;

  const maxCoverage = Math.max(1, ...sportCoverage.map(item => item.total));

  return (
    <div className="app-container py-5 sm:py-7">
      <header className="overflow-hidden rounded-[28px] border border-[var(--color-border)] bg-[var(--color-surface)] shadow-[var(--shadow-card)]">
        <div className="bg-gradient-to-br from-slate-950 via-slate-900 to-slate-800 px-5 py-7 text-white sm:px-7 sm:py-8">
          <span className="inline-flex h-8 items-center gap-2 rounded-full border border-white/10 bg-white/10 px-3 text-xs font-bold uppercase tracking-[0.18em] text-white/80">
            <BarChart3 className="h-3.5 w-3.5" />
            Statistics centre
          </span>
          <h1 className="mt-4 text-3xl font-black tracking-tight sm:text-5xl">
            Published performance
          </h1>
          <p className="mt-3 max-w-2xl text-sm leading-6 text-white/70 sm:text-base">
            A tenant-wide snapshot of competition coverage, active contests and recently published performers.
          </p>
        </div>
        <div
          data-testid="statistics-summary"
          className="grid gap-px bg-[var(--color-border)] sm:grid-cols-2 xl:grid-cols-4"
        >
          <Metric icon={<Trophy />} label="Competitions" value={home.competitions.length} />
          <Metric icon={<Radio />} label="Live now" value={home.liveContests.length} live />
          <Metric icon={<Target />} label="Upcoming" value={home.upcomingContests.length} />
          <Metric icon={<Medal />} label="Recent results" value={home.recentResults.length} />
        </div>
      </header>

      <div className="mt-6 grid gap-6 xl:grid-cols-[minmax(0,1.2fr)_minmax(320px,.8fr)]">
        <section className="surface overflow-hidden rounded-2xl shadow-[var(--shadow-card)]">
          <div className="flex flex-wrap items-center justify-between gap-3 border-b border-[var(--color-border)] px-5 py-4">
            <div>
              <p className="text-xs font-bold uppercase tracking-[0.16em] text-[var(--brand-primary)]">
                Form table
              </p>
              <h2 className="mt-1 text-xl font-black">Recently published leaders</h2>
            </div>
            <span className="rounded-full bg-[var(--color-surface-muted)] px-3 py-1.5 text-xs font-semibold muted">
              Current public feed
            </span>
          </div>
          {leaders.length === 0 ? (
            <Empty message="Leaders will appear after ranked results are published." />
          ) : (
            <div data-testid="statistics-leaderboard" className="divide-y divide-[var(--color-border)]">
              {leaders.map((leader, index) => (
                <Link
                  key={leader.participantId}
                  href={tenantPath(`/participants/${leader.participantId}`, tenantSlug)}
                  className="grid grid-cols-[2.25rem_minmax(0,1fr)_repeat(3,3.5rem)] items-center gap-2 px-4 py-4 transition hover:bg-[var(--color-surface-muted)] sm:px-5"
                >
                  <span className={`flex h-8 w-8 items-center justify-center rounded-xl text-sm font-black ${
                    index < 3
                      ? 'bg-[var(--brand-primary)] text-white'
                      : 'bg-[var(--color-surface-muted)] muted'
                  }`}>
                    {index + 1}
                  </span>
                  <span className="min-w-0">
                    <strong className="block truncate text-sm">{leader.name}</strong>
                    <small className="muted">Public results</small>
                  </span>
                  <StatCell label="Apps" value={leader.appearances} />
                  <StatCell label="Wins" value={leader.wins} />
                  <StatCell label="Podiums" value={leader.podiums} />
                </Link>
              ))}
            </div>
          )}
        </section>

        <section className="surface rounded-2xl p-5 shadow-[var(--shadow-card)]">
          <p className="text-xs font-bold uppercase tracking-[0.16em] text-[var(--brand-primary)]">
            Coverage
          </p>
          <h2 className="mt-1 text-xl font-black">Competitions by sport</h2>
          {sportCoverage.length === 0 ? (
            <Empty message="Published sport coverage will appear here." compact />
          ) : (
            <div className="mt-6 space-y-5">
              {sportCoverage.map(item => (
                <div key={item.sportId}>
                  <div className="mb-2 flex items-center justify-between gap-3 text-sm">
                    <span className="font-semibold">{item.name}</span>
                    <strong>{item.total}</strong>
                  </div>
                  <div className="h-2 overflow-hidden rounded-full bg-[var(--color-surface-muted)]">
                    <div
                      className="h-full rounded-full bg-[var(--brand-primary)]"
                      style={{ width: `${Math.max(8, (item.total / maxCoverage) * 100)}%` }}
                    />
                  </div>
                </div>
              ))}
            </div>
          )}
          <Link
            href={tenantPath('/competitions', tenantSlug)}
            className="mt-7 block rounded-xl border border-[var(--color-border)] px-4 py-3 text-center text-sm font-bold text-[var(--brand-primary)] hover:bg-[var(--color-surface-muted)]"
          >
            Browse competitions
          </Link>
        </section>
      </div>

      <section className="surface mt-6 overflow-hidden rounded-2xl shadow-[var(--shadow-card)]">
        <div className="flex flex-wrap items-center justify-between gap-3 border-b border-[var(--color-border)] px-5 py-4">
          <div>
            <p className="text-xs font-bold uppercase tracking-[0.16em] text-[var(--brand-primary)]">
              Latest
            </p>
            <h2 className="mt-1 text-xl font-black">Published performances</h2>
          </div>
          <Link
            href={tenantPath('/results', tenantSlug)}
            className="rounded-xl border border-[var(--color-border)] px-3 py-2 text-sm font-semibold hover:bg-[var(--color-surface-muted)]"
          >
            View all results
          </Link>
        </div>
        {home.recentResults.length === 0 ? (
          <Empty message="No official results have been published yet." />
        ) : (
          <div className="divide-y divide-[var(--color-border)]">
            {home.recentResults.slice(0, 12).map(result => (
              <Link
                key={result.id}
                href={tenantPath(`/contest/${result.contestId}`, tenantSlug)}
                className="grid gap-3 px-5 py-4 transition hover:bg-[var(--color-surface-muted)] sm:grid-cols-[minmax(0,1fr)_minmax(0,1fr)_6rem_7rem] sm:items-center"
              >
                <span className="min-w-0">
                  <strong className="block truncate">{result.participantName}</strong>
                  <small className="muted">{result.status}</small>
                </span>
                <span className="truncate text-sm muted">{result.contestName}</span>
                <span className="text-sm font-bold">
                  {result.rank ? `#${result.rank}` : result.position ?? '—'}
                </span>
                <span className="text-left font-black sm:text-right">
                  {result.scoreDisplay ?? result.time ?? result.score ?? '—'}
                </span>
              </Link>
            ))}
          </div>
        )}
      </section>
    </div>
  );
}

function Metric({
  icon,
  label,
  value,
  live = false,
}: {
  icon: React.ReactNode;
  label: string;
  value: number;
  live?: boolean;
}) {
  return (
    <div className="bg-[var(--color-surface)] px-5 py-4">
      <div className="flex items-center justify-between gap-3">
        <span className={`flex h-9 w-9 items-center justify-center rounded-xl bg-[var(--color-surface-muted)] [&_svg]:h-4 [&_svg]:w-4 ${
          live ? 'text-[var(--status-live)]' : 'text-[var(--brand-primary)]'
        }`}>
          {icon}
        </span>
        <strong className="text-2xl font-black tracking-tight">{value}</strong>
      </div>
      <p className="mt-2 text-xs font-bold uppercase tracking-wider muted">{label}</p>
    </div>
  );
}

function StatCell({ label, value }: { label: string; value: number }) {
  return (
    <span className="text-center">
      <strong className="block text-sm">{value}</strong>
      <small className="text-[10px] font-bold uppercase tracking-wider muted">{label}</small>
    </span>
  );
}

function Empty({ message, compact = false }: { message: string; compact?: boolean }) {
  return (
    <div className={`px-5 text-center muted ${compact ? 'py-8' : 'py-14'}`}>
      {message}
    </div>
  );
}

function PageState({
  message,
  tone = 'neutral',
}: {
  message: string;
  tone?: 'neutral' | 'error';
}) {
  return (
    <div className="app-container py-24">
      <div className={`surface mx-auto max-w-2xl rounded-2xl px-6 py-14 text-center ${
        tone === 'error' ? 'text-red-700' : 'muted'
      }`}>
        <Activity className="mx-auto mb-3 h-9 w-9 text-[var(--brand-primary)]" />
        {message}
      </div>
    </div>
  );
}
