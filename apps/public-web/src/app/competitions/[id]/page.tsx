'use client';

import Link from 'next/link';
import { useParams, usePathname } from 'next/navigation';
import { BarChart3, CalendarDays, Trophy, Users } from 'lucide-react';
import { useCallback, useEffect, useState } from 'react';
import { CompetitionHero } from '@/components/competition/competition-hero';
import { CompetitionNavigation } from '@/components/competition/competition-navigation';
import { CompetitionOutputRenderer, Empty } from '@/components/competition/competition-output-renderer';
import type { GolfLeaderboardData } from '@/components/competition/golf-leaderboard';
import type { CompetitionDetail, CompetitionFixture, CompetitionResult, CompetitionView, Playoffs } from '@/components/competition/competition-types';
import { publicApi, resolveTenantSlug, TENANT_SLUG, tenantPath } from '@/lib/public-api';
import { AlertPreferencesDialog } from '@/components/alert-preferences-dialog';
import { loadAlerts } from '@/lib/alert-preferences';
import { ContestStatus } from '@/components/contest-status';
import { normalizeStatus } from '@/lib/presentation';

export default function CompetitionDetailPage() {
  const params = useParams<{ id: string }>();
  const pathname = usePathname();
  const [competition, setCompetition] = useState<CompetitionDetail | null>(null);
  const [playoffs, setPlayoffs] = useState<Playoffs | null>(null);
  const [golfLeaderboard, setGolfLeaderboard] = useState<GolfLeaderboardData | null>(null);
  const [activeTab, setActiveTab] = useState<CompetitionView>('overview');
  const [tenantSlug, setTenantSlug] = useState(TENANT_SLUG);
  const [followed, setFollowed] = useState(false);
  const [alerts, setAlerts] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [alertDialog, setAlertDialog] = useState(false);

  useEffect(() => {
    const resolvedTenant = resolveTenantSlug();
    publicApi<CompetitionDetail>(`/competitions/${encodeURIComponent(params.id)}`, undefined, resolvedTenant)
      .then(detail => {
        const routeView = pathname.startsWith(`/competition/${params.id}/`)
          ? pathname.split('/').filter(Boolean).at(-1) as CompetitionView
          : null;
        const requested = routeView ?? new URLSearchParams(window.location.search).get('view') as CompetitionView | null;
        setTenantSlug(resolvedTenant);
        setCompetition(detail);
        setActiveTab(requested && detail.availableTabs.includes(requested)
          ? requested
          : detail.presentation?.defaultTab && detail.availableTabs.includes(detail.presentation.defaultTab)
            ? detail.presentation.defaultTab
            : detail.availableTabs[0] ?? 'overview');
        setFollowed(readFavourite(detail.id));
        setAlerts(loadAlerts('competition', detail.id).size > 0);
        if (detail.availableTabs.includes('bracket') || detail.availableTabs.includes('series')) {
          publicApi<Playoffs>(`/playoffs?competitionId=${encodeURIComponent(detail.id)}`, undefined, resolvedTenant)
            .then(setPlayoffs).catch(() => setPlayoffs(null));
        }
        if (detail.availableTabs.includes('leaderboard')) {
          publicApi<GolfLeaderboardData>(`/golf/leaderboard?competitionId=${encodeURIComponent(detail.id)}`, undefined, resolvedTenant)
            .then(setGolfLeaderboard).catch(() => setGolfLeaderboard(null));
        }
      })
      .catch((reason: Error) => setError(reason.message));
  }, [params.id, pathname]);

  useEffect(() => {
    if (!competition) return;
    const syncView = () => {
      const requested = new URLSearchParams(window.location.search)
        .get('view') as CompetitionView | null;
      if (requested && competition.availableTabs.includes(requested))
        setActiveTab(requested);
    };
    window.addEventListener('popstate', syncView);
    return () => window.removeEventListener('popstate', syncView);
  }, [competition]);

  const changeTab = useCallback((view: CompetitionView) => setActiveTab(view), []);
  const toggleFollow = () => {
    if (!competition) return;
    const current = loadFavouriteKeys(); const key = `competition:${competition.id}`;
    if (current.has(key)) current.delete(key); else current.add(key);
    try { localStorage.setItem('sports-platform:favourite-contests', JSON.stringify([...current])); setFollowed(current.has(key)); } catch { /* optimistic rollback keeps current UI */ }
  };

  if (error) return <PageMessage>Competition unavailable: {error}</PageMessage>;
  if (!competition) return <PageMessage>Loading competition…</PageMessage>;

  return <div className="app-container py-8 sm:py-10">
    <Link href={tenantPath('/competitions', tenantSlug)} className="text-sm font-medium muted hover:underline">← All competitions</Link>
    <CompetitionHero competition={competition} followed={followed} alerts={alerts} onFollow={toggleFollow} onAlerts={() => followed && setAlertDialog(true)} />
    <AlertPreferencesDialog open={alertDialog} type="competition" id={competition.id} title={competition.name} sportCode={competition.sport?.code} onClose={() => setAlertDialog(false)} onChange={setAlerts} />
    <CompetitionNavigation id={competition.id} views={competition.availableTabs} active={activeTab} labels={competition.labels} tenantSlug={tenantSlug} onChange={changeTab} />
    <main className="py-7" data-testid={`competition-panel-${activeTab}`}>
      {activeTab === 'overview' && <Overview competition={competition} playoffs={playoffs} tenantSlug={tenantSlug} onView={changeTab} />}
      {activeTab === 'fixtures' && <Fixtures fixtures={competition.fixtures} tenantSlug={tenantSlug} label={competition.labels.fixtures ?? 'Fixtures'} />}
      {activeTab === 'results' && <Results results={competition.results} tenantSlug={tenantSlug} />}
      {activeTab === 'standings' && <CompetitionOutputRenderer kind="STANDINGS" competition={competition} playoffs={playoffs} tenantSlug={tenantSlug} />}
      {activeTab === 'groups' && <CompetitionOutputRenderer kind="GROUPS" competition={competition} playoffs={playoffs} tenantSlug={tenantSlug} />}
      {activeTab === 'bracket' && <CompetitionOutputRenderer kind="BRACKET" competition={competition} playoffs={playoffs} tenantSlug={tenantSlug} />}
      {activeTab === 'series' && <CompetitionOutputRenderer kind="SERIES" competition={competition} playoffs={playoffs} tenantSlug={tenantSlug} />}
      {activeTab === 'leaderboard' && <CompetitionOutputRenderer kind="LEADERBOARD" competition={competition} playoffs={playoffs} tenantSlug={tenantSlug} golfLeaderboard={golfLeaderboard} />}
      {activeTab === 'medals' && <CompetitionOutputRenderer kind="MEDALS" competition={competition} playoffs={playoffs} tenantSlug={tenantSlug} />}
      {activeTab === 'participants' && <Participants competition={competition} tenantSlug={tenantSlug} />}
      {activeTab === 'statistics' && <Statistics statistics={competition.statistics} />}
    </main>
  </div>;
}

