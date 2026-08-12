'use client';

import { Search, ShieldAlert, ShieldCheck } from 'lucide-react';
import { useCallback, useEffect, useMemo, useState } from 'react';
import { adminApi, getStoredUser, hasAnyPermission } from '@/lib/admin-api';

type UserRecord = {
  id: string;
  email: string;
  firstName: string;
  lastName: string;
  emailVerified: boolean;
  isActive: boolean;
  isSuspended: boolean;
  lastLoginAt?: string | null;
  roles: string[];
};

type RoleRecord = {
  id: string;
  name: string;
  isSystemRole: boolean;
};

export default function UsersPage() {
  const [users, setUsers] = useState<UserRecord[]>([]);
  const [roles, setRoles] = useState<RoleRecord[]>([]);
  const [search, setSearch] = useState('');
  const [error, setError] = useState<string | null>(null);
  const [loading, setLoading] = useState(true);
  const [updating, setUpdating] = useState<string | null>(null);
  const canManage = hasAnyPermission(getStoredUser(), ['users.manage']);
  const canAssignRoles = hasAnyPermission(getStoredUser(), ['roles.assign']);

  const loadUsers = useCallback(async () => {
    setError(null);
    try {
      const [userData, roleData] = await Promise.all([
        adminApi<UserRecord[]>('/api/auth/users'),
        canAssignRoles ? adminApi<RoleRecord[]>('/api/roles') : Promise.resolve([]),
      ]);
      setUsers(userData);
      setRoles(roleData);
    } catch (reason) {
      setError(reason instanceof Error ? reason.message : 'Unable to load users.');
    } finally {
      setLoading(false);
    }
  }, [canAssignRoles]);

  useEffect(() => {
    void loadUsers();
  }, [loadUsers]);

  const filtered = useMemo(() => {
    const query = search.trim().toLowerCase();
    return users.filter((user) =>
      `${user.firstName} ${user.lastName} ${user.email} ${user.roles.join(' ')}`
        .toLowerCase()
        .includes(query),
    );
  }, [search, users]);

  const changeSuspension = async (user: UserRecord) => {
    const action = user.isSuspended ? 'unsuspend' : 'suspend';
    const reason = window.prompt(`Reason to ${action} ${user.email}:`);
    if (!reason?.trim()) return;

    setUpdating(user.id);
    setError(null);
    try {
      await adminApi(`/api/auth/users/${user.id}/${action}`, {
        method: 'POST',
        body: JSON.stringify({ reason: reason.trim() }),
      });
      await loadUsers();
    } catch (reason) {
      setError(reason instanceof Error ? reason.message : `Unable to ${action} user.`);
    } finally {
      setUpdating(null);
    }
  };

  const changeRole = async (user: UserRecord, roleId: string, assign: boolean) => {
    setUpdating(user.id);
    setError(null);
    try {
      await adminApi(`/api/roles/${assign ? 'assign' : 'remove'}`, {
        method: 'POST',
        body: JSON.stringify({ userId: user.id, roleId }),
      });
      await loadUsers();
    } catch (reason) {
      setError(reason instanceof Error ? reason.message : 'Unable to change the role assignment.');
    } finally {
      setUpdating(null);
    }
  };

  return (
    <div className="space-y-6">
      <div>
        <p className="text-sm font-semibold uppercase tracking-wider text-primary">Identity</p>
        <h1 className="text-3xl font-bold text-gray-950">Users</h1>
        <p className="mt-1 text-gray-500">Review tenant accounts and control account access.</p>
      </div>

      <label className="relative block max-w-lg">
        <Search className="absolute left-3 top-3 h-4 w-4 text-gray-400" />
        <input
          value={search}
          onChange={(event) => setSearch(event.target.value)}
          placeholder="Search name, email, or role"
          className="w-full rounded-lg border border-gray-200 bg-white py-2 pl-10 pr-3"
        />
      </label>

      {loading && <State>Loading users…</State>}
      {error && <State tone="error">{error}</State>}

      {!loading && !error && filtered.length === 0 && <State>No users found.</State>}
      <div className="overflow-hidden rounded-xl border border-gray-200 bg-white">
        {filtered.map((user) => (
          <article key={user.id} className="flex flex-col gap-4 border-b border-gray-100 p-5 last:border-b-0 md:flex-row md:items-center md:justify-between">
            <div>
              <div className="flex items-center gap-2">
                <h2 className="font-semibold text-gray-950">{user.firstName} {user.lastName}</h2>
                <span className={`rounded-full px-2 py-0.5 text-xs font-semibold ${user.isSuspended ? 'bg-red-50 text-red-700' : 'bg-green-50 text-green-700'}`}>
                  {user.isSuspended ? 'Suspended' : 'Active'}
                </span>
              </div>
              <p className="text-sm text-gray-500">{user.email}</p>
              <p className="mt-1 text-xs text-gray-400">
                {user.roles.length ? user.roles.join(', ') : 'No role'} · Email {user.emailVerified ? 'verified' : 'not verified'} · Last login {formatDate(user.lastLoginAt)}
              </p>
              {canAssignRoles && (
                <div className="mt-3 flex flex-wrap items-center gap-2">
                  {user.roles.map((roleName) => {
                    const role = roles.find((candidate) => candidate.name === roleName);
                    return <span key={roleName} className="inline-flex items-center gap-1 rounded-full bg-primary-50 px-2 py-1 text-xs font-semibold text-primary">{roleName}{role && <button disabled={updating === user.id} onClick={() => void changeRole(user, role.id, false)} aria-label={`Remove ${roleName}`} className="ml-1">×</button>}</span>;
                  })}
                  <select
                    value=""
                    disabled={updating === user.id}
                    onChange={(event) => event.target.value && void changeRole(user, event.target.value, true)}
                    className="rounded-lg border border-gray-200 bg-white px-2 py-1 text-xs"
                  >
                    <option value="">Assign role…</option>
                    {roles.filter((role) => !user.roles.includes(role.name)).map((role) => <option key={role.id} value={role.id}>{role.name}</option>)}
                  </select>
                </div>
              )}
            </div>
            {canManage && (
              <button
                type="button"
                disabled={updating === user.id}
                onClick={() => void changeSuspension(user)}
                className={`inline-flex items-center justify-center gap-2 rounded-lg px-4 py-2 text-sm font-semibold disabled:opacity-50 ${user.isSuspended ? 'bg-green-50 text-green-700 hover:bg-green-100' : 'bg-red-50 text-red-700 hover:bg-red-100'}`}
              >
                {user.isSuspended ? <ShieldCheck className="h-4 w-4" /> : <ShieldAlert className="h-4 w-4" />}
                {updating === user.id ? 'Updating…' : user.isSuspended ? 'Unsuspend' : 'Suspend'}
              </button>
            )}
          </article>
        ))}
      </div>
    </div>
  );
}

function formatDate(value?: string | null) {
  if (!value) return 'never';
  return new Intl.DateTimeFormat(undefined, { dateStyle: 'medium', timeStyle: 'short' }).format(new Date(value));
}

function State({ children, tone = 'neutral' }: { children: React.ReactNode; tone?: 'neutral' | 'error' }) {
  return <div className={`rounded-xl border p-5 ${tone === 'error' ? 'border-red-200 bg-red-50 text-red-700' : 'border-gray-200 bg-white text-gray-500'}`}>{children}</div>;
}
