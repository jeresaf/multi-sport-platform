'use client';

import * as signalR from '@microsoft/signalr';
import Link from 'next/link';
import { Activity, CalendarDays, CalendarX2, Radio, RefreshCw, Star, Trophy } from 'lucide-react';
import { useCallback, useEffect, useMemo, useRef, useState } from 'react';
import { ContestCard } from '@/components/contest-card';
import { ContestFilterBar } from '@/components/contest-filter-bar';
import { DateNavigator } from '@/components/date-navigator';
import {
  formatSport,
  publicApi,
  PUBLIC_CONTEST_HUB_URL,
  type PublicCompetition,
  type PublicContest,
  type PublicHome,
  resolveTenantSlug,
  tenantPath,
} from '@/lib/public-api';
import { applyContestLiveUpdate, type ContestLiveUpdate } from '@/lib/contest-live-update';
import { invalidateDailyFeed, loadDailyFeed, readDailyFeed, updateDailyFeed } from '@/lib/daily-feed-cache';
import { contestPhase, groupByCompetition, localDateKey, type ContestFilter } from '@/lib/presentation';

const FAVOURITES_KEY = 'sports-platform:favourite-contests';

export function MatchCentrePage({
  initialFilter = 'all',
  initialSportSlug,
  eyebrow = 'Match centre',
  title = 'Scores, fixtures & results',
  description = 'Follow live contests, upcoming fixtures and official results across every published sport.',
}: {
  initialFilter?: ContestFilter;
  initialSportSlug?: string;
  eyebrow?: string;
  title?: string;
  description?: string;
}) {
  const [tenantSlug] = useState(resolveTenantSlug);
  const [date, setDate] = useState(initialDate);
  const [filter, setFilter] = useState<ContestFilter>(() => initialContestFilter(initialFilter));
  const [sportId, setSportId] = useState<string | null>(() => browserQuery('sport'));
  const [contests, setContests] = useState<PublicContest[]>([]);
  const [competitions, setCompetitions] = useState<PublicCompetition[]>([]);
  const [sponsors, setSponsors] = useState<PublicHome['sponsors']>([]);
  const [favourites, setFavourites] = useState<Set<string>>(loadFavourites);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const liveVersions = useRef(new Map<string, number>());
  const requestVersion = useRef(0);

  useEffect(() => {
    publicApi<PublicCompetition[]>('/competitions', undefined, tenantSlug)
      .then(items => {
        setCompetitions(items);
        if (initialSportSlug) {
          const match = items.find(item => slugify(item.sportName || formatSport(item.sportId)) === initialSportSlug);
          if (match) setSportId(match.sportId);
        }
      })
      .catch(() => undefined);
  }, [initialSportSlug, tenantSlug]);

  useEffect(() => {
    publicApi<PublicHome>('/home', undefined, tenantSlug)
      .then(home => setSponsors(home.sponsors ?? []))
      .catch(() => setSponsors([]));
  }, [tenantSlug]);

  const load = useCallback(async (force = false) => {
    const request = ++requestVersion.current;
    if (force) invalidateDailyFeed(tenantSlug, date);
    const cached = readDailyFeed(tenantSlug, date);
    if (cached) {
      setContests(cached);
      setLoading(false);
      setError(null);
      return;
    }
    setLoading(true);
    setError(null);
    const from = new Date(`${date}T00:00:00`);
    const to = new Date(from);
    to.setDate(to.getDate() + 1);
    try {
      const query = new URLSearchParams({ from: from.toISOString(), to: to.toISOString() });
      const data = await loadDailyFeed(tenantSlug, date,
        () => publicApi<PublicContest[]>(`/fixtures?${query}`, undefined, tenantSlug));
      if (request !== requestVersion.current) return;
      setContests(data);
    } catch (reason) {
      if (request !== requestVersion.current) return;
      setError(reason instanceof Error ? reason.message : 'Unable to load contests');
    } finally {
      if (request === requestVersion.current) setLoading(false);
    }
  }, [date, tenantSlug]);

  useEffect(() => {
    const timer = window.setTimeout(() => void load(), 0);
    return () => window.clearTimeout(timer);
  }, [load]);

  const subscriptionKey = useMemo(() => contests.map(item => item.id).sort().join(','), [contests]);
  useEffect(() => {
    const ids = subscriptionKey ? subscriptionKey.split(',') : [];
    if (ids.length === 0) return;
    let disposed = false;
    const connection = new signalR.HubConnectionBuilder()
      .withUrl(PUBLIC_CONTEST_HUB_URL, { withCredentials: false })
      .withAutomaticReconnect([0, 2_000, 5_000, 10_000])
      .configureLogging(signalR.LogLevel.Warning)
      .build();
    const patch = (update: ContestLiveUpdate) => {
      if (!update.contestId || (liveVersions.current.get(update.contestId) ?? -1) > update.version) return;
      liveVersions.current.set(update.contestId, update.version);
      setContests(current => {
        const next = current.map(item => applyContestLiveUpdate(item, update));
        updateDailyFeed(tenantSlug, date, cached => cached.map(item => applyContestLiveUpdate(item, update)));
        return next;
      });
    };
    connection.on('ContestLiveUpdated', patch);
    connection.on('ContestSnapshot', (snapshot: { contestId: string; status: string; contestStatus?: string; lastUpdated: string; participantScores?: ContestLiveUpdate['participants'] }) => patch({
      contestId: snapshot.contestId,
      version: Date.parse(snapshot.lastUpdated) || Date.now(),
      status: snapshot.status,
      contestStatus: snapshot.contestStatus,
      participants: snapshot.participantScores,
    }));
    const joinAll = () => Promise.all(ids.map(id => connection.invoke('JoinPublicContestGroup', tenantSlug, id)));
    connection.onreconnected(() => { if (!disposed) void joinAll(); });
    connection.start().then(joinAll).catch(() => undefined);
    return () => { disposed = true; void connection.stop(); };
  }, [date, subscriptionKey, tenantSlug]);

  const updateDate = (next: string) => {
    setDate(next);
    const url = new URL(window.location.href);
    url.searchParams.set('date', next);
    window.history.replaceState({}, '', url);
  };

  const toggleFavourite = useCallback((id: string) => {
    setFavourites(current => {
      const next = new Set(current);
      const key = `contest:${id}`;
      if (next.has(key)) next.delete(key); else next.add(key);
      try { localStorage.setItem(FAVOURITES_KEY, JSON.stringify([...next])); return next; }
      catch { return current; }
    });
  }, []);

  const sports = useMemo(() => Array.from(new Map(competitions.map(item => [item.sportId, item.sportName || formatSport(item.sportId)])).entries()), [competitions]);
  const visible = useMemo(() => contests.filter(contest => {
    if (sportId && contest.sportId !== sportId) return false;
    if (filter === 'favourites') return favourites.has(`contest:${contest.id}`)
      || (!!contest.competitionId && favourites.has(`competition:${contest.competitionId}`))
      || contest.participants?.some(item => favourites.has(`participant:${item.participantId}`)) === true;
    return filter === 'all' || contestPhase(contest.contestStatus ?? contest.status) === filter;
  }), [contests, favourites, filter, sportId]);
  const groups = useMemo(() => groupByCompetition(visible), [visible]);
  const liveCount = contests.filter(
    contest => contestPhase(contest.contestStatus ?? contest.status) === 'live',
  ).length;
  const upcomingCount = contests.filter(
    contest => contestPhase(contest.contestStatus ?? contest.status) === 'upcoming',
  ).length;
  const finishedCount = contests.filter(
    contest => contestPhase(contest.contestStatus ?? contest.status) === 'finished',
  ).length;
  const favouriteCount = contests.filter(contest =>
    favourites.has(`contest:${contest.id}`)
    || (!!contest.competitionId && favourites.has(`competition:${contest.competitionId}`))
    || contest.participants?.some(
      item => favourites.has(`participant:${item.participantId}`),
    ) === true
  ).length;

  return (
    <div className="app-container py-5 sm:py-7">
      <header
        className="mb-6 overflow-hidden rounded-[28px] border border-[var(--color-border)] bg-[var(--color-surface)] shadow-[var(--shadow-card)]"
        data-testid="match-centre-header"
      >
        <div className="bg-gradient-to-br from-slate-950 via-slate-900 to-slate-800 px-5 py-7 text-white sm:px-7 sm:py-8">
          <div className="flex flex-wrap items-end justify-between gap-5">
            <div className="max-w-2xl">
              <span className="inline-flex h-8 items-center gap-2 rounded-full border border-white/10 bg-white/10 px-3 text-xs font-bold uppercase tracking-[0.18em] text-white/80">
                <Radio className="h-3.5 w-3.5" />
                {eyebrow}
              </span>
              <h1 className="mt-4 text-3xl font-black tracking-tight sm:text-5xl">{title}</h1>
              <p className="mt-3 max-w-xl text-sm leading-6 text-white/70 sm:text-base">
                {description}
              </p>
            </div>
            <button
              onClick={() => void load(true)}
              disabled={loading}
              className="inline-flex items-center gap-2 rounded-xl border border-white/15 bg-white/10 px-4 py-2.5 text-sm font-semibold text-white transition hover:bg-white/15 disabled:opacity-50"
            >
              <RefreshCw className={`h-4 w-4 ${loading ? 'animate-spin' : ''}`} />
              Refresh scores
            </button>
          </div>
        </div>
        <div className="grid gap-px bg-[var(--color-border)] sm:grid-cols-2 xl:grid-cols-4">
          <FeedMetric icon={<Activity className="h-4 w-4" />} label="Live now" value={liveCount} />
          <FeedMetric icon={<CalendarDays className="h-4 w-4" />} label="Upcoming" value={upcomingCount} />
          <FeedMetric icon={<Trophy className="h-4 w-4" />} label="Finished" value={finishedCount} />
          <FeedMetric icon={<Star className="h-4 w-4" />} label="Favourites" value={favouriteCount} />
        </div>
      </header>

      <div className="grid gap-6 xl:grid-cols-[220px_minmax(0,1fr)_260px]">
        <aside className="hidden xl:block" aria-label="Sports">
          <div className="surface sticky top-24 overflow-hidden rounded-2xl border border-[var(--color-border)] p-2 shadow-[var(--shadow-card)]">
            <button onClick={() => setSportId(null)} className={`w-full rounded-xl px-3 py-2.5 text-left text-sm font-semibold ${!sportId ? 'bg-[var(--color-surface-muted)] text-[var(--brand-primary)]' : ''}`}>All sports</button>
            {sports.map(([id, name]) => (
              <button key={id} onClick={() => setSportId(id)} className={`w-full rounded-xl px-3 py-2.5 text-left text-sm font-semibold ${sportId === id ? 'bg-[var(--color-surface-muted)] text-[var(--brand-primary)]' : ''}`}>{name}</button>
            ))}
          </div>
        </aside>

        <main className="min-w-0">
          <div className="mb-4 grid gap-3 xl:grid-cols-[minmax(0,1fr)_220px] xl:items-center">
            <DateNavigator value={date} onChange={updateDate} />
            <select className="surface rounded-xl px-3 py-3 text-sm xl:hidden" value={sportId ?? ''} onChange={event => setSportId(event.target.value || null)} aria-label="Choose sport">
              <option value="">All sports</option>
              {sports.map(([id, name]) => <option key={id} value={id}>{name}</option>)}
            </select>
          </div>
          <ContestFilterBar value={filter} onChange={setFilter} />

          {loading ? <ContestSkeleton /> : error ? (
            <StatePanel icon={<RefreshCw />} title="Coverage could not be loaded" detail={error} action={<button onClick={() => void load(true)} className="rounded-lg bg-[var(--brand-primary)] px-4 py-2 font-semibold text-white">Try again</button>} />
          ) : groups.length === 0 ? (
            <StatePanel icon={<CalendarX2 />} title="No contests found" detail={filter === 'favourites' ? 'Favourite a contest to keep it close at hand.' : 'Try another date, sport or filter.'} />
          ) : (
            <div className="mt-6 space-y-7">
              {groups.map(group => (
                <section key={group.id} aria-labelledby={`competition-${group.id}`}>
                  <div className="mb-3 flex items-end justify-between gap-4 rounded-2xl border border-[var(--color-border)] bg-[var(--color-surface)] px-4 py-3">
                    <div className="min-w-0">
                      <div className="flex items-center gap-2">
                        <span className="rounded-full bg-[var(--color-surface-muted)] px-2.5 py-1 text-[11px] font-bold uppercase tracking-wider text-[var(--brand-primary)]">
                          {formatSport(group.sportId)}
                        </span>
                        <span className="text-xs muted">{group.contests.length} contests</span>
                      </div>
                      <h2 id={`competition-${group.id}`} className="mt-2 truncate text-xl font-bold">{group.name}</h2>
                    </div>
                    {group.id !== group.sportId && (
                      <Link
                        className="shrink-0 rounded-xl border border-[var(--color-border)] px-3 py-2 text-sm font-semibold text-[var(--brand-primary)] hover:bg-[var(--color-surface-muted)]"
                        href={tenantPath(`/competition/${group.id}`, tenantSlug)}
                      >
                        Competition
                      </Link>
                    )}
                  </div>
                  <div className="contest-grid">
                    {group.contests.map(contest => <ContestCard key={contest.id} contest={contest} tenantSlug={tenantSlug} favourite={favourites.has(`contest:${contest.id}`)} onToggleFavourite={toggleFavourite} />)}
                  </div>
                </section>
              ))}
            </div>
          )}
        </main>

        <aside className="hidden xl:block" aria-label="Featured competitions">
          <div className="surface sticky top-24 rounded-2xl border border-[var(--color-border)] p-4 shadow-[var(--shadow-card)]">
            <div className="mb-3 flex items-center gap-2"><Trophy className="h-5 w-5 text-[var(--brand-accent)]" /><h2 className="font-bold">Competitions</h2></div>
            <div className="space-y-1">
              {competitions.slice(0, 8).map(competition => (
                <Link key={competition.id} href={tenantPath(`/competition/${competition.id}`, tenantSlug)} className="block rounded-xl p-3 hover:bg-[var(--color-surface-muted)]">
                  <span className="block text-sm font-semibold">{competition.name}</span>
                  <span className="text-xs muted">{formatSport(competition.sportId)}</span>
                </Link>
              ))}
            </div>
            <Link className="mt-3 block rounded-xl border border-[var(--color-border)] px-3 py-2 text-center text-sm font-semibold" href={tenantPath('/competitions', tenantSlug)}>View all competitions</Link>
          </div>
        </aside>
      </div>

      {sponsors.length > 0 && (
        <section
          className="surface mt-8 rounded-2xl p-5"
          aria-labelledby="partners-sponsors-heading"
          data-testid="public-sponsors"
        >
          <p
            id="partners-sponsors-heading"
            className="text-xs font-bold uppercase tracking-[0.18em] text-[var(--brand-primary)]"
          >
            Partners & sponsors
          </p>
          <div className="mt-4 flex flex-wrap items-center gap-3">
            {sponsors.map(sponsor => sponsor.website ? (
              <a
                key={sponsor.id}
                href={sponsor.website}
                target="_blank"
                rel="noreferrer"
                className="rounded-xl border border-[var(--color-border)] px-4 py-3 text-sm font-semibold hover:bg-[var(--color-surface-muted)]"
              >
                {sponsor.name}
              </a>
            ) : (
              <span
                key={sponsor.id}
                className="rounded-xl border border-[var(--color-border)] px-4 py-3 text-sm font-semibold"
              >
                {sponsor.name}
              </span>
            ))}
          </div>
        </section>
      )}
    </div>
  );
}

