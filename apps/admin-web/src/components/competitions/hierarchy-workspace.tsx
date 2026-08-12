'use client';

import { useCallback, useEffect, useMemo, useState } from 'react';
import { adminApi } from '@/lib/admin-api';

type Contest = { id: string; name?: string | null; contestShape: string; groupId?: string | null; roundId?: string | null; sortOrder: number };
type Group = { id: string; name: string };
type Round = { id: string; name: string; sortOrder: number };
type Stage = { id: string; name: string; type: string | number; sortOrder: number; groups: Group[]; rounds: Round[]; contests: Contest[] };
type Edition = { id: string; name: string; startDate: string; endDate?: string | null; stages: Stage[] };
type Hierarchy = { id: string; name: string; editions: Edition[] };

const stageTypes = ['Group', 'Knockout', 'GroupAndKnockout', 'League', 'Leaderboard', 'Heat', 'FightCard'];
const contestShapes = [
  ['HeadToHeadTeam', 'Head-to-head team'],
  ['HeadToHeadIndividual', 'Head-to-head individual'],
  ['MultiParticipantRanked', 'Multi-participant ranked'],
  ['Race', 'Race'], ['Heat', 'Heat'], ['BestOfSeries', 'Best-of series'],
  ['FightCardBout', 'Fight-card bout'],
];

