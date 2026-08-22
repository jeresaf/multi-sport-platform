'use client';

import { Eye, Plus, Search, ShieldAlert, ShieldCheck } from 'lucide-react';
import { useCallback, useEffect, useMemo, useState } from 'react';
import { DataTable, type DataTableColumn } from '@/components/admin/data-table';
import { ResourceDialog } from '@/components/admin/resource-dialog';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { adminApi, getSelectedTenant, getStoredUser, hasAnyPermission, isPlatformOperator } from '@/lib/admin-api';

type UserRecord = { id: string; email: string; firstName: string; lastName: string; emailVerified: boolean; isActive: boolean; isSuspended: boolean; lastLoginAt?: string | null; roles: string[] };
type RoleRecord = { id: string; name: string; isSystemRole: boolean };
type UserForm = { firstName: string; lastName: string; email: string; password: string; roleIds: string[] };
const emptyForm: UserForm = { firstName: '', lastName: '', email: '', password: '', roleIds: [] };

export default function UsersPage() {
  const currentUser = getStoredUser();
  const selectedTenant = getSelectedTenant();
  const requiresTenant = isPlatformOperator(currentUser) && !selectedTenant;
  const canManage = hasAnyPermission(currentUser, ['users.manage']);
  const canAssignRoles = hasAnyPermission(currentUser, ['roles.assign']);
  const [users, setUsers] = useState<UserRecord[]>([]);
  const [roles, setRoles] = useState<RoleRecord[]>([]);
  const [search, setSearch] = useState('');
  const [viewing, setViewing] = useState<UserRecord | null>(null);
  const [showCreate, setShowCreate] = useState(false);
  const [form, setForm] = useState<UserForm>(emptyForm);
  const [error, setError] = useState<string | null>(null);
  const [loading, setLoading] = useState(!requiresTenant);
  const [updating, setUpdating] = useState<string | null>(null);

  const loadUsers = useCallback(async () => {
    if (requiresTenant) return;
    setError(null);
    try {
      const [userData, roleData] = await Promise.all([
        adminApi<UserRecord[]>('/api/auth/users'),
        (canAssignRoles || canManage) ? adminApi<RoleRecord[]>('/api/roles') : Promise.resolve([]),
      ]);
      setUsers(userData); setRoles(roleData);
    } catch (reason) { setError(reason instanceof Error ? reason.message : 'Unable to load users.'); }
    finally { setLoading(false); }
  }, [canAssignRoles, canManage, requiresTenant]);
  useEffect(() => { void loadUsers(); }, [loadUsers]);

  const filtered = useMemo(() => {
    const query = search.trim().toLowerCase();
    return users.filter(user => `${user.firstName} ${user.lastName} ${user.email} ${user.roles.join(' ')}`.toLowerCase().includes(query));
  }, [search, users]);

  const changeSuspension = async (user: UserRecord) => {
    const action = user.isSuspended ? 'unsuspend' : 'suspend';
    const reason = window.prompt(`Reason to ${action} ${user.email}:`);
    if (!reason?.trim()) return;
    setUpdating(user.id); setError(null);
    try { await adminApi(`/api/auth/users/${user.id}/${action}`, { method: 'POST', body: JSON.stringify({ reason: reason.trim() }) }); setViewing(null); await loadUsers(); }
    catch (reason) { setError(reason instanceof Error ? reason.message : `Unable to ${action} user.`); }
    finally { setUpdating(null); }
  };

  const changeRole = async (user: UserRecord, roleId: string, assign: boolean) => {
    setUpdating(user.id); setError(null);
    try { await adminApi(`/api/roles/${assign ? 'assign' : 'remove'}`, { method: 'POST', body: JSON.stringify({ userId: user.id, roleId }) }); await loadUsers(); setViewing(null); }
    catch (reason) { setError(reason instanceof Error ? reason.message : 'Unable to change the role assignment.'); }
    finally { setUpdating(null); }
  };

  const createUser = async (event: React.FormEvent) => {
    event.preventDefault(); setUpdating('create'); setError(null);
    try { await adminApi('/api/auth/users', { method: 'POST', body: JSON.stringify(form) }); setForm(emptyForm); setShowCreate(false); await loadUsers(); }
    catch (reason) { setError(reason instanceof Error ? reason.message : 'Unable to create user.'); }
    finally { setUpdating(null); }
  };

  const columns: DataTableColumn<UserRecord>[] = [
    { key: 'name', header: 'User', render: user => <div><p className="font-semibold text-gray-950">{user.firstName} {user.lastName}</p><p className="text-xs text-gray-500">{user.email}</p></div> },
    { key: 'status', header: 'Status', render: user => <Status suspended={user.isSuspended} /> },
    { key: 'roles', header: 'Roles', render: user => <span className="text-gray-700">{user.roles.join(', ') || 'No role'}</span> },
    { key: 'verified', header: 'Email', render: user => user.emailVerified ? 'Verified' : 'Not verified' },
    { key: 'login', header: 'Last login', render: user => formatDate(user.lastLoginAt) },
    { key: 'actions', header: <span className="sr-only">Actions</span>, className: 'text-right', render: user => <div onClick={event => event.stopPropagation()}><Button size="sm" variant="ghost" onClick={() => setViewing(user)}><Eye className="mr-1 h-4 w-4" />View</Button></div> },
  ];

  if (requiresTenant) return <div className="space-y-6"><Heading /><State>Select a tenant from the workspace switcher in the header before managing users.</State></div>;
  return <div className="space-y-6">
    <div className="flex flex-wrap items-end justify-between gap-4"><Heading tenantName={selectedTenant?.name} />{canManage && <Button onClick={() => setShowCreate(true)}><Plus className="mr-2 h-4 w-4" />New user</Button>}</div>
    <label className="relative block max-w-lg"><Search className="absolute left-3 top-3 h-4 w-4 text-gray-400" /><Input value={search} onChange={event => setSearch(event.target.value)} placeholder="Search name, email, or role" className="pl-10" /></label>
    {error && <State tone="error">{error}</State>}
    {loading ? <State>Loading users…</State> : <DataTable caption="Tenant users" columns={columns} rows={filtered} rowKey={user => user.id} onRowClick={setViewing} emptyMessage="No users match this search." />}

    {viewing && <ResourceDialog title={`${viewing.firstName} ${viewing.lastName}`} description={viewing.email} onClose={() => setViewing(null)}>
      <div className="grid gap-4 sm:grid-cols-3"><Detail label="Account"><Status suspended={viewing.isSuspended} /></Detail><Detail label="Email">{viewing.emailVerified ? 'Verified' : 'Not verified'}</Detail><Detail label="Last login">{formatDate(viewing.lastLoginAt)}</Detail></div>
      <div className="mt-6"><h3 className="font-semibold text-gray-950">Roles</h3><div className="mt-2 flex flex-wrap gap-2">{viewing.roles.map(roleName => { const role = roles.find(candidate => candidate.name === roleName); return <span key={roleName} className="inline-flex items-center rounded-full bg-primary-50 px-3 py-1 text-xs font-semibold text-primary">{roleName}{canAssignRoles && role && <button disabled={updating === viewing.id} onClick={() => void changeRole(viewing, role.id, false)} className="ml-2" aria-label={`Remove ${roleName}`}>×</button>}</span>; })}{!viewing.roles.length && <span className="text-sm text-gray-500">No roles assigned.</span>}</div>{canAssignRoles && <select value="" disabled={updating === viewing.id} onChange={event => event.target.value && void changeRole(viewing, event.target.value, true)} className="mt-3 rounded-lg border border-gray-200 bg-white px-3 py-2 text-sm"><option value="">Assign another role…</option>{roles.filter(role => !viewing.roles.includes(role.name)).map(role => <option key={role.id} value={role.id}>{role.name}</option>)}</select>}</div>
      {canManage && <div className="mt-6 flex justify-end"><Button variant={viewing.isSuspended ? 'default' : 'destructive'} disabled={updating === viewing.id} onClick={() => void changeSuspension(viewing)}>{viewing.isSuspended ? <ShieldCheck className="mr-2 h-4 w-4" /> : <ShieldAlert className="mr-2 h-4 w-4" />}{viewing.isSuspended ? 'Unsuspend user' : 'Suspend user'}</Button></div>}
    </ResourceDialog>}

    {showCreate && <ResourceDialog title="Create tenant user" description={`Create an account in ${selectedTenant?.name ?? 'the current tenant'} and assign its initial access.`} onClose={() => setShowCreate(false)}><form onSubmit={createUser} className="space-y-5"><div className="grid gap-4 sm:grid-cols-2"><Field label="First name"><Input required maxLength={100} value={form.firstName} onChange={event => setForm(value => ({ ...value, firstName: event.target.value }))} /></Field><Field label="Last name"><Input required maxLength={100} value={form.lastName} onChange={event => setForm(value => ({ ...value, lastName: event.target.value }))} /></Field><Field label="Email"><Input required type="email" maxLength={254} value={form.email} onChange={event => setForm(value => ({ ...value, email: event.target.value }))} /></Field><Field label="Temporary password"><Input required type="password" minLength={8} maxLength={128} autoComplete="new-password" value={form.password} onChange={event => setForm(value => ({ ...value, password: event.target.value }))} /></Field></div><fieldset><legend className="font-semibold text-gray-950">Initial roles</legend><p className="text-sm text-gray-500">Select at least one role.</p><div className="mt-3 grid gap-2 sm:grid-cols-2">{roles.map(role => <label key={role.id} className="flex items-center gap-2 rounded-lg border p-3 text-sm"><input type="checkbox" checked={form.roleIds.includes(role.id)} onChange={event => setForm(value => ({ ...value, roleIds: event.target.checked ? [...value.roleIds, role.id] : value.roleIds.filter(id => id !== role.id) }))} /><span><strong>{role.name}</strong><span className="ml-1 text-xs text-gray-500">{role.isSystemRole ? 'Standard' : 'Custom'}</span></span></label>)}</div></fieldset><div className="flex justify-end gap-3"><Button type="button" variant="outline" onClick={() => setShowCreate(false)}>Cancel</Button><Button disabled={updating === 'create' || form.roleIds.length === 0}>{updating === 'create' ? 'Creating…' : 'Create user'}</Button></div></form></ResourceDialog>}
  </div>;
}

