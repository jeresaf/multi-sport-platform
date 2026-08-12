import type { Metadata } from 'next';
import './globals.css';
import { TenantBrandShell } from '@/components/tenant-brand-shell';

export const metadata: Metadata = {
  title: 'Sports Platform',
  description: 'Live sports scores, results, and competition coverage',
};

export default function RootLayout({
  children,
}: {
  children: React.ReactNode;
}) {
  return (
    <html lang="en">
      <body>
        <TenantBrandShell>{children}</TenantBrandShell>
      </body>
    </html>
  );
}
