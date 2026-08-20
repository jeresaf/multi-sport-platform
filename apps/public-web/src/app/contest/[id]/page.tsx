'use client';

import * as signalR from '@microsoft/signalr';
import Link from 'next/link';
import { Activity, ArrowLeft, Calendar, Clock3, MapPin, Radio, Trophy } from 'lucide-react';
import { use, useCallback, useEffect, useMemo, useState } from 'react';
import {
  PUBLIC_CONTEST_HUB_URL,
  publicApi,
  resolveTenantSlug,
  tenantPath,
} from '@/lib/public-api';

type Participant = {
  participantId: string;
  name: string;
  participantType: string;
  status: string;
  score?: number | null;
  scoreDisplay?: string | null;
};
type LiveScore = {
  eventId: string;
  participantId: string;
  secondaryParticipantId?: string | null;
  segmentId?: string | null;
  eventType?: string;
  metricName: string;
  value: number;
  displayValue?: string | null;
  clockSeconds?: number | null;
  scoredAt: string;
};
type TimelineEvent = {
  id: string;
  sequence: number;
  category: string;
  type: string;
  sportCode: string;
  sportVariant?: string | null;
  period?: string | null;
  clock?: string | null;
  primaryParticipantId?: string | null;
  primaryParticipantName?: string | null;
  secondaryParticipantId?: string | null;
  secondaryParticipantName?: string | null;
  primaryPlayerId?: string | null;
  primaryPlayerName?: string | null;
  secondaryPlayerId?: string | null;
  secondaryPlayerName?: string | null;
  value?: number | null;
  displayValue?: string | null;
  metricName?: string | null;
  status: string;
  importance: string;
  homeScoreAfter: number;
  awayScoreAfter: number;
  shootoutHomeScoreAfter: number;
  shootoutAwayScoreAfter: number;
  occurredAt: string;
};
type TimelineResponse = {
  contestId: string;
  timelineVersion: number;
  events: TimelineEvent[];
  nextSequence?: number | null;
};
type TimelineEnvelope = {
  type: string;
  contestId: string;
  timelineVersion: number;
  event: TimelineEvent;
};
type ContestDetails = {
  id: string;
  name?: string | null;
  sportId: string;
  contestShape: string;
  renderer: 'head-to-head' | 'multi-participant-leaderboard' | 'race-heat' | 'fight-card-bout' | 'cricket-scorecard';
  status: string;
  contestStatus?: string;
  venue?: string | null;
  startTime?: string | null;
  winnerId?: string | null;
  configuration: {
    parPerRound?: number | null;
    courseName?: string | null;
    courseLocation?: string | null;
    weightClass?: string | null;
    cardSection?: string | null;
    scheduledRounds?: number | null;
    resultMethod?: string | null;
    resultRound?: number | null;
    resultTimeSeconds?: number | null;
    athleticsEventName?: string | null;
    distanceMeters?: number | null;
    athleticsMetricType?: string | null;
    athleticsMaxAttempts?: number | null;
  };
  session?: { status: string; activeSegmentId?: string | null } | null;
  participants: Participant[];
  segments: Array<{ id: string; name: string; segmentType: string; status: string }>;
  events: Array<{
    id: string;
    eventType: string;
    participantId: string;
    metricValue?: string | null;
    createdAt: string;
  }>;
  results: Array<{
    participantId: string;
    rank?: number | null;
    score?: number | null;
    scoreDisplay?: string | null;
    time?: string | null;
  }>;
  liveScores: LiveScore[];
  statistics?: {
    totalEvents: number;
    liveUpdates: number;
    eventBreakdown: Array<{
      label: string;
      total: number;
      participants: Array<{ participantId: string; count: number }>;
    }>;
  };
};
type Snapshot = {
  contestId: string;
  status: string;
  recentScores: LiveScore[];
  participantStatuses: Array<{ participantId: string; newStatus: string }>;
  currentSegmentId?: string | null;
};

