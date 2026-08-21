'use client';

import Link from 'next/link';
import { usePathname } from 'next/navigation';
import { 
  LayoutDashboard, 
  Users, 
  Trophy, 
  Activity, 
  ClipboardCheck,
  ChevronDown,
  ChevronRight,
  LogOut,
  LogIn,
  Palette,
  CreditCard,
  Calendar,
  FileBarChart,
  Handshake,
  History,
  ShieldCheck,
  KeyRound,
  Medal,
  Upload,
  Bell,
  X,
} from 'lucide-react';
import { useEffect, useState } from 'react';
import { cn } from '@/components/ui/card';
import { AdminUser, getStoredUser, hasAnyPermission, isPlatformOperator, isSignedIn, logout } from '@/lib/admin-api';
import { useRouter } from 'next/navigation';
import { useTenantBranding } from '@/components/tenant-branding-provider';
import type { LucideIcon } from 'lucide-react';

type NavigationItem = {
  name: string;
  href: string;
  icon: LucideIcon;
  permissions?: string[];
  platformOnly?: boolean;
  children?: { name: string; href: string; permissions?: string[] }[];
};

const navigation: NavigationItem[] = [
  { name: 'Dashboard', href: '/', icon: LayoutDashboard },
  { name: 'Schedule', href: '/schedule', icon: Calendar, permissions: ['competitions.read', 'competitions.manage', 'contests.read'] },
  { name: 'Notifications', href: '/notifications', icon: Bell, permissions: ['notifications.read', 'notifications.manage'] },
  { name: 'Users', href: '/users', icon: Users, permissions: ['users.read', 'users.manage'] },
  { name: 'Roles', href: '/roles', icon: KeyRound, permissions: ['roles.read', 'roles.create', 'roles.update', 'roles.assign'] },
  { name: 'Sports', href: '/sports', icon: Medal, permissions: ['sports.read', 'sports.create', 'sports.manage'] },
  { 
    name: 'Tenants', 
    href: '/tenants', 
    icon: Users,
    platformOnly: true,
    permissions: ['tenants.read', 'tenants.manage'],
    children: [
      { name: 'All Tenants', href: '/tenants' },
    ]
  },
  {
    name: 'Competitions', 
    href: '/competitions', 
    icon: Trophy,
    permissions: ['competitions.read', 'competitions.manage', 'competitions.create'],
    children: [
      { name: 'All Competitions', href: '/competitions' },
      { name: 'Create New', href: '/competitions/new', permissions: ['competitions.create'] },
    ]
  },
  { name: 'Athletes', href: '/participants', icon: Users, permissions: ['participants.read', 'participants.create'] },
  { name: 'Teams & clubs', href: '/organizations', icon: Users, permissions: ['participants.read', 'participants.create'] },
  { name: 'Bulk imports', href: '/imports', icon: Upload, permissions: ['participants.read', 'participants.create'] },
  { name: 'Registration forms', href: '/registration-forms', icon: ClipboardCheck, permissions: ['participants.read', 'participants.create'] },
  { name: 'Registration review', href: '/registrations', icon: ClipboardCheck, permissions: ['competitions.manage'] },
  { 
    name: 'Live Scoring', 
    href: '/scoring', 
    icon: Activity,
    permissions: ['scoring.manage', 'scoring.submit', 'scoring.correct'],
    children: [
      { name: 'Active Contests', href: '/scoring' },
      { name: 'Football', href: '/scoring/football' },
      { name: 'Basketball', href: '/scoring/basketball' },
      { name: 'Golf', href: '/scoring/golf' },
      { name: 'Combat', href: '/scoring/combat' },
      { name: 'Athletics', href: '/scoring/athletics' },
      { name: 'Cricket', href: '/scoring/cricket' },
      { name: 'Rugby 7s & 15s', href: '/scoring/rugby' },
    ]
  },
  {
    name: 'Result Review',
    href: '/results',
    icon: ClipboardCheck,
    permissions: ['results.manage', 'results.approve', 'results.submit', 'scoring.correct'],
  },
  { name: 'Branding', href: '/branding', icon: Palette, permissions: ['branding.read', 'branding.manage'] },
  { name: 'Subscription', href: '/subscription', icon: CreditCard, permissions: ['subscriptions.read', 'subscriptions.manage'] },
  { name: 'Reports', href: '/reports', icon: FileBarChart, permissions: ['reports.read', 'reports.export'] },
  { name: 'Sponsors', href: '/sponsors', icon: Handshake, permissions: ['sponsorships.read', 'sponsorships.manage'] },
  { name: 'Audit history', href: '/audit', icon: History, permissions: ['audit.read'] },
  { name: 'Privacy', href: '/privacy', icon: ShieldCheck, permissions: ['privacy.read', 'privacy.manage'] },
];

