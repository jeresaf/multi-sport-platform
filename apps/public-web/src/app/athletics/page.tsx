'use client';

import { useEffect, useState } from 'react';
import { PublicCompetition, publicApi } from '@/lib/public-api';

type RaceEntry = {
  participantId: string;
  name: string;
  lane: number;
  timeMilliseconds?: number | null;
  displayTime?: string | null;
  place?: number | null;
  isTied: boolean;
  status: string;
  qualificationReason?: string | null;
  medal?: string | null;
};

type Race = {
  id: string;
  name: string;
  raceNumber: number;
  contestShape: string;
  status: string;
  qualifyByPlace?: number | null;
  qualifyByTime?: number | null;
  advancePlaceTies?: boolean | null;
  advanceTimeTies?: boolean | null;
  laneAssignment?: string | null;
  entries: RaceEntry[];
};

type AthleticsMeet = {
  id: string;
  name: string;
  status: string;
  eventName: string;
  distanceMeters: number;
  qualificationAppliedAt?: string | null;
  heats: Race[];
  semifinals: Race[];
  final?: Race | null;
};

export default function AthleticsPage() {
  const [meet, setMeet] = useState<AthleticsMeet | null>(null);
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
        competition.sportId === '00000000-0000-0000-0000-000000000103'
      )?.id;
      if (!competitionId) {
        setError('No published athletics meet is available.');
        return;
      }
      setMeet(await publicApi<AthleticsMeet>(
        `/athletics/meet?competitionId=${encodeURIComponent(competitionId)}`,
        undefined,
        tenantSlug,
      ));
    }

    load().catch((reason: Error) => setError(reason.message));
  }, []);

  return (
    <div className="container mx-auto max-w-6xl px-4 py-10">
      <h1 className="text-3xl font-bold text-gray-900">Athletics meet</h1>
      <p className="mt-2 text-gray-600">
        Heat qualification, final results, ties, disqualifications, and medals.
      </p>
      {error && <Message>Could not load athletics meet: {error}</Message>}
      {!error && !meet && <Message>Loading athletics meet…</Message>}
      {meet && (
        <>
          <div className="mt-8 rounded-xl bg-blue-950 p-6 text-white">
            <p className="text-sm uppercase tracking-wide text-blue-300">{meet.status}</p>
            <div className="mt-1 flex flex-wrap items-end justify-between gap-3">
              <div>
                <h2 className="text-2xl font-bold">{meet.eventName}</h2>
                <p className="mt-1 text-blue-100">{meet.name}</p>
              </div>
              <p className="text-sm text-blue-100">
                {meet.heats.length} heats
                {meet.qualificationAppliedAt ? ' · Qualification applied' : ' · Qualification pending'}
              </p>
            </div>
          </div>

          <div className="mt-8 grid gap-6 lg:grid-cols-2">
            {meet.heats.map((heat) => (
              <RaceTable key={heat.id} race={heat} />
            ))}
          </div>

          {meet.semifinals.length > 0 && (
            <div className="mt-10" data-testid="athletics-semifinals">
              <h2 className="text-2xl font-bold text-gray-900">Semifinals</h2>
              <div className="mt-4 grid gap-6 lg:grid-cols-2">
                {meet.semifinals.map((semifinal) => (
                  <RaceTable key={semifinal.id} race={semifinal} />
                ))}
              </div>
            </div>
          )}

          <div className="mt-10" data-testid="athletics-final">
            <h2 className="text-2xl font-bold text-gray-900">Final and medals</h2>
            {meet.final ? (
              <div className="mt-4"><RaceTable race={meet.final} showMedals /></div>
            ) : (
              <Message>The final will appear after heat qualification.</Message>
            )}
          </div>
        </>
      )}
    </div>
  );
}

function RaceTable({ race, showMedals = false }: { race: Race; showMedals?: boolean }) {
  return (
    <section className="overflow-hidden rounded-xl border border-gray-200 bg-white shadow-sm">
      <div className="flex items-center justify-between bg-gray-950 px-5 py-3 text-white">
        <div>
          <h3 className="font-bold">{race.name}</h3>
          {race.contestShape !== 'AthleticsFinal' && (
            <p className="mt-0.5 text-xs text-gray-300">
              Top {race.qualifyByPlace ?? 0} by place · {race.qualifyByTime ?? 0} by time
              {' · '}{race.laneAssignment ?? 'Standard'} lanes
              {' · '}place ties {race.advancePlaceTies ? 'advance' : 'do not advance'}
              {' · '}time ties {race.advanceTimeTies ? 'advance' : 'do not advance'}
            </p>
          )}
        </div>
        <span className="text-xs uppercase tracking-wide text-gray-300">{race.status}</span>
      </div>
      <div className="overflow-x-auto">
        <table className="w-full text-sm">
          <thead className="bg-gray-100 text-left text-gray-700">
            <tr>
              <th className="p-3">Place</th>
              <th className="p-3">Lane</th>
              <th className="p-3">Athlete</th>
              <th className="p-3">Time</th>
              <th className="p-3">{showMedals ? 'Medal' : 'Status'}</th>
            </tr>
          </thead>
          <tbody>
            {race.entries.map((entry) => (
              <tr key={entry.participantId} className="border-t border-gray-100">
                <td className="p-3 font-bold">
                  {entry.place ? `${entry.isTied ? 'T' : ''}${entry.place}` : '—'}
                </td>
                <td className="p-3">{entry.lane}</td>
                <td className="p-3 font-semibold">{entry.name}</td>
                <td className="p-3 tabular-nums">{entry.displayTime ?? 'DQ'}</td>
                <td className="p-3">
                  {showMedals
                    ? entry.medal
                      ? <Medal value={entry.medal} />
                      : entry.status
                    : formatStatus(entry.status)}
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </section>
  );
}

function Medal({ value }: { value: string }) {
  const colors: Record<string, string> = {
    Gold: 'bg-yellow-100 text-yellow-900',
    Silver: 'bg-gray-200 text-gray-800',
    Bronze: 'bg-orange-100 text-orange-900',
  };
  return (
    <span
      className={`rounded-full px-3 py-1 text-xs font-bold ${colors[value] ?? ''}`}
      data-testid={`medal-${value.toLowerCase()}`}
    >
      {value}
    </span>
  );
}

function formatStatus(value: string) {
  return value.replace(/([A-Z])/g, ' $1').trim();
}

function Message({ children }: { children: React.ReactNode }) {
  return (
    <div className="mt-8 rounded-xl border border-dashed border-gray-300 bg-gray-50 p-8 text-center text-gray-500">
      {children}
    </div>
  );
}
