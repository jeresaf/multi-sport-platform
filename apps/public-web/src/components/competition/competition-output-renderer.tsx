'use client';

import Link from 'next/link';
import { useMemo, useState } from 'react';
import { tenantPath } from '@/lib/public-api';
import type { CompetitionDetail, PlayoffMatch, Playoffs, StandingRow } from './competition-types';
import { ResponsiveGolfLeaderboard, type GolfLeaderboardData } from './golf-leaderboard';

export type OutputKind = 'STANDINGS' | 'GROUPS' | 'BRACKET' | 'SERIES' | 'LEADERBOARD' | 'MEDALS';

export function CompetitionOutputRenderer({ kind, competition, playoffs, tenantSlug, compact = false, golfLeaderboard }: {
  kind: OutputKind; competition: CompetitionDetail; playoffs?: Playoffs | null;
  tenantSlug: string; compact?: boolean; golfLeaderboard?: GolfLeaderboardData | null;
}) {
  const outputRenderers: Record<OutputKind, React.ReactNode> = {
    STANDINGS: <StandingsTable rows={competition.standings} compact={compact} variant={competition.presentation?.standingsVariant} />,
    GROUPS: <GroupStandings rows={competition.standings} variant={competition.presentation?.standingsVariant} />,
    BRACKET: <KnockoutBracket playoffs={playoffs} tenantSlug={tenantSlug} />,
    SERIES: <SeriesView playoffs={playoffs} tenantSlug={tenantSlug} />,
    LEADERBOARD: golfLeaderboard
      ? <ResponsiveGolfLeaderboard data={golfLeaderboard} compact={compact} />
      : <LeaderboardTable rows={competition.leaderboard ?? []} />,
    MEDALS: <MedalTable rows={competition.medals} />,
  };
  return outputRenderers[kind];
}

export function StandingsTable({ rows, compact = false, variant }: { rows: StandingRow[]; compact?: boolean; variant?: string | null }) {
  const [expanded, setExpanded] = useState<string | null>(null);
  if (rows.length === 0) return <Empty message="No standings are published." />;
  const rugby = variant?.toUpperCase().includes('RUGBY');
  const enrichedRows = rows.map(row => ({ row, metadata: standingMetadata(row.metadata) }));
  const hasBonus = rugby && enrichedRows.some(item => item.metadata.bonusPoints !== undefined);
  const hasForm = enrichedRows.some(item => item.metadata.form.length > 0);
  return <div className="overflow-x-auto rounded-2xl border border-[var(--color-border)] bg-[var(--color-surface)]">
    <table className={`w-full text-sm ${compact ? '' : 'md:min-w-[760px]'}`}>
      <thead className="sticky top-0 z-10 bg-gray-950 text-white"><tr>
        <Th>#</Th><Th sticky>Team</Th><Th>P</Th><Th optional>W</Th><Th optional>D</Th><Th optional>L</Th>
        <Th optional>{rugby ? 'PF' : 'For'}</Th><Th optional>{rugby ? 'PA' : 'Against'}</Th><Th>+/-</Th>{hasBonus && <Th optional>BP</Th>}{hasForm && <Th optional>Form</Th>}<Th>Pts</Th>
      </tr></thead>
      <tbody>{enrichedRows.map(({ row, metadata }) => {
        const open = expanded === row.id;
        return <tr key={row.id} onClick={() => setExpanded(open ? null : row.id)} className="cursor-pointer border-t border-[var(--color-border)] text-center hover:bg-[var(--color-surface-muted)]" aria-expanded={open}>
          <td className="p-3 font-bold"><span className={qualificationClass(metadata.qualificationCode)} title={metadata.qualificationCode ? `Qualification: ${metadata.qualificationCode}` : undefined}>{row.position}</span></td>
          <td className="sticky left-0 bg-inherit p-3 text-left font-semibold">{row.participantName}{metadata.pointsDeduction > 0 && <span className="ml-1 text-red-600" title={`${metadata.pointsDeduction} point deduction`}>−{metadata.pointsDeduction}</span>}{open && <span className="mt-1 block text-xs font-normal text-[var(--color-text-muted)] md:hidden">W {row.won} · D {row.drawn} · L {row.lost} · For {row.pointsFor} · Against {row.pointsAgainst}{hasBonus ? ` · BP ${metadata.bonusPoints ?? 0}` : ''}{metadata.form.length ? ` · Form ${metadata.form.join('')}` : ''}</span>}</td>
          <td className="p-3">{row.played}</td><td className="hidden p-3 md:table-cell">{row.won}</td><td className="hidden p-3 md:table-cell">{row.drawn}</td><td className="hidden p-3 md:table-cell">{row.lost}</td><td className="hidden p-3 lg:table-cell">{row.pointsFor}</td><td className="hidden p-3 lg:table-cell">{row.pointsAgainst}</td><td className="p-3">{signed(row.pointDifferential)}</td>{hasBonus && <td className="hidden p-3 md:table-cell">{metadata.bonusPoints ?? 0}</td>}{hasForm && <td className="hidden p-3 md:table-cell"><FormChips values={metadata.form} /></td>}<td className="p-3 font-bold">{row.points}</td>
        </tr>;
      })}</tbody>
    </table>
  </div>;
}

