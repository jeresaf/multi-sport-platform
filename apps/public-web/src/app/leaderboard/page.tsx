'use client';

import { useEffect, useState } from 'react';
import { PublicCompetition, publicApi } from '@/lib/public-api';
import { ResponsiveGolfLeaderboard, type GolfLeaderboardData } from '@/components/competition/golf-leaderboard';

export default function LeaderboardPage() {
  const [leaderboard, setLeaderboard] = useState<GolfLeaderboardData | null>(null);
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
      const next = await publicApi<GolfLeaderboardData>(
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
      {leaderboard && <div className="mt-8"><ResponsiveGolfLeaderboard data={leaderboard} /></div>}
    </div>
  );
}

function Message({ children }: { children: React.ReactNode }) {
  return (
    <div className="mt-8 rounded-xl border border-dashed border-gray-300 bg-gray-50 p-8 text-center text-gray-500">
      {children}
    </div>
  );
}