export function HierarchyWorkspace({ competitionId, competitionName, onClose }: {
  competitionId: string; competitionName: string; onClose: () => void;
}) {
  const [hierarchy, setHierarchy] = useState<Hierarchy | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);
  const [editionName, setEditionName] = useState('2026 season');
  const [editionStart, setEditionStart] = useState(new Date().toISOString().slice(0, 10));
  const [editionId, setEditionId] = useState('');
  const [stageName, setStageName] = useState('Main stage');
  const [stageType, setStageType] = useState('League');
  const [stageId, setStageId] = useState('');
  const [groupName, setGroupName] = useState('Group A');
  const [roundName, setRoundName] = useState('Round 1');
  const [contestName, setContestName] = useState('Opening contest');
  const [contestShape, setContestShape] = useState('HeadToHeadTeam');
  const [groupId, setGroupId] = useState('');
  const [roundId, setRoundId] = useState('');

  const load = useCallback(async () => {
    setError(null);
    try {
      const value = await adminApi<Hierarchy>(`/api/competitions/${competitionId}/hierarchy`);
      setHierarchy(value);
      setEditionId((current) => current || value.editions[0]?.id || '');
      const firstStage = value.editions.flatMap((edition) => edition.stages)[0];
      setStageId((current) => current || firstStage?.id || '');
    } catch (reason) {
      setError(reason instanceof Error ? reason.message : 'Could not load the competition hierarchy.');
    }
  }, [competitionId]);

  useEffect(() => { void load(); }, [load]);
  const stages = useMemo(() => hierarchy?.editions.flatMap((edition) => edition.stages) ?? [], [hierarchy]);
  const activeStage = stages.find((stage) => stage.id === stageId);

  async function mutate(path: string, body: object) {
    setBusy(true); setError(null);
    try {
      await adminApi(path, { method: 'POST', body: JSON.stringify(body) });
      await load();
    } catch (reason) {
      setError(reason instanceof Error ? reason.message : 'The hierarchy update failed.');
    } finally { setBusy(false); }
  }

  return <section className="rounded-xl border border-gray-200 bg-white p-6">
    <div className="flex items-start justify-between gap-4">
      <div><p className="text-sm font-semibold uppercase tracking-wider text-primary">Competition hierarchy</p><h2 className="text-2xl font-bold">{competitionName}</h2><p className="text-sm text-gray-500">Editions contain stages; each stage can contain groups, rounds, or both.</p></div>
      <button onClick={onClose} className="text-sm font-semibold text-gray-500">Close</button>
    </div>
    {error && <p role="alert" className="mt-4 rounded-lg bg-red-50 p-3 text-sm text-red-700">{error}</p>}
    <div className="mt-5 space-y-3">
      {hierarchy?.editions.map((edition) => <div key={edition.id} className="rounded-lg border p-4">
        <h3 className="font-bold">{edition.name}</h3><p className="text-xs text-gray-500">Starts {edition.startDate.slice(0, 10)}</p>
        <div className="mt-3 grid gap-3 md:grid-cols-2">{edition.stages.map((stage) => <div key={stage.id} className="rounded-lg bg-gray-50 p-3">
          <p className="font-semibold">{stage.sortOrder}. {stage.name} <span className="text-xs font-normal text-gray-500">({String(stage.type)})</span></p>
          <p className="mt-1 text-xs text-gray-600">Groups: {stage.groups.map((item) => item.name).join(', ') || 'none'} · Rounds: {stage.rounds.map((item) => item.name).join(', ') || 'none'}</p>
          <ul className="mt-2 space-y-1 text-xs">{stage.contests.map((contest) => <li key={contest.id}>{contest.sortOrder}. {contest.name || contest.contestShape}</li>)}</ul>
        </div>)}</div>
      </div>)}
      {hierarchy && hierarchy.editions.length === 0 && <p className="rounded-lg bg-gray-50 p-4 text-sm text-gray-500">No editions yet. Add the first season below.</p>}
    </div>

    <div className="mt-6 grid gap-5 border-t pt-5 lg:grid-cols-3">
      <Panel title="Add edition">
        <Field value={editionName} onChange={setEditionName} placeholder="Edition / season name" />
        <Field type="date" value={editionStart} onChange={setEditionStart} placeholder="Start date" />
        <Action disabled={busy || !editionName || !editionStart} onClick={() => mutate(`/api/competitions/${competitionId}/editions`, { name: editionName, startDate: `${editionStart}T00:00:00Z` })}>Add edition</Action>
      </Panel>
      <Panel title="Add stage">
        <Select value={editionId} onChange={setEditionId} options={(hierarchy?.editions ?? []).map((item) => [item.id, item.name])} placeholder="Select edition" />
        <Field value={stageName} onChange={setStageName} placeholder="Stage name" />
        <Select value={stageType} onChange={setStageType} options={stageTypes.map((item) => [item, item])} />
        <Action disabled={busy || !editionId || !stageName} onClick={() => mutate(`/api/competitions/${competitionId}/editions/${editionId}/stages`, { name: stageName, type: stageType, sortOrder: (hierarchy?.editions.find((item) => item.id === editionId)?.stages.length ?? 0) + 1 })}>Add stage</Action>
      </Panel>
      <Panel title="Stage structure">
        <Select value={stageId} onChange={(value) => { setStageId(value); setGroupId(''); setRoundId(''); }} options={stages.map((item) => [item.id, item.name])} placeholder="Select stage" />
        <div className="flex gap-2"><Field value={groupName} onChange={setGroupName} placeholder="Group name" /><Action disabled={busy || !stageId || !groupName} onClick={() => mutate(`/api/competitions/${competitionId}/stages/${stageId}/groups`, { name: groupName })}>Add</Action></div>
        <div className="flex gap-2"><Field value={roundName} onChange={setRoundName} placeholder="Round name" /><Action disabled={busy || !stageId || !roundName} onClick={() => mutate(`/api/competitions/${competitionId}/stages/${stageId}/rounds`, { name: roundName, sortOrder: (activeStage?.rounds.length ?? 0) + 1 })}>Add</Action></div>
      </Panel>
    </div>

    <div className="mt-5 rounded-lg border p-4">
      <h3 className="font-bold">Create manual contest</h3><p className="text-sm text-gray-500">Generated fixture tools remain available in the sport and playoff workflows.</p>
      <div className="mt-3 grid gap-3 md:grid-cols-2 lg:grid-cols-5">
        <Field value={contestName} onChange={setContestName} placeholder="Contest name" />
        <Select value={contestShape} onChange={setContestShape} options={contestShapes} />
        <Select value={groupId} onChange={setGroupId} options={(activeStage?.groups ?? []).map((item) => [item.id, item.name])} placeholder="No group" />
        <Select value={roundId} onChange={setRoundId} options={(activeStage?.rounds ?? []).map((item) => [item.id, item.name])} placeholder="No round" />
        <Action disabled={busy || !stageId || !contestName || !contestShape} onClick={() => mutate(`/api/competitions/${competitionId}/stages/${stageId}/contests`, { name: contestName, contestShape, sortOrder: (activeStage?.contests.length ?? 0) + 1, groupId: groupId || null, roundId: roundId || null })}>{busy ? 'Saving…' : 'Create contest'}</Action>
      </div>
    </div>
  </section>;
}

function Panel({ title, children }: { title: string; children: React.ReactNode }) { return <div className="space-y-2"><h3 className="font-bold">{title}</h3>{children}</div>; }
function Field({ value, onChange, placeholder, type = 'text' }: { value: string; onChange: (value: string) => void; placeholder: string; type?: string }) { return <input type={type} value={value} onChange={(event) => onChange(event.target.value)} placeholder={placeholder} className="w-full rounded-lg border px-3 py-2 text-sm" />; }
function Select({ value, onChange, options, placeholder }: { value: string; onChange: (value: string) => void; options: string[][]; placeholder?: string }) { return <select value={value} onChange={(event) => onChange(event.target.value)} className="w-full rounded-lg border bg-white px-3 py-2 text-sm">{placeholder && <option value="">{placeholder}</option>}{options.map(([id, name]) => <option key={id} value={id}>{name}</option>)}</select>; }
function Action({ children, onClick, disabled }: { children: React.ReactNode; onClick: () => void; disabled?: boolean }) { return <button type="button" disabled={disabled} onClick={onClick} className="rounded-lg bg-primary px-3 py-2 text-sm font-semibold text-white disabled:opacity-50">{children}</button>; }
