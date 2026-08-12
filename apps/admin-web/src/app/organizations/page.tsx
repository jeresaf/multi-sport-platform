'use client';

import { FormEvent, useEffect, useState } from 'react';
import { adminApi, getStoredUser, hasAnyPermission, isPlatformOperator } from '@/lib/admin-api';

type Tenant = { id: string; name: string };
type Summary = { id: string; tenantId: string; name: string; shortName: string; type: string | number };
type Entry = { id: string; athleteId: string; jerseyNumber?: string | null; position?: string | null; isActive: boolean; joinedAt: string; leftAt?: string | null; movementReason?: string | null };
type Roster = { id: string; name: string; season: string; startsAt?: string | null; endsAt?: string | null; competitionId?: string | null; isActive: boolean; entries: Entry[] };
type Registration = { id: string; competitionId?: string | null; contestId?: string | null; type: string; status: string; registeredAt?: string | null; createdAt: string };
type Profile = { id: string; tenantId: string; kind: string; name: string; shortName: string; logoUrl?: string | null; homeVenue?: string | null; coach?: string | null; manager?: string | null; contactEmail?: string | null; contactPhone?: string | null; rosters: Roster[]; registrations: Registration[] };
type AuditEntry = { id: string; action: string; reason?: string | null; timestamp: string };
type ProfileForm = { name: string; shortName: string; logoUrl: string; homeVenue: string; coach: string; manager: string; contactEmail: string; contactPhone: string; reason: string };

const blank: ProfileForm = { name: '', shortName: '', logoUrl: '', homeVenue: '', coach: '', manager: '', contactEmail: '', contactPhone: '', reason: '' };

