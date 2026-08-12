'use client';

import { useEffect, useState } from 'react';
import { publicApi } from '@/lib/public-api';

type Standing = {
  id: string;
  competitionId: string;
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
  metadata?: string | null;
};

function headToHeadPoints(row: Standing) {
  if (!row.metadata) return null;
  try {
    const detail = JSON.parse(row.metadata) as { headToHeadPoints?: number };
    return detail.headToHeadPoints ?? null;
  } catch {
    return null;
  }
}

export default function StandingsPage() {
  const [rows, setRows] = useState<Standing[]>([]);
  const [error, setError] = useState<string | null>(null);
  const tables = Array.from(rows.reduce((grouped, row) => {
    const key = `${row.competitionId}:${row.groupId}`;
    grouped.set(key, [...(grouped.get(key) ?? []), row]);
    return grouped;
  }, new Map<string, Standing[]>()).entries());

  useEffect(() => {
    publicApi<Standing[]>('/standings')
      .then(setRows)
      .catch((reason: Error) => setError(reason.message));
  }, []);

  return (
    <div className="container mx-auto max-w-6xl px-4 py-10">
      <h1 className="text-3xl font-bold text-gray-900">Standings</h1>
      <p className="mt-2 text-gray-600">Current published competition tables.</p>
      {error && <Message>Could not load standings: {error}</Message>}
      {!error && rows.length === 0 && <Message>No standings have been published yet.</Message>}
      {tables.map(([tableId, tableRows]) => {
        const showsHeadToHead = tableRows.some(row => headToHeadPoints(row) !== null);
        return tableRows && (
        <section key={tableId} className="mt-8">
          {tableRows[0].groupName && <h2 className="mb-3 text-xl font-bold text-gray-900">{tableRows[0].groupName}</h2>}
          <div className="overflow-x-auto rounded-xl border border-gray-200">
          <table className="w-full min-w-[720px] text-sm">
            <thead className="bg-gray-950 text-left text-white">
              <tr>
                <th className="p-3">#</th>
                <th className="p-3">Participant</th>
                <th className="p-3 text-center">P</th>
                <th className="p-3 text-center">W</th>
                <th className="p-3 text-center">D</th>
                <th className="p-3 text-center">L</th>
                <th className="p-3 text-center">GF</th>
                <th className="p-3 text-center">GA</th>
                <th className="p-3 text-center">+/-</th>
                {showsHeadToHead && <th className="p-3 text-center">H2H</th>}
                <th className="p-3 text-center">Pts</th>
              </tr>
            </thead>
            <tbody>
              {tableRows.sort((left, right) => left.position - right.position).map((row) => (
                <tr key={row.id} className="border-t border-gray-100 bg-white">
                  <td className="p-3 font-bold">{row.position}</td>
                  <td className="p-3 font-semibold">{row.participantName}</td>
                  <td className="p-3 text-center">{row.played}</td>
                  <td className="p-3 text-center">{row.won}</td>
                  <td className="p-3 text-center">{row.drawn}</td>
                  <td className="p-3 text-center">{row.lost}</td>
                  <td className="p-3 text-center">{row.pointsFor}</td>
                  <td className="p-3 text-center">{row.pointsAgainst}</td>
                  <td className="p-3 text-center">{row.pointDifferential}</td>
                  {showsHeadToHead && (
                    <td className="p-3 text-center">{headToHeadPoints(row) ?? '—'}</td>
                  )}
                  <td className="p-3 text-center font-bold">{row.points}</td>
                </tr>
              ))}
            </tbody>
          </table>
          </div>
        </section>
        );
      })}
    </div>
  );
}

function Message({ children }: { children: React.ReactNode }) {
  return <div className="mt-8 rounded-xl border border-dashed border-gray-300 bg-gray-50 p-8 text-center text-gray-500">{children}</div>;
}
