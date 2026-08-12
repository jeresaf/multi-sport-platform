'use client';

import { useEffect, useState } from 'react';
import { PublicCompetition, publicApi } from '@/lib/public-api';

type GolfEntry = {
  position: number;
  isTied: boolean;
  participantId: string;
  name: string;
  madeCut?: boolean | null;
  status: string;
  totalStrokes: number;
  toPar: number;
  roundsPlayed: number;
  roundScores: number[];
  roundPositions: Array<number | null>;
};

type GolfLeaderboard = {
  id: string;
  name: string;
  status: string;
  parPerRound: number;
  courseName?: string | null;
  courseLocation?: string | null;
  holePars: number[];
  cutAfterRound: number;
  cutToParticipants: number;
  cutAppliedAt?: string | null;
  scoringDirection: 'LowerIsBetter';
  entries: GolfEntry[];
};

export default function LeaderboardPage() {
  const [leaderboard, setLeaderboard] = useState<GolfLeaderboard | null>(null);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    let active = true;
    async function load() {
      const query = new URLSearchParams(window.location.search);
      const tenantSlug = query.get('tenant') ?? undefined;
      const requestedId = query.get('competitionId');
      const competitionId = requestedId ?? (await publicApi<PublicCompetition[]>(
        '/competitions',
        undefined,
        tenantSlug,
      )).find((competition) =>
        competition.sportId === '00000000-0000-0000-0000-000000000102'
      )?.id;
      if (!competitionId) {
        setError('No published golf tournament is available.');
        return;
      }
      const next = await publicApi<GolfLeaderboard>(
        `/golf/leaderboard?competitionId=${encodeURIComponent(competitionId)}`,
        undefined,
        tenantSlug,
      );
      if (active) {
        setLeaderboard(next);
        setError(null);
      }
    }

    load().catch((reason: Error) => setError(reason.message));
    const refresh = window.setInterval(() => {
      load().catch((reason: Error) => setError(reason.message));
    }, 10_000);
    return () => {
      active = false;
      window.clearInterval(refresh);
    };
  }, []);

  return (
    <div className="container mx-auto max-w-7xl px-4 py-10">
      <h1 className="text-3xl font-bold text-gray-900">Golf leaderboard</h1>
      <p className="mt-2 text-gray-600">
        Hole-by-hole totals, the cut line, and four-round tournament positions.
      </p>
      {error && <Message>Could not load leaderboard: {error}</Message>}
      {!error && !leaderboard && <Message>Loading leaderboard…</Message>}
      {leaderboard && (
        <>
          <div className="mt-8 flex flex-wrap items-center justify-between gap-4 rounded-xl bg-green-950 p-6 text-white">
            <div>
              <p className="text-sm uppercase tracking-wide text-green-300">
                {leaderboard.status}
              </p>
              <h2 className="mt-1 text-2xl font-bold">{leaderboard.name}</h2>
              <p className="mt-1 text-sm text-green-100">
                {leaderboard.courseName ?? 'Championship Course'}
                {leaderboard.courseLocation ? ` · ${leaderboard.courseLocation}` : ''}
              </p>
            </div>
            <div className="text-sm text-green-100">
              Par {leaderboard.parPerRound} · Cut after round {leaderboard.cutAfterRound}
              {leaderboard.cutAppliedAt ? ' · Cut applied' : ' · Cut pending'}
            </div>
          </div>

          {leaderboard.holePars?.length === 18 && (
            <div className="mt-4 rounded-xl border border-green-100 bg-green-50 px-4 py-3 text-sm text-green-950">
              <span className="font-semibold">Hole pars:</span>{' '}
              {leaderboard.holePars.map((par, index) => `${index + 1}:${par}`).join(' · ')}
              <span className="ml-3 text-xs text-green-700">Live · refreshes every 10 seconds</span>
            </div>
          )}

          <div className="mt-8 overflow-x-auto rounded-xl border border-gray-200">
            <table className="w-full min-w-[900px] text-sm">
              <thead className="bg-gray-950 text-left text-white">
                <tr>
                  <th className="p-3">#</th>
                  <th className="p-3">Golfer</th>
                  {[1, 2, 3, 4].map((round) => (
                    <th key={round} className="p-3 text-center">R{round}</th>
                  ))}
                  <th className="p-3 text-center">Total</th>
                  <th className="p-3 text-center">To par</th>
                  <th className="p-3">Status</th>
                </tr>
              </thead>
              <tbody>
                {leaderboard.entries.map((entry, index) => (
                  <tr
                    key={entry.participantId}
                    className={`border-t border-gray-100 ${
                      entry.status === 'MissedCut' ? 'bg-gray-50 text-gray-500' : 'bg-white'
                    }`}
                  >
                    <td className="p-3 font-bold">
                      {entry.isTied ? 'T' : ''}{entry.position}
                    </td>
                    <td
                      className="p-3 font-semibold"
                      data-testid={index === 0 ? 'golf-leader' : undefined}
                    >
                      {entry.name}
                    </td>
                    {entry.roundScores.map((score, round) => (
                      <td key={round} className="p-3 text-center">
                        <span>{score || '—'}</span>
                        {entry.roundPositions[round] && (
                          <span className="mt-0.5 block text-xs text-gray-500">
                            #{entry.roundPositions[round]}
                          </span>
                        )}
                      </td>
                    ))}
                    <td className="p-3 text-center font-semibold">
                      {entry.totalStrokes || '—'}
                    </td>
                    <td className="p-3 text-center font-bold">
                      {entry.roundsPlayed ? formatToPar(entry.toPar) : '—'}
                    </td>
                    <td className="p-3">{formatStatus(entry.status)}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        </>
      )}
    </div>
  );
}

function formatToPar(value: number) {
  if (value === 0) return 'E';
  return value > 0 ? `+${value}` : String(value);
}

function formatStatus(status: string) {
  return status === 'MissedCut' ? 'Missed cut' : status.replace(/([A-Z])/g, ' $1').trim();
}

function Message({ children }: { children: React.ReactNode }) {
  return (
    <div className="mt-8 rounded-xl border border-dashed border-gray-300 bg-gray-50 p-8 text-center text-gray-500">
      {children}
    </div>
  );
}