export default function ContestPage({ params }: { params: Promise<{ id: string }> }) {
  const { id } = use(params);
  const [contest, setContest] = useState<ContestDetails | null>(null);
  const [timeline, setTimeline] = useState<TimelineResponse | null>(null);
  const [tenantSlug, setTenantSlug] = useState('premier-league');
  const [error, setError] = useState<string | null>(null);
  const [liveState, setLiveState] = useState<'connecting' | 'live' | 'reconnecting' | 'offline'>('connecting');
  const [activeTab, setActiveTab] = useState<'overview' | 'timeline' | 'statistics' | 'details'>('overview');

  const loadContest = useCallback(async (slug: string) => {
    const [detail, eventTimeline] = await Promise.all([
      publicApi<ContestDetails>(`/contests/${id}`, { cache: 'no-store' }, slug),
      publicApi<TimelineResponse>(`/contests/${id}/timeline?mode=all&limit=200`, { cache: 'no-store' }, slug),
    ]);
    setContest({ ...detail, status: detail.contestStatus ?? detail.status });
    setTimeline(eventTimeline);
    setError(null);
  }, [id]);

  useEffect(() => {
    const slug = resolveTenantSlug();
    let disposed = false;
    queueMicrotask(() => !disposed && setTenantSlug(slug));
    const connection = new signalR.HubConnectionBuilder()
      .withUrl(PUBLIC_CONTEST_HUB_URL, { withCredentials: false })
      .withAutomaticReconnect([0, 2_000, 5_000, 10_000])
      .configureLogging(signalR.LogLevel.Warning)
      .build();

    const join = () => connection.invoke('JoinPublicContestGroup', slug, id);
    const refresh = () => void loadContest(slug).catch(() => undefined);
    const applyTimelineEvent = (message: TimelineEnvelope) => {
      setTimeline(current => {
        if (current && message.timelineVersion < current.timelineVersion) return current;
        if (current && message.timelineVersion > current.timelineVersion + 1) {
          refresh();
          return current;
        }
        const events = [...(current?.events ?? []).filter(item => item.id !== message.event.id), message.event]
          .sort((left, right) => left.sequence - right.sequence);
        return {
          contestId: message.contestId,
          timelineVersion: message.timelineVersion,
          events,
          nextSequence: current?.nextSequence,
        };
      });
    };
    connection.on('ContestSnapshot', (snapshot: Snapshot) => {
      setContest(current => current ? {
        ...current,
        status: snapshot.status,
        liveScores: snapshot.recentScores,
        session: current.session
          ? { ...current.session, activeSegmentId: snapshot.currentSegmentId }
          : null,
        participants: current.participants.map(participant => ({
          ...participant,
          status: snapshot.participantStatuses.find(item =>
            item.participantId === participant.participantId)?.newStatus ?? participant.status,
        })),
      } : current);
    });
    connection.on('LiveScoreUpdated', (score: LiveScore) => {
      setContest(current => current ? {
        ...current,
        liveScores: [...current.liveScores.filter(item => item.eventId !== score.eventId), score],
      } : current);
      refresh();
    });
    connection.on('ContestStatusChanged', (change: { status: string; contestStatus?: string }) => {
      setContest(current => current ? { ...current, status: change.contestStatus ?? change.status } : current);
    });
    connection.on('ParticipantStatusChanged', (change: { participantId: string; newStatus: string }) => {
      setContest(current => current ? {
        ...current,
        participants: current.participants.map(item => item.participantId === change.participantId
          ? { ...item, status: change.newStatus }
          : item),
      } : current);
    });
    for (const eventName of [
      'contest.timeline.event.created',
      'contest.timeline.event.updated',
      'contest.timeline.event.corrected',
      'contest.timeline.event.reversed',
    ]) connection.on(eventName, applyTimelineEvent);
    connection.on('ContestResultFinalized', refresh);
    connection.on('SegmentStarted', refresh);
    connection.on('SegmentEnded', refresh);
    connection.onreconnecting(() => setLiveState('reconnecting'));
    connection.onreconnected(async () => {
      if (disposed) return;
      try {
        await join();
        setLiveState('live');
      } catch {
        setLiveState('offline');
      }
    });
    connection.onclose(() => !disposed && setLiveState('offline'));

    queueMicrotask(() => {
      if (!disposed) loadContest(slug).catch((reason: Error) => setError(reason.message));
    });
    connection.start()
      .then(join)
      .then(() => !disposed && setLiveState('live'))
      .catch(() => !disposed && setLiveState('offline'));

    return () => {
      disposed = true;
      void connection.stop();
    };
  }, [id, loadContest]);

  if (error) return <State message={`Contest unavailable: ${error}`} />;
  if (!contest) return <State message="Loading contest…" />;

  return (
    <div className="container mx-auto max-w-6xl px-4 py-6 sm:py-10">
      <Link href={tenantPath('/', tenantSlug)} className="mb-5 inline-flex items-center gap-2 rounded-xl border border-gray-200 bg-white px-3 py-2 text-sm font-semibold text-gray-700 shadow-sm hover:bg-gray-50">
        <ArrowLeft className="h-4 w-4" /> Back to match centre
      </Link>
      <header className="overflow-hidden rounded-[28px] border border-gray-200 bg-gradient-to-br from-slate-950 via-slate-900 to-slate-800 p-6 text-white shadow-sm sm:p-8">
        <div className="mb-5 flex flex-wrap items-center justify-between gap-3">
          <span className="rounded-full bg-white/10 px-3 py-1 text-sm">{contest.contestShape}</span>
          <div className="flex gap-2">
            <span data-testid="contest-live-state" className="inline-flex items-center gap-2 rounded-full border border-white/10 bg-white/10 px-3 py-1 text-sm font-semibold">
              <Radio className="h-3.5 w-3.5" /> {liveState}
            </span>
            <span className="rounded-full border border-white/10 bg-rose-400/15 px-3 py-1 text-sm font-semibold text-rose-200">{contest.status}</span>
          </div>
        </div>
        <h1 className="text-3xl font-black tracking-tight sm:text-5xl">{contest.name || 'Published contest'}</h1>
        <div className="mt-4 flex flex-wrap gap-x-5 gap-y-2 text-sm text-white/70">
          <span className="flex items-center gap-2"><Calendar className="h-4 w-4" />{contest.startTime ? new Date(contest.startTime).toLocaleString() : 'Time to be confirmed'}</span>
          {contest.venue && <span className="flex items-center gap-2"><MapPin className="h-4 w-4" /> {contest.venue}</span>}
        </div>
      </header>

      <section data-testid="contest-summary-strip" className="mt-4 grid gap-px overflow-hidden rounded-2xl border border-gray-200 bg-gray-200 sm:grid-cols-2 lg:grid-cols-4">
        <ContestMetric icon={<Trophy className="h-4 w-4" />} label="Participants" value={contest.participants.length} />
        <ContestMetric icon={<Activity className="h-4 w-4" />} label="Timeline events" value={timeline?.events.length ?? 0} />
        <ContestMetric icon={<Clock3 className="h-4 w-4" />} label="Segments" value={contest.segments.length} />
        <ContestMetric icon={<Radio className="h-4 w-4" />} label="Connection" value={liveState} />
      </section>

      <nav className="mt-6 flex max-w-full gap-1 overflow-x-auto rounded-2xl border border-gray-200 bg-white p-1 shadow-sm" aria-label="Contest sections">
        {(['overview', 'timeline', 'statistics', 'details'] as const).map(tab => <button
          key={tab}
          type="button"
          onClick={() => setActiveTab(tab)}
          aria-current={activeTab === tab ? 'page' : undefined}
          className={`shrink-0 rounded-xl px-4 py-2.5 text-sm font-semibold capitalize transition ${activeTab === tab ? 'bg-primary text-white shadow-sm' : 'text-gray-500 hover:bg-gray-50 hover:text-gray-900'}`}
        >{tab}</button>)}
      </nav>
      <main data-testid={`contest-panel-${activeTab}`} className="min-w-0 py-8">
        {activeTab === 'overview' && <div data-testid={`contest-renderer-${contest.renderer}`}><ContestRenderer contest={contest} tenantSlug={tenantSlug} /></div>}
        {activeTab === 'timeline' && <Timeline events={timeline?.events ?? []} />}
        {activeTab === 'statistics' && <Statistics contest={contest} />}
        {activeTab === 'details' && <Segments contest={contest} />}
      </main>
    </div>
  );
}

