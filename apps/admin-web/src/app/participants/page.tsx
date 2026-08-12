'use client';

import { FormEvent, useEffect, useState } from 'react';
import { adminApi, getStoredUser, hasAnyPermission, isPlatformOperator } from '@/lib/admin-api';

type Tenant = { id: string; name: string };
type Sport = { id: string; name: string; code: string; isActive: boolean };
type AthleteSummary = { id: string; tenantId: string; name: string; shortName: string; athlete?: { nationality?: string | null; photoUrl?: string | null } | null };
type Document = { type: string; number: string; issuingCountry: string; expiresAt: string; fileUrl: string };
type AthleteProfile = {
  id: string; tenantId: string; name: string; shortName: string;
  dateOfBirth?: string | null; gender?: string | null; competitionCategory?: string | null;
  nationality?: string | null; photoUrl?: string | null; contactEmail?: string | null;
  contactPhone?: string | null; emergencyContactName?: string | null;
  emergencyContactRelationship?: string | null; emergencyContactPhone?: string | null;
  medicalClearanceStatus: string; medicalClearance: boolean;
  medicalClearanceExpiresAt?: string | null; identificationDocuments: Array<{
    type: string; number: string; issuingCountry?: string | null; expiresAt?: string | null; fileUrl?: string | null;
  }>;
  sportIds: string[];
};
type AuditEntry = { id: string; action: string; reason?: string | null; timestamp: string };
type FormState = {
  name: string; shortName: string; dateOfBirth: string; gender: string; competitionCategory: string;
  nationality: string; photoUrl: string; contactEmail: string; contactPhone: string;
  emergencyContactName: string; emergencyContactRelationship: string; emergencyContactPhone: string;
  medicalClearanceStatus: string; medicalClearanceExpiresAt: string; reason: string; documents: Document[];
};

const emptyForm: FormState = {
  name: '', shortName: '', dateOfBirth: '', gender: '', competitionCategory: '', nationality: '',
  photoUrl: '', contactEmail: '', contactPhone: '', emergencyContactName: '',
  emergencyContactRelationship: '', emergencyContactPhone: '', medicalClearanceStatus: 'NotProvided',
  medicalClearanceExpiresAt: '', reason: '', documents: [],
};