export function Sidebar({ mobile = false, onNavigate }: { mobile?: boolean; onNavigate?: () => void }) {
  const pathname = usePathname();
  const router = useRouter();
  const [expandedItems, setExpandedItems] = useState<string[]>(['Tenants', 'Competitions', 'Live Scoring']);
  const [user, setUser] = useState<AdminUser | null>(null);
  const [signedIn, setSignedIn] = useState(false);
  const branding = useTenantBranding();

  useEffect(() => {
    setUser(getStoredUser());
    setSignedIn(isSignedIn());
  }, [pathname]);

  const toggleExpanded = (name: string) => {
    setExpandedItems(prev => 
      prev.includes(name) 
        ? prev.filter(n => n !== name)
        : [...prev, name]
    );
  };

  const visibleNavigation = navigation
    .filter((item) => !item.platformOnly || isPlatformOperator(user))
    .filter((item) => !item.permissions || hasAnyPermission(user, item.permissions))
    .map((item) => ({
      ...item,
      children: item.children?.filter((child) =>
        !child.permissions || hasAnyPermission(user, child.permissions),
      ),
    }));

  return (
    <aside
      id={mobile ? 'admin-mobile-navigation' : undefined}
      aria-label="Administration navigation"
      className={`${mobile ? 'flex h-full w-72' : 'hidden w-64 md:flex'} shrink-0 flex-col bg-gray-900 text-white`}
    >
      <div className="p-4 border-b border-gray-800">
        <div className="flex items-center gap-2">
          <div className="w-8 h-8 bg-primary rounded-lg flex items-center justify-center">
            <Trophy className="h-5 w-5 text-white" />
          </div>
          <div>
            <h1 className="truncate font-bold text-lg">{branding?.shortName || branding?.tenantName || 'Sports Platform'}</h1>
            <p className="text-xs text-gray-400">Admin Console</p>
          </div>
          {mobile && <button type="button" onClick={onNavigate} className="ml-auto rounded-md p-2 hover:bg-gray-800" aria-label="Close administration navigation"><X className="h-5 w-5" /></button>}
        </div>
      </div>

      <nav className="flex-1 overflow-y-auto py-4">
        {visibleNavigation.map((item) => (
          <div key={item.name}>
            {item.children ? (
              <div>
                <button
                  onClick={() => toggleExpanded(item.name)}
                  className={cn(
                    'w-full flex items-center justify-between px-4 py-2 text-sm hover:bg-gray-800 transition-colors',
                    pathname.startsWith(item.href) && 'bg-gray-800 text-primary'
                  )}
                >
                  <div className="flex items-center gap-3">
                    <item.icon className="h-5 w-5" />
                    <span>{item.name}</span>
                  </div>
                  {expandedItems.includes(item.name) ? (
                    <ChevronDown className="h-4 w-4" />
                  ) : (
                    <ChevronRight className="h-4 w-4" />
                  )}
                </button>
                {expandedItems.includes(item.name) && (
                  <div className="ml-8 py-1">
                    {item.children.map(child => (
                      <Link
                        key={child.name}
                        href={child.href}
                        onClick={onNavigate}
                        className={cn(
                          'block px-4 py-2 text-sm hover:bg-gray-800 rounded-md transition-colors',
                          pathname === child.href && 'text-primary bg-gray-800'
                        )}
                      >
                        {child.name}
                      </Link>
                    ))}
                  </div>
                )}
              </div>
            ) : (
              <Link
                href={item.href}
                onClick={onNavigate}
                className={cn(
                  'flex items-center gap-3 px-4 py-2 text-sm hover:bg-gray-800 transition-colors',
                  pathname === item.href && 'bg-gray-800 text-primary'
                )}
              >
                <item.icon className="h-5 w-5" />
                <span>{item.name}</span>
              </Link>
            )}
          </div>
        ))}
      </nav>

      <div className="p-4 border-t border-gray-800">
        <div className="flex items-center gap-3">
          <div className="w-8 h-8 bg-gray-700 rounded-full flex items-center justify-center">
            <Users className="h-4 w-4" />
          </div>
          <div className="flex-1 min-w-0">
            <p className="text-sm font-medium truncate">
              {user ? `${user.firstName} ${user.lastName}` : 'Not signed in'}
            </p>
            <p className="text-xs text-gray-400 truncate">{user?.email ?? 'Public session'}</p>
          </div>
          <button
            title={signedIn ? 'Sign out' : 'Sign in'}
            onClick={() => {
              if (signedIn) logout();
              onNavigate?.();
              router.push('/login');
            }}
            className="rounded-md p-2 hover:bg-gray-800"
          >
            {signedIn ? <LogOut className="h-4 w-4" /> : <LogIn className="h-4 w-4" />}
          </button>
        </div>
      </div>
    </aside>
  );
}
