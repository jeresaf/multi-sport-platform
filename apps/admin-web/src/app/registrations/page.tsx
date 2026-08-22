'use client';

import { FormEvent, useCallback, useEffect, useMemo, useState } from 'react';
import { adminApi, getEffectiveTenantId, getStoredUser, hasAnyPermission, isPlatformOperator } from '@/lib/admin-api';
import { Eye, Plus } from 'lucide-react';
import { DataTable, type DataTableColumn } from '@/components/admin/data-table';
import { ResourceDialog } from '@/components/admin/resource-dialog';
import { Button } from '@/components/ui/button';

type Tenant = { id: string; name: string };
type Competition = { id: string; name: string };
type Participant = { id: string; name: string; type: string };
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
type FormSubmission = { id: string; registrationId?: string | null; formId: string; formName?: string | null; participantId: string; participantName?: string | null; participantType?: string | null; status: string; registrationStatus: string; formVersion: number; revision: number; formData: string; formSchemaSnapshot: string; submittedAt?: string | null; reviewNotes?: string | null };
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
  const [tenantId, setTenantId] = useState(getEffectiveTenantId() ?? '');
  const [competitions, setCompetitions] = useState<Competition[]>([]);
  const [participants, setParticipants] = useState<Participant[]>([]);
  const [competitionId, setCompetitionId] = useState('');
  const [queue, setQueue] = useState<ReviewQueue | null>(null);
  const [filter, setFilter] = useState('ALL');
  const [reasons, setReasons] = useState<Record<string, string>>({});
  const [busyId, setBusyId] = useState('');
  const [policy, setPolicy] = useState<EligibilityPolicy>(emptyPolicy);
  const [documentTypes, setDocumentTypes] = useState('');
  const [overrides, setOverrides] = useState<Record<string, boolean>>({});
  const [checks, setChecks] = useState<Record<string, EligibilityResult>>({});
  const [viewing, setViewing] = useState<Registration | null>(null);
  const [submissions, setSubmissions] = useState<FormSubmission[]>([]);
  const [submissionViewing, setSubmissionViewing] = useState<FormSubmission | null>(null);
  const [assistedOpen, setAssistedOpen] = useState(false);
  const [assistedParticipantId, setAssistedParticipantId] = useState('');
  const [assistedType, setAssistedType] = useState('Team');
  const [assistedSubmit, setAssistedSubmit] = useState(true);
  const [message, setMessage] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);

  const loadCompetitions = useCallback(async (scope: string) => {
    if (!scope) { setCompetitions([]); return; }
    const [items, participantItems] = await Promise.all([
      adminApi<Competition[]>(`/api/competitions?tenantId=${scope}`),
      adminApi<Participant[]>(`/api/participants?tenantId=${scope}`),
    ]);
    setCompetitions(items.sort((left, right) => left.name.localeCompare(right.name)));
    setParticipants(participantItems.sort((left, right) => left.name.localeCompare(right.name)));
    setAssistedParticipantId(current => current || participantItems[0]?.id || '');
  }, []);

  const loadQueue = useCallback(async (id: string) => {
    if (!id) { setQueue(null); return; }
    const [reviewQueue, formQueue] = await Promise.all([
      adminApi<ReviewQueue>(`/api/registrations/competition/${id}/review-queue`),
      adminApi<FormSubmission[]>(`/api/registrationforms/competition/${id}/submission-review-queue`),
    ]);
    setQueue(reviewQueue); setSubmissions(formQueue);
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
      setViewing(null);
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

  async function createAssistedRegistration(event: FormEvent) {
    event.preventDefault();
    if (!competitionId || !assistedParticipantId) return;
    setBusyId('assisted'); setError(null);
    try {
      const created = await adminApi<{ id: string }>(`/api/registrations/competition/${competitionId}`, {
        method: 'POST', body: JSON.stringify({ participantId: assistedParticipantId, type: assistedType === 'Individual' ? 0 : 1 }),
      });
      if (assistedSubmit) await adminApi(`/api/registrations/${created.id}/submit`, { method: 'POST' });
      await loadQueue(competitionId); setAssistedOpen(false);
      setMessage(`Staff-assisted ${assistedSubmit ? 'submission' : 'draft'} created.`);
    } catch (reason) { setError(reason instanceof Error ? reason.message : 'Unable to create registration.'); }
    finally { setBusyId(''); }
  }

  async function reviewSubmission(item: FormSubmission, action: 'review' | 'approve' | 'reject' | 'request-changes') {
    const notes = action === 'reject' || action === 'request-changes'
      ? window.prompt(action === 'reject' ? 'Rejection reason:' : 'Requested changes:') : null;
    if ((action === 'reject' || action === 'request-changes') && !notes?.trim()) return;
    setBusyId(item.id); setError(null);
    try {
      await adminApi(`/api/registrationforms/submissions/${item.id}/${action}`, {
        method: 'POST', body: JSON.stringify(action === 'reject' || action === 'request-changes' ? { notes } : {}),
      });
      await loadQueue(competitionId); setSubmissionViewing(null);
      setMessage(`${item.participantName ?? 'Submission'}: ${action.replace('-', ' ')} recorded.`);
    } catch (reason) { setError(reason instanceof Error ? reason.message : 'Unable to update form submission.'); }
    finally { setBusyId(''); }
  }

  const columns: DataTableColumn<Registration>[] = [
    { key: 'participant', header: 'Participant', render: item => <div><p className="font-semibold text-gray-950">{item.participantName}</p><p className="text-xs text-gray-500">{item.participantType} · {item.type}</p></div> },
    { key: 'status', header: 'Status', render: item => <span className={`rounded-full px-2 py-1 text-xs font-bold ${statusTone(item.status)}`}>{item.status.replace(/([a-z])([A-Z])/g, '$1 $2')}</span> },
    { key: 'eligibility', header: 'Eligibility', render: item => item.eligible ? <span className="font-semibold text-green-700">Eligible</span> : <span className="text-amber-700">Review required</span> },
    { key: 'submitted', header: 'Submitted', render: item => item.registeredAt?.slice(0, 10) ?? 'Not submitted' },
    { key: 'reviewer', header: 'Reviewer', render: item => item.reviewerName ?? '—' },
    { key: 'actions', header: <span className="sr-only">Actions</span>, className: 'text-right', render: item => <div onClick={event => event.stopPropagation()}><Button size="sm" variant="ghost" onClick={() => setViewing(item)}><Eye className="mr-1 h-4 w-4" />View</Button></div> },
  ];
  const submissionColumns: DataTableColumn<FormSubmission>[] = [
    { key: 'participant', header: 'Applicant', render: item => <div><p className="font-semibold text-gray-950">{item.participantName ?? item.participantId}</p><p className="text-xs text-gray-500">{item.participantType ?? 'Participant'}</p></div> },
    { key: 'form', header: 'Form', render: item => <div><p className="font-semibold">{item.formName ?? 'Registration form'}</p><p className="text-xs text-gray-500">v{item.formVersion} · revision {item.revision}</p></div> },
    { key: 'status', header: 'Form status', render: item => <span className={`rounded-full px-2 py-1 text-xs font-bold ${statusTone(item.status)}`}>{item.status.replace(/([a-z])([A-Z])/g, '$1 $2')}</span> },
    { key: 'registration', header: 'Registration', render: item => item.registrationStatus.replace(/([a-z])([A-Z])/g, '$1 $2') },
    { key: 'actions', header: <span className="sr-only">Actions</span>, className: 'text-right', render: item => <Button size="sm" variant="ghost" onClick={() => setSubmissionViewing(item)}><Eye className="mr-1 h-4 w-4" />View</Button> },
  ];

  return <div className="space-y-6">
    <section className="overflow-hidden rounded-3xl bg-gradient-to-br from-slate-950 via-slate-900 to-slate-800 p-6 text-white shadow-sm">
      <div className="flex flex-col gap-5 xl:flex-row xl:items-end xl:justify-between">
        <div className="max-w-3xl">
          <p className="text-xs font-bold uppercase tracking-[0.2em] text-slate-400">Registration operations</p>
          <h1 className="mt-2 text-3xl font-black tracking-tight">Registration review command centre</h1>
          <p className="mt-2 max-w-2xl text-sm leading-6 text-slate-300">
            Review eligibility, enforce policy, record official decisions with mandatory reasons, and preserve an auditable applicant history.
          </p>
        </div>
        <div className="grid grid-cols-2 gap-3 sm:grid-cols-4">
          <RegistrationMetric label="Visible" value={visible.length} />
          <RegistrationMetric label="Submitted" value={queue?.counts.Submitted ?? 0} />
          <RegistrationMetric label="Under review" value={queue?.counts.UnderReview ?? 0} />
          <RegistrationMetric label="Approved" value={queue?.counts.Approved ?? 0} />
        </div>
      </div>
    </section>
    {error && <p role="alert" className="rounded-md bg-red-50 p-3 text-sm text-red-700">{error}</p>}
    {message && <p role="status" className="rounded-md bg-green-50 p-3 text-sm text-green-800">{message}</p>}
    <section className="rounded-2xl border border-slate-200 bg-white p-5 shadow-sm">
      <div className="mb-4">
        <p className="text-xs font-bold uppercase tracking-[0.16em] text-slate-400">Queue controls</p>
        <h2 className="mt-1 text-lg font-extrabold text-slate-950">Review scope</h2>
      </div>
      <div className="grid gap-4 md:grid-cols-3">
      {platform && <label className="text-sm font-semibold">Tenant<select value={tenantId} onChange={event => setTenantId(event.target.value)} className="mt-1 h-10 w-full rounded-md border bg-white px-3 font-normal"><option value="">Select tenant</option>{tenants.map(item => <option key={item.id} value={item.id}>{item.name}</option>)}</select></label>}
      <label className="text-sm font-semibold">Competition<select aria-label="Competition" value={competitionId} onChange={event => void chooseCompetition(event.target.value)} className="mt-1 h-10 w-full rounded-md border bg-white px-3 font-normal"><option value="">Select competition</option>{competitions.map(item => <option key={item.id} value={item.id}>{item.name}</option>)}</select></label>
      <label className="text-sm font-semibold">Status filter<select value={filter} onChange={event => setFilter(event.target.value)} className="mt-1 h-10 w-full rounded-md border bg-white px-3 font-normal"><option value="ALL">All statuses</option>{Object.keys(queue?.counts ?? {}).map(status => <option key={status} value={status.toUpperCase()}>{status} ({queue?.counts[status] ?? 0})</option>)}</select></label>
      </div>
      {competitionId && canManage && <Button className="mt-4" onClick={() => setAssistedOpen(true)}><Plus className="mr-2 h-4 w-4" />New staff-assisted registration</Button>}
    </section>
    {!competitionId && <p className="rounded-2xl border border-dashed border-slate-200 bg-slate-50 p-8 text-center text-sm text-slate-500">Select a competition to open its official review queue.</p>}
    {queue && <>
      <section className="rounded-2xl border border-slate-200 bg-white p-5 shadow-sm"><div className="flex flex-wrap items-start justify-between gap-3"><div><p className="text-xs font-bold uppercase tracking-[0.16em] text-slate-400">Policy</p><h2 className="mt-1 text-lg font-extrabold text-slate-950">Eligibility policy</h2><p className="text-sm text-slate-500">Unset limits are not enforced. Ages are calculated on the earliest edition start date.</p></div><button type="button" disabled={busyId === 'policy'} onClick={() => void savePolicy()} className="rounded-md bg-gray-950 px-4 py-2 text-sm font-semibold text-white disabled:opacity-50">Save policy</button></div><div className="mt-4 grid gap-3 sm:grid-cols-2 lg:grid-cols-4">{([['minimumSquadSize','Min squad'],['maximumSquadSize','Max squad'],['minimumAge','Min age'],['maximumAge','Max age'],['minimumWeightKg','Min weight kg'],['maximumWeightKg','Max weight kg']] as const).map(([key,label]) => <label key={key} className="text-xs font-semibold">{label}<input type="number" min="0" value={policy[key] ?? ''} onChange={event => setPolicy(current => ({ ...current, [key]: event.target.value === '' ? null : Number(event.target.value) }))} className="mt-1 h-10 w-full rounded-md border px-3 font-normal" /></label>)}<label className="text-xs font-semibold">Registration deadline<input type="datetime-local" value={policy.registrationDeadline?.slice(0,16) ?? ''} onChange={event => setPolicy(current => ({ ...current, registrationDeadline: event.target.value ? new Date(event.target.value).toISOString() : null }))} className="mt-1 h-10 w-full rounded-md border px-3 font-normal" /></label><label className="text-xs font-semibold">Required documents<input value={documentTypes} onChange={event => setDocumentTypes(event.target.value)} placeholder="Passport, medical certificate" className="mt-1 h-10 w-full rounded-md border px-3 font-normal" /></label></div><div className="mt-3 flex flex-wrap gap-5 text-sm"><label><input type="checkbox" checked={policy.requireSquadNumbers} onChange={event => setPolicy(current => ({ ...current, requireSquadNumbers: event.target.checked }))} className="mr-2" />Require squad numbers</label><label><input type="checkbox" checked={policy.requireUniqueSquadNumbers} onChange={event => setPolicy(current => ({ ...current, requireUniqueSquadNumbers: event.target.checked }))} className="mr-2" />Require unique squad numbers</label></div></section>
      <section className="grid gap-3 sm:grid-cols-3 lg:grid-cols-6">{['Draft', 'Submitted', 'UnderReview', 'Approved', 'Rejected', 'Waitlisted', 'Withdrawn'].map(status => <div key={status} className="rounded-2xl border border-slate-200 bg-white p-4 shadow-sm"><p className="text-xs font-semibold uppercase text-gray-500">{status.replace(/([a-z])([A-Z])/g, '$1 $2')}</p><p className="text-2xl font-bold">{queue.counts[status] ?? 0}</p></div>)}</section>
      <DataTable caption="Registration review queue" columns={columns} rows={visible} rowKey={item => item.id} onRowClick={setViewing} emptyMessage="No registrations match this status." />
      <section className="space-y-3"><div><p className="text-xs font-bold uppercase tracking-[0.16em] text-slate-400">Submitted forms</p><h2 className="mt-1 text-lg font-extrabold text-slate-950">Form-submission review queue</h2><p className="text-sm text-slate-500">Review the versioned application and requested documents before making the canonical registration decision.</p></div><DataTable caption="Form submission review queue" columns={submissionColumns} rows={submissions} rowKey={item => item.id} onRowClick={setSubmissionViewing} emptyMessage="No applicant form submissions for this competition." /></section>
      {viewing && <ResourceDialog title={viewing.participantName} description={`${viewing.participantType} registration · ${viewing.status.replace(/([a-z])([A-Z])/g, '$1 $2')}`} onClose={() => setViewing(null)}>
        <div className="grid gap-4 sm:grid-cols-3"><Detail label="Registration type">{viewing.type}</Detail><Detail label="Submitted">{viewing.registeredAt?.slice(0, 10) ?? 'Not submitted'}</Detail><Detail label="Eligibility">{viewing.eligible ? 'Eligible' : 'Review required'}</Detail></div>
        {viewing.reason && <p className="mt-4 rounded-xl border border-slate-200 bg-slate-50 p-3 text-sm"><strong>Recorded reason:</strong> {viewing.reason}</p>}
        {viewing.reviewerName && <p className="mt-3 text-sm text-gray-600">Reviewed by {viewing.reviewerName} {viewing.reviewedAt ? `on ${viewing.reviewedAt.slice(0, 10)}` : ''}</p>}
        {(checks[viewing.id] || viewing.eligibilityIssues?.length > 0) && <div className={`mt-4 rounded p-3 text-sm ${(checks[viewing.id]?.isEligible ?? viewing.eligible) ? 'bg-green-50 text-green-800' : 'bg-amber-50 text-amber-900'}`}><strong>{(checks[viewing.id]?.isEligible ?? viewing.eligible) ? 'Eligibility checks passed.' : 'Eligibility issues:'}</strong>{(checks[viewing.id]?.issues ?? viewing.eligibilityIssues)?.length > 0 && <ul className="mt-1 list-disc pl-5">{(checks[viewing.id]?.issues ?? viewing.eligibilityIssues).map(issue => <li key={issue}>{issue}</li>)}</ul>}{viewing.eligibilityOverrideReason && <p className="mt-2"><strong>Override:</strong> {viewing.eligibilityOverrideReason}</p>}</div>}
        {actions(viewing.status).length > 0 && canManage && <div className="mt-5 space-y-3 border-t border-slate-200 pt-4"><label className="block text-xs font-semibold">Decision reason<input value={reasons[viewing.id] ?? ''} onChange={event => setReasons(current => ({ ...current, [viewing.id]: event.target.value }))} placeholder="Required for approval, rejection, waitlist, withdrawal, or override" maxLength={1000} className="mt-1 h-10 w-full rounded-md border px-3 font-normal" /></label><div className="flex flex-wrap items-center gap-2"><button type="button" disabled={busyId === viewing.id} onClick={() => void checkEligibility(viewing)} className="rounded-md border bg-white px-3 py-2 text-xs font-semibold disabled:opacity-50">Check eligibility</button>{canOverride && <label className="px-2 text-xs font-semibold"><input type="checkbox" checked={overrides[viewing.id] ?? false} onChange={event => setOverrides(current => ({ ...current, [viewing.id]: event.target.checked }))} className="mr-2" />Override eligibility</label>}{actions(viewing.status).map(action => <button key={action} type="button" disabled={busyId === viewing.id} onClick={() => void transition(viewing, action)} className={`rounded-md px-3 py-2 text-xs font-semibold disabled:opacity-50 ${action === 'approve' ? 'bg-green-700 text-white' : action === 'reject' ? 'bg-red-700 text-white' : 'border bg-white'}`}>{decisionLabels[action]}</button>)}</div></div>}
      </ResourceDialog>}
      {submissionViewing && <ResourceDialog title={submissionViewing.participantName ?? 'Form submission'} description={`${submissionViewing.formName ?? 'Registration form'} · ${submissionViewing.status.replace(/([a-z])([A-Z])/g, '$1 $2')}`} onClose={() => setSubmissionViewing(null)}><div className="grid gap-4 sm:grid-cols-3"><Detail label="Form version">v{submissionViewing.formVersion}</Detail><Detail label="Revision">{submissionViewing.revision}</Detail><Detail label="Canonical registration">{submissionViewing.registrationStatus}</Detail></div><div className="mt-5"><p className="text-xs font-semibold uppercase tracking-wide text-gray-500">Submitted values</p><pre className="mt-2 max-h-80 overflow-auto rounded-xl bg-slate-950 p-4 text-xs text-slate-100">{prettyJson(submissionViewing.formData)}</pre></div>{submissionViewing.reviewNotes && <p className="mt-4 rounded-xl bg-amber-50 p-3 text-sm text-amber-900"><strong>Review note:</strong> {submissionViewing.reviewNotes}</p>}<div className="mt-5 flex flex-wrap gap-2 border-t pt-4">{submissionViewing.status === 'Submitted' && <Button disabled={busyId === submissionViewing.id} onClick={() => void reviewSubmission(submissionViewing, 'review')}>Start review</Button>}{submissionViewing.status === 'UnderReview' && <><Button disabled={busyId === submissionViewing.id} onClick={() => void reviewSubmission(submissionViewing, 'approve')}>Approve form</Button><Button variant="outline" disabled={busyId === submissionViewing.id} onClick={() => void reviewSubmission(submissionViewing, 'request-changes')}>Request changes</Button><Button variant="destructive" disabled={busyId === submissionViewing.id} onClick={() => void reviewSubmission(submissionViewing, 'reject')}>Reject form</Button></>}</div></ResourceDialog>}
      {assistedOpen && <ResourceDialog title="New staff-assisted registration" description="Create an entry on behalf of an athlete or team already managed by this tenant." onClose={() => setAssistedOpen(false)}><form onSubmit={createAssistedRegistration} className="space-y-4"><label className="block text-sm font-semibold">Participant<select required value={assistedParticipantId} onChange={event => setAssistedParticipantId(event.target.value)} className="mt-1 h-10 w-full rounded-md border bg-white px-3 font-normal"><option value="">Select participant</option>{participants.map(item => <option key={item.id} value={item.id}>{item.name} · {item.type}</option>)}</select></label><label className="block text-sm font-semibold">Entry type<select value={assistedType} onChange={event => setAssistedType(event.target.value)} className="mt-1 h-10 w-full rounded-md border bg-white px-3 font-normal"><option>Team</option><option>Individual</option></select></label><label className="flex items-center gap-2 text-sm"><input type="checkbox" checked={assistedSubmit} onChange={event => setAssistedSubmit(event.target.checked)} />Submit immediately for review</label><Button disabled={busyId === 'assisted' || !assistedParticipantId}>Create registration</Button></form></ResourceDialog>}
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

function RegistrationMetric({ label, value }: { label: string; value: number }) {
  return <div className="min-w-24 rounded-2xl border border-white/10 bg-white/5 px-4 py-3 backdrop-blur">
    <div className="text-[10px] font-bold uppercase tracking-[0.16em] text-slate-400">{label}</div>
    <div className="mt-1 text-2xl font-black text-white">{value}</div>
  </div>;
}

function Detail({ label, children }: { label: string; children: React.ReactNode }) {
  return <div><p className="text-xs font-semibold uppercase tracking-wide text-gray-500">{label}</p><div className="mt-1 text-sm text-gray-900">{children}</div></div>;
}

function prettyJson(value: string) {
  try { return JSON.stringify(JSON.parse(value), null, 2); } catch { return value; }
}

