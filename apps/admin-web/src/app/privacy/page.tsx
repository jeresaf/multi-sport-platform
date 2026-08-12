'use client';

import { useCallback, useEffect, useMemo, useState } from 'react';
import { Download, Play, RefreshCw, ShieldCheck, Trash2 } from 'lucide-react';
import { adminApi, adminDownload, getStoredUser, hasAnyPermission } from '@/lib/admin-api';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';

type Tenant = { id: string; name: string };
type Profile = { id: string; email: string; firstName: string; lastName: string; phoneNumber?: string | null; tenantId?: string | null };
type Settings = { id: string; profileVisibility: number; showActivity: boolean; allowAnalytics: boolean; allowMarketing: boolean };
type Consent = { id: string; kind: number; granted: boolean; policyVersion: string; occurredAt: string };
type PrivacyRequest = { id: string; userId: string; kind: number; status: number; details: string; response?: string | null; requestedAt: string; downloadAvailable: boolean };
type PrivacySummary = { profile: Profile; settings: Settings; consents: Consent[]; requests: PrivacyRequest[] };
type AdminRequest = { request: PrivacyRequest; user?: Profile | null };
type RetentionPolicy = { id: string; tenantId: string; name: string; resource: number; retentionDays: number; action: number; isActive: boolean; runIntervalHours: number; nextRunAt: string; lastRunAt?: string | null; lastAffectedCount: number };

const consentNames = ['Terms of service', 'Privacy policy', 'Marketing', 'Analytics', 'Data processing'];
const requestNames = ['Data access', 'Data correction', 'Account deletion'];
const statusNames = ['Submitted', 'Approved', 'Rejected', 'Completed'];
const resourceNames = ['Notifications', 'Device sessions', 'Privacy requests'];

export default function PrivacyPage() {
  const storedUser = useMemo(() => getStoredUser(), []);
  const canReadAdmin = hasAnyPermission(storedUser, ['privacy.read', 'privacy.manage']);
  const canManage = hasAnyPermission(storedUser, ['privacy.manage']);
  const [summary, setSummary] = useState<PrivacySummary | null>(null);
  const [tenants, setTenants] = useState<Tenant[]>([]);
  const [tenantId, setTenantId] = useState(storedUser?.tenantId ?? '');
  const [adminRequests, setAdminRequests] = useState<AdminRequest[]>([]);
  const [policies, setPolicies] = useState<RetentionPolicy[]>([]);
  const [notice, setNotice] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);

  const loadSelf = useCallback(async () => setSummary(await adminApi<PrivacySummary>('/api/privacy/me')), []);
  const loadAdmin = useCallback(async (selectedTenant: string) => {
    if (!canReadAdmin || !selectedTenant) return;
    const query = new URLSearchParams({ tenantId: selectedTenant });
    const [requests, retention] = await Promise.all([
      adminApi<AdminRequest[]>(`/api/privacy/admin/requests?${query}`),
      adminApi<RetentionPolicy[]>(`/api/privacy/admin/retention?${query}`),
    ]);
    setAdminRequests(requests); setPolicies(retention);
  }, [canReadAdmin]);

  useEffect(() => {
    void loadSelf().catch((reason: Error) => setError(reason.message));
    if (canReadAdmin) adminApi<Tenant[]>('/api/tenants').then(items => {
      setTenants(items);
      const selected = items.find(item => item.id === storedUser?.tenantId)?.id ?? items[0]?.id ?? '';
      setTenantId(selected);
    }).catch((reason: Error) => setError(reason.message));
  }, [canReadAdmin, loadSelf, storedUser?.tenantId]);
  useEffect(() => { void loadAdmin(tenantId).catch((reason: Error) => setError(reason.message)); }, [loadAdmin, tenantId]);

  const run = async (action: () => Promise<void>, success: string) => {
    setBusy(true); setError(null); setNotice(null);
    try { await action(); setNotice(success); await loadSelf(); await loadAdmin(tenantId); }
    catch (reason) { setError(reason instanceof Error ? reason.message : 'The privacy action failed.'); }
    finally { setBusy(false); }
  };

  return <div className="space-y-6" data-testid="privacy-workspace">
    <div className="flex flex-wrap items-end justify-between gap-4">
      <div><p className="text-sm font-semibold uppercase tracking-wider text-primary">Data rights and retention</p><h1 className="text-3xl font-bold text-gray-950">Privacy controls</h1><p className="mt-1 text-gray-500">Control consent, visibility, personal data requests, and tenant retention.</p></div>
      <Button variant="outline" disabled={busy} onClick={() => void run(async () => { await loadSelf(); await loadAdmin(tenantId); }, 'Privacy information refreshed.')}><RefreshCw className="mr-2 h-4 w-4" />Refresh</Button>
    </div>
    {notice && <p role="status" className="rounded-lg border border-green-200 bg-green-50 p-3 text-green-800">{notice}</p>}
    {error && <p role="alert" className="rounded-lg border border-red-200 bg-red-50 p-3 text-red-700">{error}</p>}

    {summary && <>
      <ProfileCard summary={summary} busy={busy} run={run} />
      <div className="grid gap-6 xl:grid-cols-2">
        <ConsentCard summary={summary} busy={busy} run={run} />
        <VisibilityCard summary={summary} busy={busy} run={run} />
      </div>
      <RequestCard summary={summary} busy={busy} run={run} />
    </>}

    {canReadAdmin && <Card data-testid="privacy-administration">
      <CardHeader><CardTitle>Tenant privacy administration</CardTitle><CardDescription>Review data-subject requests and configure scheduled retention.</CardDescription></CardHeader>
      <CardContent className="space-y-6">
        <div className="max-w-md"><Label htmlFor="privacy-tenant">Tenant</Label><select id="privacy-tenant" value={tenantId} onChange={event => setTenantId(event.target.value)} className="mt-1 h-10 w-full rounded-md border px-3">{tenants.map(item => <option key={item.id} value={item.id}>{item.name}</option>)}</select></div>
        <AdminRequests requests={adminRequests} canManage={canManage} busy={busy} run={run} />
        <RetentionPolicies policies={policies} tenantId={tenantId} canManage={canManage} busy={busy} run={run} />
      </CardContent>
    </Card>}
  </div>;
}

