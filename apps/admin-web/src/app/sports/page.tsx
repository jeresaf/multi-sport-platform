'use client';

import { FormEvent, useEffect, useMemo, useState } from 'react';
import { adminApi, getStoredUser, hasAnyPermission, isPlatformOperator } from '@/lib/admin-api';

type Tenant = { id: string; name: string };
type SportVersion = {
  id: string;
  versionNumber: number;
  name: string;
  isPublished: boolean;
  createdAt: string;
  publishedAt?: string | null;
};
type SportDefinitionVersion = SportVersion & {
  sportId: string;
  createdBy: string;
  contestShape: string;
  participantType: string;
  segmentDefinitions: string;
  eventDefinitions: string;
  metricDefinitions: string;
  resultMethods: string;
  rankingRules: string;
  displayConfiguration: string;
  validationConfiguration: string;
};
type Sport = {
  id: string;
  code: string;
  name: string;
  description?: string | null;
  isActive: boolean;
  icon?: string | null;
  sportPackageVersion: number;
  supportedParticipantShapes: string[];
  versions: SportVersion[];
};
type TenantSport = { sport: Sport; enabled: boolean };
type SportForm = {
  code: string;
  name: string;
  description: string;
  icon: string;
  participantShapes: string;
  isActive: boolean;
};

const emptyForm: SportForm = {
  code: '', name: '', description: '', icon: '', participantShapes: 'team', isActive: true,
};

