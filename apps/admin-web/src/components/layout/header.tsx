'use client';

import { Bell, Search, HelpCircle, Menu } from 'lucide-react';
import { useState } from 'react';
import { useEffect } from 'react';
import { useTenantBranding } from '@/components/tenant-branding-provider';
import { AdminUser, getStoredUser, isPlatformOperator } from '@/lib/admin-api';

export function Header() {
  const [searchQuery, setSearchQuery] = useState('');
  const [user, setUser] = useState<AdminUser | null>(null);
  const branding = useTenantBranding();

  useEffect(() => setUser(getStoredUser()), []);

  return (
    <header className="bg-white border-b border-gray-200 px-6 py-3">
      <div className="flex items-center justify-between">
        <div className="flex items-center gap-4 flex-1">
          <button className="lg:hidden p-2 hover:bg-gray-100 rounded-lg">
            <Menu className="h-5 w-5" />
          </button>
          
          <div className="relative flex-1 max-w-md">
            <Search className="absolute left-3 top-1/2 -translate-y-1/2 h-4 w-4 text-gray-400" />
            <input
              type="text"
              placeholder="Search competitions, tenants, contests..."
              value={searchQuery}
              onChange={(e) => setSearchQuery(e.target.value)}
              className="w-full pl-10 pr-4 py-2 border border-gray-200 rounded-lg text-sm focus:outline-none focus:ring-2 focus:ring-primary focus:border-transparent"
            />
          </div>
        </div>

        <div className="flex items-center gap-2">
          <button className="p-2 hover:bg-gray-100 rounded-lg relative">
            <Bell className="h-5 w-5 text-gray-500" />
            <span className="absolute top-1 right-1 w-2 h-2 bg-red-500 rounded-full"></span>
          </button>
          
          <button className="p-2 hover:bg-gray-100 rounded-lg">
            <HelpCircle className="h-5 w-5 text-gray-500" />
          </button>

          <div className="h-8 w-px bg-gray-200 mx-2"></div>

          <span className="hidden max-w-48 truncate text-sm font-medium text-gray-600 sm:block">
            {isPlatformOperator(user) ? 'All tenants' : branding?.tenantName || 'Tenant administration'}
          </span>
        </div>
      </div>
    </header>
  );
}
