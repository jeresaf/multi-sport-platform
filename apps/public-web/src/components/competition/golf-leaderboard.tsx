'use client';

import { useState } from 'react';

export type GolfLeaderboardEntry = {
  participantId: string;
  name: string;
  position: number;
  isTied: boolean;
  status: string;
  totalStrokes: number;
  toPar: number;
  roundsPlayed: number;
  todayToPar?: number;
  thru?: number;
  roundScores: number[];
  roundPositions?: Array<number | null>;
};

export type GolfLeaderboardData = {
  id: string;
  name: string;
  status: string;
  parPerRound: number;
  courseName?: string | null;
  courseLocation?: string | null;
  holePars?: number[];
  cutAfterRound: number;
  cutToParticipants?: number;
  cutAppliedAt?: string | null;
  entries: GolfLeaderboardEntry[];
};

export function ResponsiveGolfLeaderboard({ data, compact = false }: { data: GolfLeaderboardData; compact?: boolean }) {
  const [expanded, setExpanded] = useState<string | null>(null);
  if (data.entries.length === 0) return <div className="rounded-2xl border border-dashed border-[var(--color-border)] bg-[var(--color-surface-muted)] p-8 text-center muted">Leaderboard positions will appear when scoring begins.</div>;
  return <section aria-label={`${data.name} leaderboard`}>
    {!compact && <header className="mb-5 rounded-2xl bg-emerald-950 p-5 text-white sm:p-6">
      <p className="text-xs font-bold uppercase tracking-wider text-emerald-300">{friendlyStatus(data.status)}</p>
      <div className="mt-1 flex flex-wrap items-end justify-between gap-3"><div><h2 className="text-2xl font-bold">{data.name}</h2><p className="mt-1 text-sm text-emerald-100">{data.courseName ?? 'Championship Course'}{data.courseLocation ? ` · ${data.courseLocation}` : ''}</p></div><p className="text-sm text-emerald-100">Par {data.parPerRound} · Cut after R{data.cutAfterRound} · {data.cutAppliedAt ? 'Cut applied' : 'Cut pending'}</p></div>
    </header>}

    <div className="space-y-2 md:hidden">{data.entries.map((entry, index) => {
      const open = expanded === entry.participantId;
      return <button type="button" key={entry.participantId} onClick={() => setExpanded(open ? null : entry.participantId)} aria-expanded={open} className="surface block w-full rounded-xl p-4 text-left">
        <span className="grid grid-cols-[2.5rem_minmax(0,1fr)_auto] items-center gap-2"><strong>{position(entry)}</strong><span className="min-w-0"><span className="block truncate font-semibold">{entry.name}</span><span className="text-xs muted">{formatStatus(entry.status)}</span></span><strong className="tabular-nums">{entry.roundsPlayed ? signed(entry.toPar) : '—'}</strong></span>
        {open && <span className="mt-3 block border-t border-[var(--color-border)] pt-3 text-sm"><span className="grid grid-cols-3 gap-2"><Detail label="Today" value={entry.thru ? signed(entry.todayToPar ?? 0) : '—'} /><Detail label="Thru" value={thru(entry.thru)} /><Detail label="Strokes" value={entry.totalStrokes || '—'} /></span><span className="mt-3 flex flex-wrap gap-2">{[0, 1, 2, 3].map(round => <span key={round} className="rounded-lg bg-[var(--color-surface-muted)] px-3 py-2">R{round + 1} <strong>{entry.roundScores[round] || '—'}</strong></span>)}</span></span>}
      </button>;
    })}</div>

    <div className="hidden overflow-x-auto rounded-2xl border border-[var(--color-border)] md:block"><table className="w-full text-sm"><thead className="bg-gray-950 text-white"><tr><Th>Pos</Th><Th>Player</Th>{[1, 2, 3, 4].map(round => <Th key={round} desktop>R{round}</Th>)}<Th>Today</Th><Th>Thru</Th><Th>Total</Th><Th tablet>Status</Th></tr></thead><tbody>{data.entries.map((entry, index) => <tr key={entry.participantId} className={`border-t border-[var(--color-border)] ${inactive(entry.status) ? 'text-[var(--color-text-muted)]' : ''}`}><td className="p-3 font-bold">{position(entry)}</td><td data-testid={index === 0 ? 'golf-leader' : undefined} className="p-3 font-semibold">{entry.name}</td>{[0, 1, 2, 3].map(round => <td key={round} className="hidden p-3 text-center xl:table-cell">{entry.roundScores[round] || '—'}</td>)}<td className="p-3 text-center font-semibold">{entry.thru ? signed(entry.todayToPar ?? 0) : '—'}</td><td className="p-3 text-center">{thru(entry.thru)}</td><td className="p-3 text-center font-bold">{entry.roundsPlayed ? signed(entry.toPar) : '—'}</td><td className="hidden p-3 lg:table-cell">{formatStatus(entry.status)}</td></tr>)}</tbody></table></div>
  </section>;
}

function Detail({ label, value }: { label: string; value: React.ReactNode }) { return <span><span className="block text-xs muted">{label}</span><strong className="tabular-nums">{value}</strong></span>; }
function Th({ children, desktop = false, tablet = false }: { children: React.ReactNode; desktop?: boolean; tablet?: boolean }) { return <th scope="col" className={`p-3 text-left ${desktop ? 'hidden text-center xl:table-cell' : ''} ${tablet ? 'hidden lg:table-cell' : ''}`}>{children}</th>; }
function position(entry: GolfLeaderboardEntry) { return `${entry.isTied ? 'T' : ''}${entry.position}`; }
function signed(value: number) { return value === 0 ? 'E' : value > 0 ? `+${value}` : String(value); }
function thru(value?: number) { return !value ? '—' : value >= 18 ? 'F' : value; }
function inactive(status: string) { return ['missedcut', 'withdrawn', 'disqualified'].includes(status.toLowerCase()); }
function formatStatus(value: string) { return value.replace(/([A-Z])/g, ' $1').trim(); }
function friendlyStatus(value: string) { return value.toLowerCase() === 'inprogress' ? 'Live' : formatStatus(value); }