function ContestRenderer({ contest, tenantSlug }: { contest: ContestDetails; tenantSlug: string }) {
  if (contest.renderer === 'head-to-head') return <HeadToHead contest={contest} tenantSlug={tenantSlug} />;
  if (contest.renderer === 'race-heat') return <RaceHeat contest={contest} />;
  if (contest.renderer === 'fight-card-bout') return <FightCardBout contest={contest} />;
  if (contest.renderer === 'cricket-scorecard') return <CricketScorecard contest={contest} />;
  return <Leaderboard contest={contest} />;
}

function totals(contest: ContestDetails) {
  const result = new Map<string, number>();
  for (const score of contest.liveScores) {
    result.set(score.participantId, (result.get(score.participantId) ?? 0) + score.value);
  }
  return result;
}

function scoreText(contest: ContestDetails, participantId: string) {
  const result = contest.results.find(item => item.participantId === participantId);
  const participant = contest.participants.find(item => item.participantId === participantId);
  return result?.scoreDisplay ?? result?.time ?? result?.score?.toString()
    ?? participant?.scoreDisplay ?? participant?.score?.toString()
    ?? totals(contest).get(participantId)?.toString() ?? '—';
}

function HeadToHead({ contest, tenantSlug }: { contest: ContestDetails; tenantSlug: string }) {
  return <section className="grid items-center gap-4 md:grid-cols-[1fr_auto_1fr]">
    {contest.participants.length === 0 && <State message="Participants will appear when the lineup is published." compact />}
    {contest.participants.map((participant, index) => <div key={participant.participantId} className="contents">
      {index === 1 && <div className="text-center text-sm font-bold text-gray-400">VS</div>}
      <Link href={tenantPath(`/participants/${participant.participantId}`, tenantSlug)} className="rounded-2xl border border-gray-200 bg-white p-7 text-center shadow-sm transition hover:-translate-y-0.5 hover:shadow-md">
        <p className="text-sm text-gray-500">{participant.participantType} · {participant.status}</p>
        <h2 className="mt-2 text-xl font-bold">{participant.name}</h2>
        <p className="mt-5 text-4xl font-black text-primary">{scoreText(contest, participant.participantId)}</p>
      </Link>
    </div>)}
  </section>;
}

