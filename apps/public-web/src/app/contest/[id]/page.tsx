'use client';

import * as signalR from '@microsoft/signalr';
import Link from 'next/link';
import { ArrowLeft, Calendar, MapPin, Radio } from 'lucide-react';
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
type ContestDetails = {
  id: string;
  name?: string | null;
  sportId: string;
  contestShape: string;
  renderer: 'head-to-head' | 'multi-participant-leaderboard' | 'race-heat' | 'fight-card-bout' | 'cricket-scorecard';
  status: string;
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
  const [tenantSlug, setTenantSlug] = useState('premier-league');
  const [error, setError] = useState<string | null>(null);
  const [liveState, setLiveState] = useState<'connecting' | 'live' | 'reconnecting' | 'offline'>('connecting');

  const loadContest = useCallback(async (slug: string) => {
    const detail = await publicApi<ContestDetails>(`/contests/${id}`, { cache: 'no-store' }, slug);
    setContest(detail);
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
    });
    connection.on('ContestStatusChanged', (change: { status: string }) => {
      setContest(current => current ? { ...current, status: change.status } : current);
    });
    connection.on('ParticipantStatusChanged', (change: { participantId: string; newStatus: string }) => {
      setContest(current => current ? {
        ...current,
        participants: current.participants.map(item => item.participantId === change.participantId
          ? { ...item, status: change.newStatus }
          : item),
      } : current);
    });
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
    <div className="container mx-auto max-w-5xl px-4 py-10">
      <Link href={tenantPath('/', tenantSlug)} className="mb-6 inline-flex items-center gap-2 text-sm font-medium text-primary hover:underline">
        <ArrowLeft className="h-4 w-4" /> Back to live coverage
      </Link>
      <header className="rounded-2xl bg-gray-950 p-7 text-white">
        <div className="mb-5 flex flex-wrap items-center justify-between gap-3">
          <span className="rounded-full bg-white/10 px-3 py-1 text-sm">{contest.contestShape}</span>
          <div className="flex gap-2">
            <span data-testid="contest-live-state" className="inline-flex items-center gap-2 rounded-full bg-white/10 px-3 py-1 text-sm">
              <Radio className="h-3.5 w-3.5" /> {liveState}
            </span>
            <span className="rounded-full bg-red-500/20 px-3 py-1 text-sm font-semibold text-red-200">{contest.status}</span>
          </div>
        </div>
        <h1 className="text-3xl font-bold">{contest.name || 'Published contest'}</h1>
        <div className="mt-4 flex flex-wrap gap-5 text-sm text-gray-300">
          <span className="flex items-center gap-2"><Calendar className="h-4 w-4" />{contest.startTime ? new Date(contest.startTime).toLocaleString() : 'Time to be confirmed'}</span>
          {contest.venue && <span className="flex items-center gap-2"><MapPin className="h-4 w-4" /> {contest.venue}</span>}
        </div>
      </header>

      <main data-testid={`contest-renderer-${contest.renderer}`} className="py-8">
        <ContestRenderer contest={contest} tenantSlug={tenantSlug} />
      </main>

      <section className="grid gap-8 border-t border-gray-200 pt-8 lg:grid-cols-[1fr_280px]">
        <Timeline contest={contest} />
        <Segments contest={contest} />
      </section>
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
  return result?.scoreDisplay ?? result?.time ?? result?.score?.toString()
    ?? totals(contest).get(participantId)?.toString() ?? '—';
}

function HeadToHead({ contest, tenantSlug }: { contest: ContestDetails; tenantSlug: string }) {
  return <section className="grid items-center gap-4 md:grid-cols-[1fr_auto_1fr]">
    {contest.participants.length === 0 && <State message="Participants will appear when the lineup is published." compact />}
    {contest.participants.map((participant, index) => <div key={participant.participantId} className="contents">
      {index === 1 && <div className="text-center text-sm font-bold text-gray-400">VS</div>}
      <Link href={tenantPath(`/participants/${participant.participantId}`, tenantSlug)} className="rounded-2xl border bg-white p-7 text-center shadow-sm">
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

function Timeline({ contest }: { contest: ContestDetails }) {
  const live = [...contest.liveScores].reverse();
  return <div><h2 className="mb-4 text-2xl font-bold">Event timeline</h2>{live.length === 0 && contest.events.length === 0 ? <State message="No public events have been recorded." compact /> : <div className="space-y-3">{live.map(event => <div key={event.eventId} className="rounded-lg border p-4"><div className="flex justify-between gap-4"><strong>{event.eventType ?? event.metricName}</strong><time className="text-sm text-gray-500">{new Date(event.scoredAt).toLocaleTimeString()}</time></div><p className="mt-1 text-sm text-gray-600">{event.displayValue ?? `${event.metricName}: ${event.value}`}</p></div>)}{live.length === 0 && contest.events.map(event => <div key={event.id} className="rounded-lg border p-4"><strong>{event.eventType}</strong>{event.metricValue && <p className="text-sm text-gray-600">{event.metricValue}</p>}</div>)}</div>}</div>;
}

function Segments({ contest }: { contest: ContestDetails }) {
  const current = contest.session?.activeSegmentId;
  return <aside><h2 className="mb-4 text-lg font-bold">Segments</h2>{contest.segments.length === 0 ? <State message="No segments configured." compact /> : <div className="space-y-2">{contest.segments.map(segment => <div key={segment.id} className={`rounded-lg p-3 ${segment.id === current ? 'bg-primary/10 ring-1 ring-primary' : 'bg-gray-50'}`}><p className="font-medium">{segment.name}</p><p className="text-xs text-gray-500">{segment.id === current ? 'Current' : segment.status}</p></div>)}</div>}</aside>;
}

function State({ message, compact = false }: { message: string; compact?: boolean }) {
  return <div className={`w-full px-4 text-center text-gray-500 ${compact ? 'py-8' : 'py-24'}`}>{message}</div>;
}