export default function PublicHomePage() {
  return <MatchCentrePage />;
}


function FeedMetric({
  icon,
  label,
  value,
}: {
  icon: React.ReactNode;
  label: string;
  value: number;
}) {
  return (
    <div className="bg-[var(--color-surface)] px-4 py-4 sm:px-5">
      <div className="flex items-center justify-between gap-3">
        <span className="flex h-9 w-9 items-center justify-center rounded-xl bg-[var(--color-surface-muted)] text-[var(--brand-primary)]">
          {icon}
        </span>
        <strong className="text-2xl font-black tracking-tight">{value}</strong>
      </div>
      <p className="mt-2 text-xs font-bold uppercase tracking-wider muted">{label}</p>
    </div>
  );
}

function ContestSkeleton() {
  return <div className="contest-grid mt-6" role="status" aria-label="Loading contests">{[0, 1, 2].map(item => <div key={item} className="surface h-52 animate-pulse rounded-2xl bg-[var(--color-surface-muted)]" />)}</div>;
}

function StatePanel({ icon, title, detail, action }: { icon: React.ReactNode; title: string; detail: string; action?: React.ReactNode }) {
  return <div className="surface mt-6 rounded-2xl px-6 py-14 text-center"><div className="mx-auto mb-3 flex h-12 w-12 items-center justify-center rounded-full bg-[var(--color-surface-muted)] text-[var(--brand-primary)]">{icon}</div><h2 className="text-lg font-bold">{title}</h2><p className="mx-auto mt-1 max-w-md text-sm muted">{detail}</p>{action && <div className="mt-5">{action}</div>}</div>;
}

function slugify(value: string) {
  return value.toLowerCase().replaceAll(/[^a-z0-9]+/g, '-').replaceAll(/(^-|-$)/g, '');
}

function browserQuery(name: string) {
  return typeof window === 'undefined'
    ? null
    : new URLSearchParams(window.location.search).get(name);
}

function initialDate() {
  const requested = browserQuery('date');
  return requested && /^\d{4}-\d{2}-\d{2}$/.test(requested)
    ? requested
    : localDateKey(new Date());
}

function initialContestFilter(fallback: ContestFilter): ContestFilter {
  const requested = browserQuery('filter');
  return requested && ['all', 'live', 'upcoming', 'finished', 'favourites'].includes(requested)
    ? requested as ContestFilter
    : fallback;
}

function loadFavourites() {
  if (typeof window === 'undefined') return new Set<string>();
  try {
    const values = JSON.parse(localStorage.getItem(FAVOURITES_KEY) ?? '[]') as string[];
    return new Set(values.map(value => value.includes(':') ? value : `contest:${value}`));
  } catch {
    return new Set<string>();
  }
}