function Leaderboard({ contest }: { contest: ContestDetails }) {
  const scoreTotals = totals(contest);
  const rows = [...contest.participants].sort((a, b) => {
    const aRank = contest.results.find(item => item.participantId === a.participantId)?.rank;
    const bRank = contest.results.find(item => item.participantId === b.participantId)?.rank;
    return (aRank ?? 999) - (bRank ?? 999) || (scoreTotals.get(b.participantId) ?? 0) - (scoreTotals.get(a.participantId) ?? 0);
  });
  return <section>
    <h2 className="text-2xl font-bold">{contest.configuration.courseName ?? 'Leaderboard'}</h2>
    {contest.configuration.courseLocation && <p className="mt-1 text-gray-500">{contest.configuration.courseLocation} · Par {contest.configuration.parPerRound}</p>}
    {rows.length === 0 ? <State message="Leaderboard entries will appear when participants are published." compact /> : <div className="mt-5 overflow-x-auto"><table className="w-full text-left"><thead><tr className="border-b text-sm text-gray-500"><th className="p-3">Pos</th><th className="p-3">Participant</th><th className="p-3 text-right">Score</th></tr></thead><tbody>{rows.map((participant, index) => <tr key={participant.participantId} className="border-b"><td className="p-3 font-bold">{contest.results.find(item => item.participantId === participant.participantId)?.rank ?? index + 1}</td><td className="p-3">{participant.name}</td><td className="p-3 text-right font-bold">{scoreText(contest, participant.participantId)}</td></tr>)}</tbody></table></div>}
  </section>;
}

function RaceHeat({ contest }: { contest: ContestDetails }) {
  const rows = [...contest.participants].sort((a, b) => (contest.results.find(item => item.participantId === a.participantId)?.rank ?? 999) - (contest.results.find(item => item.participantId === b.participantId)?.rank ?? 999));
  return <section><p className="text-sm font-semibold uppercase tracking-wide text-primary">{contest.configuration.athleticsEventName ?? contest.contestShape}{contest.configuration.distanceMeters ? ` · ${contest.configuration.distanceMeters}m` : ''}</p><h2 className="mt-1 text-2xl font-bold">Race results</h2>{rows.length === 0 ? <State message="Lane assignments will appear when the heat is published." compact /> : <div className="mt-5 space-y-2">{rows.map((participant, index) => <div key={participant.participantId} className="grid grid-cols-[52px_1fr_auto] items-center rounded-xl border bg-white p-4"><strong>{contest.results.find(item => item.participantId === participant.participantId)?.rank ?? index + 1}</strong><span>{participant.name}</span><strong>{scoreText(contest, participant.participantId)}</strong></div>)}</div>}</section>;
}