function Overview({ competition, playoffs, tenantSlug, onView }: { competition: CompetitionDetail; playoffs: Playoffs | null; tenantSlug: string; onView: (view: CompetitionView) => void }) {
  const live = competition.fixtures.filter(item => normalizeStatus(item.contestStatus ?? item.status) === 'live').slice(0, 4);
  const upcoming = competition.fixtures.filter(item => ['notstarted', 'startingsoon', 'scheduled', 'ready', 'checkinopen'].includes(normalizeStatus(item.contestStatus ?? item.status))).slice(0, 4);
  const latest = competition.results.slice(0, 6);
  return <div className="space-y-6">
    {['combat', 'mma'].includes(competition.sport?.code?.toLowerCase() ?? '') && <Link href={tenantPath(`/fight-card?competitionId=${competition.id}`, tenantSlug)} className="surface flex items-center justify-between rounded-2xl p-5 font-bold hover:shadow-md"><span>Open the full fight card</span><span aria-hidden>→</span></Link>}
    <section className="grid gap-4 sm:grid-cols-2 xl:grid-cols-4">
      <Metric label="Contests" value={competition.statistics.contestCount} icon={CalendarDays} />
      <Metric label="Live now" value={competition.statistics.liveContests} icon={BarChart3} />
      <Metric label="Participants" value={competition.statistics.participantCount} icon={Users} />
      <Metric label="Completed" value={competition.statistics.completedContests} icon={Trophy} />
    </section>
    {live.length > 0 && <OverviewModule title="Live now" action={() => onView('fixtures')}><Fixtures fixtures={live} tenantSlug={tenantSlug} label="fixtures" compact /></OverviewModule>}
    {upcoming.length > 0 && <OverviewModule title="Next fixtures" action={() => onView('fixtures')}><Fixtures fixtures={upcoming} tenantSlug={tenantSlug} label="fixtures" compact /></OverviewModule>}
    {latest.length > 0 && <OverviewModule title="Latest results" action={() => onView('results')}><Results results={latest} tenantSlug={tenantSlug} compact /></OverviewModule>}
    {competition.standings.length > 0 && <OverviewModule title="Standings preview" action={() => onView(competition.availableTabs.includes('groups') ? 'groups' : 'standings')}><CompetitionOutputRenderer kind={competition.availableTabs.includes('groups') ? 'GROUPS' : 'STANDINGS'} competition={competition} playoffs={playoffs} tenantSlug={tenantSlug} compact /></OverviewModule>}
    {playoffs && <OverviewModule title="Bracket preview" action={() => onView('bracket')}><CompetitionOutputRenderer kind="BRACKET" competition={competition} playoffs={{ ...playoffs, rounds: playoffs.rounds.slice(-2) }} tenantSlug={tenantSlug} compact /></OverviewModule>}
    <section className="grid gap-5 lg:grid-cols-[2fr_1fr]">
      <div className="surface rounded-2xl p-6"><h2 className="text-xl font-bold">Competition structure</h2><div className="mt-4 space-y-3">{competition.editions.map(edition => <article key={edition.id} className="rounded-xl bg-[var(--color-surface-muted)] p-4"><div className="flex justify-between gap-2"><h3 className="font-bold">{edition.name}</h3><span className="text-sm muted">{edition.status}</span></div><div className="mt-3 flex flex-wrap gap-2">{edition.stages.map(stage => <span key={stage.id} className="surface rounded-full px-3 py-1 text-xs">{stage.name}</span>)}</div></article>)}</div></div>
      <aside className="surface rounded-2xl p-6"><Trophy className="h-7 w-7 text-amber-500" /><h2 className="mt-3 font-bold">Official configuration</h2><p className="mt-2 text-sm muted">{competition.ruleset ? `${competition.ruleset.name} · version ${competition.ruleset.versionNumber}` : 'Published sport defaults'}</p>{competition.sponsors.length > 0 && <div className="mt-5 border-t border-[var(--color-border)] pt-4"><p className="text-xs font-bold uppercase muted">Partners</p><p className="mt-2 font-semibold">{competition.sponsors.map(item => item.name).join(' · ')}</p></div>}</aside>
    </section>
  </div>;
}

