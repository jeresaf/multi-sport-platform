'use client';

import { useCallback, useEffect, useMemo, useState } from 'react';
import { adminApi, getStoredUser, hasAnyPermission, isPlatformOperator } from '@/lib/admin-api';

type Tenant = { id: string; name: string };
type Competition = { id: string; name: string };
type Registration = {
  id: string;
  participantId: string;
  participantName: string;
  participantType: string;
  status: string;
  type: string;
  createdAt: string;
  registeredAt?: string | null;
  reviewedBy?: string | null;
  reviewerName?: string | null;
  reviewedAt?: string | null;
  reason?: string | null;
  eligible: boolean;
  eligibilityIssues: string[];
  eligibilityCheckedAt?: string | null;
  eligibilityOverrideReason?: string | null;
};
type ReviewQueue = {
  competition: { id: string; name: string; tenantId: string };
  counts: Record<string, number>;
  registrations: Registration[];
};
type EligibilityPolicy = {
  minimumSquadSize: number | null; maximumSquadSize: number | null;
  minimumAge: number | null; maximumAge: number | null;
  minimumWeightKg: number | null; maximumWeightKg: number | null;
  registrationDeadline: string | null; requiredDocumentTypes: string[];
  requireSquadNumbers: boolean; requireUniqueSquadNumbers: boolean;
};
type EligibilityResult = { isEligible: boolean; issues: string[] };
const emptyPolicy: EligibilityPolicy = { minimumSquadSize: null, maximumSquadSize: null, minimumAge: null, maximumAge: null, minimumWeightKg: null, maximumWeightKg: null, registrationDeadline: null, requiredDocumentTypes: [], requireSquadNumbers: false, requireUniqueSquadNumbers: true };

const requiredReasonActions = new Set(['approve', 'reject', 'waitlist', 'withdraw']);
const decisionLabels: Record<string, string> = {
  review: 'Start review', approve: 'Approve', reject: 'Reject',
  waitlist: 'Waitlist', withdraw: 'Withdraw', submit: 'Submit',
};

