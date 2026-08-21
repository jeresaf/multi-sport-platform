'use client';

import { usePathname } from 'next/navigation';
import { useEffect, useState } from 'react';
import { Header } from './header';
import { Sidebar } from './sidebar';

export function AdminShell({ children }: { children: React.ReactNode }) {
  const pathname = usePathname();
  const [navigationOpen, setNavigationOpen] = useState(false);

  useEffect(() => {
    if (!navigationOpen) return;
    const close = (event: KeyboardEvent) => {
      if (event.key === 'Escape') setNavigationOpen(false);
    };
    document.addEventListener('keydown', close);
    return () => document.removeEventListener('keydown', close);
  }, [navigationOpen]);
  if (pathname === '/login') {
    return <main className="min-h-screen bg-gray-950">{children}</main>;
  }

  return (
    <div className="flex h-screen bg-gray-100">
      <Sidebar />
      {navigationOpen && (
        <div className="fixed inset-0 z-50 md:hidden">
          <button type="button" className="absolute inset-0 bg-black/60" aria-label="Close administration navigation" onClick={() => setNavigationOpen(false)} />
          <div className="relative h-full w-72 shadow-2xl">
            <Sidebar mobile onNavigate={() => setNavigationOpen(false)} />
          </div>
        </div>
      )}
      <div className="flex min-w-0 flex-1 flex-col overflow-hidden">
        <Header onOpenNavigation={() => setNavigationOpen(true)} />
        <main className="flex-1 overflow-y-auto p-4 md:p-6">{children}</main>
      </div>
    </div>
  );
}
