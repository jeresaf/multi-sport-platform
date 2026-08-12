'use client';

import { KeyRound, Pencil, Plus, Trash2, X } from 'lucide-react';
import { useEffect, useMemo, useState } from 'react';
import { adminApi, getStoredUser, hasAnyPermission } from '@/lib/admin-api';

type RoleRecord = {
  id: string;
  name: string;
  description?: string | null;
  isSystemRole: boolean;
  permissions: string[];
  tenantId?: string | null;
};

export default function RolesPage() {
  const [roles, setRoles] = useState<RoleRecord[]>([]);
  const [catalogue, setCatalogue] = useState<string[]>([]);
  const [editing, setEditing] = useState<RoleRecord | null>(null);
  const [showForm, setShowForm] = useState(false);
  const [name, setName] = useState('');
  const [description, setDescription] = useState('');
  const [selected, setSelected] = useState<string[]>([]);
  const [error, setError] = useState<string | null>(null);
  const [saving, setSaving] = useState(false);
  const user = getStoredUser();
  const canCreate = hasAnyPermission(user, ['roles.create']);
  const canUpdate = hasAnyPermission(user, ['roles.update']);
  const canDelete = hasAnyPermission(user, ['roles.delete']);

  const load = async () => {
    setError(null);
    try {
      const [roleData, permissionData] = await Promise.all([
        adminApi<RoleRecord[]>('/api/roles'),
        adminApi<string[]>('/api/roles/permissions'),
      ]);
      setRoles(roleData);
      setCatalogue(permissionData);
    } catch (reason) {
      setError(reason instanceof Error ? reason.message : 'Unable to load roles.');
    }
  };

  useEffect(() => {
    void load();
  }, []);

  const groups = useMemo(() => {
    const result = new Map<string, string[]>();
    for (const permission of catalogue) {
      const capability = permission.split('.')[0];
      result.set(capability, [...(result.get(capability) ?? []), permission]);
    }
    return [...result.entries()];
  }, [catalogue]);

  const openCreate = () => {
    setEditing(null);
    setName('');
    setDescription('');
    setSelected([]);
    setShowForm(true);
  };

  const openEdit = (role: RoleRecord) => {
    setEditing(role);
    setName(role.name);
    setDescription(role.description ?? '');
    setSelected(role.permissions);
    setShowForm(true);
  };

  const save = async (event: React.FormEvent) => {
    event.preventDefault();
    setSaving(true);
    setError(null);
    try {
      await adminApi(editing ? `/api/roles/${editing.id}` : '/api/roles', {
        method: editing ? 'PUT' : 'POST',
        body: JSON.stringify({ name: name.trim(), description: description.trim() || null, permissions: selected }),
      });
      setShowForm(false);
      await load();
    } catch (reason) {
      setError(reason instanceof Error ? reason.message : 'Unable to save role.');
    } finally {
      setSaving(false);
    }
  };

  const remove = async (role: RoleRecord) => {
    if (!window.confirm(`Delete the role “${role.name}”?`)) return;
    setError(null);
    try {
      await adminApi(`/api/roles/${role.id}`, { method: 'DELETE' });
      await load();
    } catch (reason) {
      setError(reason instanceof Error ? reason.message : 'Unable to delete role.');
    }
  };

  return (
    <div className="space-y-6">
      <div className="flex flex-col gap-4 md:flex-row md:items-end md:justify-between">
        <div>
          <p className="text-sm font-semibold uppercase tracking-wider text-primary">Identity</p>
          <h1 className="text-3xl font-bold text-gray-950">Roles and permissions</h1>
          <p className="mt-1 text-gray-500">Capability-based access roles for the current tenant.</p>
        </div>
        {canCreate && (
          <button onClick={openCreate} className="inline-flex items-center justify-center gap-2 rounded-lg bg-primary px-4 py-2 font-semibold text-white">
            <Plus className="h-4 w-4" /> Custom role
          </button>
        )}
      </div>

      {error && <div className="rounded-xl border border-red-200 bg-red-50 p-4 text-red-700">{error}</div>}

      <div className="grid gap-4 lg:grid-cols-2">
        {roles.map((role) => (
          <article key={role.id} className="rounded-xl border border-gray-200 bg-white p-5">
            <div className="flex items-start justify-between gap-3">
              <div className="flex gap-3">
                <span className="rounded-lg bg-primary-50 p-2 text-primary"><KeyRound className="h-5 w-5" /></span>
                <div>
                  <h2 className="font-bold text-gray-950">{role.name}</h2>
                  <p className="text-sm text-gray-500">{role.description ?? 'No description'}</p>
                </div>
              </div>
              <span className="rounded-full bg-gray-100 px-2 py-1 text-xs font-semibold text-gray-600">{role.isSystemRole ? 'Standard' : 'Custom'}</span>
            </div>
            <div className="mt-4 flex flex-wrap gap-1.5">
              {role.permissions.map((permission) => <span key={permission} className="rounded bg-gray-100 px-2 py-1 text-xs text-gray-600">{permission}</span>)}
              {!role.permissions.length && <span className="text-sm text-gray-400">No capabilities</span>}
            </div>
            {!role.isSystemRole && (canUpdate || canDelete) && (
              <div className="mt-5 flex gap-2 border-t border-gray-100 pt-4">
                {canUpdate && <button onClick={() => openEdit(role)} className="inline-flex items-center gap-2 rounded-lg border px-3 py-2 text-sm font-semibold"><Pencil className="h-4 w-4" /> Edit</button>}
                {canDelete && <button onClick={() => void remove(role)} className="inline-flex items-center gap-2 rounded-lg bg-red-50 px-3 py-2 text-sm font-semibold text-red-700"><Trash2 className="h-4 w-4" /> Delete</button>}
              </div>
            )}
          </article>
        ))}
      </div>

      {showForm && (
        <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/40 p-4">
          <form onSubmit={save} className="max-h-[90vh] w-full max-w-3xl overflow-y-auto rounded-2xl bg-white p-6 shadow-xl">
            <div className="flex items-center justify-between">
              <h2 className="text-xl font-bold">{editing ? 'Edit custom role' : 'Create custom role'}</h2>
              <button type="button" onClick={() => setShowForm(false)} aria-label="Close"><X className="h-5 w-5" /></button>
            </div>
            <div className="mt-5 grid gap-4 md:grid-cols-2">
              <label className="text-sm font-semibold">Name<input required minLength={2} maxLength={100} value={name} onChange={(event) => setName(event.target.value)} className="mt-1 w-full rounded-lg border p-2 font-normal" /></label>
              <label className="text-sm font-semibold">Description<input maxLength={500} value={description} onChange={(event) => setDescription(event.target.value)} className="mt-1 w-full rounded-lg border p-2 font-normal" /></label>
            </div>
            <div className="mt-6 space-y-4">
              {groups.map(([capability, permissions]) => (
                <fieldset key={capability} className="rounded-xl border p-4">
                  <legend className="px-2 font-semibold capitalize">{capability}</legend>
                  <div className="grid gap-2 sm:grid-cols-2 lg:grid-cols-3">
                    {permissions.map((permission) => (
                      <label key={permission} className="flex items-center gap-2 text-sm">
                        <input type="checkbox" checked={selected.includes(permission)} onChange={(event) => setSelected((current) => event.target.checked ? [...current, permission] : current.filter((item) => item !== permission))} />
                        {permission}
                      </label>
                    ))}
                  </div>
                </fieldset>
              ))}
            </div>
            <div className="mt-6 flex justify-end gap-3">
              <button type="button" onClick={() => setShowForm(false)} className="rounded-lg border px-4 py-2 font-semibold">Cancel</button>
              <button disabled={saving} className="rounded-lg bg-primary px-4 py-2 font-semibold text-white disabled:opacity-50">{saving ? 'Saving…' : 'Save role'}</button>
            </div>
          </form>
        </div>
      )}
    </div>
  );
}