function ProfileCard({ summary, busy, run }: { summary: PrivacySummary; busy: boolean; run: Runner }) {
  const [profile, setProfile] = useState(summary.profile);
  useEffect(() => setProfile(summary.profile), [summary.profile]);
  return <Card><CardHeader><CardTitle>Personal data correction</CardTitle><CardDescription>Your email remains the account identifier; update the personal fields attached to it.</CardDescription></CardHeader><CardContent className="grid gap-4 md:grid-cols-2 xl:grid-cols-4">
    <Field label="First name" value={profile.firstName} onChange={firstName => setProfile(current => ({ ...current, firstName }))} />
    <Field label="Last name" value={profile.lastName} onChange={lastName => setProfile(current => ({ ...current, lastName }))} />
    <Field label="Phone number" value={profile.phoneNumber ?? ''} onChange={phoneNumber => setProfile(current => ({ ...current, phoneNumber }))} />
    <div className="flex items-end"><Button disabled={busy} onClick={() => void run(async () => { await adminApi('/api/privacy/profile', { method: 'PUT', body: JSON.stringify({ firstName: profile.firstName, lastName: profile.lastName, phoneNumber: profile.phoneNumber, reason: 'User corrected personal profile data.' }) }); }, 'Profile correction saved and audited.')}>Save correction</Button></div>
  </CardContent></Card>;
}

function ConsentCard({ summary, busy, run }: { summary: PrivacySummary; busy: boolean; run: Runner }) {
  const latest = new Map<number, Consent>();
  summary.consents.forEach(item => { if (!latest.has(item.kind)) latest.set(item.kind, item); });
  return <Card><CardHeader><CardTitle>Consent</CardTitle><CardDescription>Every change is retained as a versioned consent event.</CardDescription></CardHeader><CardContent className="space-y-3">{consentNames.map((name, kind) => {
    const current = latest.get(kind);
    return <div key={name} className="flex items-center justify-between gap-4 rounded-lg border p-3"><div><p className="font-medium">{name}</p><p className="text-xs text-gray-500">{current ? `${current.granted ? 'Granted' : 'Not granted'} · policy ${current.policyVersion}` : 'No decision recorded'}</p></div><Button variant={current?.granted ? 'outline' : 'default'} disabled={busy} onClick={() => void run(async () => { await adminApi(`/api/privacy/consents/${kind}`, { method: 'PUT', body: JSON.stringify({ granted: !current?.granted, policyVersion: '2026-08', reason: `${name} preference changed by the user.` }) }); }, `${name} consent updated.`)}>{current?.granted ? 'Revoke' : 'Grant'}</Button></div>;
  })}</CardContent></Card>;
}

