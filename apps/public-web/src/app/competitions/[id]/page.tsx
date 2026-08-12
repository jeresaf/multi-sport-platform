'use client';

import Link from 'next/link';
import { useParams } from 'next/navigation';
import { BarChart3, CalendarDays, Trophy, Users } from 'lucide-react';
import { useEffect, useState } from 'react';
import {
  formatSport,
  publicApi,
  resolveTenantSlug,
  TENANT_SLUG,
  tenantPath,
} from '@/lib/public-api';

type Participant = { id: string; name: string };
type Fixture = {
  id: string;
  name?: string | null;
  contestShape: string;
  status: string;
  startTime?: string | null;
  venue?: string | null;
  participants: Array<{ participantId: string; name: string }>;
};
type Result = {
  id: string;
  contestId: string;
  contestName: string;
  participantId: string;
  participantName: string;
  rank?: number | null;
  position?: string | null;
  score?: number | null;
  scoreDisplay?: string | null;
  time?: string | null;
  status: string;
};
type Standing = {
  id: string;
  groupId: string;
  groupName?: string | null;
  participantId: string;
  participantName: string;
  position: number;
  played: number;
  won: number;
  drawn: number;
  lost: number;
  pointsFor: number;
  pointsAgainst: number;
  pointDifferential: number;
  points: number;
};
type CompetitionDetail = {
  id: string;
  name: string;
  description?: string | null;
  sportId: string;
  status: string;
  publishedAt?: string | null;
  ruleset?: { id: string; name: string; versionNumber: number } | null;
  labels: Record<string, string>;
  availableTabs: string[];
  editions: Array<{
    id: string;
    name: string;
    startDate?: string | null;
    endDate?: string | null;
    status: string;
    stages: Array<{ id: string; name: string; stageType: string; sortOrder: number }>;
  }>;
  fixtures: Fixture[];
  results: Result[];
  standings: Standing[];
  bracket?: {
    id: string;
    name: string;
    status: string;
    roundCount: number;
    championId?: string | null;
    championName?: string | null;
  } | null;
  leaderboard?: Array<{
    participantId: string;
    participantName: string;
    rank?: number | null;
    score?: number | null;
    scoreDisplay?: string | null;
  }> | null;
  participants: Participant[];
  statistics: {
    contestCount: number;
    completedContests: number;
    liveContests: number;
    upcomingContests: number;
    participantCount: number;
    publishedResultCount: number;
    scoringEventCount: number;
  };
};

export default function CompetitionDetailPage() {
  const params = useParams<{ id: string }>();
  const [competition, setCompetition] = useState<CompetitionDetail | null>(null);
  const [activeTab, setActiveTab] = useState('overview');
  const [tenantSlug, setTenantSlug] = useState(TENANT_SLUG);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    const resolvedTenant = resolveTenantSlug();
    publicApi<CompetitionDetail>(
      `/competitions/${encodeURIComponent(params.id)}`,
      undefined,
      resolvedTenant,
    )
      .then(detail => {
        setTenantSlug(resolvedTenant);
        setCompetition(detail);
        setActiveTab(detail.availableTabs[0] ?? 'overview');
      })
      .catch((reason: Error) => setError(reason.message));
  }, [params.id]);

  if (error) return <PageMessage>Competition unavailable: {error}</PageMessage>;
  if (!competition) return <PageMessage>Loading competition…</PageMessage>;

  return (
    <div className="container mx-auto max-w-7xl px-4 py-8 sm:py-10">
      <Link href={tenantPath('/competitions', tenantSlug)} className="text-sm font-medium text-gray-600 hover:underline">
        ← All competitions
      </Link>
      <header className="mt-5 rounded-3xl bg-gray-950 p-6 text-white sm:p-9">
        <div className="flex flex-wrap items-start justify-between gap-4">
          <div>
            <p className="text-sm font-semibold uppercase tracking-wider text-cyan-300">
              {formatSport(competition.sportId)}
            </p>
            <h1 className="mt-2 text-3xl font-bold sm:text-4xl">{competition.name}</h1>
            {competition.description && <p className="mt-3 max-w-3xl text-gray-300">{competition.description}</p>}
          </div>
          <span className="rounded-full bg-white/10 px-3 py-1 text-sm font-semibold">{competition.status}</span>
        </div>
      </header>

      <nav className="mt-6 flex gap-2 overflow-x-auto border-b border-gray-200 pb-px" aria-label="Competition sections">
        {competition.availableTabs.map(tab => (
          <button
            key={tab}
            type="button"
            data-testid={`competition-tab-${tab}`}
            onClick={() => setActiveTab(tab)}
            className={`shrink-0 border-b-2 px-4 py-3 text-sm font-semibold ${
              activeTab === tab
                ? 'border-[var(--brand-primary)] text-gray-950'
                : 'border-transparent text-gray-500 hover:text-gray-900'
            }`}
          >
            {competition.labels[tab] ?? tab}
          </button>
        ))}
      </nav>

      <main className="py-7" data-testid={`competition-panel-${activeTab}`}>
        {activeTab === 'overview' && <Overview competition={competition} />}
        {activeTab === 'fixtures' && <Fixtures fixtures={competition.fixtures} tenantSlug={tenantSlug} label={competition.labels.fixtures} />}
        {activeTab === 'results' && <Results results={competition.results} tenantSlug={tenantSlug} />}
        {activeTab === 'standings' && <Standings rows={competition.standings} />}
        {activeTab === 'bracket' && <Bracket competition={competition} tenantSlug={tenantSlug} />}
        {activeTab === 'leaderboard' && <Leaderboard rows={competition.leaderboard ?? []} />}
        {activeTab === 'participants' && <Participants participants={competition.participants} tenantSlug={tenantSlug} label={competition.labels.participants} />}
        {activeTab === 'statistics' && <Statistics statistics={competition.statistics} />}
      </main>
    </div>
  );
}

