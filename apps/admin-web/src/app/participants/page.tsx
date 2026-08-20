'use client';

import { FormEvent, useEffect, useMemo, useState } from 'react';
import { Activity, Plus, Search, ShieldCheck, UserRound, UsersRound } from 'lucide-react';
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
  const [search, setSearch] = useState('');
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

  const filteredAthletes = useMemo(
    () =>
      athletes.filter((athlete) =>
        `${athlete.name} ${athlete.shortName} ${athlete.athlete?.nationality ?? ''}`
          .toLowerCase()
          .includes(search.toLowerCase())
      ),
    [athletes, search]
  );

  const selectedSummary = useMemo(
    () => athletes.find((athlete) => athlete.id === selectedId) ?? null,
    [athletes, selectedId]
  );

  const registeredSportCount = profile?.sportIds.length ?? 0;

  const field = (key: keyof FormState, label: string, type = 'text') => <label className="text-sm font-semibold">{label}<input type={type} value={String(form[key])} disabled={!canManageSensitive && Boolean(selectedId)} onChange={event => setForm(current => ({ ...current, [key]: event.target.value }))} className="mt-1 h-10 w-full rounded-md border px-3 font-normal disabled:bg-gray-100" /></label>;

  return <div className="space-y-6">
    <section className="overflow-hidden rounded-2xl border border-gray-200 bg-white shadow-sm">
      <div className="border-b border-gray-100 bg-gradient-to-br from-gray-950 via-gray-900 to-gray-800 px-6 py-7 text-white lg:px-8">
        <div className="flex flex-wrap items-end justify-between gap-5">
          <div className="max-w-2xl">
            <p className="text-xs font-bold uppercase tracking-[0.22em] text-white/60">Participant operations</p>
            <h1 className="mt-2 text-3xl font-bold tracking-tight sm:text-4xl">Athletes & profiles</h1>
            <p className="mt-2 max-w-xl text-sm leading-6 text-white/70">
              Manage reusable athletes, registrations, protected records, medical clearance, and audit history.
            </p>
          </div>
          {canCreate && (
            <button
              type="button"
              onClick={() => void selectAthlete('')}
              className="inline-flex items-center gap-2 rounded-xl bg-primary px-4 py-2.5 text-sm font-semibold text-white shadow-sm transition hover:opacity-90"
            >
              <Plus className="h-4 w-4" />
              New athlete
            </button>
          )}
        </div>
      </div>

      <div className="grid gap-px bg-gray-200 sm:grid-cols-2 xl:grid-cols-4">
        <ParticipantMetric label="Athletes" value={athletes.length} hint="Reusable profiles" />
        <ParticipantMetric label="Sports" value={sports.length} hint="Active sports" />
        <ParticipantMetric label="Registered" value={registeredSportCount} hint="Selected athlete" />
        <ParticipantMetric
          label="Clearance"
          value={profile?.medicalClearance ? 'Cleared' : selectedId ? profile?.medicalClearanceStatus ?? 'Restricted' : '—'}
          hint="Selected athlete"
        />
      </div>
    </section>

    {error && <p role="alert" className="rounded-xl border border-red-100 bg-red-50 p-3 text-sm text-red-700">{error}</p>}
    {message && <p className="rounded-xl border border-emerald-100 bg-emerald-50 p-3 text-sm text-emerald-800">{message}</p>}

    {platform && (
      <section className="rounded-2xl border border-gray-200 bg-white p-4 shadow-sm">
        <label className="block max-w-md text-sm font-semibold text-gray-700">
          Tenant
          <select
            value={tenantId}
            onChange={event => setTenantId(event.target.value)}
            className="mt-1 h-11 w-full rounded-xl border border-gray-200 bg-gray-50 px-3 font-normal outline-none focus:border-primary focus:bg-white"
          >
            <option value="">Select tenant</option>
            {tenants.map(item => <option key={item.id} value={item.id}>{item.name}</option>)}
          </select>
        </label>
      </section>
    )}

    <div className="grid gap-6 xl:grid-cols-[22rem_minmax(0,1fr)]">
      <aside className="self-start overflow-hidden rounded-2xl border border-gray-200 bg-white shadow-sm xl:sticky xl:top-6">
        <div className="border-b border-gray-100 p-4">
          <div className="flex items-center justify-between gap-3">
            <div>
              <p className="text-xs font-semibold uppercase tracking-wider text-gray-400">Roster directory</p>
              <h2 className="mt-1 font-bold text-gray-950">Tenant athletes</h2>
            </div>
            <span className="rounded-full bg-gray-100 px-2.5 py-1 text-xs font-semibold text-gray-600">{athletes.length}</span>
          </div>

          <label className="relative mt-4 block">
            <span className="sr-only">Search athletes</span>
            <Search className="absolute left-3 top-3 h-4 w-4 text-gray-400" />
            <input
              value={search}
              onChange={event => setSearch(event.target.value)}
              placeholder="Search name or nationality"
              className="w-full rounded-xl border border-gray-200 bg-gray-50 py-2.5 pl-10 pr-3 text-sm outline-none focus:border-primary focus:bg-white"
            />
          </label>
        </div>

        <div className="max-h-[680px] space-y-1 overflow-y-auto p-2">
          {canCreate && (
            <button
              type="button"
              onClick={() => void selectAthlete('')}
              className={`flex w-full items-center gap-3 rounded-xl p-3 text-left transition ${
                !selectedId ? 'bg-primary/10 text-primary' : 'text-gray-700 hover:bg-gray-50'
              }`}
            >
              <span className="flex h-10 w-10 items-center justify-center rounded-xl bg-white shadow-sm">
                <Plus className="h-4 w-4" />
              </span>
              <span>
                <strong className="block text-sm">Create a new athlete</strong>
                <span className="text-xs opacity-70">Start a reusable profile</span>
              </span>
            </button>
          )}

          {filteredAthletes.map(item => {
            const active = item.id === selectedId;
            return (
              <button
                type="button"
                key={item.id}
                onClick={() => void selectAthlete(item.id)}
                className={`flex w-full items-center gap-3 rounded-xl p-3 text-left transition ${
                  active ? 'bg-primary/10 text-primary' : 'text-gray-700 hover:bg-gray-50'
                }`}
              >
                <span className={`flex h-10 w-10 shrink-0 items-center justify-center rounded-xl ${
                  active ? 'bg-white' : 'bg-gray-100'
                }`}>
                  <UserRound className="h-4 w-4" />
                </span>
                <span className="min-w-0">
                  <strong className="block truncate text-sm">{item.name}</strong>
                  <span className="block truncate text-xs opacity-70">
                    {[item.shortName, item.athlete?.nationality].filter(Boolean).join(' · ') || 'Athlete profile'}
                  </span>
                </span>
              </button>
            );
          })}

          {filteredAthletes.length === 0 && (
            <p className="px-3 py-8 text-center text-sm text-gray-500">No athletes match this search.</p>
          )}
        </div>

        {!canReadSensitive && (
          <div className="border-t border-gray-100 bg-amber-50 p-3 text-xs text-amber-800">
            Sensitive fields require the dedicated profile permission.
          </div>
        )}
      </aside>

      <form onSubmit={selectedId ? saveProfile : createAthlete} className="overflow-hidden rounded-2xl border border-gray-200 bg-white shadow-sm">
        <div className="sticky top-0 z-10 flex flex-wrap items-center justify-between gap-4 border-b border-gray-100 bg-white/95 px-5 py-4 backdrop-blur">
          <div className="flex min-w-0 items-center gap-3">
            <div className="flex h-11 w-11 shrink-0 items-center justify-center rounded-xl bg-primary/10 text-primary">
              {selectedId ? <UserRound className="h-5 w-5" /> : <UsersRound className="h-5 w-5" />}
            </div>
            <div className="min-w-0">
              <p className="text-xs font-semibold uppercase tracking-wider text-gray-400">
                {selectedId ? 'Athlete profile' : 'New athlete'}
              </p>
              <h2 className="truncate text-lg font-bold text-gray-950">
                {selectedId ? selectedSummary?.name ?? form.name : 'Create athlete profile'}
              </h2>
            </div>
          </div>

          {selectedId && canReadSensitive && (
            <div className="flex flex-wrap gap-2">
              <span className={`inline-flex items-center gap-1.5 rounded-full px-2.5 py-1 text-xs font-semibold ${
                profile?.medicalClearance ? 'bg-emerald-50 text-emerald-700' : 'bg-amber-50 text-amber-700'
              }`}>
                <ShieldCheck className="h-3.5 w-3.5" />
                {profile?.medicalClearance ? 'Medical cleared' : profile?.medicalClearanceStatus ?? 'Not provided'}
              </span>
              <span className="inline-flex items-center gap-1.5 rounded-full bg-blue-50 px-2.5 py-1 text-xs font-semibold text-blue-700">
                <Activity className="h-3.5 w-3.5" />
                {registeredSportCount} sport{registeredSportCount === 1 ? '' : 's'}
              </span>
            </div>
          )}
        </div>

        <div className="space-y-6 p-5">
          <section>
            <div className="mb-4">
              <h3 className="font-bold text-gray-950">Identity</h3>
              <p className="mt-1 text-sm text-gray-500">Core participant information shared across competitions.</p>
            </div>
            <div className="grid gap-4 md:grid-cols-2">
              {field('name', 'Full name')}
              {field('shortName', 'Short/display name')}
              {field('dateOfBirth', 'Date of birth', 'date')}
              {field('gender', 'Gender')}
              {field('competitionCategory', 'Competition category')}
              {field('nationality', 'Nationality')}
              {field('photoUrl', 'Photo URL', 'url')}
            </div>
          </section>

          {selectedId && canReadSensitive && <>
            <section className="border-t border-gray-100 pt-6">
              <div className="mb-4">
                <h3 className="font-bold text-gray-950">Protected contact & emergency details</h3>
                <p className="mt-1 text-sm text-gray-500">Restricted information available only to authorized operators.</p>
              </div>
              <div className="grid gap-4 md:grid-cols-2">
                {field('contactEmail', 'Contact email', 'email')}
                {field('contactPhone', 'Contact phone', 'tel')}
                {field('emergencyContactName', 'Emergency contact name')}
                {field('emergencyContactRelationship', 'Relationship')}
                {field('emergencyContactPhone', 'Emergency contact phone', 'tel')}
              </div>
            </section>

            <section className="border-t border-gray-100 pt-6">
              <div className="mb-4">
                <h3 className="font-bold text-gray-950">Medical clearance</h3>
                <p className="mt-1 text-sm text-gray-500">Track eligibility and clearance validity.</p>
              </div>
              <div className="grid gap-4 md:grid-cols-2">
                <label className="text-sm font-semibold">
                  Status
                  <select
                    value={form.medicalClearanceStatus}
                    disabled={!canManageSensitive}
                    onChange={event => setForm(current => ({ ...current, medicalClearanceStatus: event.target.value }))}
                    className="mt-1 h-10 w-full rounded-md border bg-white px-3 font-normal disabled:bg-gray-100"
                  >
                    <option>NotProvided</option><option>Pending</option><option>Cleared</option><option>Expired</option><option>Rejected</option>
                  </select>
                </label>
                {field('medicalClearanceExpiresAt', 'Expires', 'date')}
              </div>
            </section>

            <section className="border-t border-gray-100 pt-6">
              <div className="mb-4 flex items-center justify-between gap-3">
                <div>
                  <h3 className="font-bold text-gray-950">Identification documents</h3>
                  <p className="mt-1 text-sm text-gray-500">{form.documents.length} document record{form.documents.length === 1 ? '' : 's'}.</p>
                </div>
                {canManageSensitive && (
                  <button
                    type="button"
                    onClick={() => setForm(current => ({ ...current, documents: [...current.documents, { type: '', number: '', issuingCountry: '', expiresAt: '', fileUrl: '' }] }))}
                    className="rounded-xl border border-gray-200 px-3 py-2 text-xs font-semibold hover:bg-gray-50"
                  >
                    Add document
                  </button>
                )}
              </div>
              {form.documents.map((document, index) => (
                <div key={index} className="mb-3 grid gap-3 rounded-xl border border-gray-100 bg-gray-50 p-3 md:grid-cols-2 xl:grid-cols-5">
                  {(['type', 'number', 'issuingCountry', 'expiresAt', 'fileUrl'] as const).map(key => (
                    <label key={key} className="text-xs font-semibold">
                      {key.replace(/([A-Z])/g, ' $1')}
                      <input
                        type={key === 'expiresAt' ? 'date' : key === 'fileUrl' ? 'url' : 'text'}
                        value={document[key]}
                        disabled={!canManageSensitive}
                        onChange={event => setForm(current => ({
                          ...current,
                          documents: current.documents.map((item, position) =>
                            position === index ? { ...item, [key]: event.target.value } : item)
                        }))}
                        className="mt-1 h-9 w-full rounded-md border px-2 font-normal disabled:bg-gray-100"
                      />
                    </label>
                  ))}
                </div>
              ))}
              {form.documents.length === 0 && <p className="rounded-xl bg-gray-50 p-4 text-sm text-gray-500">No identification documents recorded.</p>}
            </section>

            <section className="border-t border-gray-100 pt-6">
              <div className="mb-4">
                <h3 className="font-bold text-gray-950">Sport registrations</h3>
                <p className="mt-1 text-sm text-gray-500">Register this athlete for the sports they can participate in.</p>
              </div>
              <div className="flex flex-wrap gap-2">
                {profile?.sportIds.map(id => (
                  <span key={id} className="rounded-full bg-blue-50 px-3 py-1 text-xs font-semibold text-blue-800">
                    {sports.find(item => item.id === id)?.name ?? id}
                  </span>
                ))}
              </div>
              {canManageSensitive && (
                <div className="mt-3 flex gap-2">
                  <select value={sportId} onChange={event => setSportId(event.target.value)} className="h-10 flex-1 rounded-md border bg-white px-3">
                    <option value="">Select sport</option>
                    {sports.filter(item => !profile?.sportIds.includes(item.id)).map(item => <option key={item.id} value={item.id}>{item.name}</option>)}
                  </select>
                  <button type="button" disabled={!sportId || busy} onClick={() => void registerSport()} className="rounded-md border px-4 text-sm font-semibold disabled:opacity-50">Register</button>
                </div>
              )}
            </section>

            {canManageSensitive && (
              <section className="border-t border-gray-100 pt-6">
                {field('reason', 'Audit reason')}
              </section>
            )}

            <section className="border-t border-gray-100 pt-6">
              <h3 className="mb-3 font-bold text-gray-950">Audit history</h3>
              {history.length ? (
                <ul className="space-y-2">
                  {history.map(item => (
                    <li key={item.id} className="rounded-xl border border-gray-100 bg-gray-50 p-3 text-xs">
                      <strong>{item.action}</strong> · {new Date(item.timestamp).toLocaleString()} · {item.reason}
                    </li>
                  ))}
                </ul>
              ) : (
                <p className="text-sm text-gray-500">No profile changes recorded.</p>
              )}
            </section>
          </>}

          <div className="border-t border-gray-100 pt-5">
            <button
              type="submit"
              disabled={busy || (!selectedId && !canCreate) || (Boolean(selectedId) && !canManageSensitive)}
              className="rounded-xl bg-primary px-5 py-2.5 text-sm font-semibold text-white shadow-sm disabled:opacity-50"
            >
              {selectedId ? 'Save protected profile' : 'Create athlete'}
            </button>
          </div>
        </div>
      </form>
    </div>

  </div>;
}

function ParticipantMetric({
  label,
  value,
  hint,
}: {
  label: string;
  value: number | string;
  hint: string;
}) {
  return (
    <div className="bg-white px-5 py-4">
      <p className="text-xs font-semibold uppercase tracking-wider text-gray-400">{label}</p>
      <div className="mt-1 flex items-end justify-between gap-3">
        <strong className="truncate text-2xl font-bold tracking-tight text-gray-950">{value}</strong>
        <span className="shrink-0 text-xs text-gray-500">{hint}</span>
      </div>
    </div>
  );
}

