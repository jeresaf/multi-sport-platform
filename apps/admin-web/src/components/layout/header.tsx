'use client';

import { Bell, Search, HelpCircle, Menu } from 'lucide-react';
import Link from 'next/link';
import { useRouter } from 'next/navigation';
import { FormEvent, useEffect, useState } from 'react';
import { useTenantBranding } from '@/components/tenant-branding-provider';
import {
  AdminUser,
  SelectedTenant,
  adminApi,
  getSelectedTenant,
  getStoredUser,
  isPlatformOperator,
  setSelectedTenant,
} from '@/lib/admin-api';

type TenantOption = SelectedTenant;

export function Header({ onOpenNavigation }: { onOpenNavigation: () => void }) {
  const [searchQuery, setSearchQuery] = useState('');
  const [helpOpen, setHelpOpen] = useState(false);
  const [user, setUser] = useState<AdminUser | null>(null);
  const [tenants, setTenants] = useState<TenantOption[]>([]);
  const [selectedTenantId, setSelectedTenantId] = useState('');
  const branding = useTenantBranding();
  const router = useRouter();

  useEffect(() => {
    const storedUser = getStoredUser();
    setUser(storedUser);
    setSelectedTenantId(getSelectedTenant()?.id ?? '');
    if (isPlatformOperator(storedUser)) {
      adminApi<TenantOption[]>('/api/tenants')
        .then(setTenants)
        .catch(() => setTenants([]));
    }
  }, []);

  function switchTenant(tenantId: string) {
    const tenant = tenants.find((item) => item.id === tenantId) ?? null;
    setSelectedTenant(tenant);
    setSelectedTenantId(tenantId);
    window.location.assign('/');
  }

  function search(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    const query = searchQuery.trim();
    if (!query) return;
    window.dispatchEvent(new CustomEvent('admin:competition-search', { detail: query }));
    router.push(`/competitions?search=${encodeURIComponent(query)}`);
  }

  return (
    <header className="bg-white border-b border-gray-200 px-6 py-3">
      <div className="flex items-center justify-between">
        <div className="flex items-center gap-4 flex-1">
          <button
            type="button"
            onClick={onOpenNavigation}
            className="rounded-lg p-2 hover:bg-gray-100 md:hidden"
            aria-label="Open administration navigation"
            aria-controls="admin-mobile-navigation"
          >
            <Menu className="h-5 w-5" />
          </button>
          
          <form className="relative max-w-md flex-1" role="search" onSubmit={search}>
            <Search className="absolute left-3 top-1/2 -translate-y-1/2 h-4 w-4 text-gray-400" />
            <input
              type="text"
              aria-label="Search competitions"
              placeholder="Search competitions..."
              value={searchQuery}
              onChange={(e) => setSearchQuery(e.target.value)}
              className="w-full pl-10 pr-4 py-2 border border-gray-200 rounded-lg text-sm focus:outline-none focus:ring-2 focus:ring-primary focus:border-transparent"
            />
          </form>
        </div>

        <div className="flex items-center gap-2">
          <Link
            href="/notifications"
            className="relative rounded-lg p-2 hover:bg-gray-100"
            aria-label="Open notifications"
          >
            <Bell className="h-5 w-5 text-gray-500" />
          </Link>
          
          <button
            type="button"
            className="rounded-lg p-2 hover:bg-gray-100"
            aria-label="Open help"
            aria-expanded={helpOpen}
            onClick={() => setHelpOpen(value => !value)}
          >
            <HelpCircle className="h-5 w-5 text-gray-500" />
          </button>

          <div className="h-8 w-px bg-gray-200 mx-2"></div>

          {isPlatformOperator(user) ? (
            <label className="hidden sm:block">
              <span className="sr-only">Active tenant workspace</span>
              <select
                aria-label="Active tenant workspace"
                value={selectedTenantId}
                onChange={(event) => switchTenant(event.target.value)}
                className="h-10 max-w-64 rounded-lg border border-gray-200 bg-white px-3 text-sm font-semibold text-gray-700"
              >
                <option value="">All tenants (platform)</option>
                {tenants.map((tenant) => (
                  <option key={tenant.id} value={tenant.id}>{tenant.name}</option>
                ))}
              </select>
            </label>
          ) : (
            <span className="hidden max-w-48 truncate text-sm font-medium text-gray-600 sm:block">
              {branding?.tenantName || 'Tenant administration'}
            </span>
          )}
        </div>
      </div>
      {helpOpen && (
        <aside className="mt-3 rounded-lg border border-blue-200 bg-blue-50 p-3 text-sm text-blue-950" role="status">
          <div className="flex flex-wrap items-center justify-between gap-3">
            <p><strong>Need help?</strong> Review audit history for changes, or use reports for operational exports.</p>
            <div className="flex gap-2">
              <Link className="rounded-md border border-blue-300 bg-white px-3 py-1.5 font-semibold" href="/audit" onClick={() => setHelpOpen(false)}>Audit history</Link>
              <Link className="rounded-md bg-blue-700 px-3 py-1.5 font-semibold text-white" href="/reports" onClick={() => setHelpOpen(false)}>Reports</Link>
            </div>
          </div>
        </aside>
      )}
    </header>
  );
}