export default function ParticipantsPage() {
  const user = getStoredUser();
  const platform = isPlatformOperator(user);
  const canCreate = hasAnyPermission(user, ['participants.create']);
  const canReadSensitive = hasAnyPermission(user, ['participants.read-sensitive']);
  const canManageSensitive = hasAnyPermission(user, ['participants.manage-sensitive']);
  const [tenants, setTenants] = useState<Tenant[]>([]);
  const [tenantId, setTenantId] = useState(user?.tenantId ?? '');
  const [athletes, setAthletes] = useState<AthleteSummary[]>([]);
  const [sports, setSports] = useState<Sport[]>([]);
  const [selectedId, setSelectedId] = useState('');
  const [profile, setProfile] = useState<AthleteProfile | null>(null);
  const [history, setHistory] = useState<AuditEntry[]>([]);
  const [form, setForm] = useState<FormState>(emptyForm);
  const [sportId, setSportId] = useState('');
  const [busy, setBusy] = useState(false);
  const [message, setMessage] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);

  async function loadAthletes(scope = tenantId) {
    if (!scope) return setAthletes([]);
    const items = await adminApi<AthleteSummary[]>(`/api/participants?tenantId=${scope}&participantType=Athlete`);
    setAthletes(items.sort((a, b) => a.name.localeCompare(b.name)));
  }

  useEffect(() => {
    Promise.all([
      adminApi<Sport[]>('/api/sports').then(items => setSports(items.filter(item => item.isActive))),
      platform ? adminApi<Tenant[]>('/api/tenants').then(items => {
        setTenants(items);
        setTenantId(current => current || items[0]?.id || '');
      }) : Promise.resolve(),
    ]).catch(reason => setError(reason instanceof Error ? reason.message : 'Unable to load participant data.'));
  }, [platform]);

  useEffect(() => {
    setSelectedId(''); setProfile(null); setHistory([]); setForm(emptyForm);
    void loadAthletes(tenantId).catch(reason => setError(reason instanceof Error ? reason.message : 'Unable to load athletes.'));
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [tenantId]);

  async function selectAthlete(id: string) {
    setSelectedId(id); setError(null); setMessage(null);
    const summary = athletes.find(item => item.id === id);
    if (!id) { setProfile(null); setHistory([]); return setForm(emptyForm); }
    if (!canReadSensitive) {
      setProfile(null); setHistory([]);
      return setForm({ ...emptyForm, name: summary?.name ?? '', shortName: summary?.shortName ?? '', nationality: summary?.athlete?.nationality ?? '', photoUrl: summary?.athlete?.photoUrl ?? '' });
    }
    try {
      const [details, audit] = await Promise.all([
        adminApi<AthleteProfile>(`/api/participants/athletes/${id}/profile`),
        adminApi<AuditEntry[]>(`/api/participants/athletes/${id}/history`),
      ]);
      setProfile(details); setHistory(audit);
      setForm({
        name: details.name, shortName: details.shortName,
        dateOfBirth: details.dateOfBirth?.slice(0, 10) ?? '', gender: details.gender ?? '',
        competitionCategory: details.competitionCategory ?? '', nationality: details.nationality ?? '',
        photoUrl: details.photoUrl ?? '', contactEmail: details.contactEmail ?? '', contactPhone: details.contactPhone ?? '',
        emergencyContactName: details.emergencyContactName ?? '', emergencyContactRelationship: details.emergencyContactRelationship ?? '',
        emergencyContactPhone: details.emergencyContactPhone ?? '', medicalClearanceStatus: details.medicalClearanceStatus,
        medicalClearanceExpiresAt: details.medicalClearanceExpiresAt?.slice(0, 10) ?? '', reason: '',
        documents: details.identificationDocuments.map(item => ({ type: item.type, number: item.number, issuingCountry: item.issuingCountry ?? '', expiresAt: item.expiresAt?.slice(0, 10) ?? '', fileUrl: item.fileUrl ?? '' })),
      });
    } catch (reason) { setError(reason instanceof Error ? reason.message : 'Unable to load the athlete profile.'); }
  }

  async function createAthlete(event: FormEvent) {
    event.preventDefault(); if (!canCreate || !tenantId) return;
    setBusy(true); setError(null);
    try {
      const created = await adminApi<AthleteSummary>('/api/participants/athletes', { method: 'POST', body: JSON.stringify({ tenantId, name: form.name, shortName: form.shortName }) });
      await loadAthletes(); setMessage('Athlete created. Complete the protected profile fields.');
      await selectAthlete(created.id);
    } catch (reason) { setError(reason instanceof Error ? reason.message : 'Unable to create athlete.'); }
    finally { setBusy(false); }
  }

  async function saveProfile(event: FormEvent) {
    event.preventDefault(); if (!selectedId || !canManageSensitive) return;
    setBusy(true); setError(null); setMessage(null);
    try {
      const updated = await adminApi<AthleteProfile>(`/api/participants/athletes/${selectedId}/profile`, {
        method: 'PUT', body: JSON.stringify({
          ...form,
          dateOfBirth: form.dateOfBirth || null,
          medicalClearanceExpiresAt: form.medicalClearanceExpiresAt || null,
          identificationDocuments: form.documents.map(item => ({ ...item, expiresAt: item.expiresAt || null, fileUrl: item.fileUrl || null, issuingCountry: item.issuingCountry || null })),
          documents: undefined,
        }),
      });
      setProfile(updated); await loadAthletes(); setMessage('Protected athlete profile saved and audited.');
      await selectAthlete(selectedId);
    } catch (reason) { setError(reason instanceof Error ? reason.message : 'Unable to save athlete profile.'); }
    finally { setBusy(false); }
  }

  async function registerSport() {
    if (!selectedId || !sportId || !canManageSensitive) return;
    setBusy(true); setError(null);
    try {
      await adminApi<void>(`/api/participants/athletes/${selectedId}/sports/${sportId}`, { method: 'POST' });
      setMessage('Sport registration added and audited.'); setSportId(''); await selectAthlete(selectedId);
    } catch (reason) { setError(reason instanceof Error ? reason.message : 'Unable to register sport.'); }
    finally { setBusy(false); }
  }

  const field = (key: keyof FormState, label: string, type = 'text') => <label className="text-sm font-semibold">{label}<input type={type} value={String(form[key])} disabled={!canManageSensitive && Boolean(selectedId)} onChange={event => setForm(current => ({ ...current, [key]: event.target.value }))} className="mt-1 h-10 w-full rounded-md border px-3 font-normal disabled:bg-gray-100" /></label>;

  return <div className="space-y-6">
    <div><h1 className="text-3xl font-bold text-gray-950">Athlete profiles</h1><p className="mt-1 text-sm text-gray-600">Reusable tenant athletes with permission-protected contact, medical, emergency, and identification records.</p></div>
    {error && <p role="alert" className="rounded-md bg-red-50 p-3 text-sm text-red-700">{error}</p>}
    {message && <p className="rounded-md bg-green-50 p-3 text-sm text-green-800">{message}</p>}
    {platform && <label className="block max-w-md text-sm font-semibold">Tenant<select value={tenantId} onChange={event => setTenantId(event.target.value)} className="mt-1 h-10 w-full rounded-md border bg-white px-3 font-normal"><option value="">Select tenant</option>{tenants.map(item => <option key={item.id} value={item.id}>{item.name}</option>)}</select></label>}
    <div className="grid gap-6 lg:grid-cols-[18rem_1fr]">
      <aside className="space-y-3 rounded-xl border bg-white p-4"><h2 className="font-bold">Tenant athletes</h2><select aria-label="Select athlete" value={selectedId} onChange={event => void selectAthlete(event.target.value)} className="h-10 w-full rounded-md border bg-white px-3"><option value="">Create a new athlete</option>{athletes.map(item => <option key={item.id} value={item.id}>{item.name}</option>)}</select><p className="text-xs text-gray-500">{athletes.length} reusable profiles</p>{!canReadSensitive && <p className="rounded bg-amber-50 p-2 text-xs text-amber-800">Sensitive fields require the dedicated profile permission.</p>}</aside>
      <form onSubmit={selectedId ? saveProfile : createAthlete} className="space-y-5 rounded-xl border bg-white p-5">
        <div className="grid gap-4 md:grid-cols-2">{field('name', 'Full name')}{field('shortName', 'Short/display name')}{field('dateOfBirth', 'Date of birth', 'date')}{field('gender', 'Gender')}{field('competitionCategory', 'Competition category')}{field('nationality', 'Nationality')}{field('photoUrl', 'Photo URL', 'url')}</div>
        {selectedId && canReadSensitive && <>
          <section><h2 className="mb-3 font-bold">Protected contact and emergency details</h2><div className="grid gap-4 md:grid-cols-2">{field('contactEmail', 'Contact email', 'email')}{field('contactPhone', 'Contact phone', 'tel')}{field('emergencyContactName', 'Emergency contact name')}{field('emergencyContactRelationship', 'Relationship')}{field('emergencyContactPhone', 'Emergency contact phone', 'tel')}</div></section>
          <section><h2 className="mb-3 font-bold">Medical clearance</h2><div className="grid gap-4 md:grid-cols-2"><label className="text-sm font-semibold">Status<select value={form.medicalClearanceStatus} disabled={!canManageSensitive} onChange={event => setForm(current => ({ ...current, medicalClearanceStatus: event.target.value }))} className="mt-1 h-10 w-full rounded-md border bg-white px-3 font-normal disabled:bg-gray-100"><option>NotProvided</option><option>Pending</option><option>Cleared</option><option>Expired</option><option>Rejected</option></select></label>{field('medicalClearanceExpiresAt', 'Expires', 'date')}</div><p className="mt-2 text-xs text-gray-500">Effective clearance: {profile?.medicalClearance ? 'Yes' : 'No'}</p></section>
          <section><div className="mb-3 flex items-center justify-between"><h2 className="font-bold">Identification documents</h2>{canManageSensitive && <button type="button" onClick={() => setForm(current => ({ ...current, documents: [...current.documents, { type: '', number: '', issuingCountry: '', expiresAt: '', fileUrl: '' }] }))} className="rounded-md border px-3 py-2 text-xs font-semibold">Add document</button>}</div>{form.documents.map((document, index) => <div key={index} className="mb-3 grid gap-3 rounded-lg bg-gray-50 p-3 md:grid-cols-2 xl:grid-cols-5">{(['type', 'number', 'issuingCountry', 'expiresAt', 'fileUrl'] as const).map(key => <label key={key} className="text-xs font-semibold">{key.replace(/([A-Z])/g, ' $1')}<input type={key === 'expiresAt' ? 'date' : key === 'fileUrl' ? 'url' : 'text'} value={document[key]} disabled={!canManageSensitive} onChange={event => setForm(current => ({ ...current, documents: current.documents.map((item, position) => position === index ? { ...item, [key]: event.target.value } : item) }))} className="mt-1 h-9 w-full rounded-md border px-2 font-normal disabled:bg-gray-100" /></label>)}</div>)}</section>
          <section><h2 className="mb-3 font-bold">Sport registrations</h2><div className="flex flex-wrap gap-2">{profile?.sportIds.map(id => <span key={id} className="rounded-full bg-blue-50 px-3 py-1 text-xs font-semibold text-blue-800">{sports.find(item => item.id === id)?.name ?? id}</span>)}</div>{canManageSensitive && <div className="mt-3 flex gap-2"><select value={sportId} onChange={event => setSportId(event.target.value)} className="h-10 flex-1 rounded-md border bg-white px-3"><option value="">Select sport</option>{sports.filter(item => !profile?.sportIds.includes(item.id)).map(item => <option key={item.id} value={item.id}>{item.name}</option>)}</select><button type="button" disabled={!sportId || busy} onClick={() => void registerSport()} className="rounded-md border px-4 text-sm font-semibold disabled:opacity-50">Register</button></div>}</section>
          {canManageSensitive && field('reason', 'Audit reason')}
          <section><h2 className="mb-2 font-bold">Audit history</h2>{history.length ? <ul className="space-y-2">{history.map(item => <li key={item.id} className="rounded bg-gray-50 p-2 text-xs"><strong>{item.action}</strong> · {new Date(item.timestamp).toLocaleString()} · {item.reason}</li>)}</ul> : <p className="text-sm text-gray-500">No profile changes recorded.</p>}</section>
        </>}
        <button type="submit" disabled={busy || (!selectedId && !canCreate) || (Boolean(selectedId) && !canManageSensitive)} className="rounded-md bg-primary px-4 py-2 text-sm font-semibold text-white disabled:opacity-50">{selectedId ? 'Save protected profile' : 'Create athlete'}</button>
      </form>
    </div>
  </div>;
}