function Overview({ competition }: { competition: CompetitionDetail }) {
  return (
    <div className="grid gap-5 lg:grid-cols-[2fr_1fr]">
      <section className="rounded-2xl border bg-white p-6">
        <h2 className="text-xl font-bold text-gray-900">Competition structure</h2>
        {competition.editions.length === 0 ? <Empty message="No editions have been published." /> : (
          <div className="mt-4 space-y-4">
            {competition.editions.map(edition => (
              <article key={edition.id} className="rounded-xl bg-gray-50 p-4">
                <div className="flex flex-wrap justify-between gap-2">
                  <h3 className="font-bold text-gray-900">{edition.name}</h3><span className="text-sm text-gray-500">{edition.status}</span>
                </div>
                <p className="mt-1 text-sm text-gray-500">{dateRange(edition.startDate, edition.endDate)}</p>
                <div className="mt-3 flex flex-wrap gap-2">
                  {edition.stages.map(stage => <span key={stage.id} className="rounded-full bg-white px-3 py-1 text-xs text-gray-700">{stage.name}</span>)}
                </div>
              </article>
            ))}
          </div>
        )}
      </section>
      <aside className="rounded-2xl border bg-gray-50 p-6">
        <Trophy className="h-7 w-7 text-amber-500" />
        <h2 className="mt-3 font-bold text-gray-900">Official configuration</h2>
        <p className="mt-2 text-sm text-gray-600">
          {competition.ruleset
            ? `${competition.ruleset.name} · version ${competition.ruleset.versionNumber}`
            : 'Published sport defaults'}
        </p>
        <p className="mt-4 text-sm text-gray-500">Published {formatDate(competition.publishedAt)}</p>
      </aside>
    </div>
  );
}

function Fixtures({ fixtures, tenantSlug, label }: { fixtures: Fixture[]; tenantSlug: string; label: string }) {
  if (fixtures.length === 0) return <Empty message={`No ${label.toLowerCase()} are published.`} />;
  return <div className="grid gap-4 md:grid-cols-2">{fixtures.map(fixture => (
    <Link key={fixture.id} href={tenantPath(`/contest/${fixture.id}`, tenantSlug)} className="rounded-xl border bg-white p-5 hover:shadow-md">
      <div className="flex justify-between gap-3 text-xs uppercase text-gray-500"><span>{fixture.status}</span><span>{formatDate(fixture.startTime)}</span></div>
      <h2 className="mt-3 font-bold text-gray-900">{fixture.name || fixture.participants.map(item => item.name).join(' vs ') || fixture.contestShape}</h2>
      {fixture.venue && <p className="mt-2 text-sm text-gray-500">{fixture.venue}</p>}
    </Link>
  ))}</div>;
}

function Results({ results, tenantSlug }: { results: Result[]; tenantSlug: string }) {
  if (results.length === 0) return <Empty message="No official results are published." />;
  return <div className="overflow-x-auto rounded-xl border"><table className="w-full min-w-[640px] text-sm"><thead className="bg-gray-950 text-left text-white"><tr><th className="p-3">Contest</th><th className="p-3">Participant</th><th className="p-3">Position</th><th className="p-3">Result</th></tr></thead><tbody>{results.map(result => <tr key={result.id} className="border-t"><td className="p-3"><Link className="hover:underline" href={tenantPath(`/contest/${result.contestId}`, tenantSlug)}>{result.contestName}</Link></td><td className="p-3 font-semibold">{result.participantName}</td><td className="p-3">{result.position ?? result.rank ?? '—'}</td><td className="p-3 font-semibold">{result.scoreDisplay ?? result.time ?? result.score ?? 'Official'}</td></tr>)}</tbody></table></div>;
}