function GroupStandings({ rows, variant }: { rows: StandingRow[]; variant?: string | null }) {
  const groups = useMemo(() => Array.from(rows.reduce((map, row) => {
    const key = row.groupId || 'overall'; const current = map.get(key) ?? { id: key, name: row.groupName ?? 'Overall', rows: [] as StandingRow[] }; current.rows.push(row); map.set(key, current); return map;
  }, new Map<string, { id: string; name: string; rows: StandingRow[] }>()).values()), [rows]);
  const [selected, setSelected] = useState(groups[0]?.id ?? '');
  if (groups.length === 0) return <Empty message="No group standings are published." />;
  const active = groups.find(group => group.id === selected) ?? groups[0];
  return <div>
    <div className="mb-4 flex gap-2 overflow-x-auto lg:hidden">{groups.map(group => <button key={group.id} onClick={() => setSelected(group.id)} className={`shrink-0 rounded-full px-4 py-2 text-sm font-semibold ${active.id === group.id ? 'bg-[var(--brand-primary)] text-white' : 'surface'}`}>{group.name}</button>)}</div>
    <div className="hidden gap-5 lg:grid lg:grid-cols-2">{groups.map(group => <section key={group.id}><h2 className="mb-3 text-lg font-bold">{group.name}</h2><StandingsTable rows={group.rows} compact variant={variant} /></section>)}</div>
    <section className="lg:hidden"><h2 className="mb-3 text-lg font-bold">{active.name}</h2><StandingsTable rows={active.rows} compact variant={variant} /></section>
  </div>;
}

function KnockoutBracket({ playoffs, tenantSlug }: { playoffs?: Playoffs | null; tenantSlug: string }) {
  const [roundId, setRoundId] = useState(playoffs?.rounds[0]?.id ?? '');
  if (!playoffs) return <Empty message="No bracket is published." />;
  const selected = playoffs.rounds.find(round => round.id === roundId) ?? playoffs.rounds[0];
  return <div>
    {playoffs.championName && <p className="mb-4 rounded-xl bg-amber-50 p-4 font-semibold text-amber-950">Champion: {playoffs.championName}</p>}
    <div className="mb-4 flex gap-2 overflow-x-auto lg:hidden">{playoffs.rounds.map(round => <button key={round.id} onClick={() => setRoundId(round.id)} className={`shrink-0 rounded-full px-4 py-2 text-sm font-semibold ${selected?.id === round.id ? 'bg-[var(--brand-primary)] text-white' : 'surface'}`}>{round.name}</button>)}</div>
    <div className="hidden min-w-max grid-flow-col auto-cols-[280px] gap-5 overflow-x-auto pb-4 lg:grid">{playoffs.rounds.map(round => <RoundColumn key={round.id} name={round.name} matches={round.matches} tenantSlug={tenantSlug} />)}</div>
    {selected && <div className="lg:hidden"><RoundColumn name={selected.name} matches={selected.matches} tenantSlug={tenantSlug} /></div>}
  </div>;
}

