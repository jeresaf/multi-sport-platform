'use client';

import { Building2, Eye, Plus, RefreshCw, Search, Trash2 } from 'lucide-react';
import { useCallback, useEffect, useMemo, useState } from 'react';
import { adminApi, getEffectiveTenantId, getStoredUser, hasAnyPermission, isPlatformOperator } from '@/lib/admin-api';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { DataTable, type DataTableColumn } from '@/components/admin/data-table';
import { ResourceDialog } from '@/components/admin/resource-dialog';

type Tenant = {
  id: string; name: string; shortName: string; slug: string; description?: string | null;
  status: string | number; country?: string | null; timezone?: string | null;
  currency?: string | null; defaultLanguage?: string | null;
};
type Contact = {
  id: string; name: string; email: string; phone?: string | null; role?: string | null;
  isPrimary: boolean; isActive: boolean;
};
type ProfileForm = { name: string; shortName: string; description: string };
type LocaleForm = { country: string; timezone: string; currency: string; defaultLanguage: string };
type ContactForm = { name: string; email: string; phone: string; role: string; isPrimary: boolean };

const emptyProfile: ProfileForm = { name: '', shortName: '', description: '' };
const emptyLocale: LocaleForm = { country: '', timezone: '', currency: '', defaultLanguage: '' };
const emptyContact: ContactForm = { name: '', email: '', phone: '', role: '', isPrimary: false };
const statusNames = ['Active', 'Suspended', 'Archived'];

function statusName(status: Tenant['status']) {
  return typeof status === 'number' ? statusNames[status] ?? String(status) : status;
}

