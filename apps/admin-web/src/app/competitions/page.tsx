'use client';

import Link from 'next/link';
import { CheckCircle2, Layers3, Plus, Search, Settings2, Trophy } from 'lucide-react';
import { useEffect, useMemo, useState } from 'react';
import { adminApi, getEffectiveTenantId } from '@/lib/admin-api';
import { HierarchyWorkspace } from '@/components/competitions/hierarchy-workspace';
import { PresentationSettingsPanel } from '@/components/competitions/presentation-settings';
import { DataTable, type DataTableColumn } from '@/components/admin/data-table';
import { ResourceDialog } from '@/components/admin/resource-dialog';
import { Button } from '@/components/ui/button';

type Competition = {
  id: string;
  tenantId: string;
  name: string;
  description?: string | null;
  sportId: string;
  status: string | number;
  publishedAt?: string | null;
};
type Bracket = {
  id: string;
  name: string;
  status: string | number;
  size: number;
  isSeeded: boolean;
  drawSeed?: number | null;
  championId?: string | null;
  rounds: Array<{
    id: string;
    roundNumber: number;
    name: string;
    matches: Array<{
      id: string;
      matchNumber: number;
      participant1Id?: string | null;
      participant2Id?: string | null;
      winnerId?: string | null;
      status: string | number;
      isAggregateTie: boolean;
      isThirdPlace: boolean;
      score1?: string | null;
      score2?: string | null;
    }>;
  }>;
};
type BracketOverride = {
  id: string;
  bracketMatchId: string;
  winnerId: string;
  reason: string;
  status: string | number;
  requestedAt: string;
  reviewedAt?: string | null;
  reviewNotes?: string | null;
};