function RoundColumn({ name, matches, tenantSlug }: { name: string; matches: PlayoffMatch[]; tenantSlug: string }) {
  return <section><h2 className="sticky top-0 z-10 mb-3 bg-[var(--color-background)] py-2 text-lg font-bold">{name}</h2><div className="space-y-3">{matches.map(match => <article key={match.id} className="surface rounded-xl p-4"><p className="text-xs font-bold uppercase text-[var(--color-text-muted)]">Match {match.matchNumber}{match.isThirdPlace ? ' · Third place' : ''}</p><TeamLine name={match.participant1Name} score={match.score1} /><TeamLine name={match.participant2Name} score={match.score2} />{match.contestId && <Link className="mt-3 inline-block text-xs font-semibold text-[var(--brand-primary)]" href={tenantPath(`/contest/${match.contestId}`, tenantSlug)}>Match details →</Link>}</article>)}</div></section>;
}

function SeriesView({ playoffs, tenantSlug }: { playoffs?: Playoffs | null; tenantSlug: string }) {
  const matches = playoffs?.rounds.flatMap(round => round.matches.map(match => ({ round: round.name, match }))).filter(item => item.match.seriesId) ?? [];
  if (matches.length === 0) return <Empty message="No series are published." />;
  return <div className="grid gap-5 lg:grid-cols-2">{matches.map(({ round, match }) => {
    const wins = new Map<string, number>(); match.seriesGames.forEach(game => { if (game.winnerName) wins.set(game.winnerName, (wins.get(game.winnerName) ?? 0) + 1); });
    const orderedWins = Array.from(wins.entries()).sort((a, b) => b[1] - a[1]);
    const heading = orderedWins.length === 0
      ? `${match.participant1Name ?? 'TBD'} vs ${match.participant2Name ?? 'TBD'}`
      : orderedWins.length > 1 && orderedWins[0][1] === orderedWins[1][1]
        ? `Series tied ${orderedWins[0][1]}–${orderedWins[1][1]}`
        : `${orderedWins[0][0]} lead ${orderedWins[0][1]}–${orderedWins[1]?.[1] ?? 0}`;
    return <section key={match.id} className="surface rounded-2xl p-5"><p className="text-xs font-bold uppercase tracking-wide text-[var(--brand-primary)]">{round}</p><h2 className="mt-1 text-xl font-bold">{heading}</h2><p className="mt-1 text-sm muted">First to {Math.floor(match.bestOf / 2) + 1}</p><ol className="mt-4 divide-y divide-[var(--color-border)]">{match.seriesGames.map(game => <li key={game.id} className="grid grid-cols-[4rem_1fr_auto] gap-2 py-3 text-sm"><strong>Game {game.seriesGameNumber}</strong><span>{game.homeParticipantName ?? 'TBD'} vs {game.awayParticipantName ?? 'TBD'}</span><span className="font-bold tabular-nums">{game.homeScore && game.awayScore ? `${game.homeScore}–${game.awayScore}` : game.status}</span></li>)}</ol>{match.contestId && <Link href={tenantPath(`/contest/${match.contestId}`, tenantSlug)} className="mt-3 inline-block text-sm font-semibold text-[var(--brand-primary)]">Series details →</Link>}</section>;
  })}</div>;
}

