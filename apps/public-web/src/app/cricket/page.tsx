'use client';

import { useEffect, useState } from 'react';
import { PublicCompetition, publicApi } from '@/lib/public-api';

type Standing = {
  position: number;
  participantId: string;
  name: string;
  played: number;
  won: number;
  lost: number;
  tied: number;
  points: number;
  netRunRate: number;
};

type Innings = {
  inningsNumber: number;
  battingTeamId: string;
  name: string;
  runs: number;
  wickets: number;
  overs: string;
  extras: number;
};

type Match = {
  id: string;
  name: string;
  contestShape: string;
  status: string;
  innings: Innings[];
  target?: number | null;
  winnerName?: string | null;
  result?: string | null;
};

type CricketTournament = {
  id: string;
  name: string;
  standings: Standing[];
  groupMatches: Match[];
  semifinals: Match[];
  final?: Match | null;
  championName?: string | null;
};

export default function CricketPage() {
  const [tournament, setTournament] = useState<CricketTournament | null>(null);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    async function load() {
      const query = new URLSearchParams(window.location.search);
      const tenantSlug = query.get('tenant') ?? undefined;
      const requestedId = query.get('competitionId');
      const competitionId = requestedId ?? (await publicApi<PublicCompetition[]>(
        '/competitions',
        undefined,
        tenantSlug,
      )).find((competition) =>
        competition.sportId === '00000000-0000-0000-0000-000000000104'
      )?.id;
      if (!competitionId) {
        setError('No published T20 tournament is available.');
        return;
      }
      setTournament(await publicApi<CricketTournament>(
        `/cricket/tournament?competitionId=${encodeURIComponent(competitionId)}`,
        undefined,
        tenantSlug,
      ));
    }
    load().catch((reason: Error) => setError(reason.message));
  }, []);

  return (
    <div className="container mx-auto max-w-6xl px-4 py-10">
      <h1 className="text-3xl font-bold text-gray-900">T20 cricket tournament</h1>
      <p className="mt-2 text-gray-600">
        Group standings, innings scorecards, knockout matches, and champion.
      </p>
      {error && <Message>Could not load cricket tournament: {error}</Message>}
      {!error && !tournament && <Message>Loading cricket tournament…</Message>}
      {tournament && (
        <>
          <section className="mt-8 rounded-xl bg-emerald-950 p-6 text-white">
            <p className="text-sm uppercase tracking-wide text-emerald-300">T20</p>
            <div className="mt-1 flex flex-wrap items-end justify-between gap-3">
              <h2 className="text-2xl font-bold">{tournament.name}</h2>
              <p className="text-emerald-100" data-testid="cricket-champion">
                {tournament.championName
                  ? `Champion: ${tournament.championName}`
                  : 'Champion to be decided'}
              </p>
            </div>
          </section>

          <section className="mt-8 overflow-hidden rounded-xl border bg-white shadow-sm">
            <h2 className="bg-gray-950 px-5 py-3 text-xl font-bold text-white">
              Group standings
            </h2>
            <div className="overflow-x-auto">
              <table className="w-full text-sm">
                <thead className="bg-gray-100 text-left">
                  <tr>
                    <th className="p-3">Pos</th><th className="p-3">Team</th>
                    <th className="p-3">P</th><th className="p-3">W</th>
                    <th className="p-3">L</th><th className="p-3">T</th>
                    <th className="p-3">Pts</th><th className="p-3">NRR</th>
                  </tr>
                </thead>
                <tbody>
                  {tournament.standings.map((row) => (
                    <tr key={row.participantId} className="border-t">
                      <td className="p-3 font-semibold">{row.position}</td>
                      <td className="p-3">{row.name}</td>
                      <td className="p-3">{row.played}</td>
                      <td className="p-3">{row.won}</td>
                      <td className="p-3">{row.lost}</td>
                      <td className="p-3">{row.tied}</td>
                      <td className="p-3 font-bold">{row.points}</td>
                      <td className="p-3">{Number(row.netRunRate).toFixed(3)}</td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          </section>

          <MatchSection title="Group matches" matches={tournament.groupMatches} />
          <MatchSection title="Semifinals" matches={tournament.semifinals} />
          <section className="mt-10">
            <h2 className="text-2xl font-bold text-gray-900">Final</h2>
            {tournament.final
              ? <div className="mt-4"><MatchCard match={tournament.final} /></div>
              : <Message>The final appears after group qualification.</Message>}
          </section>
        </>
      )}
    </div>
  );
}

function MatchSection({ title, matches }: { title: string; matches: Match[] }) {
  return (
    <section className="mt-10">
      <h2 className="text-2xl font-bold text-gray-900">{title}</h2>
      {matches.length === 0
        ? <Message>No matches are available yet.</Message>
        : <div className="mt-4 grid gap-4 md:grid-cols-2">
            {matches.map((match) => <MatchCard key={match.id} match={match} />)}
          </div>}
    </section>
  );
}

function MatchCard({ match }: { match: Match }) {
  return (
    <article className="rounded-xl border bg-white p-5 shadow-sm">
      <div className="flex justify-between gap-3">
        <h3 className="font-bold text-gray-900">{match.name}</h3>
        <span className="text-xs uppercase text-gray-500">{match.status}</span>
      </div>
      <div className="mt-3 space-y-1">
        {match.innings.map((innings) => (
          <div key={innings.inningsNumber} className="flex justify-between text-sm">
            <span>{innings.name}</span>
            <strong>{innings.runs}/{innings.wickets} ({innings.overs} ov)</strong>
          </div>
        ))}
      </div>
      {match.target && <p className="mt-2 text-xs text-gray-500">Target: {match.target}</p>}
      {match.result && (
        <p className="mt-3 font-semibold text-emerald-800">
          {match.winnerName ? `${match.winnerName} — ` : ''}{match.result}
        </p>
      )}
    </article>
  );
}

function Message({ children }: { children: React.ReactNode }) {
  return <div className="mt-6 rounded-lg border bg-gray-50 p-5 text-gray-600">{children}</div>;
}