export default function CompetitionsPage() {
  const [competitions, setCompetitions] = useState<Competition[]>([]);
  const [search, setSearch] = useState('');
  const [statusFilter, setStatusFilter] = useState('all');
  const [tenantId, setTenantId] = useState('');
  const [error, setError] = useState<string | null>(null);
  const [loading, setLoading] = useState(false);
  const [selectedCompetition, setSelectedCompetition] = useState<Competition | null>(null);
  const [viewingCompetition, setViewingCompetition] = useState<Competition | null>(null);
  const [hierarchyCompetition, setHierarchyCompetition] = useState<Competition | null>(null);
  const [presentationCompetition, setPresentationCompetition] = useState<Competition | null>(null);
  const [bracket, setBracket] = useState<Bracket | null>(null);
  const [overrideHistory, setOverrideHistory] = useState<BracketOverride[]>([]);
  const [playoffError, setPlayoffError] = useState<string | null>(null);
  const [qualifierCount, setQualifierCount] = useState(4);
  const [legsPerTie, setLegsPerTie] = useState(1);
  const [includeThirdPlace, setIncludeThirdPlace] = useState(false);
  const [randomDraw, setRandomDraw] = useState(false);
  const [creatingPlayoffs, setCreatingPlayoffs] = useState(false);
  const [groupsCount, setGroupsCount] = useState(2);
  const [teamsPerGroup, setTeamsPerGroup] = useState(4);
  const [qualifiersPerGroup, setQualifiersPerGroup] = useState(1);
  const [bestRunnerUpCount, setBestRunnerUpCount] = useState(1);
  const [creatingGroupPlayoffs, setCreatingGroupPlayoffs] = useState(false);
  const [semifinalBestOf, setSemifinalBestOf] = useState(3);
  const [finalBestOf, setFinalBestOf] = useState(5);
  const [semifinalHomeSequence, setSemifinalHomeSequence] = useState('H,A,H');
  const [finalHomeSequence, setFinalHomeSequence] = useState('H,H,A,A,H');
  const [creatingSeriesPlayoffs, setCreatingSeriesPlayoffs] = useState(false);

  useEffect(() => {
    const requestedSearch = new URLSearchParams(window.location.search).get('search');
    if (requestedSearch?.trim()) queueMicrotask(() => setSearch(requestedSearch.trim()));
    const updateSearch = (event: Event) => {
      const query = (event as CustomEvent<string>).detail;
      if (query?.trim()) setSearch(query.trim());
    };
    window.addEventListener('admin:competition-search', updateSearch);
    return () => window.removeEventListener('admin:competition-search', updateSearch);
  }, []);

  async function load() {
    if (!tenantId) return;
    setLoading(true);
    setError(null);
    try {
      setCompetitions(
        await adminApi<Competition[]>(`/api/competitions?tenantId=${tenantId}`)
      );
    } catch (reason) {
      setError(reason instanceof Error ? reason.message : 'Could not load competitions.');
    } finally {
      setLoading(false);
    }
  }

  async function loadPlayoffs(competition: Competition) {
    setSelectedCompetition(competition);
    setPlayoffError(null);
    try {
      const [playoffBracket, history] = await Promise.all([
        adminApi<Bracket>(`/api/competitions/${competition.id}/playoffs`),
        adminApi<BracketOverride[]>(`/api/competitions/${competition.id}/playoffs/overrides`),
      ]);
      setBracket(playoffBracket);
      setOverrideHistory(history);
    } catch (reason) {
      setBracket(null);
      setOverrideHistory([]);
      setPlayoffError(reason instanceof Error ? reason.message : 'Could not load this playoff bracket.');
    }
  }

  async function createPlayoffs() {
    if (!selectedCompetition) return;
    setCreatingPlayoffs(true);
    setPlayoffError(null);
    try {
      await adminApi(`/api/competitions/${selectedCompetition.id}/playoffs/knockout`, {
        method: 'POST',
        body: JSON.stringify({
          bracketName: `${selectedCompetition.name} playoffs`,
          stageName: 'Knockout',
          startTimeUtc: new Date(Date.now() + 24 * 60 * 60 * 1000).toISOString(),
          qualifierCount,
          legsPerTie,
          includeThirdPlace,
          randomDraw,
        }),
      });
      await loadPlayoffs(selectedCompetition);
    } catch (reason) {
      setPlayoffError(reason instanceof Error ? reason.message : 'Could not create the playoff bracket.');
    } finally {
      setCreatingPlayoffs(false);
    }
  }

  async function createGroupPlayoffs() {
    if (!selectedCompetition) return;
    setCreatingGroupPlayoffs(true);
    setPlayoffError(null);
    try {
      await adminApi(`/api/competitions/${selectedCompetition.id}/fixtures/group-knockout`, {
        method: 'POST',
        body: JSON.stringify({
          editionName: new Date().getUTCFullYear().toString(),
          groupStageName: 'Group stage',
          knockoutStageName: 'Knockout stage',
          bracketName: `${selectedCompetition.name} finals`,
          startTimeUtc: new Date(Date.now() + 24 * 60 * 60 * 1000).toISOString(),
          groupsCount,
          teamsPerGroup,
          qualifiersPerGroup,
          bestRunnerUpCount,
          tieBreakers: ['points', 'pointDifferential', 'pointsFor', 'wins'],
        }),
      });
      await loadPlayoffs(selectedCompetition);
    } catch (reason) {
      setPlayoffError(reason instanceof Error ? reason.message : 'Could not create the group tournament.');
    } finally {
      setCreatingGroupPlayoffs(false);
    }
  }

  async function createSeriesPlayoffs() {
    if (!selectedCompetition) return;
    setCreatingSeriesPlayoffs(true);
    setPlayoffError(null);
    try {
      await adminApi(`/api/competitions/${selectedCompetition.id}/playoffs/top-four`, {
        method: 'POST',
        body: JSON.stringify({
          bracketName: `${selectedCompetition.name} series playoffs`,
          stageName: 'Series playoffs',
          startTimeUtc: new Date(Date.now() + 24 * 60 * 60 * 1000).toISOString(),
          semifinalBestOf,
          finalBestOf,
          semifinalHomeSequence,
          finalHomeSequence,
        }),
      });
      await loadPlayoffs(selectedCompetition);
    } catch (reason) {
      setPlayoffError(reason instanceof Error ? reason.message : 'Could not create the series playoffs.');
    } finally {
      setCreatingSeriesPlayoffs(false);
    }
  }

  useEffect(() => {
    const effectiveTenantId = getEffectiveTenantId();
    if (effectiveTenantId) setTenantId(effectiveTenantId);
    else setError('Select a tenant workspace before managing competitions.');
  }, []);

  useEffect(() => {
    void load();
    // load only when the tenant scope changes
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [tenantId]);

  const statusOptions = useMemo(
    () => Array.from(new Set(competitions.map((competition) => String(competition.status)))).sort(),
    [competitions]
  );

  const filtered = useMemo(
    () =>
      competitions.filter((competition) => {
        const matchesSearch = competition.name.toLowerCase().includes(search.toLowerCase());
        const matchesStatus =
          statusFilter === 'all' || String(competition.status) === statusFilter;
        return matchesSearch && matchesStatus;
      }),
    [competitions, search, statusFilter]
  );

  const representedSports = useMemo(
    () => new Set(competitions.map((competition) => competition.sportId)).size,
    [competitions]
  );
  const columns: DataTableColumn<Competition>[] = [
    { key: 'name', header: 'Competition', render: item => <div><p className="font-semibold text-gray-950">{item.name}</p><p className="max-w-md text-xs text-gray-500">{item.description ?? 'No description'}</p></div> },
    { key: 'sport', header: 'Sport', render: item => <span className="font-mono text-xs">{item.sportId.slice(0, 8)}</span> },
    { key: 'status', header: 'Status', render: item => <span className="rounded-full bg-gray-100 px-2 py-1 text-xs font-semibold text-gray-700">{String(item.status)}</span> },
    { key: 'publication', header: 'Publication', render: item => item.publishedAt ? <span className="inline-flex items-center gap-1 text-emerald-700"><CheckCircle2 className="h-4 w-4" />Published</span> : <span className="text-gray-500">Draft</span> },
    { key: 'actions', header: <span className="sr-only">Actions</span>, className: 'text-right', render: item => <div className="flex justify-end gap-1" onClick={event => event.stopPropagation()}><Button size="sm" variant="ghost" onClick={() => setHierarchyCompetition(item)}><Layers3 className="mr-1 h-4 w-4" />Structure</Button><Button size="sm" variant="ghost" onClick={() => setPresentationCompetition(item)}><Settings2 className="mr-1 h-4 w-4" />Presentation</Button><Button size="sm" variant="ghost" onClick={() => void loadPlayoffs(item)}><Trophy className="mr-1 h-4 w-4" />Playoffs</Button></div> },
  ];

  const publishedCount = useMemo(
    () => competitions.filter((competition) => Boolean(competition.publishedAt)).length,
    [competitions]
  );

  return (
    <div className="space-y-6">
      <section className="overflow-hidden rounded-2xl border border-gray-200 bg-white shadow-sm">
        <div className="border-b border-gray-100 bg-gradient-to-br from-gray-950 via-gray-900 to-gray-800 px-6 py-7 text-white lg:px-8">
          <div className="flex flex-wrap items-end justify-between gap-5">
            <div className="max-w-2xl">
              <p className="text-xs font-bold uppercase tracking-[0.22em] text-white/60">Tenant workspace</p>
              <h1 className="mt-2 text-3xl font-bold tracking-tight sm:text-4xl">Competitions</h1>
              <p className="mt-2 max-w-xl text-sm leading-6 text-white/70">
                Operate competitions, tournament structures, presentation settings, and playoff formats from one workspace.
              </p>
            </div>
            <Link
              href="/competitions/new"
              className="inline-flex items-center gap-2 rounded-xl bg-primary px-4 py-2.5 text-sm font-semibold text-white shadow-sm transition hover:opacity-90"
            >
              <Plus className="h-4 w-4" />
              Create competition
            </Link>
          </div>
        </div>

        <div className="grid gap-px bg-gray-200 sm:grid-cols-2 xl:grid-cols-4">
          <MetricCard label="Competitions" value={competitions.length} hint="Tenant total" />
          <MetricCard label="Published" value={publishedCount} hint="Visible publicly" />
          <MetricCard label="Sports" value={representedSports} hint="Represented here" />
          <MetricCard label="In view" value={filtered.length} hint="After filters" />
        </div>
      </section>

      <section className="rounded-2xl border border-gray-200 bg-white p-4 shadow-sm">
        <div className="grid gap-3 lg:grid-cols-[minmax(0,1fr)_220px_320px_auto]">
          <label className="relative">
            <span className="sr-only">Search competitions</span>
            <Search className="absolute left-3 top-3 h-4 w-4 text-gray-400" />
            <input
              value={search}
              onChange={(event) => setSearch(event.target.value)}
              placeholder="Search competitions"
              className="w-full rounded-xl border border-gray-200 bg-gray-50 py-2.5 pl-10 pr-3 text-sm outline-none transition focus:border-primary focus:bg-white"
            />
          </label>

          <select
            aria-label="Competition status"
            value={statusFilter}
            onChange={(event) => setStatusFilter(event.target.value)}
            className="rounded-xl border border-gray-200 bg-gray-50 px-3 py-2.5 text-sm outline-none focus:border-primary focus:bg-white"
          >
            <option value="all">All statuses</option>
            {statusOptions.map((status) => (
              <option key={status} value={status}>{status}</option>
            ))}
          </select>

          <input
            value={tenantId}
            onChange={(event) => setTenantId(event.target.value)}
            placeholder="Tenant ID"
            className="rounded-xl border border-gray-200 bg-gray-50 px-3 py-2.5 text-sm outline-none transition focus:border-primary focus:bg-white"
          />

          <button
            onClick={() => void load()}
            className="rounded-xl border border-gray-200 bg-white px-4 py-2.5 text-sm font-semibold text-gray-700 transition hover:bg-gray-50"
          >
            Refresh
          </button>
        </div>
      </section>

      {loading && <State>Loading competitions…</State>}
      {error && <State tone="error">{error} <Link href="/login" className="font-semibold underline">Sign in</Link></State>}
      {!loading && !error && tenantId && <DataTable caption="Competitions" columns={columns} rows={filtered} rowKey={item => item.id} onRowClick={setViewingCompetition} emptyMessage="No competitions found for this tenant." />}

      {viewingCompetition && <ResourceDialog title={viewingCompetition.name} description={`Competition ${viewingCompetition.id}`} onClose={() => setViewingCompetition(null)}><div className="grid gap-4 sm:grid-cols-3"><div><p className="text-xs font-semibold uppercase text-gray-500">Status</p><p className="mt-1 font-semibold">{String(viewingCompetition.status)}</p></div><div><p className="text-xs font-semibold uppercase text-gray-500">Publication</p><p className="mt-1">{viewingCompetition.publishedAt ? 'Published' : 'Draft'}</p></div><div><p className="text-xs font-semibold uppercase text-gray-500">Sport ID</p><p className="mt-1 font-mono text-xs">{viewingCompetition.sportId}</p></div></div><p className="mt-5 text-sm text-gray-600">{viewingCompetition.description ?? 'No description has been added.'}</p><div className="mt-6 flex flex-wrap justify-end gap-2"><Button variant="outline" onClick={() => { setViewingCompetition(null); setHierarchyCompetition(viewingCompetition); }}><Layers3 className="mr-2 h-4 w-4" />Structure</Button><Button variant="outline" onClick={() => { setViewingCompetition(null); setPresentationCompetition(viewingCompetition); }}><Settings2 className="mr-2 h-4 w-4" />Presentation</Button><Button onClick={() => { setViewingCompetition(null); void loadPlayoffs(viewingCompetition); }}><Trophy className="mr-2 h-4 w-4" />Playoffs</Button></div></ResourceDialog>}
      {hierarchyCompetition && (
        <HierarchyWorkspace
          competitionId={hierarchyCompetition.id}
          competitionName={hierarchyCompetition.name}
          onClose={() => setHierarchyCompetition(null)}
        />
      )}
      {presentationCompetition && (
        <PresentationSettingsPanel
          competitionId={presentationCompetition.id}
          competitionName={presentationCompetition.name}
          onClose={() => setPresentationCompetition(null)}
        />
      )}
      {selectedCompetition && (
        <section className="rounded-xl border border-gray-200 bg-white p-6">
          <div className="flex items-start justify-between gap-4">
            <div>
              <p className="text-sm font-semibold uppercase tracking-wider text-primary">Playoff history</p>
              <h2 className="text-2xl font-bold">{selectedCompetition.name}</h2>
            </div>
            <button onClick={() => setSelectedCompetition(null)} className="text-sm font-semibold text-gray-500">Close</button>
          </div>
          {playoffError && <p className="mt-4 rounded-lg bg-amber-50 p-3 text-sm text-amber-800">{playoffError}</p>}
          {!bracket && (
            <div className="mt-5 rounded-xl border border-gray-200 bg-gray-50 p-4">
              <h3 className="font-bold text-gray-900">Create knockout bracket</h3>
              <p className="mt-1 text-sm text-gray-500">Uses the highest-ranked standings entries and advances any first-round byes automatically.</p>
              <div className="mt-4 grid gap-4 md:grid-cols-2 xl:grid-cols-4">
                <label className="text-sm font-medium text-gray-700">
                  Qualifiers (2–64)
                  <input type="number" min={2} max={64} value={qualifierCount} onChange={(event) => setQualifierCount(Number(event.target.value))} className="mt-1 w-full rounded-lg border border-gray-200 bg-white px-3 py-2" />
                </label>
                <label className="text-sm font-medium text-gray-700">
                  Tie format
                  <select value={legsPerTie} onChange={(event) => setLegsPerTie(Number(event.target.value))} className="mt-1 w-full rounded-lg border border-gray-200 bg-white px-3 py-2">
                    <option value={1}>Single leg</option>
                    <option value={2}>Two-leg aggregate</option>
                  </select>
                </label>
                <label className="flex items-center gap-2 self-end rounded-lg border border-gray-200 bg-white px-3 py-2 text-sm font-medium">
                  <input type="checkbox" checked={includeThirdPlace} onChange={(event) => setIncludeThirdPlace(event.target.checked)} /> Third-place playoff
                </label>
                <label className="flex items-center gap-2 self-end rounded-lg border border-gray-200 bg-white px-3 py-2 text-sm font-medium">
                  <input type="checkbox" checked={randomDraw} onChange={(event) => setRandomDraw(event.target.checked)} /> Reproducible random draw
                </label>
              </div>
              <button disabled={creatingPlayoffs || qualifierCount < 2 || qualifierCount > 64} onClick={() => void createPlayoffs()} className="mt-4 rounded-lg bg-primary px-4 py-2 text-sm font-semibold text-white disabled:opacity-50">
                {creatingPlayoffs ? 'Creating…' : 'Create bracket'}
              </button>
              <div className="mt-6 border-t border-gray-200 pt-5">
                <h3 className="font-bold text-gray-900">Create top-four series playoffs</h3>
                <p className="mt-1 text-sm text-gray-500">Uses the top four standings. H is the higher seed at home and A is the opponent at home.</p>
                <div className="mt-4 grid gap-4 md:grid-cols-2 xl:grid-cols-4">
                  <SeriesLengthField label="Semifinals" value={semifinalBestOf} onChange={(value) => { setSemifinalBestOf(value); setSemifinalHomeSequence(defaultHomeSequence(value)); }} />
                  <label className="text-sm font-medium text-gray-700">
                    Semifinal home sequence
                    <input value={semifinalHomeSequence} onChange={(event) => setSemifinalHomeSequence(event.target.value)} className="mt-1 w-full rounded-lg border border-gray-200 bg-white px-3 py-2" />
                  </label>
                  <SeriesLengthField label="Final" value={finalBestOf} onChange={(value) => { setFinalBestOf(value); setFinalHomeSequence(defaultHomeSequence(value)); }} />
                  <label className="text-sm font-medium text-gray-700">
                    Final home sequence
                    <input value={finalHomeSequence} onChange={(event) => setFinalHomeSequence(event.target.value)} className="mt-1 w-full rounded-lg border border-gray-200 bg-white px-3 py-2" />
                  </label>
                </div>
                <button disabled={creatingSeriesPlayoffs} onClick={() => void createSeriesPlayoffs()} className="mt-4 rounded-lg border border-primary px-4 py-2 text-sm font-semibold text-primary disabled:opacity-50">
                  {creatingSeriesPlayoffs ? 'Creating…' : 'Create series playoffs'}
                </button>
              </div>
              <div className="mt-6 border-t border-gray-200 pt-5">
                <h3 className="font-bold text-gray-900">Create group stage and pending bracket</h3>
                <p className="mt-1 text-sm text-gray-500">Requires exactly the configured number of approved registrations. Qualification is applied after every group result is approved.</p>
                <div className="mt-4 grid gap-4 md:grid-cols-2 xl:grid-cols-4">
                  <NumberField label="Groups" value={groupsCount} min={2} max={16} onChange={setGroupsCount} />
                  <NumberField label="Teams per group" value={teamsPerGroup} min={2} max={16} onChange={setTeamsPerGroup} />
                  <NumberField label="Automatic qualifiers/group" value={qualifiersPerGroup} min={1} max={Math.max(1, teamsPerGroup - 1)} onChange={setQualifiersPerGroup} />
                  <NumberField label="Best runners-up" value={bestRunnerUpCount} min={0} max={groupsCount} onChange={setBestRunnerUpCount} />
                </div>
                <p className="mt-3 text-xs text-gray-500">Tie-breakers: points, point differential, points scored, then wins.</p>
                <button disabled={creatingGroupPlayoffs || qualifiersPerGroup >= teamsPerGroup} onClick={() => void createGroupPlayoffs()} className="mt-4 rounded-lg border border-primary px-4 py-2 text-sm font-semibold text-primary disabled:opacity-50">
                  {creatingGroupPlayoffs ? 'Creating…' : 'Create group tournament'}
                </button>
              </div>
            </div>
          )}
          {bracket && (
            <div className="mt-5 space-y-5">
              <p className="text-sm text-gray-500">
                {bracket.size}-slot {bracket.isSeeded ? 'seeded' : 'random'} bracket
                {bracket.drawSeed != null ? ` · draw seed ${bracket.drawSeed}` : ''}
              </p>
              {bracket.rounds.sort((a, b) => a.roundNumber - b.roundNumber).map(round => (
                <div key={round.id}>
                  <h3 className="font-bold">{round.name}</h3>
                  <div className="mt-2 grid gap-3 md:grid-cols-2">
                    {round.matches.sort((a, b) => a.matchNumber - b.matchNumber).map(match => (
                      <div key={match.id} className="rounded-lg border p-3 text-sm">
                        <div className="flex flex-wrap gap-2 text-xs font-semibold uppercase text-gray-500">
                          <span>Match {match.matchNumber}</span>
                          {match.isAggregateTie && <span className="text-primary">Two-leg aggregate</span>}
                          {match.isThirdPlace && <span className="text-amber-700">Third place</span>}
                        </div>
                        <p className="mt-2 break-all">{match.participant1Id ?? 'Pending'} {match.score1 ?? ''}</p>
                        <p className="break-all">{match.participant2Id ?? 'Pending'} {match.score2 ?? ''}</p>
                        <p className="mt-2 font-semibold">Status: {String(match.status)}</p>
                        {match.winnerId && <p className="break-all text-green-700">Winner: {match.winnerId}</p>}
                      </div>
                    ))}
                  </div>
                </div>
              ))}
              <div>
                <h3 className="font-bold">Approved override history</h3>
                {overrideHistory.length === 0 ? (
                  <p className="mt-2 text-sm text-gray-500">No manual advancement decisions.</p>
                ) : (
                  <div className="mt-2 space-y-2">
                    {overrideHistory.map(item => (
                      <div key={item.id} className="rounded-lg border px-3 py-2 text-sm">
                        <p className="font-semibold">{String(item.status)} — {item.reason}</p>
                        <p className="break-all text-gray-500">Winner {item.winnerId}</p>
                        {item.reviewNotes && <p className="text-gray-600">{item.reviewNotes}</p>}
                      </div>
                    ))}
                  </div>
                )}
              </div>
            </div>
          )}
        </section>
      )}
    </div>
  );
}

function MetricCard({
  label,
  value,
  hint,
}: {
  label: string;
  value: number;
  hint: string;
}) {
  return (
    <div className="bg-white px-5 py-4">
      <p className="text-xs font-semibold uppercase tracking-wider text-gray-400">{label}</p>
      <div className="mt-1 flex items-end justify-between gap-3">
        <strong className="text-2xl font-bold tracking-tight text-gray-950">{value}</strong>
        <span className="text-xs text-gray-500">{hint}</span>
      </div>
    </div>
  );
}

function State({ children, tone = 'neutral' }: { children: React.ReactNode; tone?: 'neutral' | 'error' }) {
  return <div className={`rounded-xl border p-5 ${tone === 'error' ? 'border-red-200 bg-red-50 text-red-700' : 'border-gray-200 bg-white text-gray-500'}`}>{children}</div>;
}

function NumberField({ label, value, min, max, onChange }: { label: string; value: number; min: number; max: number; onChange: (value: number) => void }) {
  return (
    <label className="text-sm font-medium text-gray-700">
      {label}
      <input type="number" min={min} max={max} value={value} onChange={(event) => onChange(Number(event.target.value))} className="mt-1 w-full rounded-lg border border-gray-200 bg-white px-3 py-2" />
    </label>
  );
}

function SeriesLengthField({ label, value, onChange }: { label: string; value: number; onChange: (value: number) => void }) {
  return (
    <label className="text-sm font-medium text-gray-700">
      {label}
      <select value={value} onChange={(event) => onChange(Number(event.target.value))} className="mt-1 w-full rounded-lg border border-gray-200 bg-white px-3 py-2">
        <option value={3}>Best of 3</option>
        <option value={5}>Best of 5</option>
        <option value={7}>Best of 7</option>
      </select>
    </label>
  );
}

function defaultHomeSequence(bestOf: number) {
  if (bestOf === 5) return 'H,H,A,A,H';
  if (bestOf === 7) return 'H,H,A,A,H,A,H';
  return 'H,A,H';
}
