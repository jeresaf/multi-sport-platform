'use client';

import Link from 'next/link';
import { FormEvent, useCallback, useEffect, useMemo, useState } from 'react';
import { useParams } from 'next/navigation';
import { ArrowLeft, CheckCircle2, FileUp, Save, Send, UserPlus } from 'lucide-react';
import { applicantApi, getApplicantSession, platformApi } from '@/lib/applicant-api';
import { publicApi, resolveTenantSlug, tenantPath } from '@/lib/public-api';

type Option = { value: string; label: string };
type Field = { id: string; fieldType: string; label: string; placeholder?: string | null; isRequired: boolean; options?: Option[] | null; conditionalFieldId?: string | null; conditionalValue?: string | null; order: number; helpText?: string | null; consentText?: string | null; allowedContentTypes?: string[] | null };
type RegistrationForm = { id: string; name: string; description?: string | null; version: number; fields: Field[] };
type Participant = { id: string; name: string; shortName: string; type: string };
type Draft = { id: string; formSubmissionId: string; status: string; formStatus: string };
type RegistrationDetail = { id: string; status: string; participantId: string; submission?: { id: string; formId: string; status: string; formData: string; formSchemaSnapshot: string; reviewNotes?: string | null } | null };
type TenantIdentity = { id: string };

