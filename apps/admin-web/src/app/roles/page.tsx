'use client';

import { Eye, Pencil, Plus, Trash2 } from 'lucide-react';
import { useCallback, useEffect, useMemo, useState } from 'react';
import { DataTable, type DataTableColumn } from '@/components/admin/data-table';
import { ResourceDialog } from '@/components/admin/resource-dialog';
import { Button } from '@/components/ui/button';
import { adminApi, getSelectedTenant, getStoredUser, hasAnyPermission, isPlatformOperator } from '@/lib/admin-api';

type RoleRecord = { id: string; name: string; description?: string | null; isSystemRole: boolean; permissions: string[]; tenantId?: string | null; tenantName?: string | null };

export default function RolesPage() {
  const user = getStoredUser();
  const platform = isPlatformOperator(user);
  const tenant = getSelectedTenant();
  const [roles, setRoles] = useState<RoleRecord[]>([]);
  const [catalogue, setCatalogue] = useState<string[]>([]);
  const [viewing, setViewing] = useState<RoleRecord | null>(null);
  const [editing, setEditing] = useState<RoleRecord | null>(null);
  const [showForm, setShowForm] = useState(false);
  const [name, setName] = useState('');
  const [description, setDescription] = useState('');
  const [selected, setSelected] = useState<string[]>([]);
  const [error, setError] = useState<string | null>(null);
  const [loading, setLoading] = useState(true);
  const [saving, setSaving] = useState(false);
  const canCreate = hasAnyPermission(user, ['roles.create']);
  const canUpdate = hasAnyPermission(user, ['roles.update']);
  const canDelete = hasAnyPermission(user, ['roles.delete']);

  const load = useCallback(async () => {
    setError(null);
    try {
      const [roleData, permissionData] = await Promise.all([adminApi<RoleRecord[]>('/api/roles'), adminApi<string[]>('/api/roles/permissions')]);
      setRoles(roleData); setCatalogue(permissionData);
    } catch (reason) { setError(reason instanceof Error ? reason.message : 'Unable to load roles.'); }
    finally { setLoading(false); }
  }, [setCatalogue, setError, setLoading, setRoles]);
  useEffect(() => { void load(); }, [load]);

  const permissionGroups = useMemo(() => {
    const result = new Map<string, string[]>();
    for (const permission of catalogue) { const capability = permission.split('.')[0]; result.set(capability, [...(result.get(capability) ?? []), permission]); }
    return [...result.entries()];
  }, [catalogue]);
  const tenantGroups = useMemo(() => {
    const grouped = new Map<string, RoleRecord[]>();
    for (const role of roles) { const label = role.tenantName ?? tenant?.name ?? (role.tenantId ? 'Tenant' : 'Platform'); grouped.set(label, [...(grouped.get(label) ?? []), role]); }
    return [...grouped.entries()].sort(([a], [b]) => a.localeCompare(b));
  }, [roles, tenant?.name]);

  const openCreate = () => { setEditing(null); setName(''); setDescription(''); setSelected([]); setShowForm(true); };
  const openEdit = (role: RoleRecord) => { setEditing(role); setName(role.name); setDescription(role.description ?? ''); setSelected(role.permissions); setViewing(null); setShowForm(true); };
  const save = async (event: React.FormEvent) => {
    event.preventDefault(); setSaving(true); setError(null);
    try { await adminApi(editing ? `/api/roles/${editing.id}` : '/api/roles', { method: editing ? 'PUT' : 'POST', body: JSON.stringify({ name: name.trim(), description: description.trim() || null, permissions: selected }) }); setShowForm(false); await load(); }
    catch (reason) { setError(reason instanceof Error ? reason.message : 'Unable to save role.'); }
    finally { setSaving(false); }
  };
  const remove = async (role: RoleRecord) => {
    if (!window.confirm(`Delete the role “${role.name}” from ${role.tenantName ?? tenant?.name ?? 'the platform'}?`)) return;
    try { await adminApi(`/api/roles/${role.id}`, { method: 'DELETE' }); await load(); }
    catch (reason) { setError(reason instanceof Error ? reason.message : 'Unable to delete role.'); }
  };

  const columns: DataTableColumn<RoleRecord>[] = [
    { key: 'role', header: 'Role', render: role => <div><p className="font-semibold text-gray-950">{role.name}</p><p className="max-w-md text-xs text-gray-500">{role.description ?? 'No description'}</p></div> },
    { key: 'tenant', header: 'Tenant', render: role => <span className="font-medium">{role.tenantName ?? tenant?.name ?? (role.tenantId ? 'Tenant' : 'Platform')}</span> },
    { key: 'type', header: 'Type', render: role => <Badge>{role.isSystemRole ? 'Standard' : 'Custom'}</Badge> },
    { key: 'permissions', header: 'Capabilities', render: role => role.permissions.length },
    { key: 'actions', header: <span className="sr-only">Actions</span>, className: 'text-right', render: role => <div className="flex justify-end gap-1" onClick={event => event.stopPropagation()}><Button size="sm" variant="ghost" onClick={() => setViewing(role)}><Eye className="mr-1 h-4 w-4" />View</Button>{!role.isSystemRole && canUpdate && <Button size="sm" variant="ghost" onClick={() => openEdit(role)} aria-label={`Edit ${role.name}`}><Pencil className="h-4 w-4" /></Button>}{!role.isSystemRole && canDelete && <Button size="sm" variant="ghost" className="text-red-700" onClick={() => void remove(role)} aria-label={`Delete ${role.name}`}><Trash2 className="h-4 w-4" /></Button>}</div> },
  ];

  return <div className="space-y-6">
    <div className="flex flex-col gap-4 md:flex-row md:items-end md:justify-between"><div><p className="text-sm font-semibold uppercase tracking-wider text-primary">Identity</p><h1 className="text-3xl font-bold text-gray-950">Roles and permissions</h1><p className="mt-1 text-gray-500">{platform && !tenant ? 'Roles are grouped by tenant so identical names remain distinct.' : `Access roles for ${tenant?.name ?? 'the current tenant'}.`}</p></div>{canCreate && <Button onClick={openCreate}><Plus className="mr-2 h-4 w-4" />New role</Button>}</div>
    {error && <State tone="error">{error}</State>}
    {loading ? <State>Loading roles…</State> : tenantGroups.length ? tenantGroups.map(([tenantName, items]) => <section key={tenantName} className="space-y-2"><div className="flex items-center gap-3"><h2 className="text-lg font-bold text-gray-900">{tenantName}</h2><span className="text-sm text-gray-500">{items.length} role{items.length === 1 ? '' : 's'}</span></div><DataTable caption={`${tenantName} roles`} columns={columns} rows={items} rowKey={role => role.id} onRowClick={setViewing} /></section>) : <State>No roles found.</State>}
    {viewing && <ResourceDialog title={viewing.name} description={`${viewing.tenantName ?? tenant?.name ?? 'Platform'} · ${viewing.isSystemRole ? 'Standard role' : 'Custom role'}`} onClose={() => setViewing(null)}><p className="text-sm text-gray-600">{viewing.description ?? 'No description has been provided.'}</p><h3 className="mt-5 font-semibold">Capabilities ({viewing.permissions.length})</h3><div className="mt-2 flex flex-wrap gap-2">{viewing.permissions.map(permission => <span key={permission} className="rounded-md bg-gray-100 px-2 py-1 text-xs text-gray-700">{permission}</span>)}{!viewing.permissions.length && <span className="text-sm text-gray-500">No capabilities assigned.</span>}</div>{!viewing.isSystemRole && canUpdate && <div className="mt-6 flex justify-end"><Button onClick={() => openEdit(viewing)}><Pencil className="mr-2 h-4 w-4" />Edit role</Button></div>}</ResourceDialog>}
    {showForm && <ResourceDialog title={editing ? 'Edit custom role' : 'Create custom role'} description={`This role belongs to ${tenant?.name ?? (platform ? 'the platform workspace' : 'the current tenant')}.`} onClose={() => setShowForm(false)}><form onSubmit={save}><div className="grid gap-4 md:grid-cols-2"><label className="text-sm font-semibold">Name<input required minLength={2} maxLength={100} value={name} onChange={event => setName(event.target.value)} className="mt-1 w-full rounded-lg border p-2 font-normal" /></label><label className="text-sm font-semibold">Description<input maxLength={500} value={description} onChange={event => setDescription(event.target.value)} className="mt-1 w-full rounded-lg border p-2 font-normal" /></label></div><div className="mt-6 space-y-4">{permissionGroups.map(([capability, permissions]) => <fieldset key={capability} className="rounded-xl border p-4"><legend className="px-2 font-semibold capitalize">{capability}</legend><div className="grid gap-2 sm:grid-cols-2 lg:grid-cols-3">{permissions.map(permission => <label key={permission} className="flex items-center gap-2 text-sm"><input type="checkbox" checked={selected.includes(permission)} onChange={event => setSelected(current => event.target.checked ? [...current, permission] : current.filter(item => item !== permission))} />{permission}</label>)}</div></fieldset>)}</div><div className="mt-6 flex justify-end gap-3"><Button type="button" variant="outline" onClick={() => setShowForm(false)}>Cancel</Button><Button disabled={saving}>{saving ? 'Saving…' : 'Save role'}</Button></div></form></ResourceDialog>}
  </div>;
}

function Badge({ children }: { children: React.ReactNode }) { return <span className="rounded-full bg-gray-100 px-2 py-1 text-xs font-semibold text-gray-600">{children}</span>; }
function State({ children, tone = 'neutral' }: { children: React.ReactNode; tone?: 'neutral' | 'error' }) { return <div className={`rounded-xl border p-5 ${tone === 'error' ? 'border-red-200 bg-red-50 text-red-700' : 'border-gray-200 bg-white text-gray-500'}`}>{children}</div>; }
