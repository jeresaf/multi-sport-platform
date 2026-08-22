'use client';

import { createContext, useContext, useEffect, useState } from 'react';
import { usePathname } from 'next/navigation';
import { adminApi, getSelectedTenant, getStoredUser, isPlatformOperator } from '@/lib/admin-api';

export type AppliedBranding = {
  tenantName: string;
  shortName?: string | null;
  logoUrl?: string | null;
  primaryColor?: string | null;
  secondaryColor?: string | null;
  accentColor?: string | null;
};

type TenantDashboardBranding = { branding: AppliedBranding };
const BrandingContext = createContext<AppliedBranding | null>(null);
const defaults = ['14 165 233', '124 58 237', '245 158 11'];

function colorChannels(value: string | null | undefined, fallback: string) {
  if (!value || !/^#[0-9a-f]{6}$/i.test(value)) return fallback;
  return `${parseInt(value.slice(1, 3), 16)} ${parseInt(value.slice(3, 5), 16)} ${parseInt(value.slice(5, 7), 16)}`;
}

export function TenantBrandingProvider({ children }: { children: React.ReactNode }) {
  const pathname = usePathname();
  const [branding, setBranding] = useState<AppliedBranding | null>(null);

  useEffect(() => {
    const user = getStoredUser();
    const root = document.documentElement;
    const apply = (value: AppliedBranding | null) => {
      root.style.setProperty('--brand-primary', colorChannels(value?.primaryColor, defaults[0]));
      root.style.setProperty('--brand-secondary', colorChannels(value?.secondaryColor, defaults[1]));
      root.style.setProperty('--brand-accent', colorChannels(value?.accentColor, defaults[2]));
      setBranding(value);
    };

    const selectedTenant = isPlatformOperator(user) ? getSelectedTenant() : null;
    if ((!user?.tenantId && !selectedTenant) || (isPlatformOperator(user) && !selectedTenant)) {
      apply(null);
      return;
    }

    adminApi<TenantDashboardBranding>('/api/tenant-admin/dashboard')
      .then((dashboard) => apply(dashboard.branding))
      .catch(() => apply(null));
  }, [pathname]);

  return <BrandingContext.Provider value={branding}>{children}</BrandingContext.Provider>;
}

export function useTenantBranding() {
  return useContext(BrandingContext);
}
