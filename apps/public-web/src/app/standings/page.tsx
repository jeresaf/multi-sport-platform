'use client';

import Link from 'next/link';
import { Layers3, Table2, Trophy, Users } from 'lucide-react';
import { useEffect, useMemo, useState } from 'react';
import { publicApi, resolveTenantSlug, tenantPath } from '@/lib/public-api';

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

type StandingMetadata = {
  headToHeadPoints?: number;
  bonusPoints?: number;
  form?: string[] | string;
  qualificationCode?: string;
  pointsDeduction?: number;
};

function standingMetadata(row: Standing): StandingMetadata {
  if (!row.metadata) return {};
  try {
    return JSON.parse(row.metadata) as StandingMetadata;
  } catch {
    return {};
  }
}

function headToHeadPoints(row: Standing) {
  return standingMetadata(row).headToHeadPoints ?? null;
}

function recentForm(row: Standing) {
  const form = standingMetadata(row).form;
  if (Array.isArray(form)) return form.map(value => String(value)).slice(-5);
  return typeof form === 'string' ? form.split('').filter(Boolean).slice(-5) : [];
}

export default function StandingsPage() {
  const [rows, setRows] = useState<Standing[]>([]);
  const [selectedTable, setSelectedTable] = useState('');
  const [tenantSlug] = useState(resolveTenantSlug);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    publicApi<Standing[]>('/standings', undefined, tenantSlug)
      .then(setRows)
      .catch((reason: Error) => setError(reason.message))
      .finally(() => setLoading(false));
  }, [tenantSlug]);

  const tables = useMemo(() => rows.reduce((grouped, row) => {
    const key = `${row.competitionId}:${row.groupId}`;
    grouped.set(key, [...(grouped.get(key) ?? []), row]);
    return grouped;
  }, new Map<string, Standing[]>()), [rows]);

  const tableEntries = [...tables.entries()];
  const activeKey = selectedTable && tables.has(selectedTable)
    ? selectedTable
    : tableEntries[0]?.[0] ?? '';
  const activeRows = [...(tables.get(activeKey) ?? [])]
    .sort((left, right) => left.position - right.position);
  const leader = activeRows[0];
  const showsHeadToHead = activeRows.some(row => headToHeadPoints(row) !== null);
  const showsForm = activeRows.some(row => recentForm(row).length > 0);
  const participantCount = new Set(rows.map(row => row.participantId)).size;

  if (loading) return <PageState message="Loading published standings…" />;
  if (error) return <PageState message={`Standings could not be loaded: ${error}`} tone="error" />;

  return (
    <div className="app-container py-5 sm:py-7">
      <header className="overflow-hidden rounded-[28px] border border-[var(--color-border)] bg-[var(--color-surface)] shadow-[var(--shadow-card)]">
        <div className="bg-gradient-to-br from-slate-950 via-slate-900 to-slate-800 px-5 py-7 text-white sm:px-7 sm:py-8">
          <span className="inline-flex h-8 items-center gap-2 rounded-full border border-white/10 bg-white/10 px-3 text-xs font-bold uppercase tracking-[0.18em] text-white/80">
            <Table2 className="h-3.5 w-3.5" />
            Standings centre
          </span>
          <h1 className="mt-4 text-3xl font-black tracking-tight sm:text-5xl">Competition tables</h1>
          <p className="mt-3 max-w-2xl text-sm leading-6 text-white/70 sm:text-base">
            Follow published rankings, group tables and tie-break information across active competitions.
          </p>
        </div>
        <div className="grid gap-px bg-[var(--color-border)] sm:grid-cols-3">
          <Metric icon={<Layers3 />} label="Published tables" value={tables.size} />
          <Metric icon={<Users />} label="Participants" value={participantCount} />
          <Metric icon={<Trophy />} label="Leader points" value={leader?.points ?? 0} />
        </div>
      </header>

      {tableEntries.length === 0 ? (
        <div className="surface mt-6 rounded-2xl px-6 py-16 text-center muted">
          No standings have been published yet.
        </div>
      ) : (
        <>
          <section className="surface mt-6 rounded-2xl p-4 shadow-[var(--shadow-card)]">
            <label className="block">
              <span className="mb-2 block text-xs font-bold uppercase tracking-[0.14em] muted">
                Choose table
              </span>
              <select
                value={activeKey}
                onChange={event => setSelectedTable(event.target.value)}
                className="w-full rounded-xl border border-[var(--color-border)] bg-[var(--color-surface)] px-4 py-3 font-semibold md:max-w-md"
              >
                {tableEntries.map(([key, tableRows], index) => (
                  <option key={key} value={key}>
                    {tableRows[0]?.groupName || `Competition table ${index + 1}`}
                  </option>
                ))}
              </select>
            </label>
          </section>

          <section className="surface mt-4 overflow-hidden rounded-2xl shadow-[var(--shadow-card)]">
            <div className="flex flex-wrap items-end justify-between gap-3 border-b border-[var(--color-border)] px-5 py-4">
              <div>
                <p className="text-xs font-bold uppercase tracking-[0.14em] text-[var(--brand-primary)]">
                  Live published order
                </p>
                <h2 className="mt-1 text-xl font-black">
                  {activeRows[0]?.groupName || 'Competition table'}
                </h2>
              </div>
              <span className="rounded-full bg-[var(--color-surface-muted)] px-3 py-1.5 text-xs font-semibold muted">
                {activeRows.length} participants
              </span>
            </div>

            <div className="overflow-x-auto" data-testid="published-standings">
              <table className="w-full min-w-[760px] text-sm">
                <thead className="bg-slate-950 text-left text-white">
                  <tr>
                    <th className="w-14 p-3 text-center">#</th>
                    <th className="p-3">Participant</th>
                    <th className="p-3 text-center">P</th>
                    <th className="p-3 text-center">W</th>
                    <th className="p-3 text-center">D</th>
                    <th className="p-3 text-center">L</th>
                    {showsForm && <th className="p-3 text-center">Form</th>}
                    <th className="p-3 text-center">F</th>
                    <th className="p-3 text-center">A</th>
                    <th className="p-3 text-center">+/-</th>
                    {showsHeadToHead && <th className="p-3 text-center">H2H</th>}
                    <th className="p-3 text-center">Pts</th>
                  </tr>
                </thead>
                <tbody>
                  {activeRows.map(row => (
                    <tr
                      key={row.id}
                      className="border-t border-[var(--color-border)] bg-[var(--color-surface)] transition hover:bg-[var(--color-surface-muted)]"
                    >
                      <td className="p-3 text-center">
                        <span className={`inline-flex h-8 w-8 items-center justify-center rounded-xl font-black ${
                          row.position <= 3
                            ? 'bg-[var(--brand-primary)] text-white'
                            : 'bg-[var(--color-surface-muted)]'
                        }`}>
                          {row.position}
                        </span>
                      </td>
                      <td className="p-3">
                        <Link
                          href={tenantPath(`/participants/${row.participantId}`, tenantSlug)}
                          className="font-bold hover:text-[var(--brand-primary)]"
                        >
                          {row.participantName}
                        </Link>
                        {standingMetadata(row).qualificationCode && (
                          <span className="ml-2 rounded-full bg-[var(--color-surface-muted)] px-2 py-0.5 text-[10px] font-black uppercase text-[var(--brand-primary)]">
                            {standingMetadata(row).qualificationCode}
                          </span>
                        )}
                      </td>
                      <td className="p-3 text-center">{row.played}</td>
                      <td className="p-3 text-center">{row.won}</td>
                      <td className="p-3 text-center">{row.drawn}</td>
                      <td className="p-3 text-center">{row.lost}</td>
                      {showsForm && (
                        <td className="p-3">
                          <div className="flex justify-center gap-1" aria-label={`Recent form for ${row.participantName}`}>
                            {recentForm(row).map((value, index) => (
                              <span
                                key={`${row.id}-form-${index}`}
                                className={`flex h-6 w-6 items-center justify-center rounded-full text-[10px] font-black text-white ${
                                  value.toUpperCase() === 'W'
                                    ? 'bg-emerald-600'
                                    : value.toUpperCase() === 'D'
                                      ? 'bg-amber-500'
                                      : 'bg-rose-600'
                                }`}
                              >
                                {value.slice(0, 1).toUpperCase()}
                              </span>
                            ))}
                          </div>
                        </td>
                      )}
                      <td className="p-3 text-center">{row.pointsFor}</td>
                      <td className="p-3 text-center">{row.pointsAgainst}</td>
                      <td className="p-3 text-center font-semibold">
                        {row.pointDifferential > 0 ? '+' : ''}{row.pointDifferential}
                      </td>
                      {showsHeadToHead && (
                        <td className="p-3 text-center">{headToHeadPoints(row) ?? '—'}</td>
                      )}
                      <td className="p-3 text-center text-base font-black text-[var(--brand-primary)]">
                        {row.points}
                        {(standingMetadata(row).pointsDeduction ?? 0) > 0 && (
                          <small className="block text-[10px] font-semibold text-rose-600">
                            -{standingMetadata(row).pointsDeduction} deduction
                          </small>
                        )}
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
            <div className="border-t border-[var(--color-border)] px-5 py-3 text-xs muted">
              <p>P: played · W: won · D: drawn · L: lost · F/A: points or goals for and against.</p>
              <details className="mt-2">
                <summary className="cursor-pointer font-bold text-[var(--brand-primary)]">
                  Table order and qualification guide
                </summary>
                <p className="mt-2 max-w-3xl leading-5">
                  Positions follow the published competition rules. Head-to-head values, recent form,
                  qualification codes, bonus points and deductions appear only when supplied by that competition.
                </p>
              </details>
            </div>
          </section>
        </>
      )}
    </div>
  );
}

function Metric({
  icon,
  label,
  value,
}: {
  icon: React.ReactNode;
  label: string;
  value: number;
}) {
  return (
    <div className="bg-[var(--color-surface)] px-5 py-4">
      <div className="flex items-center justify-between gap-3">
        <span className="flex h-9 w-9 items-center justify-center rounded-xl bg-[var(--color-surface-muted)] text-[var(--brand-primary)] [&_svg]:h-4 [&_svg]:w-4">
          {icon}
        </span>
        <strong className="text-2xl font-black tracking-tight">{value}</strong>
      </div>
      <p className="mt-2 text-xs font-bold uppercase tracking-wider muted">{label}</p>
    </div>
  );
}

function PageState({
  message,
  tone = 'neutral',
}: {
  message: string;
  tone?: 'neutral' | 'error';
}) {
  return (
    <div className="app-container py-24">
      <div className={`surface mx-auto max-w-2xl rounded-2xl px-6 py-14 text-center ${
        tone === 'error' ? 'text-red-700' : 'muted'
      }`}>
        {message}
      </div>
    </div>
  );
}