function VisibilityCard({ summary, busy, run }: { summary: PrivacySummary; busy: boolean; run: Runner }) {
  const [settings, setSettings] = useState(summary.settings);
  useEffect(() => setSettings(summary.settings), [summary.settings]);
  return <Card><CardHeader><CardTitle>Visibility and data use</CardTitle><CardDescription>Public APIs still use explicit safe-field allow-lists regardless of these preferences.</CardDescription></CardHeader><CardContent className="space-y-4">
    <div><Label htmlFor="profile-visibility">Profile visibility</Label><select id="profile-visibility" value={settings.profileVisibility} onChange={event => setSettings(current => ({ ...current, profileVisibility: Number(event.target.value) }))} className="mt-1 h-10 w-full rounded-md border px-3"><option value={0}>Public</option><option value={1}>Tenant only</option><option value={2}>Private</option></select></div>
    <Check label="Show account activity" checked={settings.showActivity} onChange={showActivity => setSettings(current => ({ ...current, showActivity }))} />
    <Check label="Allow analytics" checked={settings.allowAnalytics} onChange={allowAnalytics => setSettings(current => ({ ...current, allowAnalytics }))} />
    <Check label="Allow marketing" checked={settings.allowMarketing} onChange={allowMarketing => setSettings(current => ({ ...current, allowMarketing }))} />
    <Button disabled={busy} onClick={() => void run(async () => { await adminApi('/api/privacy/settings', { method: 'PUT', body: JSON.stringify({ ...settings, reason: 'User updated visibility and data-use preferences.' }) }); }, 'Visibility preferences saved and audited.')}>Save preferences</Button>
  </CardContent></Card>;
}

function RequestCard({ summary, busy, run }: { summary: PrivacySummary; busy: boolean; run: Runner }) {
  const [kind, setKind] = useState(0); const [details, setDetails] = useState('Provide a copy of my personal account data.');
  return <Card><CardHeader><CardTitle>Data-subject requests</CardTitle><CardDescription>Request an immediate credential-safe access package or submit an account-deletion request.</CardDescription></CardHeader><CardContent className="space-y-4">
    <div className="grid gap-4 md:grid-cols-[220px_1fr_auto]"><div><Label htmlFor="request-kind">Request type</Label><select id="request-kind" value={kind} onChange={event => setKind(Number(event.target.value))} className="mt-1 h-10 w-full rounded-md border px-3"><option value={0}>Data access</option><option value={2}>Account deletion</option></select></div><Field label="Request details" value={details} onChange={setDetails} /><div className="flex items-end"><Button disabled={busy || !details.trim()} onClick={() => void run(async () => { await adminApi('/api/privacy/requests', { method: 'POST', body: JSON.stringify({ kind, details }) }); }, kind === 0 ? 'Data access package generated and audited.' : 'Account-deletion request submitted and audited.')}>Submit request</Button></div></div>
    <div className="space-y-2">{summary.requests.map(item => <div key={item.id} className="flex flex-wrap items-center justify-between gap-3 rounded-lg border p-3"><div><p className="font-medium">{requestNames[item.kind]} · {statusNames[item.status]}</p><p className="text-sm text-gray-600">{item.details}</p><p className="text-xs text-gray-500">{new Date(item.requestedAt).toLocaleString()}{item.response ? ` · ${item.response}` : ''}</p></div>{item.downloadAvailable && <Button variant="outline" onClick={() => void adminDownload(`/api/privacy/requests/${item.id}/download`)}><Download className="mr-2 h-4 w-4" />JSON</Button>}</div>)}{summary.requests.length === 0 && <p className="text-sm text-gray-500">No privacy requests yet.</p>}</div>
  </CardContent></Card>;
}

function AdminRequests({ requests, canManage, busy, run }: { requests: AdminRequest[]; canManage: boolean; busy: boolean; run: Runner }) {
  const decide = (item: PrivacyRequest, approved: boolean) => {
    const response = window.prompt(approved ? 'Approval response' : 'Rejection reason'); if (!response) return;
    void run(async () => { await adminApi(`/api/privacy/admin/requests/${item.id}/decision`, { method: 'PUT', body: JSON.stringify({ approved, response }) }); }, `Privacy request ${approved ? 'approved' : 'rejected'} and audited.`);
  };
  const complete = (item: PrivacyRequest) => { const response = window.prompt('Completion response'); if (!response) return; void run(async () => { await adminApi(`/api/privacy/admin/requests/${item.id}/complete`, { method: 'PUT', body: JSON.stringify({ response }) }); }, 'Privacy request completed and audited.'); };
  return <section><h3 className="font-semibold text-gray-950">Data-subject request queue</h3><div className="mt-2 space-y-2">{requests.map(({ request, user }) => <div key={request.id} className="flex flex-wrap items-center justify-between gap-3 rounded-lg border p-3"><div><p className="font-medium">{requestNames[request.kind]} · {statusNames[request.status]}</p><p className="text-sm text-gray-600">{user ? `${user.firstName} ${user.lastName} (${user.email})` : request.userId}</p><p className="text-xs text-gray-500">{request.details}</p></div>{canManage && <div className="flex gap-2">{request.status === 0 && <><Button size="sm" disabled={busy} onClick={() => decide(request, true)}>Approve</Button><Button size="sm" variant="outline" disabled={busy} onClick={() => decide(request, false)}>Reject</Button></>}{request.status === 1 && <Button size="sm" disabled={busy} onClick={() => complete(request)}>Complete</Button>}</div>}</div>)}{requests.length === 0 && <p className="text-sm text-gray-500">No tenant requests.</p>}</div></section>;
}