export default function CompetitionApplicationPage() {
  const params = useParams<{ competitionId: string }>();
  const [tenantSlug, setTenantSlug] = useState('');
  const [forms, setForms] = useState<RegistrationForm[]>([]);
  const [participants, setParticipants] = useState<Participant[]>([]);
  const [formId, setFormId] = useState('');
  const [participantId, setParticipantId] = useState('');
  const [draftId, setDraftId] = useState('');
  const [values, setValues] = useState<Record<string, unknown>>({});
  const [newName, setNewName] = useState('');
  const [newShortName, setNewShortName] = useState('');
  const [newType, setNewType] = useState('Individual');
  const [creatingParticipant, setCreatingParticipant] = useState(false);
  const [busy, setBusy] = useState(false);
  const [message, setMessage] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);

  const selectedForm = useMemo(() => forms.find(item => item.id === formId), [formId, forms]);
  const loadParticipants = useCallback(async () => {
    const items = await applicantApi<Participant[]>('/api/applicant/participants');
    setParticipants(items); setParticipantId(current => current || items[0]?.id || '');
  }, []);

  useEffect(() => {
    const slug = resolveTenantSlug();
    const session = getApplicantSession();
    if (!session) {
      const returnTo = `/apply/${params.competitionId}${window.location.search}`;
      window.location.replace(tenantPath(`/login?returnTo=${encodeURIComponent(returnTo)}`, slug));
      return;
    }
    Promise.all([
      publicApi<TenantIdentity>('', undefined, slug),
      platformApi<RegistrationForm[]>(`/api/registrationforms/public/competition/${params.competitionId}`),
      applicantApi<Participant[]>('/api/applicant/participants'),
    ]).then(([identity, availableForms, participantItems]) => {
      if (session.user.tenantId !== identity.id) {
        const returnTo = `/apply/${params.competitionId}${window.location.search}`;
        window.location.replace(tenantPath(`/login?returnTo=${encodeURIComponent(returnTo)}`, slug));
        return;
      }
      setTenantSlug(slug); setForms(availableForms); setFormId(availableForms[0]?.id ?? '');
      setParticipants(participantItems); setParticipantId(current => current || participantItems[0]?.id || '');
      const existingId = new URLSearchParams(window.location.search).get('registration');
      if (existingId) applicantApi<RegistrationDetail>(`/api/applicant/registrations/${existingId}`).then(detail => {
        if (!detail.submission) return;
        setDraftId(detail.id); setParticipantId(detail.participantId); setFormId(detail.submission.formId);
        try { setValues(JSON.parse(detail.submission.formData) as Record<string, unknown>); } catch { setValues({}); }
      }).catch(reason => setError(reason instanceof Error ? reason.message : 'Unable to resume draft.'));
    }).catch(reason => setError(reason instanceof Error ? reason.message : 'Unable to open registration.'));
  }, [loadParticipants, params.competitionId]);

  async function createParticipant(event: FormEvent) {
    event.preventDefault(); setBusy(true); setError(null);
    try {
      const created = await applicantApi<Participant>('/api/applicant/participants', { method: 'POST', body: JSON.stringify({ name: newName, shortName: newShortName, type: newType }) });
      await loadParticipants(); setParticipantId(created.id); setCreatingParticipant(false); setMessage(`${created.name} is ready to register.`);
    } catch (reason) { setError(reason instanceof Error ? reason.message : 'Unable to create participant.'); }
    finally { setBusy(false); }
  }

  async function startDraft() {
    setBusy(true); setError(null);
    try {
      const draft = await applicantApi<Draft>('/api/applicant/registrations', { method: 'POST', body: JSON.stringify({ competitionId: params.competitionId, participantId, formId }) });
      setDraftId(draft.id); setMessage('Draft created. You can save and return at any time.');
    } catch (reason) { setError(reason instanceof Error ? reason.message : 'Unable to create draft.'); }
    finally { setBusy(false); }
  }

  async function saveDraft(showMessage = true) {
    if (!draftId) return;
    await applicantApi(`/api/applicant/registrations/${draftId}/draft`, { method: 'PUT', body: JSON.stringify({ formData: values }) });
    if (showMessage) setMessage('Draft saved.');
  }

  async function submit() {
    setBusy(true); setError(null); setMessage(null);
    try {
      await saveDraft(false);
      await applicantApi(`/api/applicant/registrations/${draftId}/submit`, { method: 'POST' });
      window.location.assign(tenantPath('/my-registrations?submitted=1', tenantSlug));
    } catch (reason) { setError(reason instanceof Error ? reason.message : 'Unable to submit registration.'); }
    finally { setBusy(false); }
  }

  async function upload(field: Field, file: File) {
    if (!draftId) return;
    setBusy(true); setError(null);
    try {
      const body = new FormData(); body.append('file', file);
      const uploaded = await applicantApi<{ url: string; fileName: string; contentType: string; size: number }>(`/api/applicant/registrations/${draftId}/files/${field.id}`, { method: 'POST', body });
      setValues(current => ({ ...current, [field.id]: uploaded })); setMessage(`${file.name} uploaded.`);
    } catch (reason) { setError(reason instanceof Error ? reason.message : 'Upload failed.'); }
    finally { setBusy(false); }
  }

  return <main className="app-container py-8">
    <Link href={tenantPath(`/competitions/${params.competitionId}`, tenantSlug)} className="inline-flex items-center gap-2 text-sm font-bold muted"><ArrowLeft className="h-4 w-4" />Back to competition</Link>
    <div className="mt-5 grid gap-6 lg:grid-cols-[0.75fr_1.25fr]">
      <aside className="space-y-5"><div><p className="text-xs font-bold uppercase tracking-[0.18em] text-[var(--brand-primary)]">Competition application</p><h1 className="mt-2 text-3xl font-black">Register a participant</h1><p className="mt-2 muted">Choose who you are entering, complete the official form, save a draft, then submit it for review.</p></div>
        {error && <p role="alert" className="rounded-xl bg-red-50 p-3 text-sm text-red-700">{error}</p>}{message && <p role="status" className="rounded-xl bg-green-50 p-3 text-sm text-green-800">{message}</p>}
        <section className="surface rounded-2xl border border-[var(--color-border)] p-5"><h2 className="font-black">1. Participant</h2>{participants.length > 0 && !creatingParticipant && <label className="mt-4 block text-sm font-bold">Your athlete or team<select value={participantId} disabled={Boolean(draftId)} onChange={event => setParticipantId(event.target.value)} className="mt-1 h-11 w-full rounded-xl border bg-[var(--color-surface)] px-3 font-normal">{participants.map(item => <option key={item.id} value={item.id}>{item.name} · {item.type}</option>)}</select></label>}<button disabled={Boolean(draftId)} onClick={() => setCreatingParticipant(value => !value)} className="mt-3 inline-flex items-center gap-2 text-sm font-bold text-[var(--brand-primary)] disabled:opacity-40"><UserPlus className="h-4 w-4" />Create another participant</button>{(participants.length === 0 || creatingParticipant) && <form onSubmit={createParticipant} className="mt-4 space-y-3"><input required placeholder="Athlete or team name" value={newName} onChange={event => setNewName(event.target.value)} className="h-10 w-full rounded-xl border px-3" /><input required placeholder="Short name" value={newShortName} onChange={event => setNewShortName(event.target.value)} className="h-10 w-full rounded-xl border px-3" /><select value={newType} onChange={event => setNewType(event.target.value)} className="h-10 w-full rounded-xl border bg-[var(--color-surface)] px-3"><option>Individual</option><option>Team</option></select><button disabled={busy} className="rounded-xl bg-[var(--brand-primary)] px-4 py-2 text-sm font-bold text-white">Create participant</button></form>}</section>
        <section className="surface rounded-2xl border border-[var(--color-border)] p-5"><h2 className="font-black">2. Official form</h2>{forms.length ? <label className="mt-4 block text-sm font-bold">Registration form<select value={formId} disabled={Boolean(draftId)} onChange={event => setFormId(event.target.value)} className="mt-1 h-11 w-full rounded-xl border bg-[var(--color-surface)] px-3 font-normal">{forms.map(item => <option key={item.id} value={item.id}>{item.name} · v{item.version}</option>)}</select></label> : <p className="mt-3 text-sm muted">Registration is not open because no active form is published.</p>}{selectedForm?.description && <p className="mt-2 text-sm muted">{selectedForm.description}</p>}{!draftId && forms.length > 0 && <button disabled={busy || !participantId || !formId} onClick={() => void startDraft()} className="mt-4 w-full rounded-xl bg-[var(--brand-primary)] px-4 py-3 font-bold text-white disabled:opacity-50">Start registration</button>}</section>
        <Link href={tenantPath('/my-registrations', tenantSlug)} className="block rounded-xl border border-[var(--color-border)] p-3 text-center text-sm font-bold">View My Registrations</Link>
      </aside>
      <section className="surface rounded-2xl border border-[var(--color-border)] p-5 sm:p-7"><div className="flex items-start justify-between gap-3"><div><p className="text-xs font-bold uppercase tracking-wider muted">3. Application details</p><h2 className="mt-1 text-xl font-black">{selectedForm?.name ?? 'Select a form'}</h2></div>{draftId && <span className="rounded-full bg-amber-50 px-3 py-1 text-xs font-bold text-amber-800">Draft</span>}</div>{!draftId ? <div className="mt-8 rounded-xl border border-dashed border-[var(--color-border)] p-8 text-center muted">Select a participant and start the registration to unlock the form.</div> : <div className="mt-6 space-y-5">{selectedForm?.fields.sort((a,b) => a.order-b.order).filter(field => visible(field, values)).map(field => <DynamicField key={field.id} field={field} value={values[field.id]} onChange={value => setValues(current => ({ ...current, [field.id]: value }))} onUpload={file => void upload(field, file)} />)}<div className="flex flex-col gap-3 border-t border-[var(--color-border)] pt-5 sm:flex-row"><button disabled={busy} onClick={() => void saveDraft()} className="inline-flex flex-1 items-center justify-center gap-2 rounded-xl border border-[var(--color-border)] px-4 py-3 font-bold"><Save className="h-4 w-4" />Save draft</button><button disabled={busy} onClick={() => void submit()} className="inline-flex flex-1 items-center justify-center gap-2 rounded-xl bg-[var(--brand-primary)] px-4 py-3 font-bold text-white"><Send className="h-4 w-4" />Submit registration</button></div></div>}</section>
    </div>
  </main>;
}