export default function TenantsPage() {
  const user = getStoredUser();
  const platform = isPlatformOperator(user);
  const canUpdate = hasAnyPermission(user, ['tenants.update']);
  const canManage = platform && hasAnyPermission(user, ['tenants.manage']);
  const [tenants, setTenants] = useState<Tenant[]>([]);
  const [selectedId, setSelectedId] = useState('');
  const [contacts, setContacts] = useState<Contact[]>([]);
  const [search, setSearch] = useState('');
  const [profile, setProfile] = useState<ProfileForm>(emptyProfile);
  const [locale, setLocale] = useState<LocaleForm>(emptyLocale);
  const [contact, setContact] = useState<ContactForm>(emptyContact);
  const [editingContactId, setEditingContactId] = useState<string | null>(null);
  const [showCreate, setShowCreate] = useState(false);
  const [create, setCreate] = useState({ name: '', shortName: '', slug: '' });
  const [error, setError] = useState<string | null>(null);
  const [notice, setNotice] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);
  const [loading, setLoading] = useState(true);

  const selected = tenants.find(item => item.id === selectedId) ?? null;

  const loadTenants = useCallback(async (preferredId?: string) => {
    const items = await adminApi<Tenant[]>('/api/tenants');
    setTenants(items);
    setSelectedId(current => {
      const wanted = preferredId ?? current ?? getEffectiveTenantId() ?? user?.tenantId ?? '';
      return items.some(item => item.id === wanted) ? wanted : items[0]?.id ?? '';
    });
  }, [user?.tenantId]);

  const loadContacts = useCallback(async (tenantId: string) => {
    setContacts(tenantId ? await adminApi<Contact[]>(`/api/tenants/${tenantId}/contacts`) : []);
  }, []);

  useEffect(() => {
    loadTenants().catch(reason => setError(reason instanceof Error ? reason.message : 'Unable to load tenants.'))
      .finally(() => setLoading(false));
  }, [loadTenants]);

  useEffect(() => {
    if (!selected) return;
    setProfile({ name: selected.name, shortName: selected.shortName, description: selected.description ?? '' });
    setLocale({ country: selected.country ?? '', timezone: selected.timezone ?? '', currency: selected.currency ?? '', defaultLanguage: selected.defaultLanguage ?? '' });
    setEditingContactId(null);
    setContact(emptyContact);
    loadContacts(selected.id).catch(reason => setError(reason instanceof Error ? reason.message : 'Unable to load contacts.'));
  }, [selected?.id, loadContacts]); // eslint-disable-line react-hooks/exhaustive-deps

  const filtered = useMemo(() => tenants.filter(item =>
    `${item.name} ${item.slug}`.toLowerCase().includes(search.toLowerCase())), [search, tenants]);
  const columns: DataTableColumn<Tenant>[] = [
    { key: 'name', header: 'Tenant', render: item => <div><p className="font-semibold text-gray-950">{item.name}</p><p className="text-xs text-gray-500">{item.shortName}</p></div> },
    { key: 'slug', header: 'Public slug', render: item => `/${item.slug}` },
    { key: 'status', header: 'Status', render: item => <Status status={statusName(item.status)} /> },
    { key: 'country', header: 'Country', render: item => item.country || '—' },
    { key: 'timezone', header: 'Timezone', render: item => item.timezone || '—' },
    { key: 'actions', header: <span className="sr-only">Actions</span>, className: 'text-right', render: item => <Button size="sm" variant="ghost" onClick={event => { event.stopPropagation(); setSelectedId(item.id); }}><Eye className="mr-1 h-4 w-4" />View</Button> },
  ];

  const run = async (action: () => Promise<void>, success: string) => {
    setBusy(true); setError(null); setNotice(null);
    try { await action(); setNotice(success); }
    catch (reason) { setError(reason instanceof Error ? reason.message : 'The tenant operation failed.'); }
    finally { setBusy(false); }
  };

  const refresh = async () => {
    await loadTenants(selectedId);
    if (selectedId) await loadContacts(selectedId);
  };

  const createTenant = (event: React.FormEvent) => {
    event.preventDefault();
    void run(async () => {
      const created = await adminApi<Tenant>('/api/tenants', { method: 'POST', body: JSON.stringify(create) });
      setCreate({ name: '', shortName: '', slug: '' }); setShowCreate(false);
      await loadTenants(created.id);
    }, 'Tenant created with its standard role catalogue.');
  };

  const saveProfile = (event: React.FormEvent) => {
    event.preventDefault(); if (!selected) return;
    void run(async () => {
      await adminApi(`/api/tenants/${selected.id}`, { method: 'PUT', body: JSON.stringify({ ...profile, description: profile.description || null }) });
      await loadTenants(selected.id);
    }, 'Tenant profile updated.');
  };

  const saveLocale = (event: React.FormEvent) => {
    event.preventDefault(); if (!selected) return;
    void run(async () => {
      await adminApi(`/api/tenants/${selected.id}/localization`, { method: 'PUT', body: JSON.stringify(locale) });
      await loadTenants(selected.id);
    }, 'Tenant localization updated.');
  };

  const saveContact = (event: React.FormEvent) => {
    event.preventDefault(); if (!selected) return;
    void run(async () => {
      await adminApi(editingContactId ? `/api/tenants/contacts/${editingContactId}` : `/api/tenants/${selected.id}/contacts`, {
        method: editingContactId ? 'PUT' : 'POST', body: JSON.stringify(contact),
      });
      setEditingContactId(null); setContact(emptyContact); await loadContacts(selected.id);
    }, editingContactId ? 'Tenant contact updated.' : 'Tenant contact added.');
  };

  const changeLifecycle = (action: 'activate' | 'suspend' | 'archive') => {
    if (!selected) return;
    if (action === 'archive' && !window.confirm(`Archive “${selected.name}”? This is a terminal lifecycle state.`)) return;
    void run(async () => {
      await adminApi(`/api/tenants/${selected.id}/${action}`, { method: 'POST' });
      await loadTenants(selected.id);
    }, `Tenant ${action === 'suspend' ? 'suspended' : action === 'activate' ? 'activated' : 'archived'}.`);
  };

  const removeContact = (item: Contact) => {
    if (!selected || !window.confirm(`Delete contact “${item.name}”?`)) return;
    void run(async () => {
      await adminApi(`/api/tenants/contacts/${item.id}`, { method: 'DELETE' });
      await loadContacts(selected.id);
    }, 'Tenant contact deleted.');
  };

  return <div className="space-y-6">
    <div className="flex flex-wrap items-end justify-between gap-4">
      <div><p className="text-sm font-semibold uppercase tracking-wider text-primary">Platform</p><h1 className="text-3xl font-bold text-gray-950">Tenants</h1><p className="mt-1 text-gray-500">Lifecycle, contacts, and regional settings in one connected workspace.</p></div>
      <div className="flex gap-2"><Button variant="outline" disabled={busy} onClick={() => void run(refresh, 'Tenant data refreshed.')}><RefreshCw className="mr-2 h-4 w-4" />Refresh</Button>{canManage && <Button onClick={() => setShowCreate(value => !value)}><Plus className="mr-2 h-4 w-4" />New tenant</Button>}</div>
    </div>
    {error && <State tone="error">{error}</State>}{notice && <State tone="success">{notice}</State>}
    <label className="relative block max-w-lg"><Search className="absolute left-3 top-3 h-4 w-4 text-gray-400" /><Input className="pl-10" value={search} onChange={e => setSearch(e.target.value)} placeholder="Search name or slug" /></label>
    {loading ? <State>Loading tenants…</State> : <DataTable caption="Platform tenants" columns={columns} rows={filtered} rowKey={item => item.id} onRowClick={item => setSelectedId(item.id)} emptyMessage="No tenants match this search." />}
    <div>
      {selected ? <main className="space-y-5">
        <section className="rounded-xl border bg-white p-5"><div className="flex flex-wrap items-start justify-between gap-4"><div className="flex gap-3"><span className="rounded-lg bg-primary-50 p-3"><Building2 className="h-6 w-6 text-primary" /></span><div><h2 className="text-xl font-bold">{selected.name}</h2><p className="text-sm text-gray-500">/{selected.slug} · {selected.id}</p></div></div><div className="flex flex-wrap gap-2"><Status status={statusName(selected.status)} />{canManage && statusName(selected.status) === 'Active' && <Button size="sm" variant="outline" onClick={() => changeLifecycle('suspend')} disabled={busy}>Suspend</Button>}{canManage && statusName(selected.status) === 'Suspended' && <Button size="sm" onClick={() => changeLifecycle('activate')} disabled={busy}>Activate</Button>}{canManage && statusName(selected.status) !== 'Archived' && <Button size="sm" variant="destructive" onClick={() => changeLifecycle('archive')} disabled={busy}>Archive</Button>}</div></div></section>
        <div className="grid gap-5 lg:grid-cols-2">
          <form onSubmit={saveProfile} className="space-y-4 rounded-xl border bg-white p-5"><FormHeading title="Profile" description="Core tenant identity. The public slug remains immutable." /><Field label="Name"><Input required maxLength={200} disabled={!canUpdate} value={profile.name} onChange={e => setProfile(v => ({ ...v, name: e.target.value }))} /></Field><Field label="Short name"><Input required maxLength={100} disabled={!canUpdate} value={profile.shortName} onChange={e => setProfile(v => ({ ...v, shortName: e.target.value }))} /></Field><Field label="Description"><textarea maxLength={1000} disabled={!canUpdate} className="min-h-24 w-full rounded-md border p-3 text-sm disabled:opacity-60" value={profile.description} onChange={e => setProfile(v => ({ ...v, description: e.target.value }))} /></Field>{canUpdate && <Button disabled={busy}>Save profile</Button>}</form>
          <form onSubmit={saveLocale} className="space-y-4 rounded-xl border bg-white p-5"><FormHeading title="Localization" description="Defaults used for schedules, prices, and communication." /><Field label="Country"><Input maxLength={100} disabled={!canUpdate} placeholder="Kenya" value={locale.country} onChange={e => setLocale(v => ({ ...v, country: e.target.value }))} /></Field><div className="grid gap-4 sm:grid-cols-2"><Field label="Timezone"><Input maxLength={100} disabled={!canUpdate} placeholder="Africa/Nairobi" value={locale.timezone} onChange={e => setLocale(v => ({ ...v, timezone: e.target.value }))} /></Field><Field label="Currency"><Input maxLength={3} disabled={!canUpdate} placeholder="KES" value={locale.currency} onChange={e => setLocale(v => ({ ...v, currency: e.target.value.toUpperCase() }))} /></Field></div><Field label="Default language"><Input maxLength={10} disabled={!canUpdate} placeholder="en-KE" value={locale.defaultLanguage} onChange={e => setLocale(v => ({ ...v, defaultLanguage: e.target.value }))} /></Field>{canUpdate && <Button disabled={busy}>Save localization</Button>}</form>
        </div>
        <section className="space-y-4 rounded-xl border bg-white p-5"><FormHeading title="Contacts" description="Operational tenant contacts; only one contact is marked primary." />{contacts.map(item => <div key={item.id} className="flex flex-wrap items-center justify-between gap-3 rounded-lg border p-3"><div><p className="font-semibold">{item.name} {item.isPrimary && <span className="ml-1 rounded bg-primary-50 px-2 py-0.5 text-xs text-primary">Primary</span>}</p><p className="text-sm text-gray-600">{item.email}{item.phone ? ` · ${item.phone}` : ''}{item.role ? ` · ${item.role}` : ''}</p></div>{canUpdate && <div className="flex gap-2"><Button type="button" size="sm" variant="outline" onClick={() => { setEditingContactId(item.id); setContact({ name: item.name, email: item.email, phone: item.phone ?? '', role: item.role ?? '', isPrimary: item.isPrimary }); }}>Edit</Button><Button type="button" size="icon" variant="destructive" aria-label={`Delete ${item.name}`} onClick={() => removeContact(item)}><Trash2 className="h-4 w-4" /></Button></div>}</div>)}{!contacts.length && <p className="text-sm text-gray-500">No tenant contacts have been added.</p>}{canUpdate && <form onSubmit={saveContact} className="grid gap-3 border-t pt-4 md:grid-cols-2 xl:grid-cols-5"><Field label="Name"><Input required maxLength={200} value={contact.name} onChange={e => setContact(v => ({ ...v, name: e.target.value }))} /></Field><Field label="Email"><Input required type="email" maxLength={200} value={contact.email} onChange={e => setContact(v => ({ ...v, email: e.target.value }))} /></Field><Field label="Phone"><Input maxLength={50} value={contact.phone} onChange={e => setContact(v => ({ ...v, phone: e.target.value }))} /></Field><Field label="Role"><Input maxLength={100} value={contact.role} onChange={e => setContact(v => ({ ...v, role: e.target.value }))} /></Field><div className="flex items-end gap-2"><label className="flex h-10 items-center gap-2 text-sm"><input type="checkbox" checked={contact.isPrimary} onChange={e => setContact(v => ({ ...v, isPrimary: e.target.checked }))} />Primary</label><Button disabled={busy}>{editingContactId ? 'Update' : 'Add'}</Button>{editingContactId && <Button type="button" variant="ghost" onClick={() => { setEditingContactId(null); setContact(emptyContact); }}>Cancel</Button>}</div></form>}</section>
      </main> : <State>Select a tenant row to view and manage it.</State>}
    </div>
    {showCreate && <ResourceDialog title="Create tenant" description="The tenant will receive the standard role catalogue and can then be opened as a workspace." onClose={() => setShowCreate(false)}><form onSubmit={createTenant} className="space-y-4"><Field label="Tenant name"><Input aria-label="Tenant name" required maxLength={200} value={create.name} onChange={e => setCreate(v => ({ ...v, name: e.target.value }))} /></Field><Field label="Short name"><Input aria-label="Short name" required maxLength={100} value={create.shortName} onChange={e => setCreate(v => ({ ...v, shortName: e.target.value }))} /></Field><Field label="Public slug"><Input aria-label="Public slug" required maxLength={100} pattern="[a-z0-9]+(?:-[a-z0-9]+)*" placeholder="regional-league" value={create.slug} onChange={e => setCreate(v => ({ ...v, slug: e.target.value.toLowerCase() }))} /></Field><div className="flex justify-end gap-3"><Button type="button" variant="outline" onClick={() => setShowCreate(false)}>Cancel</Button><Button disabled={busy}>Create tenant</Button></div></form></ResourceDialog>}
  </div>;
}

function Field({ label, children }: { label: string; children: React.ReactNode }) { return <div><Label>{label}</Label><div className="mt-1">{children}</div></div>; }
function FormHeading({ title, description }: { title: string; description: string }) { return <div><h3 className="font-bold text-gray-950">{title}</h3><p className="text-sm text-gray-500">{description}</p></div>; }
function Status({ status }: { status: string }) { const tone = status === 'Active' ? 'bg-green-50 text-green-700' : status === 'Suspended' ? 'bg-amber-50 text-amber-700' : 'bg-gray-100 text-gray-600'; return <span className={`rounded-full px-2.5 py-1 text-xs font-semibold ${tone}`}>{status}</span>; }
function State({ children, tone = 'neutral' }: { children: React.ReactNode; tone?: 'neutral' | 'error' | 'success' }) { const color = tone === 'error' ? 'border-red-200 bg-red-50 text-red-700' : tone === 'success' ? 'border-green-200 bg-green-50 text-green-700' : 'border-gray-200 bg-white text-gray-500'; return <div role={tone === 'error' ? 'alert' : tone === 'success' ? 'status' : undefined} className={`rounded-xl border p-4 ${color}`}>{children}</div>; }