export default function OrganizationsPage() {
  const user = getStoredUser();
  const platform = isPlatformOperator(user);
  const canCreate = hasAnyPermission(user, ['participants.create']);
  const canReadSensitive = hasAnyPermission(user, ['participants.read-sensitive']);
  const canManage = hasAnyPermission(user, ['participants.manage-sensitive']);
  const [tenants, setTenants] = useState<Tenant[]>([]);
  const [tenantId, setTenantId] = useState(user?.tenantId ?? '');
  const [organizations, setOrganizations] = useState<Summary[]>([]);
  const [athletes, setAthletes] = useState<Summary[]>([]);
  const [selectedId, setSelectedId] = useState('');
  const [kind, setKind] = useState('Team');
  const [profile, setProfile] = useState<Profile | null>(null);
  const [history, setHistory] = useState<AuditEntry[]>([]);
  const [form, setForm] = useState<ProfileForm>(blank);
  const [roster, setRoster] = useState({ name: '', season: '', startsAt: '', endsAt: '', competitionId: '', reason: '' });
  const [movement, setMovement] = useState({ rosterId: '', athleteId: '', jerseyNumber: '', position: '', reason: '' });
  const [busy, setBusy] = useState(false);
  const [message, setMessage] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);

  async function load(scope = tenantId) {
    if (!scope) { setOrganizations([]); setAthletes([]); return; }
    const [teams, clubs, athleteItems] = await Promise.all([
      adminApi<Summary[]>(`/api/participants?tenantId=${scope}&participantType=Team`),
      adminApi<Summary[]>(`/api/participants?tenantId=${scope}&participantType=Club`),
      adminApi<Summary[]>(`/api/participants?tenantId=${scope}&participantType=Athlete`),
    ]);
    setOrganizations([...teams, ...clubs].sort((a, b) => a.name.localeCompare(b.name)));
    setAthletes(athleteItems.sort((a, b) => a.name.localeCompare(b.name)));
  }

  useEffect(() => {
    if (platform) adminApi<Tenant[]>('/api/tenants').then(items => { setTenants(items); setTenantId(current => current || items[0]?.id || ''); }).catch(reason => setError(reason instanceof Error ? reason.message : 'Unable to load tenants.'));
  }, [platform]);

  useEffect(() => {
    setSelectedId(''); setProfile(null); setHistory([]); setForm(blank);
    void load(tenantId).catch(reason => setError(reason instanceof Error ? reason.message : 'Unable to load teams and clubs.'));
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [tenantId]);

  async function selectOrganization(id: string) {
    setSelectedId(id); setError(null); setMessage(null);
    const summary = organizations.find(item => item.id === id);
    if (!id) { setProfile(null); setHistory([]); setForm(blank); return; }
    if (!canReadSensitive) { setProfile(null); setHistory([]); setForm({ ...blank, name: summary?.name ?? '', shortName: summary?.shortName ?? '' }); return; }
    try {
      const [details, audit] = await Promise.all([
        adminApi<Profile>(`/api/participants/organizations/${id}/profile`),
        adminApi<AuditEntry[]>(`/api/participants/organizations/${id}/history`),
      ]);
      setProfile(details); setHistory(audit);
      setForm({ name: details.name, shortName: details.shortName, logoUrl: details.logoUrl ?? '', homeVenue: details.homeVenue ?? '', coach: details.coach ?? '', manager: details.manager ?? '', contactEmail: details.contactEmail ?? '', contactPhone: details.contactPhone ?? '', reason: '' });
    } catch (reason) { setError(reason instanceof Error ? reason.message : 'Unable to load organization profile.'); }
  }

  async function submitProfile(event: FormEvent) {
    event.preventDefault(); setBusy(true); setError(null); setMessage(null);
    try {
      if (!selectedId) {
        const created = await adminApi<Summary>('/api/participants', { method: 'POST', body: JSON.stringify({ tenantId, name: form.name, shortName: form.shortName, type: kind }) });
        await load(); setMessage(`${kind} created. Complete its profile.`); await selectOrganization(created.id);
      } else {
        await adminApi<Profile>(`/api/participants/organizations/${selectedId}/profile`, { method: 'PUT', body: JSON.stringify(form) });
        await load(); await selectOrganization(selectedId); setMessage('Organization profile saved and audited.');
      }
    } catch (reason) { setError(reason instanceof Error ? reason.message : 'Unable to save organization profile.'); }
    finally { setBusy(false); }
  }

  async function createRoster() {
    if (!selectedId) return; setBusy(true); setError(null);
    try {
      await adminApi<Profile>(`/api/participants/teams/${selectedId}/rosters`, { method: 'POST', body: JSON.stringify({ ...roster, startsAt: roster.startsAt || null, endsAt: roster.endsAt || null, competitionId: roster.competitionId || null }) });
      setRoster({ name: '', season: '', startsAt: '', endsAt: '', competitionId: '', reason: '' }); await selectOrganization(selectedId); setMessage('Seasonal roster created and audited.');
    } catch (reason) { setError(reason instanceof Error ? reason.message : 'Unable to create roster.'); }
    finally { setBusy(false); }
  }

  async function transferAthlete() {
    if (!selectedId || !movement.rosterId || !movement.athleteId) return; setBusy(true); setError(null);
    try {
      await adminApi<Profile>(`/api/participants/teams/${selectedId}/rosters/${movement.rosterId}/athletes/${movement.athleteId}/transfer`, { method: 'POST', body: JSON.stringify({ jerseyNumber: movement.jerseyNumber || null, position: movement.position || null, reason: movement.reason }) });
      setMovement({ rosterId: '', athleteId: '', jerseyNumber: '', position: '', reason: '' }); await selectOrganization(selectedId); setMessage('Athlete movement recorded; prior memberships remain in history.');
    } catch (reason) { setError(reason instanceof Error ? reason.message : 'Unable to move athlete.'); }
    finally { setBusy(false); }
  }

  const field = (key: keyof ProfileForm, label: string, type = 'text') => <label className="text-sm font-semibold">{label}<input type={type} value={form[key]} disabled={Boolean(selectedId) && !canManage} onChange={event => setForm(current => ({ ...current, [key]: event.target.value }))} className="mt-1 h-10 w-full rounded-md border px-3 font-normal disabled:bg-gray-100" /></label>;
  const input = (key: keyof typeof roster, label: string, type = 'text') => <label className="text-xs font-semibold">{label}<input type={type} value={roster[key]} onChange={event => setRoster(current => ({ ...current, [key]: event.target.value }))} className="mt-1 h-9 w-full rounded-md border px-2 font-normal" /></label>;

  return <div className="space-y-6">
    <div><h1 className="text-3xl font-bold text-gray-950">Team and club profiles</h1><p className="mt-1 text-sm text-gray-600">Tenant organizations, seasonal rosters, athlete movement history, and historical competition registrations.</p></div>
    {error && <p role="alert" className="rounded-md bg-red-50 p-3 text-sm text-red-700">{error}</p>}{message && <p className="rounded-md bg-green-50 p-3 text-sm text-green-800">{message}</p>}
    {platform && <label className="block max-w-md text-sm font-semibold">Tenant<select value={tenantId} onChange={event => setTenantId(event.target.value)} className="mt-1 h-10 w-full rounded-md border bg-white px-3 font-normal"><option value="">Select tenant</option>{tenants.map(item => <option key={item.id} value={item.id}>{item.name}</option>)}</select></label>}
    <div className="grid gap-6 lg:grid-cols-[18rem_1fr]">
      <aside className="space-y-3 rounded-xl border bg-white p-4"><h2 className="font-bold">Teams and clubs</h2><select value={selectedId} onChange={event => void selectOrganization(event.target.value)} className="h-10 w-full rounded-md border bg-white px-3"><option value="">Create new</option>{organizations.map(item => <option key={item.id} value={item.id}>{item.name}</option>)}</select><p className="text-xs text-gray-500">{organizations.length} profiles</p>{!canReadSensitive && <p className="rounded bg-amber-50 p-2 text-xs text-amber-800">Contact, roster, registration, and history data require protected profile access.</p>}</aside>
      <div className="space-y-5">
        <form onSubmit={submitProfile} className="space-y-4 rounded-xl border bg-white p-5"><h2 className="font-bold">{selectedId ? `${profile?.kind ?? 'Organization'} profile` : 'Create organization'}</h2>{!selectedId && <label className="text-sm font-semibold">Kind<select value={kind} onChange={event => setKind(event.target.value)} className="mt-1 h-10 w-full rounded-md border bg-white px-3 font-normal"><option>Team</option><option>Club</option></select></label>}<div className="grid gap-4 md:grid-cols-2">{field('name', 'Name')}{field('shortName', 'Short name')}{field('logoUrl', 'Logo URL', 'url')}{field('homeVenue', 'Home venue')}{field('coach', 'Coach')}{field('manager', 'Manager')}{field('contactEmail', 'Contact email', 'email')}{field('contactPhone', 'Contact phone', 'tel')}</div>{selectedId && canManage && field('reason', 'Audit reason')}<button disabled={busy || (!selectedId && !canCreate) || (Boolean(selectedId) && !canManage)} className="rounded-md bg-primary px-4 py-2 text-sm font-semibold text-white disabled:opacity-50">{selectedId ? 'Save profile' : `Create ${kind.toLowerCase()}`}</button></form>
        {profile?.kind === 'Team' && canManage && <section className="space-y-3 rounded-xl border bg-white p-5"><h2 className="font-bold">Create seasonal roster</h2><div className="grid gap-3 md:grid-cols-3">{input('name', 'Roster name')}{input('season', 'Season')}{input('startsAt', 'Starts', 'date')}{input('endsAt', 'Ends', 'date')}{input('competitionId', 'Competition ID')}{input('reason', 'Audit reason')}</div><button type="button" onClick={() => void createRoster()} disabled={busy || !roster.name || !roster.season} className="rounded-md border px-4 py-2 text-sm font-semibold disabled:opacity-50">Create roster</button></section>}
        {profile?.kind === 'Team' && canManage && profile.rosters.length > 0 && <section className="space-y-3 rounded-xl border bg-white p-5"><h2 className="font-bold">Move athlete to roster</h2><div className="grid gap-3 md:grid-cols-3"><select value={movement.rosterId} onChange={event => setMovement(current => ({ ...current, rosterId: event.target.value }))} className="h-10 rounded-md border bg-white px-3"><option value="">Target roster</option>{profile.rosters.filter(item => item.isActive).map(item => <option key={item.id} value={item.id}>{item.name} · {item.season}</option>)}</select><select value={movement.athleteId} onChange={event => setMovement(current => ({ ...current, athleteId: event.target.value }))} className="h-10 rounded-md border bg-white px-3"><option value="">Athlete</option>{athletes.map(item => <option key={item.id} value={item.id}>{item.name}</option>)}</select>{(['jerseyNumber', 'position', 'reason'] as const).map(key => <input key={key} aria-label={key} placeholder={key.replace(/([A-Z])/g, ' $1')} value={movement[key]} onChange={event => setMovement(current => ({ ...current, [key]: event.target.value }))} className="h-10 rounded-md border px-3" />)}</div><button type="button" onClick={() => void transferAthlete()} disabled={busy || !movement.rosterId || !movement.athleteId || !movement.reason} className="rounded-md border px-4 py-2 text-sm font-semibold disabled:opacity-50">Record movement</button></section>}
        {profile && canReadSensitive && <section className="rounded-xl border bg-white p-5"><h2 className="mb-3 font-bold">Seasonal roster history</h2>{profile.rosters.length === 0 ? <p className="text-sm text-gray-500">No rosters recorded.</p> : profile.rosters.map(item => <div key={item.id} className="mb-4 rounded-lg bg-gray-50 p-3"><strong>{item.name} · {item.season}</strong><p className="text-xs text-gray-500">{item.startsAt?.slice(0, 10) ?? 'No start'} – {item.endsAt?.slice(0, 10) ?? 'Open ended'}</p><ul className="mt-2 space-y-1 text-xs">{item.entries.map(entry => <li key={entry.id}>{athletes.find(athlete => athlete.id === entry.athleteId)?.name ?? entry.athleteId} · {entry.isActive ? 'Active' : `Left ${entry.leftAt?.slice(0, 10) ?? ''}`} {entry.movementReason && `· ${entry.movementReason}`}</li>)}</ul></div>)}</section>}
        {profile && canReadSensitive && <section className="grid gap-5 rounded-xl border bg-white p-5 md:grid-cols-2"><div><h2 className="mb-2 font-bold">Historical registrations</h2>{profile.registrations.length ? <ul className="space-y-2 text-xs">{profile.registrations.map(item => <li key={item.id} className="rounded bg-gray-50 p-2"><strong>{item.status}</strong> · {item.type} · {item.competitionId ?? item.contestId}</li>)}</ul> : <p className="text-sm text-gray-500">No registrations recorded.</p>}</div><div><h2 className="mb-2 font-bold">Audit history</h2>{history.length ? <ul className="space-y-2 text-xs">{history.map(item => <li key={item.id} className="rounded bg-gray-50 p-2"><strong>{item.action}</strong> · {item.reason}</li>)}</ul> : <p className="text-sm text-gray-500">No changes recorded.</p>}</div></section>}
      </div>
    </div>
  </div>;
}
