'use client';

import * as signalR from '@microsoft/signalr';
import Link from 'next/link';
import { CalendarX2, RefreshCw, Trophy } from 'lucide-react';
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
  resolveTenantSlug,
  tenantPath,
} from '@/lib/public-api';
import { applyContestLiveUpdate, type ContestLiveUpdate } from '@/lib/contest-live-update';
import { invalidateDailyFeed, loadDailyFeed, readDailyFeed, updateDailyFeed } from '@/lib/daily-feed-cache';
import { contestPhase, groupByCompetition, localDateKey, type ContestFilter } from '@/lib/presentation';

const FAVOURITES_KEY = 'sports-platform:favourite-contests';

export function MatchCentrePage({ initialFilter = 'all', initialSportSlug }: { initialFilter?: ContestFilter; initialSportSlug?: string }) {
  const [tenantSlug] = useState(resolveTenantSlug);
  const [date, setDate] = useState(initialDate);
  const [filter, setFilter] = useState<ContestFilter>(() => initialContestFilter(initialFilter));
  const [sportId, setSportId] = useState<string | null>(() => browserQuery('sport'));
  const [contests, setContests] = useState<PublicContest[]>([]);
  const [competitions, setCompetitions] = useState<PublicCompetition[]>([]);
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
    connection.on('ContestSnapshot', (snapshot: { contestId: string; status: string; lastUpdated: string; participantScores?: ContestLiveUpdate['participants'] }) => patch({
      contestId: snapshot.contestId,
      version: Date.parse(snapshot.lastUpdated) || Date.now(),
      status: snapshot.status,
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
    return filter === 'all' || contestPhase(contest.status) === filter;
  }), [contests, favourites, filter, sportId]);
  const groups = useMemo(() => groupByCompetition(visible), [visible]);

  return (
    <div className="app-container py-5 sm:py-7">
      <header className="mb-6" data-testid="match-centre-header">
        <p className="text-sm font-bold uppercase tracking-[0.18em] text-[var(--brand-primary)]">Match centre</p>
        <h1 className="mt-1 text-3xl font-extrabold tracking-tight sm:text-4xl">Scores & schedule</h1>
        <p className="mt-2 muted">Live, upcoming and official results across every published sport.</p>
      </header>

      <div className="grid gap-6 xl:grid-cols-[220px_minmax(0,1fr)_260px]">
        <aside className="hidden xl:block" aria-label="Sports">
          <div className="surface sticky top-24 overflow-hidden rounded-2xl p-2">
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
                  <div className="mb-3 flex items-end justify-between gap-4">
                    <div className="min-w-0">
                      <p className="text-xs font-bold uppercase tracking-wider text-[var(--brand-primary)]">{formatSport(group.sportId)}</p>
                      <h2 id={`competition-${group.id}`} className="truncate text-xl font-bold">{group.name}</h2>
                    </div>
                    {group.id !== group.sportId && <Link className="shrink-0 text-sm font-semibold text-[var(--brand-primary)]" href={tenantPath(`/competition/${group.id}`, tenantSlug)}>Competition</Link>}
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
          <div className="surface sticky top-24 rounded-2xl p-4">
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
    </div>
  );
}

export default function PublicHomePage() {
  return <MatchCentrePage />;
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
