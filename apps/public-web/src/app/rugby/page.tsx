'use client';

import { useEffect, useState } from 'react';
import { PublicCompetition, publicApi } from '@/lib/public-api';

const RUGBY_ID = '00000000-0000-0000-0000-000000000106';

type Standing = {
  id: string;
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
  metadata?: string | null;
};

type Match = {
  id: string;
  name?: string | null;
  status: string;
  startTime?: string | null;
  venue?: string | null;
  teams: Array<{ participantId: string; name: string; score: number }>;
};

type RugbyTournament = {
  variant: 'fifteens' | 'sevens';
  formatName: string;
  playersPerSide: number;
  halfDurationMinutes: number;
  standingsModel: {
    winPoints: number;
    drawPoints: number;
    lossPoints: number;
    usesTryBonus: boolean;
    usesLosingBonus: boolean;
    usesGrandSlamBonus: boolean;
  };
  matches: Match[];
  standings: Standing[];
};

type RugbyMetadata = {
  tries?: number;
  tryBonusPoints?: number;
  losingBonusPoints?: number;
  grandSlamBonusPoints?: number;
};

export default function RugbyPage() {
  const [competition, setCompetition] = useState<PublicCompetition | null>(null);
  const [standings, setStandings] = useState<Standing[]>([]);
  const [matches, setMatches] = useState<Match[]>([]);
  const [format, setFormat] = useState<RugbyTournament | null>(null);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    async function load() {
      const query = new URLSearchParams(window.location.search);
      const tenant = query.get('tenant') ?? 'six-nations';
      const requestedId = query.get('competitionId');
      const competitions = await publicApi<PublicCompetition[]>(
        '/competitions', undefined, tenant,
      );
      const selected = competitions.find(item =>
        item.id === requestedId || (!requestedId && item.sportId === RUGBY_ID)
      );
      if (!selected) throw new Error('No published rugby competition is available.');
      setCompetition(selected);
      const tournament = await publicApi<RugbyTournament>(
        `/rugby/tournament?competitionId=${encodeURIComponent(selected.id)}`,
        undefined,
        tenant,
      );
      setStandings(tournament.standings);
      setMatches(tournament.matches);
      setFormat(tournament);
    }
    load().catch((reason: Error) => setError(reason.message));
  }, []);

  return (
    <div className="container mx-auto max-w-6xl px-4 py-10">
      <section className="rounded-2xl bg-gradient-to-br from-emerald-950 to-teal-800 p-8 text-white">
        <p className="text-sm font-semibold uppercase tracking-[0.2em] text-emerald-200">
          {format?.formatName ?? 'Rugby Union'}
        </p>
        <h1 className="mt-2 text-4xl font-bold">{competition?.name ?? 'Six Nations'}</h1>
        <p className="mt-3 max-w-3xl text-emerald-50">
          {format?.variant === 'sevens'
            ? 'Fast tournament coverage with seven players per side, 7-minute halves, golden-point knockout extra time, and 3/2/1 pool points.'
            : 'Full international 15-a-side coverage with 40-minute halves, 4-point wins, try and losing bonus points, and Grand Slam recognition.'}
        </p>
        <nav aria-label="Rugby formats" className="mt-6 flex flex-wrap gap-3 text-sm font-semibold">
          <a className="rounded-full bg-white px-4 py-2 text-emerald-950" href="/rugby?tenant=six-nations">Explore Rugby 15s</a>
          <a className="rounded-full border border-emerald-200 px-4 py-2 text-white" href="/rugby?tenant=national-sports-demo&competitionId=70000000-0000-0000-0000-000000000100">Explore Rugby Sevens</a>
        </nav>
      </section>

      {error && <Message>Could not load rugby coverage: {error}</Message>}
      {!error && !competition && <Message>Loading rugby competition…</Message>}

      {competition && (
        <>
          <section className="mt-8 overflow-hidden rounded-xl border bg-white shadow-sm">
            <h2 className="bg-gray-950 px-5 py-3 text-xl font-bold text-white">Championship table</h2>
            {standings.length === 0 ? <Message>No official standings are published yet.</Message> : (
              <div className="overflow-x-auto">
                <table className="w-full min-w-[800px] text-sm">
                  <thead className="bg-gray-100 text-left">
                    <tr>
                      <th className="p-3">Pos</th><th className="p-3">Team</th>
                      <th className="p-3">P</th><th className="p-3">W</th>
                      <th className="p-3">D</th><th className="p-3">L</th>
                      <th className="p-3">PF</th><th className="p-3">PA</th>
                      <th className="p-3">+/-</th><th className="p-3">Tries</th>
                      {format?.standingsModel.usesTryBonus && <th className="p-3">BP</th>}
                      <th className="p-3">Pts</th>
                    </tr>
                  </thead>
                  <tbody>{standings.map(row => {
                    const detail = parseMetadata(row.metadata);
                    const bonus = (detail.tryBonusPoints ?? 0)
                      + (detail.losingBonusPoints ?? 0)
                      + (detail.grandSlamBonusPoints ?? 0);
                    return (
                      <tr key={row.id} className="border-t">
                        <td className="p-3 font-bold">{row.position}</td>
                        <td className="p-3 font-semibold">{row.participantName}</td>
                        <td className="p-3">{row.played}</td><td className="p-3">{row.won}</td>
                        <td className="p-3">{row.drawn}</td><td className="p-3">{row.lost}</td>
                        <td className="p-3">{row.pointsFor}</td><td className="p-3">{row.pointsAgainst}</td>
                        <td className="p-3">{row.pointDifferential}</td><td className="p-3">{detail.tries ?? 0}</td>
                        {format?.standingsModel.usesTryBonus && <td className="p-3">{bonus}</td>}
                        <td className="p-3 font-bold">{row.points}</td>
                      </tr>
                    );
                  })}</tbody>
                </table>
              </div>
            )}
          </section>

          <section className="mt-10">
            <h2 className="text-2xl font-bold">Fixtures and results</h2>
            {matches.length === 0 ? <Message>No fixtures are published yet.</Message> : (
              <div className="mt-4 grid gap-4 md:grid-cols-2">
                {matches.map((match, index) => (
                  <article key={match.id} className="rounded-xl border bg-white p-5 shadow-sm">
                    <div className="flex justify-between gap-3">
                      <p className="text-xs font-semibold uppercase text-gray-500">{match.name ?? `Match ${index + 1}`}</p>
                      <span className="text-xs uppercase text-gray-500">{match.status}</span>
                    </div>
                    {match.teams.map(team => (
                      <div key={team.participantId} className="mt-2 flex justify-between gap-4">
                        <span>{team.name}</span><strong>{team.score || match.status !== 'Scheduled' ? team.score : '—'}</strong>
                      </div>
                    ))}
                    <p className="mt-3 text-xs text-gray-500">
                      {match.startTime ? new Date(match.startTime).toLocaleString() : 'Date TBC'} · {match.venue ?? 'Venue TBC'}
                    </p>
                  </article>
                ))}
              </div>
            )}
          </section>
        </>
      )}
    </div>
  );
}

function parseMetadata(value?: string | null): RugbyMetadata {
  if (!value) return {};
  try { return JSON.parse(value) as RugbyMetadata; } catch { return {}; }
}

function Message({ children }: { children: React.ReactNode }) {
  return <div className="m-5 rounded-xl border border-dashed bg-gray-50 p-6 text-center text-gray-600">{children}</div>;
}