export default function RegistrationReviewPage() {
  const user = getStoredUser();
  const platform = isPlatformOperator(user);
  const canManage = hasAnyPermission(user, ['competitions.manage']);
  const canOverride = platform || hasAnyPermission(user, ['registrations.override']);
  const [tenants, setTenants] = useState<Tenant[]>([]);
  const [tenantId, setTenantId] = useState(user?.tenantId ?? '');
  const [competitions, setCompetitions] = useState<Competition[]>([]);
  const [competitionId, setCompetitionId] = useState('');
  const [queue, setQueue] = useState<ReviewQueue | null>(null);
  const [filter, setFilter] = useState('ALL');
  const [reasons, setReasons] = useState<Record<string, string>>({});
  const [busyId, setBusyId] = useState('');
  const [policy, setPolicy] = useState<EligibilityPolicy>(emptyPolicy);
  const [documentTypes, setDocumentTypes] = useState('');
  const [overrides, setOverrides] = useState<Record<string, boolean>>({});
  const [checks, setChecks] = useState<Record<string, EligibilityResult>>({});
  const [message, setMessage] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);

  const loadCompetitions = useCallback(async (scope: string) => {
    if (!scope) { setCompetitions([]); return; }
    const items = await adminApi<Competition[]>(`/api/competitions?tenantId=${scope}`);
    setCompetitions(items.sort((left, right) => left.name.localeCompare(right.name)));
  }, []);

  const loadQueue = useCallback(async (id: string) => {
    if (!id) { setQueue(null); return; }
    setQueue(await adminApi<ReviewQueue>(`/api/registrations/competition/${id}/review-queue`));
  }, []);

  useEffect(() => {
    if (!platform) return;
    adminApi<Tenant[]>('/api/tenants')
      .then(items => { setTenants(items); setTenantId(current => current || items[0]?.id || ''); })
      .catch(reason => setError(reason instanceof Error ? reason.message : 'Unable to load tenants.'));
  }, [platform]);

  useEffect(() => {
    setCompetitionId(''); setQueue(null); setReasons({}); setError(null);
    void loadCompetitions(tenantId)
      .catch(reason => setError(reason instanceof Error ? reason.message : 'Unable to load competitions.'));
  }, [loadCompetitions, tenantId]);

  const visible = useMemo(() => queue?.registrations.filter(item =>
    filter === 'ALL' || item.status.toUpperCase() === filter) ?? [], [filter, queue]);

  async function chooseCompetition(id: string) {
    setCompetitionId(id); setError(null); setMessage(null); setReasons({});
    try {
      await loadQueue(id);
      const configured = await adminApi<EligibilityPolicy | null>(`/api/registrations/competition/${id}/eligibility-policy`);
      setPolicy(configured ?? emptyPolicy);
      setDocumentTypes((configured?.requiredDocumentTypes ?? []).join(', '));
      setChecks({}); setOverrides({});
    }
    catch (reason) { setError(reason instanceof Error ? reason.message : 'Unable to load the review queue.'); }
  }

  async function transition(registration: Registration, action: string) {
    const reason = reasons[registration.id]?.trim() ?? '';
    if (requiredReasonActions.has(action) && !reason) {
      setError(`Enter a reason before choosing ${decisionLabels[action].toLowerCase()}.`);
      return;
    }
    setBusyId(registration.id); setError(null); setMessage(null);
    try {
      const body = action === 'approve'
        ? { notes: reason, overrideEligibility: overrides[registration.id] === true, overrideReason: overrides[registration.id] ? reason : undefined }
        : action === 'waitlist' ? { notes: reason }
        : action === 'reject' || action === 'withdraw' ? { reason } : undefined;
      await adminApi(`/api/registrations/${registration.id}/${action}`, {
        method: 'POST', ...(body ? { body: JSON.stringify(body) } : {}),
      });
      await loadQueue(competitionId);
      setReasons(current => ({ ...current, [registration.id]: '' }));
      setMessage(`${registration.participantName}: ${decisionLabels[action]} recorded, audited, and notified.`);
    } catch (reasonValue) {
      setError(reasonValue instanceof Error ? reasonValue.message : 'Unable to update the registration.');
    } finally { setBusyId(''); }
  }

  async function checkEligibility(registration: Registration) {
    setBusyId(registration.id); setError(null);
    try {
      const result = await adminApi<EligibilityResult>(`/api/registrations/${registration.id}/eligibility`);
      setChecks(current => ({ ...current, [registration.id]: result }));
    }
    catch (reason) { setError(reason instanceof Error ? reason.message : 'Unable to check eligibility.'); }
    finally { setBusyId(''); }
  }

  async function savePolicy() {
    if (!competitionId) return;
    setBusyId('policy'); setError(null);
    try {
      const saved = await adminApi<EligibilityPolicy>(`/api/registrations/competition/${competitionId}/eligibility-policy`, {
        method: 'PUT', body: JSON.stringify({ ...policy, requiredDocumentTypes: documentTypes.split(',').map(value => value.trim()).filter(Boolean) }),
      });
      setPolicy(saved); setDocumentTypes(saved.requiredDocumentTypes.join(', '));
      setMessage('Eligibility policy saved and audited.'); setChecks({});
    } catch (reason) { setError(reason instanceof Error ? reason.message : 'Unable to save eligibility policy.'); }
    finally { setBusyId(''); }
  }

  return <div className="space-y-6">
    <div><h1 className="text-3xl font-bold text-gray-950">Competition registration review</h1><p className="mt-1 text-sm text-gray-600">Official decisions, mandatory reasons, eligibility, audit history, and applicant notifications.</p></div>
    {error && <p role="alert" className="rounded-md bg-red-50 p-3 text-sm text-red-700">{error}</p>}
    {message && <p role="status" className="rounded-md bg-green-50 p-3 text-sm text-green-800">{message}</p>}
    <div className="grid gap-4 md:grid-cols-3">
      {platform && <label className="text-sm font-semibold">Tenant<select value={tenantId} onChange={event => setTenantId(event.target.value)} className="mt-1 h-10 w-full rounded-md border bg-white px-3 font-normal"><option value="">Select tenant</option>{tenants.map(item => <option key={item.id} value={item.id}>{item.name}</option>)}</select></label>}
      <label className="text-sm font-semibold">Competition<select value={competitionId} onChange={event => void chooseCompetition(event.target.value)} className="mt-1 h-10 w-full rounded-md border bg-white px-3 font-normal"><option value="">Select competition</option>{competitions.map(item => <option key={item.id} value={item.id}>{item.name}</option>)}</select></label>
      <label className="text-sm font-semibold">Status filter<select value={filter} onChange={event => setFilter(event.target.value)} className="mt-1 h-10 w-full rounded-md border bg-white px-3 font-normal"><option value="ALL">All statuses</option>{Object.keys(queue?.counts ?? {}).map(status => <option key={status} value={status.toUpperCase()}>{status} ({queue?.counts[status] ?? 0})</option>)}</select></label>
    </div>
    {!competitionId && <p className="rounded-xl border bg-white p-5 text-sm text-gray-500">Select a competition to open its official review queue.</p>}
    {queue && <>
      <section className="rounded-xl border bg-white p-5"><div className="flex flex-wrap items-start justify-between gap-3"><div><h2 className="font-bold">Eligibility policy</h2><p className="text-sm text-gray-500">Unset limits are not enforced. Ages are calculated on the earliest edition start date.</p></div><button type="button" disabled={busyId === 'policy'} onClick={() => void savePolicy()} className="rounded-md bg-gray-950 px-4 py-2 text-sm font-semibold text-white disabled:opacity-50">Save policy</button></div><div className="mt-4 grid gap-3 sm:grid-cols-2 lg:grid-cols-4">{([['minimumSquadSize','Min squad'],['maximumSquadSize','Max squad'],['minimumAge','Min age'],['maximumAge','Max age'],['minimumWeightKg','Min weight kg'],['maximumWeightKg','Max weight kg']] as const).map(([key,label]) => <label key={key} className="text-xs font-semibold">{label}<input type="number" min="0" value={policy[key] ?? ''} onChange={event => setPolicy(current => ({ ...current, [key]: event.target.value === '' ? null : Number(event.target.value) }))} className="mt-1 h-10 w-full rounded-md border px-3 font-normal" /></label>)}<label className="text-xs font-semibold">Registration deadline<input type="datetime-local" value={policy.registrationDeadline?.slice(0,16) ?? ''} onChange={event => setPolicy(current => ({ ...current, registrationDeadline: event.target.value ? new Date(event.target.value).toISOString() : null }))} className="mt-1 h-10 w-full rounded-md border px-3 font-normal" /></label><label className="text-xs font-semibold">Required documents<input value={documentTypes} onChange={event => setDocumentTypes(event.target.value)} placeholder="Passport, medical certificate" className="mt-1 h-10 w-full rounded-md border px-3 font-normal" /></label></div><div className="mt-3 flex flex-wrap gap-5 text-sm"><label><input type="checkbox" checked={policy.requireSquadNumbers} onChange={event => setPolicy(current => ({ ...current, requireSquadNumbers: event.target.checked }))} className="mr-2" />Require squad numbers</label><label><input type="checkbox" checked={policy.requireUniqueSquadNumbers} onChange={event => setPolicy(current => ({ ...current, requireUniqueSquadNumbers: event.target.checked }))} className="mr-2" />Require unique squad numbers</label></div></section>
      <section className="grid gap-3 sm:grid-cols-3 lg:grid-cols-6">{['Draft', 'Submitted', 'UnderReview', 'Approved', 'Rejected', 'Waitlisted', 'Withdrawn'].map(status => <div key={status} className="rounded-lg border bg-white p-3"><p className="text-xs font-semibold uppercase text-gray-500">{status.replace(/([a-z])([A-Z])/g, '$1 $2')}</p><p className="text-2xl font-bold">{queue.counts[status] ?? 0}</p></div>)}</section>
      <section className="space-y-3">
        {visible.length === 0 && <p className="rounded-xl border bg-white p-5 text-sm text-gray-500">No registrations match this status.</p>}
        {visible.map(item => <article key={item.id} className="rounded-xl border bg-white p-5">
          <div className="flex flex-wrap items-start justify-between gap-3"><div><h2 className="font-bold text-gray-950">{item.participantName}</h2><p className="text-sm text-gray-500">{item.participantType} · submitted {item.registeredAt?.slice(0, 10) ?? 'not yet'}</p></div><span className={`rounded-full px-3 py-1 text-xs font-bold ${statusTone(item.status)}`}>{item.status.replace(/([a-z])([A-Z])/g, '$1 $2')}{item.eligible ? ' · Eligible' : ''}</span></div>
          {item.reason && <p className="mt-3 rounded bg-gray-50 p-3 text-sm"><strong>Recorded reason:</strong> {item.reason}</p>}
          {item.reviewerName && <p className="mt-2 text-xs text-gray-500">Reviewed by {item.reviewerName} {item.reviewedAt ? `on ${item.reviewedAt.slice(0, 10)}` : ''}</p>}
          {(checks[item.id] || item.eligibilityIssues?.length > 0) && <div className={`mt-3 rounded p-3 text-sm ${(checks[item.id]?.isEligible ?? item.eligible) ? 'bg-green-50 text-green-800' : 'bg-amber-50 text-amber-900'}`}><strong>{(checks[item.id]?.isEligible ?? item.eligible) ? 'Eligibility checks passed.' : 'Eligibility issues:'}</strong>{(checks[item.id]?.issues ?? item.eligibilityIssues)?.length > 0 && <ul className="mt-1 list-disc pl-5">{(checks[item.id]?.issues ?? item.eligibilityIssues).map(issue => <li key={issue}>{issue}</li>)}</ul>}{item.eligibilityOverrideReason && <p className="mt-2"><strong>Override:</strong> {item.eligibilityOverrideReason}</p>}</div>}
          {actions(item.status).length > 0 && canManage && <div className="mt-4 space-y-3 border-t pt-4"><label className="block text-xs font-semibold">Decision reason<input value={reasons[item.id] ?? ''} onChange={event => setReasons(current => ({ ...current, [item.id]: event.target.value }))} placeholder="Required for approval, rejection, waitlist, withdrawal, or override" maxLength={1000} className="mt-1 h-10 w-full rounded-md border px-3 font-normal" /></label><div className="flex flex-wrap items-center gap-2"><button type="button" disabled={busyId === item.id} onClick={() => void checkEligibility(item)} className="rounded-md border bg-white px-3 py-2 text-xs font-semibold disabled:opacity-50">Check eligibility</button>{canOverride && <label className="px-2 text-xs font-semibold"><input type="checkbox" checked={overrides[item.id] ?? false} onChange={event => setOverrides(current => ({ ...current, [item.id]: event.target.checked }))} className="mr-2" />Override eligibility</label>}{actions(item.status).map(action => <button key={action} type="button" disabled={busyId === item.id} onClick={() => void transition(item, action)} className={`rounded-md px-3 py-2 text-xs font-semibold disabled:opacity-50 ${action === 'approve' ? 'bg-green-700 text-white' : action === 'reject' ? 'bg-red-700 text-white' : 'border bg-white'}`}>{decisionLabels[action]}</button>)}</div></div>}
        </article>)}
      </section>
    </>}
  </div>;
}

function actions(status: string) {
  if (status === 'Draft') return ['submit'];
  if (status === 'Submitted') return ['review', 'approve', 'reject', 'waitlist', 'withdraw'];
  if (status === 'UnderReview') return ['approve', 'reject', 'waitlist', 'withdraw'];
  if (status === 'Approved' || status === 'Waitlisted') return ['withdraw'];
  return [];
}

function statusTone(status: string) {
  if (status === 'Approved') return 'bg-green-100 text-green-800';
  if (status === 'Rejected' || status === 'Withdrawn') return 'bg-red-100 text-red-800';
  if (status === 'Submitted' || status === 'UnderReview') return 'bg-blue-100 text-blue-800';
  if (status === 'Waitlisted') return 'bg-amber-100 text-amber-800';
  return 'bg-gray-100 text-gray-700';
}