function Heading({ tenantName }: { tenantName?: string }) { return <div><p className="text-sm font-semibold uppercase tracking-wider text-primary">Identity</p><h1 className="text-3xl font-bold text-gray-950">Users</h1><p className="mt-1 text-gray-500">Review accounts and access{tenantName ? ` for ${tenantName}` : ''}.</p></div>; }
function Field({ label, children }: { label: string; children: React.ReactNode }) { return <label className="text-sm font-semibold">{label}<div className="mt-1">{children}</div></label>; }
function Detail({ label, children }: { label: string; children: React.ReactNode }) { return <div><p className="text-xs font-semibold uppercase tracking-wide text-gray-500">{label}</p><div className="mt-1 text-sm text-gray-900">{children}</div></div>; }
function Status({ suspended }: { suspended: boolean }) { return <span className={`rounded-full px-2 py-1 text-xs font-semibold ${suspended ? 'bg-red-50 text-red-700' : 'bg-green-50 text-green-700'}`}>{suspended ? 'Suspended' : 'Active'}</span>; }
function formatDate(value?: string | null) { if (!value) return 'Never'; return new Intl.DateTimeFormat(undefined, { dateStyle: 'medium', timeStyle: 'short' }).format(new Date(value)); }
function State({ children, tone = 'neutral' }: { children: React.ReactNode; tone?: 'neutral' | 'error' }) { return <div className={`rounded-xl border p-5 ${tone === 'error' ? 'border-red-200 bg-red-50 text-red-700' : 'border-gray-200 bg-white text-gray-500'}`}>{children}</div>; }
