'use client';

import { useEffect, useState } from 'react';
import { PublicCompetition, publicApi } from '@/lib/public-api';

type PlayoffMatch = {
  id: string;
  contestId: string;
  seriesId?: string | null;
  bestOf: number;
  isAggregateTie: boolean;
  isThirdPlace: boolean;
  score1?: string | null;
  score2?: string | null;
  matchNumber: number;
  participant1Name?: string | null;
  participant2Name?: string | null;
  winnerName?: string | null;
  status: string;
  seriesGames: Array<{
    id: string;
    seriesGameNumber: number;
    status: string;
    homeParticipantName?: string | null;
    awayParticipantName?: string | null;
    winnerName?: string | null;
  }>;
};

type PlayoffRound = {
  id: string;
  roundNumber: number;
  name: string;
  matches: PlayoffMatch[];
};

type Playoffs = {
  id: string;
  competitionId: string;
  name: string;
  status: string;
  championId?: string | null;
  championName?: string | null;
  rounds: PlayoffRound[];
};

export default function PlayoffsPage() {
  const [playoffs, setPlayoffs] = useState<Playoffs | null>(null);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    async function load() {
      const requestedId = new URLSearchParams(window.location.search)
        .get('competitionId');
      const tenantSlug = new URLSearchParams(window.location.search)
        .get('tenant') ?? undefined;
      const competitionId = requestedId ?? (await publicApi<PublicCompetition[]>(
        '/competitions',
        undefined,
        tenantSlug,
      ))[0]?.id;
      if (!competitionId) {
        setError('No published competition is available.');
        return;
      }
      setPlayoffs(await publicApi<Playoffs>(
        `/playoffs?competitionId=${encodeURIComponent(competitionId)}`,
        undefined,
        tenantSlug,
      ));
    }

    load().catch((reason: Error) => setError(reason.message));
  }, []);

  return (
    <div className="container mx-auto max-w-6xl px-4 py-10">
      <h1 className="text-3xl font-bold text-gray-900">Playoffs</h1>
      <p className="mt-2 text-gray-600">
        Qualified teams, knockout progression, and the competition champion.
      </p>

      {error && <Message>No playoff bracket is available: {error}</Message>}
      {!error && !playoffs && <Message>Loading playoff bracket…</Message>}
      {playoffs && (
        <>
          <div className="mt-8 rounded-xl bg-gray-950 p-6 text-white">
            <p className="text-sm uppercase tracking-wide text-primary-300">
              {playoffs.status}
            </p>
            <h2 className="mt-1 text-2xl font-bold">{playoffs.name}</h2>
            <p className="mt-3 text-lg" data-testid="playoff-champion">
              {playoffs.championName
                ? `Champion: ${playoffs.championName}`
                : 'Champion pending'}
            </p>
          </div>

          <div className="mt-8 grid gap-6 lg:grid-cols-2">
            {playoffs.rounds.map((round) => (
              <section key={round.id}>
                <h2 className="text-xl font-bold text-gray-900">{round.name}</h2>
                <div className="mt-3 space-y-3">
                  {round.matches.map((match) => (
                    <article
                      key={match.id}
                      className="rounded-xl border border-gray-200 bg-white p-4 shadow-sm"
                    >
                      <div className="flex items-center justify-between text-xs uppercase tracking-wide text-gray-500">
                        <span>
                          Match {match.matchNumber}
                          {match.isThirdPlace ? ' · Third place' : ''}
                          {match.isAggregateTie
                            ? ' · Two-leg aggregate'
                            : match.bestOf > 1
                              ? ` · Best of ${match.bestOf}`
                              : ''}
                        </span>
                        <span>{match.status}</span>
                      </div>
                      <div className="mt-3 grid grid-cols-[1fr_auto_1fr] items-center gap-3 text-center">
                        <strong>{match.participant1Name ?? 'TBD'} {match.score1 ?? ''}</strong>
                        <span className="text-gray-400">vs</span>
                        <strong>{match.participant2Name ?? 'TBD'} {match.score2 ?? ''}</strong>
                      </div>
                      {match.winnerName && (
                        <p className="mt-3 text-center text-sm font-medium text-green-700">
                          Winner: {match.winnerName}
                        </p>
                      )}
                      {match.seriesGames.length > 0 && (
                        <ol className="mt-4 grid gap-2 border-t border-gray-100 pt-3 text-xs text-gray-600 sm:grid-cols-3">
                          {match.seriesGames.map((game) => (
                            <li key={game.id} className="rounded bg-gray-50 px-2 py-2">
                              <span className="block font-semibold">
                                {match.isAggregateTie ? 'Leg' : 'Game'} {game.seriesGameNumber}
                              </span>
                              {game.homeParticipantName && game.awayParticipantName && (
                                <span className="block">{game.homeParticipantName} vs {game.awayParticipantName}</span>
                              )}
                              <span>{game.winnerName
                                ? `${game.winnerName} won`
                                : game.status}</span>
                            </li>
                          ))}
                        </ol>
                      )}
                    </article>
                  ))}
                </div>
              </section>
            ))}
          </div>
        </>
      )}
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