function OverviewModule({ title, action, children }: { title: string; action: () => void; children: React.ReactNode }) { return <section><div className="mb-3 flex items-center justify-between"><h2 className="text-xl font-bold">{title}</h2><button onClick={action} className="text-sm font-semibold text-[var(--brand-primary)]">View all →</button></div>{children}</section>; }

function Fixtures({ fixtures, tenantSlug, label, compact = false }: { fixtures: CompetitionFixture[]; tenantSlug: string; label: string; compact?: boolean }) {
  if (fixtures.length === 0) return <Empty message={`No ${label.toLowerCase()} are published.`} />;
  return <div className={`grid gap-3 ${compact ? 'md:grid-cols-2' : 'md:grid-cols-2 xl:grid-cols-3'}`}>{fixtures.map(fixture => <Link key={fixture.id} href={tenantPath(`/contest/${fixture.id}`, tenantSlug)} className="surface rounded-xl p-4 transition hover:-translate-y-0.5 hover:shadow-md"><div className="flex items-center justify-between gap-3"><ContestStatus status={fixture.contestStatus ?? fixture.status} /><span className="text-xs font-bold uppercase muted">{formatDate(fixture.startTime)}</span></div><h3 className="mt-3 font-bold">{fixture.name || fixture.participants.map(item => item.name).join(' vs ') || fixture.contestShape}</h3>{fixture.venue && <p className="mt-2 text-sm muted">{fixture.venue}</p>}</Link>)}</div>;
}