function FightCardBout({ contest }: { contest: ContestDetails }) {
  const detail = [contest.configuration.weightClass, contest.configuration.cardSection, contest.configuration.scheduledRounds ? `${contest.configuration.scheduledRounds} rounds` : null].filter(Boolean).join(' · ');
  return <section><p className="text-center text-sm font-semibold uppercase tracking-wide text-primary">{detail || 'Fight card bout'}</p><div className="mt-5 grid gap-4 md:grid-cols-2">{contest.participants.map(participant => <div key={participant.participantId} className={`rounded-2xl border p-7 text-center ${contest.winnerId === participant.participantId ? 'border-primary bg-primary/5' : 'bg-white'}`}><h2 className="text-xl font-bold">{participant.name}</h2><p className="mt-4 text-3xl font-black">{scoreText(contest, participant.participantId)}</p>{contest.winnerId === participant.participantId && <p className="mt-2 font-semibold text-primary">Winner</p>}</div>)}</div>{contest.configuration.resultMethod && <p className="mt-5 text-center text-lg font-semibold">{contest.configuration.resultMethod}{contest.configuration.resultRound ? ` · Round ${contest.configuration.resultRound}` : ''}</p>}</section>;
}

function CricketScorecard({ contest }: { contest: ContestDetails }) {
  const scoreTotals = totals(contest);
  return <section><h2 className="text-2xl font-bold">Scorecard</h2><div className="mt-5 grid gap-4 md:grid-cols-2">{contest.participants.map(participant => <div key={participant.participantId} className="rounded-xl border bg-white p-6"><p className="text-sm text-gray-500">{participant.status}</p><h3 className="mt-1 text-lg font-bold">{participant.name}</h3><p className="mt-3 text-3xl font-black text-primary">{scoreText(contest, participant.participantId)}</p><p className="text-xs text-gray-500">Live total {scoreTotals.get(participant.participantId) ?? 0}</p></div>)}</div>{contest.participants.length === 0 && <State message="The scorecard will appear after the toss and lineup are published." compact />}</section>;
}

function Timeline({ events }: { events: TimelineEvent[] }) {
  const rows = [...events].reverse();
  return <div><h2 className="mb-4 text-2xl font-bold">Event timeline</h2>{rows.length === 0 ? <State message="No public events have been recorded." compact /> : <div className="space-y-3">{rows.map(event => event.sportCode === 'FOOTBALL' ? <FootballTimelineEvent key={event.id} event={event} /> : <GenericTimelineEvent key={event.id} event={event} />)}</div>}</div>;
}

function GenericTimelineEvent({ event }: { event: TimelineEvent }) {
  return <div className="rounded-2xl border border-gray-200 bg-white p-4 shadow-sm"><div className="flex justify-between gap-4"><strong>{label(event.type)}</strong><span className="text-sm text-gray-500">{event.clock ?? new Date(event.occurredAt).toLocaleTimeString()}</span></div><p className="mt-1 text-sm text-gray-600">{event.displayValue ?? (event.metricName && event.value != null ? `${label(event.metricName)}: ${event.value}` : label(event.category))}</p><div className="mt-2 flex gap-2 text-xs font-semibold text-gray-500"><span>{label(event.category)}</span>{event.status !== 'CONFIRMED' && <span>· {label(event.status)}</span>}</div></div>;
}