function visible(field: Field, values: Record<string, unknown>) { return !field.conditionalFieldId || String(values[field.conditionalFieldId] ?? '').toLowerCase() === String(field.conditionalValue ?? '').toLowerCase(); }
function DynamicField({ field, value, onChange, onUpload }: { field: Field; value: unknown; onChange: (value: unknown) => void; onUpload: (file: File) => void }) {
  const label = <span>{field.label}{field.isRequired && <span className="text-red-600"> *</span>}</span>;
  const inputClass = 'mt-1 h-11 w-full rounded-xl border border-[var(--color-border)] bg-[var(--color-surface)] px-3 font-normal';
  if (field.fieldType === 'Consent') return <label className="flex gap-3 rounded-xl bg-[var(--color-surface-muted)] p-4 text-sm"><input type="checkbox" checked={value === true} onChange={event => onChange(event.target.checked)} /><span>{field.consentText || field.label}</span></label>;
  if (field.fieldType === 'Checkbox') return <fieldset><legend className="text-sm font-bold">{label}</legend><div className="mt-2 flex flex-wrap gap-3">{field.options?.map(option => <label key={option.value} className="flex gap-2 text-sm"><input type="checkbox" checked={Array.isArray(value) && value.includes(option.value)} onChange={event => { const current = Array.isArray(value) ? value as string[] : []; onChange(event.target.checked ? [...current, option.value] : current.filter(item => item !== option.value)); }} />{option.label}</label>)}</div></fieldset>;
  if (field.fieldType === 'Select') return <label className="block text-sm font-bold">{label}<select value={String(value ?? '')} onChange={event => onChange(event.target.value)} className={inputClass}><option value="">Select</option>{field.options?.map(option => <option key={option.value} value={option.value}>{option.label}</option>)}</select></label>;
  if (field.fieldType === 'File') return <label className="block text-sm font-bold">{label}<span className="mt-1 flex min-h-20 items-center justify-center rounded-xl border border-dashed border-[var(--color-border)] p-3"><FileUp className="mr-2 h-5 w-5" /><input type="file" accept={field.allowedContentTypes?.join(',')} onChange={event => event.target.files?.[0] && onUpload(event.target.files[0])} /></span>{typeof value === 'object' && value && <span className="mt-1 flex items-center gap-1 text-xs text-green-700"><CheckCircle2 className="h-3 w-3" />{(value as { fileName?: string }).fileName ?? 'Uploaded'}</span>}</label>;
  return <label className="block text-sm font-bold">{label}<input type={field.fieldType === 'Number' ? 'number' : field.fieldType === 'Date' ? 'date' : 'text'} value={String(value ?? '')} onChange={event => onChange(field.fieldType === 'Number' && event.target.value ? Number(event.target.value) : event.target.value)} placeholder={field.placeholder ?? ''} className={inputClass} />{field.helpText && <span className="text-xs muted">{field.helpText}</span>}</label>;
}