function LeaderboardTable({ rows }: { rows: NonNullable<CompetitionDetail['leaderboard']> }) {
  if (rows.length === 0) return <Empty message="Leaderboard positions will appear after official scores are published." />;
  return <div className="overflow-hidden rounded-2xl border border-[var(--color-border)] bg-[var(--color-surface)]">{rows.map((row, index) => <div key={`${row.participantId}-${index}`} className="grid grid-cols-[3rem_1fr_auto] items-center border-b border-[var(--color-border)] p-4 last:border-0"><strong>{row.rank ?? index + 1}</strong><span className="font-semibold">{row.participantName}</span><span className="font-bold tabular-nums">{row.scoreDisplay ?? row.score ?? '—'}</span></div>)}</div>;
}

function MedalTable({ rows }: { rows: CompetitionDetail['medals'] }) {
  if (rows.length === 0) return <Empty message="No medal table is published." />;
  return <div className="overflow-x-auto rounded-2xl border border-[var(--color-border)] bg-[var(--color-surface)]"><table className="w-full min-w-[480px] text-sm"><thead className="bg-gray-950 text-white"><tr><Th>#</Th><Th>Participant</Th><Th>Gold</Th><Th>Silver</Th><Th>Bronze</Th><Th>Total</Th></tr></thead><tbody>{rows.map((row, index) => <tr key={row.participantId} className="border-t border-[var(--color-border)]"><td className="p-3 font-bold">{index + 1}</td><td className="p-3 font-semibold">{row.participantName}</td><td className="p-3 text-center">{row.gold}</td><td className="p-3 text-center">{row.silver}</td><td className="p-3 text-center">{row.bronze}</td><td className="p-3 text-center font-bold">{row.total}</td></tr>)}</tbody></table></div>;
}

function TeamLine({ name, score }: { name?: string | null; score?: string | null }) { return <div className="mt-2 flex justify-between gap-3"><strong className="truncate">{name ?? 'TBD'}</strong><span className="font-bold tabular-nums">{score ?? '—'}</span></div>; }
type StandingMetadata = { bonusPoints?: number; form: string[]; qualificationCode: string; pointsDeduction: number };
function standingMetadata(value?: string | null): StandingMetadata {
  if (!value) return { form: [], qualificationCode: '', pointsDeduction: 0 };
  try {
    const parsed = JSON.parse(value) as Record<string, unknown>;
    const rawForm = parsed.form;
    return {
      bonusPoints: typeof parsed.bonusPoints === 'number' ? parsed.bonusPoints : undefined,
      form: Array.isArray(rawForm) ? rawForm.map(String) : typeof rawForm === 'string' ? rawForm.split('').filter(Boolean) : [],
      qualificationCode: typeof parsed.qualificationCode === 'string' ? parsed.qualificationCode.trim() : '',
      pointsDeduction: typeof parsed.pointsDeduction === 'number' ? parsed.pointsDeduction : 0,
    };
  } catch { return { form: [], qualificationCode: '', pointsDeduction: 0 }; }
}
function FormChips({ values }: { values: string[] }) { return <span className="flex justify-center gap-1">{values.map((value, index) => <span key={`${value}-${index}`} className={`inline-grid size-5 place-items-center rounded-full text-[10px] font-bold text-white ${value === 'W' ? 'bg-emerald-600' : value === 'D' ? 'bg-slate-500' : 'bg-red-600'}`}>{value}</span>)}</span>; }
function qualificationClass(code: string) { return code ? 'inline-grid size-7 place-items-center rounded-full border-2 border-emerald-500 bg-emerald-50 text-emerald-900' : ''; }
function Th({ children, optional = false, sticky = false }: { children: React.ReactNode; optional?: boolean; sticky?: boolean }) { return <th className={`p-3 text-left ${optional ? 'hidden md:table-cell' : ''} ${sticky ? 'sticky left-0 bg-gray-950' : ''}`}>{children}</th>; }
function signed(value: number) { return value > 0 ? `+${value}` : value; }
export function Empty({ message }: { message: string }) { return <div className="rounded-2xl border border-dashed border-[var(--color-border)] bg-[var(--color-surface-muted)] p-8 text-center text-[var(--color-text-muted)]">{message}</div>; }
