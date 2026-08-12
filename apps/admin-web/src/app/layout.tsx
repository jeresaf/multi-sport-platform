import type { Metadata } from 'next';
import './globals.css';
import { Providers } from '@/components/providers';
import { AdminShell } from '@/components/layout/admin-shell';
import { TenantBrandingProvider } from '@/components/tenant-branding-provider';

export const metadata: Metadata = {
  title: 'Sports Platform Admin',
  description: 'Multi-tenant multi-sport competition management platform',
};

export default function RootLayout({
  children,
}: {
  children: React.ReactNode;
}) {
  return (
    <html lang="en">
      <body>
        <Providers>
          <TenantBrandingProvider>
            <AdminShell>{children}</AdminShell>
          </TenantBrandingProvider>
        </Providers>
      </body>
    </html>
  );
}