function RetentionPolicies({ policies, tenantId, canManage, busy, run }: { policies: RetentionPolicy[]; tenantId: string; canManage: boolean; busy: boolean; run: Runner }) {
  const [form, setForm] = useState({ name: 'Notification retention', resource: 0, retentionDays: 365, action: 0, runIntervalHours: 24, isActive: true });
  return <section><h3 className="font-semibold text-gray-950">Retention schedules</h3>{canManage && <div className="mt-2 grid gap-3 rounded-lg border bg-gray-50 p-3 md:grid-cols-3 xl:grid-cols-6"><Field label="Policy name" value={form.name} onChange={name => setForm(current => ({ ...current, name }))} /><SelectNumber label="Resource" value={form.resource} options={resourceNames} onChange={resource => setForm(current => ({ ...current, resource }))} /><NumberField label="Retention days" value={form.retentionDays} onChange={retentionDays => setForm(current => ({ ...current, retentionDays }))} /><SelectNumber label="Action" value={form.action} options={['Delete', 'Anonymize']} onChange={action => setForm(current => ({ ...current, action }))} /><NumberField label="Run every hours" value={form.runIntervalHours} onChange={runIntervalHours => setForm(current => ({ ...current, runIntervalHours }))} /><div className="flex items-end"><Button disabled={busy || !tenantId} onClick={() => void run(async () => { await adminApi('/api/privacy/admin/retention', { method: 'POST', body: JSON.stringify({ tenantId, ...form, reason: 'Tenant administrator configured retention.' }) }); }, 'Retention schedule created and audited.')}>Add policy</Button></div></div>}
    <div className="mt-3 space-y-2">{policies.map(policy => <div key={policy.id} className="flex flex-wrap items-center justify-between gap-3 rounded-lg border p-3"><div><p className="font-medium">{policy.name} · {resourceNames[policy.resource]}</p><p className="text-xs text-gray-500">{policy.retentionDays} days · every {policy.runIntervalHours}h · {policy.isActive ? 'Active' : 'Paused'} · last affected {policy.lastAffectedCount}</p></div>{canManage && <div className="flex gap-2"><Button size="sm" variant="outline" disabled={busy} onClick={() => void run(async () => { await adminApi(`/api/privacy/admin/retention/${policy.id}/run`, { method: 'POST', body: JSON.stringify({ reason: 'Manual verification run.' }) }); }, 'Retention policy executed and audited.')}><Play className="mr-1 h-4 w-4" />Run</Button><Button size="sm" variant="outline" aria-label={`Delete ${policy.name}`} disabled={busy} onClick={() => void run(async () => { await adminApi(`/api/privacy/admin/retention/${policy.id}?reason=${encodeURIComponent('Obsolete retention configuration removed.')}`, { method: 'DELETE' }); }, 'Retention policy deleted and audited.')}><Trash2 className="h-4 w-4" /></Button></div>}</div>)}{policies.length === 0 && <p className="text-sm text-gray-500">No retention schedules configured.</p>}</div>
  </section>;
}

type Runner = (action: () => Promise<void>, success: string) => Promise<void>;
function Field({ label, value, onChange }: { label: string; value: string; onChange: (value: string) => void }) { const id = `privacy-${label.toLowerCase().replaceAll(' ', '-')}`; return <div><Label htmlFor={id}>{label}</Label><Input id={id} value={value} onChange={event => onChange(event.target.value)} className="mt-1" /></div>; }
function NumberField({ label, value, onChange }: { label: string; value: number; onChange: (value: number) => void }) { const id = `privacy-${label.toLowerCase().replaceAll(' ', '-')}`; return <div><Label htmlFor={id}>{label}</Label><Input id={id} type="number" min={1} value={value} onChange={event => onChange(Number(event.target.value))} className="mt-1" /></div>; }
function SelectNumber({ label, value, options, onChange }: { label: string; value: number; options: string[]; onChange: (value: number) => void }) { const id = `privacy-${label.toLowerCase()}`; return <div><Label htmlFor={id}>{label}</Label><select id={id} value={value} onChange={event => onChange(Number(event.target.value))} className="mt-1 h-10 w-full rounded-md border px-3">{options.map((option, index) => <option key={option} value={index}>{option}</option>)}</select></div>; }
function Check({ label, checked, onChange }: { label: string; checked: boolean; onChange: (value: boolean) => void }) { return <label className="flex items-center gap-3 rounded-lg border p-3 text-sm"><input type="checkbox" checked={checked} onChange={event => onChange(event.target.checked)} />{label}</label>; }