function Results({ results, tenantSlug, compact = false }: { results: CompetitionResult[]; tenantSlug: string; compact?: boolean }) {
  if (results.length === 0) return <Empty message="No official results are published." />;
  return <div className="overflow-x-auto rounded-2xl border border-[var(--color-border)]"><table className={`w-full text-sm ${compact ? '' : 'min-w-[640px]'}`}><thead className="bg-gray-950 text-left text-white"><tr><th className="p-3">Contest</th><th className="p-3">Participant</th><th className="p-3">Position</th><th className="p-3">Result</th></tr></thead><tbody>{results.map(result => <tr key={result.id} className="border-t border-[var(--color-border)]"><td className="p-3"><Link className="hover:underline" href={tenantPath(`/contest/${result.contestId}`, tenantSlug)}>{result.contestName}</Link></td><td className="p-3 font-semibold">{result.participantName}</td><td className="p-3">{result.position ?? result.rank ?? '—'}</td><td className="p-3 font-semibold">{result.scoreDisplay ?? result.time ?? result.score ?? 'Official'}</td></tr>)}</tbody></table></div>;
}

function Participants({ competition, tenantSlug }: { competition: CompetitionDetail; tenantSlug: string }) { const label = competition.labels.participants ?? 'Participants'; if (!competition.participants.length) return <Empty message={`No ${label.toLowerCase()} are published.`} />; return <div className="grid gap-3 sm:grid-cols-2 lg:grid-cols-3">{competition.participants.map(participant => <Link key={participant.id} href={tenantPath(`/participants/${participant.id}`, tenantSlug)} className="surface flex items-center gap-3 rounded-xl p-4 hover:shadow-md"><span className="flex h-9 w-9 items-center justify-center rounded-full bg-[var(--color-surface-muted)] font-bold">{participant.name.slice(0, 2).toUpperCase()}</span><span className="font-semibold">{participant.name}</span></Link>)}</div>; }
function Statistics({ statistics }: { statistics: CompetitionDetail['statistics'] }) { const cards = [['Contests', statistics.contestCount, CalendarDays], ['Completed', statistics.completedContests, Trophy], ['Live', statistics.liveContests, BarChart3], ['Upcoming', statistics.upcomingContests, CalendarDays], ['Participants', statistics.participantCount, Users], ['Official results', statistics.publishedResultCount, Trophy], ['Scoring events', statistics.scoringEventCount, BarChart3]] as const; return <div className="grid grid-cols-2 gap-4 md:grid-cols-3 xl:grid-cols-4">{cards.map(([label, value, Icon]) => <Metric key={label} label={label} value={value} icon={Icon} />)}</div>; }
function Metric({ label, value, icon: Icon }: { label: string; value: number; icon: typeof Trophy }) { return <div className="surface rounded-xl p-5"><Icon className="h-5 w-5 muted" /><p className="mt-3 text-3xl font-bold">{value}</p><p className="text-sm muted">{label}</p></div>; }
function PageMessage({ children }: { children: React.ReactNode }) { return <div className="app-container py-16"><div role="status" className="rounded-xl border border-dashed border-[var(--color-border)] bg-[var(--color-surface-muted)] p-8 text-center muted">{children}</div></div>; }
function loadFavouriteKeys() {
  if (typeof window === 'undefined') return new Set<string>();
  try {
    const values = JSON.parse(localStorage.getItem('sports-platform:favourite-contests') ?? '[]') as string[];
    return new Set(values.map(value => value.includes(':') ? value : `contest:${value}`));
  } catch { return new Set<string>(); }
}
function readFavourite(id: string) { return loadFavouriteKeys().has(`competition:${id}`); }
function formatDate(value?: string | null) { return value ? new Date(value).toLocaleString([], { dateStyle: 'medium', timeStyle: 'short' }) : 'TBC'; }