function FootballTimelineEvent({ event }: { event: TimelineEvent }) {
  const minute = event.clock ? `${Number(event.clock.split(':')[0])}'` : null;
  const actor = event.primaryPlayerName ?? event.secondaryParticipantName ?? event.primaryParticipantName;
  const isShootout = event.type.startsWith('PENALTY_SHOOTOUT_');
  const score = event.category === 'SCORING' && event.type !== 'PENALTY_MISSED'
    ? isShootout
      ? `${event.shootoutHomeScoreAfter}–${event.shootoutAwayScoreAfter} pens`
      : `${event.homeScoreAfter}–${event.awayScoreAfter}`
    : null;
  const symbol = event.type.includes('GOAL') || event.type === 'PENALTY_SHOOTOUT_SCORE'
    ? '⚽'
    : event.type.includes('CARD')
      ? event.type === 'YELLOW_CARD' ? '🟨' : '🟥'
      : event.type.includes('SUBSTITUTION') || event.type.startsWith('PLAYER_')
        ? '↔'
        : event.type.startsWith('VAR_')
          ? 'VAR'
          : '•';
  return <article className={`rounded-2xl border border-gray-200 bg-white p-4 shadow-sm transition ${event.status === 'REVERSED' ? 'opacity-60' : 'hover:border-gray-300 hover:shadow-md'}`}>
    <div className="grid grid-cols-[3rem_2.25rem_minmax(0,1fr)_auto] items-start gap-2">
      <time className="font-bold text-gray-500">{minute ?? '—'}</time>
      <span aria-hidden className="text-center font-bold">{symbol}</span>
      <div className="min-w-0"><strong>{label(event.type)}</strong>{actor && <p className="truncate text-sm text-gray-600">{actor}</p>}{event.type === 'SUBSTITUTION' && event.secondaryPlayerName && <p className="text-xs text-gray-500">{event.secondaryPlayerName} on</p>}{event.type === 'ASSIST' && actor && <p className="text-xs text-gray-500">Assist</p>}</div>
      {score && <strong className="whitespace-nowrap text-primary">{score}</strong>}
    </div>
    {(event.status !== 'CONFIRMED' || event.type.startsWith('VAR_')) && <div className="mt-2 flex gap-2 pl-20 text-xs font-semibold"><span className="rounded-full bg-gray-100 px-2 py-1">{label(event.status)}</span>{event.type.startsWith('VAR_') && <span className="rounded-full bg-blue-50 px-2 py-1 text-blue-700">Video review</span>}</div>}
  </article>;
}

function label(value: string) {
  return value.replaceAll('_', ' ').toLowerCase().replace(/\b\w/g, letter => letter.toUpperCase());
}

function Segments({ contest }: { contest: ContestDetails }) {
  const current = contest.session?.activeSegmentId;
  return <aside><h2 className="mb-4 text-lg font-bold">Segments</h2>{contest.segments.length === 0 ? <State message="No segments configured." compact /> : <div className="space-y-2">{contest.segments.map(segment => <div key={segment.id} className={`rounded-lg p-3 ${segment.id === current ? 'bg-primary/10 ring-1 ring-primary' : 'bg-gray-50'}`}><p className="font-medium">{segment.name}</p><p className="text-xs text-gray-500">{segment.id === current ? 'Current' : segment.status}</p></div>)}</div>}</aside>;
}

function Statistics({ contest }: { contest: ContestDetails }) {
  const breakdown = contest.statistics?.eventBreakdown ?? [];
  if (breakdown.length === 0) return <State message="Statistics will appear when match events are recorded." compact />;
  const participants = contest.participants.slice(0, 2);
  const count = (row: (typeof breakdown)[number], id?: string) =>
    id ? row.participants.find(item => item.participantId === id)?.count ?? 0 : 0;
  return <section className="surface overflow-hidden rounded-2xl border border-gray-200 shadow-sm">
    <div className="grid grid-cols-[minmax(0,1fr)_minmax(6rem,2fr)_minmax(0,1fr)] gap-2 bg-gray-950 p-3 text-center text-sm font-semibold text-white">
      <span className="truncate">{participants[0]?.name ?? 'Home'}</span><span>Event</span><span className="truncate">{participants[1]?.name ?? 'Away'}</span>
    </div>
    {breakdown.map(row => <div key={row.label} className="grid grid-cols-[minmax(0,1fr)_minmax(6rem,2fr)_minmax(0,1fr)] gap-2 border-t border-gray-200 p-3 text-center">
      <strong>{count(row, participants[0]?.participantId)}</strong><span className="capitalize text-gray-600">{row.label.replaceAll('_', ' ')}</span><strong>{count(row, participants[1]?.participantId)}</strong>
    </div>)}
  </section>;
}

function ContestMetric({
  icon,
  label,
  value,
}: {
  icon: React.ReactNode;
  label: string;
  value: number | string;
}) {
  return (
    <div className="bg-white px-5 py-4">
      <div className="flex items-center justify-between gap-3">
        <span className="flex h-9 w-9 items-center justify-center rounded-xl bg-gray-100 text-primary">{icon}</span>
        <strong className="truncate text-lg font-black capitalize text-gray-950">{value}</strong>
      </div>
      <p className="mt-2 text-xs font-bold uppercase tracking-wider text-gray-400">{label}</p>
    </div>
  );
}

function State({ message, compact = false }: { message: string; compact?: boolean }) {
  return <div className={`w-full px-4 text-center text-gray-500 ${compact ? 'py-8' : 'py-24'}`}>{message}</div>;
}