export default function SportsPage() {
  const user = getStoredUser();
  const platform = isPlatformOperator(user);
  const canManage = hasAnyPermission(user, ['sports.create', 'sports.manage']);
  const [sports, setSports] = useState<Sport[]>([]);
  const [tenants, setTenants] = useState<Tenant[]>([]);
  const [tenantId, setTenantId] = useState(user?.tenantId ?? '');
  const [tenantSports, setTenantSports] = useState<TenantSport[]>([]);
  const [editingId, setEditingId] = useState<string | null>(null);
  const [form, setForm] = useState<SportForm>(emptyForm);
  const [saving, setSaving] = useState(false);
  const [busySportId, setBusySportId] = useState<string | null>(null);
  const [versionSportId, setVersionSportId] = useState<string | null>(null);
  const [selectedVersionId, setSelectedVersionId] = useState('');
  const [version, setVersion] = useState<SportDefinitionVersion | null>(null);
  const [newVersionName, setNewVersionName] = useState('');
  const [versionBusy, setVersionBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [notice, setNotice] = useState<string | null>(null);

  const enabledById = useMemo(
    () => new Map(tenantSports.map(item => [item.sport.id, item.enabled])),
    [tenantSports],
  );

  async function loadCatalogue() {
    const items = await adminApi<Sport[]>('/api/sports');
    setSports(items.sort((a, b) => a.name.localeCompare(b.name)));
  }

  useEffect(() => {
    setError(null);
    Promise.all([
      loadCatalogue(),
      adminApi<Tenant[]>('/api/tenants').then(items => {
        setTenants(items);
        if (!tenantId) setTenantId(items[0]?.id ?? '');
      }),
    ]).catch(reason => setError(reason instanceof Error ? reason.message : 'Unable to load sports.'));
    // tenantId is initialized once from the signed-in identity.
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);

  useEffect(() => {
    if (!tenantId) {
      setTenantSports([]);
      return;
    }
    adminApi<TenantSport[]>(`/api/sports/tenant/${tenantId}`)
      .then(setTenantSports)
      .catch(reason => setError(reason instanceof Error ? reason.message : 'Unable to load tenant sports.'));
  }, [tenantId]);

  function beginEdit(sport: Sport) {
    setEditingId(sport.id);
    setForm({
      code: sport.code,
      name: sport.name,
      description: sport.description ?? '',
      icon: sport.icon ?? '',
      participantShapes: sport.supportedParticipantShapes.join(', '),
      isActive: sport.isActive,
    });
    setNotice(null);
    setError(null);
  }

  function resetForm() {
    setEditingId(null);
    setForm(emptyForm);
  }

  async function saveSport(event: FormEvent) {
    event.preventDefault();
    if (!platform) return;
    setSaving(true);
    setError(null);
    setNotice(null);
    const supportedParticipantShapes = form.participantShapes
      .split(',').map(item => item.trim()).filter(Boolean);
    try {
      if (editingId) {
        await adminApi<Sport>(`/api/sports/${editingId}`, {
          method: 'PUT',
          body: JSON.stringify({
            name: form.name,
            description: form.description || null,
            icon: form.icon || null,
            supportedParticipantShapes,
            isActive: form.isActive,
          }),
        });
        setNotice('Sport updated.');
      } else {
        await adminApi<Sport>('/api/sports', {
          method: 'POST',
          body: JSON.stringify({
            code: form.code,
            name: form.name,
            description: form.description || null,
            icon: form.icon || null,
            supportedParticipantShapes,
          }),
        });
        setNotice('Sport created.');
      }
      resetForm();
      await loadCatalogue();
      if (tenantId) setTenantSports(await adminApi<TenantSport[]>(`/api/sports/tenant/${tenantId}`));
    } catch (reason) {
      setError(reason instanceof Error ? reason.message : 'Unable to save the sport.');
    } finally {
      setSaving(false);
    }
  }

  async function setEnabled(sport: Sport, enabled: boolean) {
    if (!tenantId) return;
    setBusySportId(sport.id);
    setError(null);
    setNotice(null);
    try {
      await adminApi<TenantSport>(`/api/sports/tenant/${tenantId}/${sport.id}`, {
        method: 'PUT', body: JSON.stringify({ enabled }),
      });
      setTenantSports(await adminApi<TenantSport[]>(`/api/sports/tenant/${tenantId}`));
      setNotice(`${sport.name} ${enabled ? 'enabled' : 'disabled'} for the tenant.`);
    } catch (reason) {
      setError(reason instanceof Error ? reason.message : 'Unable to update the tenant sport.');
    } finally {
      setBusySportId(null);
    }
  }

  async function openVersions(sport: Sport) {
    setVersionSportId(sport.id);
    setNewVersionName(`${sport.name} definition`);
    const first = sport.versions[0];
    setSelectedVersionId(first?.id ?? '');
    setVersion(first ? await adminApi<SportDefinitionVersion>(`/api/sports/versions/${first.id}`) : null);
    setError(null);
    setNotice(null);
  }

  async function selectVersion(versionId: string) {
    setSelectedVersionId(versionId);
    setVersion(versionId ? await adminApi<SportDefinitionVersion>(`/api/sports/versions/${versionId}`) : null);
  }

  async function createVersion(clone: boolean) {
    if (!versionSportId || !newVersionName.trim() || (clone && !selectedVersionId)) return;
    setVersionBusy(true);
    setError(null);
    try {
      const path = clone
        ? `/api/sports/${versionSportId}/versions/${selectedVersionId}/clone`
        : `/api/sports/${versionSportId}/versions`;
      const created = await adminApi<SportDefinitionVersion>(path, {
        method: 'POST', body: JSON.stringify({ name: newVersionName.trim() }),
      });
      await loadCatalogue();
      setSelectedVersionId(created.id);
      setVersion(created);
      setNewVersionName('');
      setNotice(clone ? 'Definition cloned into a new editable draft.' : 'Definition draft created.');
    } catch (reason) {
      setError(reason instanceof Error ? reason.message : 'Unable to create the definition version.');
    } finally {
      setVersionBusy(false);
    }
  }

  async function saveVersion() {
    if (!version || version.isPublished) return;
    setVersionBusy(true);
    setError(null);
    try {
      const updated = await adminApi<SportDefinitionVersion>(`/api/sports/versions/${version.id}`, {
        method: 'PUT',
        body: JSON.stringify({
          contestShape: version.contestShape,
          participantType: version.participantType,
          segmentDefinitions: version.segmentDefinitions,
          eventDefinitions: version.eventDefinitions,
          metricDefinitions: version.metricDefinitions,
          resultMethods: version.resultMethods,
          rankingRules: version.rankingRules,
          displayConfiguration: version.displayConfiguration,
          validationConfiguration: version.validationConfiguration,
        }),
      });
      setVersion(updated);
      setNotice('Definition draft saved.');
    } catch (reason) {
      setError(reason instanceof Error ? reason.message : 'Unable to save the definition version.');
    } finally {
      setVersionBusy(false);
    }
  }

  async function publishVersion() {
    if (!version || version.isPublished || !window.confirm('Publish this version permanently? Published definitions cannot be edited.')) return;
    setVersionBusy(true);
    setError(null);
    try {
      await adminApi<void>(`/api/sports/versions/${version.id}/publish`, { method: 'POST' });
      const published = await adminApi<SportDefinitionVersion>(`/api/sports/versions/${version.id}`);
      setVersion(published);
      await loadCatalogue();
      setNotice(`Version ${published.versionNumber} published and available for new competitions.`);
    } catch (reason) {
      setError(reason instanceof Error ? reason.message : 'Unable to publish the definition version.');
    } finally {
      setVersionBusy(false);
    }
  }

  return (
    <div className="space-y-6">
      <header>
        <p className="text-sm font-semibold uppercase tracking-wider text-primary">Universal domain</p>
        <h1 className="text-3xl font-bold text-gray-950">Sports catalogue</h1>
        <p className="mt-1 text-gray-500">Manage versioned sport packages and choose the sports available to each tenant.</p>
      </header>

      {error && <p role="alert" className="rounded-lg bg-red-50 p-4 text-sm font-semibold text-red-700">{error}</p>}
      {notice && <p role="status" className="rounded-lg bg-green-50 p-4 text-sm font-semibold text-green-700">{notice}</p>}

      <section className="rounded-xl border bg-white p-5">
        <div className="flex flex-wrap items-end justify-between gap-4">
          <div className="min-w-64">
            <label htmlFor="sport-tenant" className="text-sm font-semibold">Tenant availability</label>
            {platform ? (
              <select id="sport-tenant" value={tenantId} onChange={event => setTenantId(event.target.value)} className="mt-1 h-10 w-full rounded-md border bg-white px-3">
                {tenants.map(tenant => <option key={tenant.id} value={tenant.id}>{tenant.name}</option>)}
              </select>
            ) : (
              <p className="mt-1 text-sm text-gray-600">{tenants.find(item => item.id === tenantId)?.name ?? 'Your tenant'}</p>
            )}
          </div>
          <p className="text-sm text-gray-500">{tenantSports.filter(item => item.enabled).length} enabled</p>
        </div>
      </section>

      {platform && canManage && (
        <form onSubmit={saveSport} className="rounded-xl border bg-white p-6">
          <div className="flex items-center justify-between gap-3">
            <h2 className="text-lg font-bold">{editingId ? 'Edit sport' : 'Add sport'}</h2>
            {editingId && <button type="button" onClick={resetForm} className="text-sm font-semibold text-gray-600 hover:text-gray-950">Cancel</button>}
          </div>
          <div className="mt-4 grid gap-4 md:grid-cols-2 lg:grid-cols-3">
            <Field label="Code"><input required disabled={Boolean(editingId)} value={form.code} onChange={event => setForm(current => ({ ...current, code: event.target.value }))} className="h-10 w-full rounded-md border px-3 uppercase disabled:bg-gray-100" placeholder="HANDBALL" /></Field>
            <Field label="Name"><input required value={form.name} onChange={event => setForm(current => ({ ...current, name: event.target.value }))} className="h-10 w-full rounded-md border px-3" /></Field>
            <Field label="Icon"><input value={form.icon} onChange={event => setForm(current => ({ ...current, icon: event.target.value }))} className="h-10 w-full rounded-md border px-3" placeholder="Emoji or icon key" /></Field>
            <Field label="Participant shapes"><input required value={form.participantShapes} onChange={event => setForm(current => ({ ...current, participantShapes: event.target.value }))} className="h-10 w-full rounded-md border px-3" placeholder="team, individual, pair" /></Field>
            <Field label="Description" wide><input value={form.description} onChange={event => setForm(current => ({ ...current, description: event.target.value }))} className="h-10 w-full rounded-md border px-3" /></Field>
          </div>
          {editingId && <label className="mt-4 flex items-center gap-2 text-sm font-semibold"><input type="checkbox" checked={form.isActive} onChange={event => setForm(current => ({ ...current, isActive: event.target.checked }))} /> Active in the platform catalogue</label>}
          <button disabled={saving} className="mt-5 rounded-md bg-primary px-4 py-2 text-sm font-semibold text-white disabled:opacity-50">{saving ? 'Saving…' : editingId ? 'Save changes' : 'Create sport'}</button>
        </form>
      )}

      <section className="grid gap-4 md:grid-cols-2 xl:grid-cols-3">
        {sports.map(sport => (
          <article key={sport.id} className="rounded-xl border bg-white p-5">
            <div className="flex items-start justify-between gap-3">
              <div className="flex gap-3">
                <span aria-hidden className="text-3xl">{sport.icon || '🏅'}</span>
                <div><h2 className="font-bold text-gray-950">{sport.name}</h2><p className="text-xs font-semibold tracking-wide text-gray-500">{sport.code}</p></div>
              </div>
              <span className={`rounded-full px-2 py-1 text-xs font-semibold ${sport.isActive ? 'bg-green-50 text-green-700' : 'bg-gray-100 text-gray-600'}`}>{sport.isActive ? 'Active' : 'Inactive'}</span>
            </div>
            <p className="mt-3 min-h-10 text-sm text-gray-600">{sport.description || 'No description supplied.'}</p>
            <div className="mt-4 flex flex-wrap gap-2 text-xs">
              <span className="rounded bg-blue-50 px-2 py-1 font-semibold text-blue-700">Package v{sport.sportPackageVersion}</span>
              {sport.supportedParticipantShapes.map(shape => <span key={shape} className="rounded bg-gray-100 px-2 py-1 text-gray-700">{shape}</span>)}
            </div>
            <p className="mt-3 text-xs text-gray-500">{sport.versions.length} definition version{sport.versions.length === 1 ? '' : 's'} · {sport.versions.filter(item => item.isPublished).length} published</p>
            <div className="mt-4 flex items-center justify-between gap-3 border-t pt-4">
              {tenantId && sport.isActive ? (
                <label className="flex items-center gap-2 text-sm font-semibold">
                  <input type="checkbox" checked={enabledById.get(sport.id) ?? false} disabled={busySportId === sport.id || !canManage} onChange={event => void setEnabled(sport, event.target.checked)} />
                  Enabled for tenant
                </label>
              ) : <span className="text-xs text-gray-400">Not available to tenants</span>}
              {platform && canManage && <div className="flex gap-3">
                <button type="button" onClick={() => void openVersions(sport)} className="text-sm font-semibold text-primary hover:underline">Versions</button>
                <button type="button" onClick={() => beginEdit(sport)} className="text-sm font-semibold text-primary hover:underline">Edit</button>
              </div>}
            </div>
          </article>
        ))}
      </section>

      {platform && canManage && versionSportId && (
        <section className="rounded-xl border bg-white p-6">
          <div className="flex flex-wrap items-start justify-between gap-4">
            <div>
              <p className="text-sm font-semibold uppercase tracking-wide text-primary">Immutable rules</p>
              <h2 className="text-xl font-bold">{sports.find(item => item.id === versionSportId)?.name} definition versions</h2>
              <p className="mt-1 text-sm text-gray-500">Edit drafts, clone any baseline, and publish only after every JSON definition is valid.</p>
            </div>
            <button type="button" onClick={() => { setVersionSportId(null); setVersion(null); }} className="text-sm font-semibold text-gray-600 hover:text-gray-950">Close</button>
          </div>

          <div className="mt-5 grid gap-4 lg:grid-cols-[minmax(15rem,0.7fr)_minmax(0,2fr)]">
            <aside className="space-y-4">
              <div>
                <label htmlFor="definition-version" className="text-sm font-semibold">Version</label>
                <select id="definition-version" value={selectedVersionId} onChange={event => void selectVersion(event.target.value)} className="mt-1 h-10 w-full rounded-md border bg-white px-3">
                  <option value="">Select a version</option>
                  {(sports.find(item => item.id === versionSportId)?.versions ?? []).map(item =>
                    <option key={item.id} value={item.id}>v{item.versionNumber} · {item.name} · {item.isPublished ? 'Published' : 'Draft'}</option>)}
                </select>
              </div>
              <div className="rounded-lg border bg-gray-50 p-4">
                <label htmlFor="new-version-name" className="text-sm font-semibold">New version name</label>
                <input id="new-version-name" value={newVersionName} onChange={event => setNewVersionName(event.target.value)} className="mt-1 h-10 w-full rounded-md border bg-white px-3" placeholder="2027 rules" />
                <div className="mt-3 grid gap-2">
                  <button type="button" disabled={versionBusy || !newVersionName.trim()} onClick={() => void createVersion(false)} className="rounded-md bg-gray-900 px-3 py-2 text-sm font-semibold text-white disabled:opacity-50">Create blank draft</button>
                  <button type="button" disabled={versionBusy || !newVersionName.trim() || !selectedVersionId} onClick={() => void createVersion(true)} className="rounded-md border bg-white px-3 py-2 text-sm font-semibold disabled:opacity-50">Clone selected version</button>
                </div>
              </div>
            </aside>

            {version ? (
              <div className="space-y-4">
                <div className="flex flex-wrap items-center justify-between gap-3 rounded-lg bg-gray-50 px-4 py-3">
                  <div><span className="font-bold">v{version.versionNumber} · {version.name}</span><p className="text-xs text-gray-500">Created {new Date(version.createdAt).toLocaleString()} by {version.createdBy}</p></div>
                  <span className={`rounded-full px-2 py-1 text-xs font-semibold ${version.isPublished ? 'bg-green-100 text-green-800' : 'bg-amber-100 text-amber-800'}`}>{version.isPublished ? 'Published and immutable' : 'Editable draft'}</span>
                </div>
                <div className="grid gap-4 md:grid-cols-2">
                  <VersionField label="Contest shape" value={version.contestShape} disabled={version.isPublished} onChange={value => setVersion(current => current ? { ...current, contestShape: value } : current)} />
                  <VersionField label="Participant type" value={version.participantType} disabled={version.isPublished} onChange={value => setVersion(current => current ? { ...current, participantType: value } : current)} />
                </div>
                <div className="grid gap-4 xl:grid-cols-2">
                  <JsonField label="Segment definitions (array)" value={version.segmentDefinitions} disabled={version.isPublished} onChange={value => setVersion(current => current ? { ...current, segmentDefinitions: value } : current)} />
                  <EventDefinitionsField value={version.eventDefinitions} disabled={version.isPublished} onChange={value => setVersion(current => current ? { ...current, eventDefinitions: value } : current)} />
                  <MetricDefinitionsField value={version.metricDefinitions} disabled={version.isPublished} onChange={value => setVersion(current => current ? { ...current, metricDefinitions: value } : current)} />
                  <JsonField label="Result methods (array)" value={version.resultMethods} disabled={version.isPublished} onChange={value => setVersion(current => current ? { ...current, resultMethods: value } : current)} />
                  <JsonField label="Ranking rules (array)" value={version.rankingRules} disabled={version.isPublished} onChange={value => setVersion(current => current ? { ...current, rankingRules: value } : current)} />
                  <JsonField label="Display configuration (object)" value={version.displayConfiguration} disabled={version.isPublished} onChange={value => setVersion(current => current ? { ...current, displayConfiguration: value } : current)} />
                  <JsonField label="Validation configuration (object)" value={version.validationConfiguration} disabled={version.isPublished} onChange={value => setVersion(current => current ? { ...current, validationConfiguration: value } : current)} />
                </div>
                {!version.isPublished && <div className="flex flex-wrap gap-3 border-t pt-4">
                  <button type="button" disabled={versionBusy} onClick={() => void saveVersion()} className="rounded-md bg-primary px-4 py-2 text-sm font-semibold text-white disabled:opacity-50">Save draft</button>
                  <button type="button" disabled={versionBusy} onClick={() => void publishVersion()} className="rounded-md border border-green-700 px-4 py-2 text-sm font-semibold text-green-800 disabled:opacity-50">Publish permanently</button>
                </div>}
              </div>
            ) : <div className="flex min-h-48 items-center justify-center rounded-lg border border-dashed text-sm text-gray-500">Select a version or create a draft.</div>}
          </div>
        </section>
      )}
    </div>
  );
}

function Field({ label, wide, children }: { label: string; wide?: boolean; children: React.ReactNode }) {
  return <label className={`text-sm font-semibold ${wide ? 'lg:col-span-2' : ''}`}>{label}<span className="mt-1 block font-normal">{children}</span></label>;
}

function VersionField({ label, value, disabled, onChange }: { label: string; value: string; disabled: boolean; onChange: (value: string) => void }) {
  return <label className="text-sm font-semibold">{label}<input required value={value} disabled={disabled} onChange={event => onChange(event.target.value)} className="mt-1 h-10 w-full rounded-md border px-3 font-normal disabled:bg-gray-100" /></label>;
}

function JsonField({ label, value, disabled, onChange }: { label: string; value: string; disabled: boolean; onChange: (value: string) => void }) {
  return <label className="text-sm font-semibold">{label}<textarea value={value} disabled={disabled} onChange={event => onChange(event.target.value)} rows={5} spellCheck={false} className="mt-1 w-full rounded-md border p-3 font-mono text-xs font-normal disabled:bg-gray-100" /></label>;
}

type EventEditorDefinition = {
  id?: string; code?: string; name?: string; segmentApplicability?: string[];
  requiresParticipant?: boolean; requiresSecondaryParticipant?: boolean;
  requiredMetric?: string | null; timingRequirement?: string; correctionPolicy?: string;
  validation?: { minimumValue?: number; maximumValue?: number; minimumClockSeconds?: number; maximumClockSeconds?: number; [key: string]: unknown };
  [key: string]: unknown;
};

function EventDefinitionsField({ value, disabled, onChange }: { value: string; disabled: boolean; onChange: (value: string) => void }) {
  let definitions: EventEditorDefinition[] = [];
  let parseError: string | null = null;
  try {
    const parsed: unknown = JSON.parse(value);
    if (!Array.isArray(parsed)) throw new Error('Event definitions must be an array.');
    definitions = parsed as EventEditorDefinition[];
  } catch (reason) {
    parseError = reason instanceof Error ? reason.message : 'Invalid event JSON.';
  }
  const commit = (next: EventEditorDefinition[]) => onChange(JSON.stringify(next, null, 2));
  const update = (index: number, changes: Partial<EventEditorDefinition>) => commit(definitions.map((item, position) => position === index ? { ...item, ...changes } : item));
  const updateValidation = (index: number, key: string, next: number | undefined) => update(index, { validation: { ...(definitions[index].validation ?? {}), [key]: next } });

  return <div className="space-y-3 xl:col-span-2">
    <div className="flex flex-wrap items-center justify-between gap-2"><div><p className="text-sm font-semibold">Event definitions</p><p className="text-xs text-gray-500">Configure participants, metrics, timing, corrections, segments, and validation without a deployment.</p></div>
      <button type="button" disabled={disabled || Boolean(parseError)} onClick={() => commit([...definitions, { id: `EVENT_${definitions.length + 1}`, name: `Event ${definitions.length + 1}`, segmentApplicability: ['*'], requiresParticipant: true, requiresSecondaryParticipant: false, requiredMetric: null, timingRequirement: 'None', correctionPolicy: 'ApprovalRequired', validation: {} }])} className="rounded-md border bg-white px-3 py-2 text-xs font-semibold disabled:opacity-50">Add event</button>
    </div>
    {parseError ? <p role="alert" className="rounded-md bg-red-50 p-3 text-xs font-semibold text-red-700">{parseError} — correct the advanced JSON below.</p> : definitions.length === 0 ? <p className="rounded-md border border-dashed p-4 text-center text-xs text-gray-500">No events configured.</p> : definitions.map((definition, index) => {
      const validation = definition.validation ?? {};
      const code = definition.id ?? definition.code ?? '';
      return <article key={`${code}-${index}`} className="rounded-lg border bg-gray-50 p-4"><div className="grid gap-3 md:grid-cols-2 xl:grid-cols-4">
        <MetricInput label="Code" value={code} disabled={disabled} onChange={next => update(index, { id: next, code: undefined })} />
        <MetricInput label="Name" value={definition.name ?? ''} disabled={disabled} onChange={next => update(index, { name: next })} />
        <MetricInput label="Applicable segments" value={(definition.segmentApplicability ?? ['*']).join(', ')} disabled={disabled} placeholder="*, Half, Round" onChange={next => update(index, { segmentApplicability: commaValues(next) })} />
        <MetricInput label="Required metric" value={definition.requiredMetric ?? ''} disabled={disabled} placeholder="score" onChange={next => update(index, { requiredMetric: next || null })} />
        <label className="text-xs font-semibold">Timing<select value={definition.timingRequirement ?? 'None'} disabled={disabled} onChange={event => update(index, { timingRequirement: event.target.value })} className="mt-1 h-9 w-full rounded-md border bg-white px-2 font-normal disabled:bg-gray-100"><option>None</option><option>Timestamp</option><option>Clock</option><option>TimestampOrClock</option></select></label>
        <label className="text-xs font-semibold">Correction policy<select value={definition.correctionPolicy ?? 'ApprovalRequired'} disabled={disabled} onChange={event => update(index, { correctionPolicy: event.target.value })} className="mt-1 h-9 w-full rounded-md border bg-white px-2 font-normal disabled:bg-gray-100"><option>ApprovalRequired</option><option>Prohibited</option></select></label>
        <label className="flex items-center gap-2 pt-5 text-xs font-semibold"><input type="checkbox" checked={definition.requiresParticipant ?? false} disabled={disabled} onChange={event => update(index, { requiresParticipant: event.target.checked })} />Participant required</label>
        <label className="flex items-center gap-2 pt-5 text-xs font-semibold"><input type="checkbox" checked={definition.requiresSecondaryParticipant ?? false} disabled={disabled} onChange={event => update(index, { requiresSecondaryParticipant: event.target.checked })} />Secondary participant required</label>
        <MetricNumberInput label="Minimum value" value={validation.minimumValue} disabled={disabled} onChange={next => updateValidation(index, 'minimumValue', next)} />
        <MetricNumberInput label="Maximum value" value={validation.maximumValue} disabled={disabled} onChange={next => updateValidation(index, 'maximumValue', next)} />
        <MetricNumberInput label="Minimum clock seconds" value={validation.minimumClockSeconds} disabled={disabled} onChange={next => updateValidation(index, 'minimumClockSeconds', next)} />
        <MetricNumberInput label="Maximum clock seconds" value={validation.maximumClockSeconds} disabled={disabled} onChange={next => updateValidation(index, 'maximumClockSeconds', next)} />
      </div>{!disabled && <button type="button" onClick={() => commit(definitions.filter((_, position) => position !== index))} className="mt-3 text-xs font-semibold text-red-700 hover:underline">Remove event</button>}</article>;
    })}
    <details className="rounded-lg border bg-white p-3"><summary className="cursor-pointer text-xs font-semibold">Advanced event JSON</summary><textarea aria-label="Advanced event JSON" value={value} disabled={disabled} onChange={event => onChange(event.target.value)} rows={8} spellCheck={false} className="mt-3 w-full rounded-md border p-3 font-mono text-xs font-normal disabled:bg-gray-100" /></details>
  </div>;
}

const metricTypes = [
  'Integer', 'Decimal', 'Duration', 'ClockTime', 'Distance', 'Weight',
  'Boolean', 'Text', 'Enumeration', 'CompositeScore',
] as const;

type MetricEditorDefinition = {
  id?: string;
  code?: string;
  name?: string;
  type?: string;
  unit?: string;
  sortDirection?: string;
  validation?: {
    precision?: number;
    minimum?: number;
    maximum?: number;
    maximumLength?: number;
    pattern?: string;
    allowedValues?: string[];
    components?: string[];
    [key: string]: unknown;
  };
  [key: string]: unknown;
};

function MetricDefinitionsField({ value, disabled, onChange }: {
  value: string;
  disabled: boolean;
  onChange: (value: string) => void;
}) {
  let definitions: MetricEditorDefinition[] = [];
  let parseError: string | null = null;
  try {
    const parsed: unknown = JSON.parse(value);
    if (!Array.isArray(parsed)) throw new Error('Metric definitions must be an array.');
    definitions = parsed as MetricEditorDefinition[];
  } catch (reason) {
    parseError = reason instanceof Error ? reason.message : 'Invalid metric JSON.';
  }

  function commit(next: MetricEditorDefinition[]) {
    onChange(JSON.stringify(next, null, 2));
  }

  function update(index: number, updater: (current: MetricEditorDefinition) => MetricEditorDefinition) {
    commit(definitions.map((item, itemIndex) => itemIndex === index ? updater(item) : item));
  }

  function updateValidation(index: number, key: string, fieldValue: unknown) {
    update(index, current => ({
      ...current,
      validation: { ...(current.validation ?? {}), [key]: fieldValue },
    }));
  }

  return (
    <div className="space-y-3 xl:col-span-2">
      <div className="flex flex-wrap items-center justify-between gap-2">
        <div>
          <p className="text-sm font-semibold">Metric definitions</p>
          <p className="text-xs text-gray-500">Types, validation, units, precision, and ranking are enforced by the API.</p>
        </div>
        <button type="button" disabled={disabled || Boolean(parseError)} onClick={() => commit([...definitions, {
          id: `METRIC_${definitions.length + 1}`,
          name: `Metric ${definitions.length + 1}`,
          type: 'Integer',
          sortDirection: 'None',
          validation: { precision: 0 },
        }])} className="rounded-md border bg-white px-3 py-2 text-xs font-semibold disabled:opacity-50">Add metric</button>
      </div>

      {parseError ? <p role="alert" className="rounded-md bg-red-50 p-3 text-xs font-semibold text-red-700">{parseError} — correct the advanced JSON below.</p> : definitions.length === 0 ? (
        <p className="rounded-md border border-dashed p-4 text-center text-xs text-gray-500">No metrics configured.</p>
      ) : definitions.map((definition, index) => {
        const validation = definition.validation ?? {};
        const type = definition.type ?? 'Integer';
        const code = definition.id ?? definition.code ?? '';
        return (
          <article key={`${code}-${index}`} className="rounded-lg border bg-gray-50 p-4">
            <div className="grid gap-3 md:grid-cols-2 xl:grid-cols-4">
              <MetricInput label="Code" value={code} disabled={disabled} onChange={next => update(index, current => ({ ...current, id: next, code: undefined }))} />
              <MetricInput label="Name" value={definition.name ?? ''} disabled={disabled} onChange={next => update(index, current => ({ ...current, name: next }))} />
              <label className="text-xs font-semibold">Type<select value={type} disabled={disabled} onChange={event => update(index, current => ({
                ...current,
                type: event.target.value,
                validation: { ...(current.validation ?? {}), precision: defaultMetricPrecision(event.target.value) },
              }))} className="mt-1 h-9 w-full rounded-md border bg-white px-2 font-normal disabled:bg-gray-100">
                {metricTypes.map(item => <option key={item} value={item}>{metricTypeLabel(item)}</option>)}
                {type === 'Percentage' && <option value="Percentage">Percentage (legacy decimal)</option>}
              </select></label>
              <MetricInput label="Unit" value={definition.unit ?? ''} disabled={disabled} placeholder="s, m, kg, points" onChange={next => update(index, current => ({ ...current, unit: next || undefined }))} />
              <label className="text-xs font-semibold">Precision<input type="number" min={0} max={6} value={validation.precision ?? defaultMetricPrecision(type)} disabled={disabled || ['Integer', 'Boolean', 'Text', 'Enumeration'].includes(type)} onChange={event => updateValidation(index, 'precision', Number(event.target.value))} className="mt-1 h-9 w-full rounded-md border px-2 font-normal disabled:bg-gray-100" /></label>
              <label className="text-xs font-semibold">Ranking<select value={definition.sortDirection ?? 'None'} disabled={disabled} onChange={event => update(index, current => ({ ...current, sortDirection: event.target.value }))} className="mt-1 h-9 w-full rounded-md border bg-white px-2 font-normal disabled:bg-gray-100"><option>None</option><option>Ascending</option><option>Descending</option></select></label>
              {['Integer', 'Decimal', 'Duration', 'ClockTime', 'Distance', 'Weight', 'Percentage'].includes(type) && <>
                <MetricNumberInput label="Minimum" value={validation.minimum} disabled={disabled} onChange={next => updateValidation(index, 'minimum', next)} />
                <MetricNumberInput label="Maximum" value={validation.maximum} disabled={disabled} onChange={next => updateValidation(index, 'maximum', next)} />
              </>}
              {type === 'Text' && <>
                <MetricNumberInput label="Maximum length" value={validation.maximumLength} disabled={disabled} onChange={next => updateValidation(index, 'maximumLength', next)} />
                <MetricInput label="Validation pattern" value={validation.pattern ?? ''} disabled={disabled} placeholder="Regular expression" onChange={next => updateValidation(index, 'pattern', next || undefined)} />
              </>}
              {type === 'Enumeration' && <MetricInput label="Allowed values" value={(validation.allowedValues ?? []).join(', ')} disabled={disabled} placeholder="Gold, Silver, Bronze" onChange={next => updateValidation(index, 'allowedValues', commaValues(next))} />}
              {type === 'CompositeScore' && <MetricInput label="Ordered components" value={(validation.components ?? []).join(', ')} disabled={disabled} placeholder="sets, points" onChange={next => updateValidation(index, 'components', commaValues(next))} />}
            </div>
            {!disabled && <button type="button" onClick={() => commit(definitions.filter((_, itemIndex) => itemIndex !== index))} className="mt-3 text-xs font-semibold text-red-700 hover:underline">Remove metric</button>}
          </article>
        );
      })}

      <details className="rounded-lg border bg-white p-3">
        <summary className="cursor-pointer text-xs font-semibold">Advanced metric JSON</summary>
        <textarea aria-label="Advanced metric JSON" value={value} disabled={disabled} onChange={event => onChange(event.target.value)} rows={8} spellCheck={false} className="mt-3 w-full rounded-md border p-3 font-mono text-xs font-normal disabled:bg-gray-100" />
      </details>
    </div>
  );
}

function MetricInput({ label, value, disabled, placeholder, onChange }: {
  label: string; value: string; disabled: boolean; placeholder?: string; onChange: (value: string) => void;
}) {
  return <label className="text-xs font-semibold">{label}<input value={value} disabled={disabled} placeholder={placeholder} onChange={event => onChange(event.target.value)} className="mt-1 h-9 w-full rounded-md border px-2 font-normal disabled:bg-gray-100" /></label>;
}

function MetricNumberInput({ label, value, disabled, onChange }: {
  label: string; value?: number; disabled: boolean; onChange: (value: number | undefined) => void;
}) {
  return <label className="text-xs font-semibold">{label}<input type="number" value={value ?? ''} disabled={disabled} onChange={event => onChange(event.target.value === '' ? undefined : Number(event.target.value))} className="mt-1 h-9 w-full rounded-md border px-2 font-normal disabled:bg-gray-100" /></label>;
}

function defaultMetricPrecision(type: string) {
  if (['Integer', 'Boolean', 'Text', 'Enumeration'].includes(type)) return 0;
  if (['Duration', 'Distance'].includes(type)) return 3;
  return 2;
}

function commaValues(value: string) {
  return value.split(',').map(item => item.trim()).filter(Boolean);
}

function metricTypeLabel(value: string) {
  return value.replace(/([a-z])([A-Z])/g, '$1 $2');
}