function Standings({ rows }: { rows: Standing[] }) {
  if (rows.length === 0) return <Empty message="No standings are published." />;
  return <div className="overflow-x-auto rounded-xl border"><table className="w-full min-w-[760px] text-sm"><thead className="bg-gray-950 text-white"><tr><th className="p-3 text-left">#</th><th className="p-3 text-left">Participant</th><th className="p-3">P</th><th className="p-3">W</th><th className="p-3">D</th><th className="p-3">L</th><th className="p-3">+/-</th><th className="p-3">Pts</th></tr></thead><tbody>{rows.map(row => <tr key={row.id} className="border-t text-center"><td className="p-3 text-left font-bold">{row.position}</td><td className="p-3 text-left font-semibold">{row.participantName}</td><td className="p-3">{row.played}</td><td className="p-3">{row.won}</td><td className="p-3">{row.drawn}</td><td className="p-3">{row.lost}</td><td className="p-3">{row.pointDifferential}</td><td className="p-3 font-bold">{row.points}</td></tr>)}</tbody></table></div>;
}

function Bracket({ competition, tenantSlug }: { competition: CompetitionDetail; tenantSlug: string }) {
  if (!competition.bracket) return <Empty message="No bracket is published." />;
  return <section className="rounded-2xl border bg-white p-6"><Trophy className="h-8 w-8 text-amber-500" /><h2 className="mt-3 text-2xl font-bold">{competition.bracket.name}</h2><p className="mt-2 text-gray-600">{competition.bracket.roundCount} rounds · {competition.bracket.status}</p><p className="mt-3 font-semibold">{competition.bracket.championName ? `Champion: ${competition.bracket.championName}` : 'Champion pending'}</p><Link href={tenantPath(`/playoffs?competitionId=${competition.id}`, tenantSlug)} className="mt-5 inline-block rounded-lg bg-gray-950 px-4 py-2 text-sm font-semibold text-white">Open full bracket</Link></section>;
}

function Leaderboard({ rows }: { rows: NonNullable<CompetitionDetail['leaderboard']> }) {
  if (rows.length === 0) return <Empty message="Leaderboard positions will appear after official scores are published." />;
  return <div className="space-y-2">{rows.map((row, index) => <div key={`${row.participantId}-${index}`} className="grid grid-cols-[3rem_1fr_auto] items-center rounded-xl border bg-white p-4"><strong>{row.rank ?? index + 1}</strong><span className="font-semibold">{row.participantName}</span><span>{row.scoreDisplay ?? row.score ?? '—'}</span></div>)}</div>;
}

function Participants({ participants, tenantSlug, label }: { participants: Participant[]; tenantSlug: string; label: string }) {
  if (participants.length === 0) return <Empty message={`No ${label.toLowerCase()} are published.`} />;
  return <div className="grid gap-3 sm:grid-cols-2 lg:grid-cols-3">{participants.map(participant => <Link key={participant.id} href={tenantPath(`/participants/${participant.id}`, tenantSlug)} className="flex items-center gap-3 rounded-xl border bg-white p-4 hover:shadow-md"><Users className="h-5 w-5 text-gray-400" /><span className="font-semibold text-gray-900">{participant.name}</span></Link>)}</div>;
}

function Statistics({ statistics }: { statistics: CompetitionDetail['statistics'] }) {
  const cards = [
    ['Contests', statistics.contestCount, CalendarDays],
    ['Completed', statistics.completedContests, Trophy],
    ['Live', statistics.liveContests, BarChart3],
    ['Upcoming', statistics.upcomingContests, CalendarDays],
    ['Participants', statistics.participantCount, Users],
    ['Official results', statistics.publishedResultCount, Trophy],
    ['Scoring events', statistics.scoringEventCount, BarChart3],
  ] as const;
  return <div className="grid gap-4 grid-cols-2 md:grid-cols-3 xl:grid-cols-4">{cards.map(([label, value, Icon]) => <div key={label} className="rounded-xl border bg-white p-5"><Icon className="h-5 w-5 text-gray-400" /><p className="mt-3 text-3xl font-bold text-gray-900">{value}</p><p className="text-sm text-gray-500">{label}</p></div>)}</div>;
}

function Empty({ message }: { message: string }) {
  return <div className="rounded-xl border border-dashed bg-gray-50 p-8 text-center text-gray-500">{message}</div>;
}

function PageMessage({ children }: { children: React.ReactNode }) {
  return <div className="container mx-auto px-4 py-16"><div role="status" className="rounded-xl border border-dashed bg-gray-50 p-8 text-center text-gray-500">{children}</div></div>;
}

function formatDate(value?: string | null) {
  return value ? new Date(value).toLocaleString() : 'To be confirmed';
}

function dateRange(start?: string | null, end?: string | null) {
  if (!start && !end) return 'Dates to be confirmed';
  return `${start ? new Date(start).toLocaleDateString() : 'TBC'} – ${end ? new Date(end).toLocaleDateString() : 'TBC'}`;
}
