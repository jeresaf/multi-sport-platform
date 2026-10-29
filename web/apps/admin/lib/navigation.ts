// The shell's navigation (admin-app.md): every entry names the action its screen needs, and the shell shows only the
// entries the user's capabilities allow. Entries whose screens come later open a "not available yet" page.
import { Activity, Building2, CreditCard, LayoutDashboard, Package, ScrollText, Settings, Users, type LucideIcon } from "lucide-react";
import { platformPath, tenantPath } from "./workspace";

export type NavItem = {
  label: string;
  href: string;
  icon: LucideIcon;
  /** The action the screen needs; none for the workspace's overview. */
  action?: string;
};

export function tenantNavigation(slug: string): NavItem[] {
  const root = tenantPath(slug);
  return [
    { label: "Overview", href: root, icon: LayoutDashboard },
    { label: "Organisation", href: `${root}/settings/organisation`, icon: Settings, action: "tenant.settings.read" },
    { label: "Members & roles", href: `${root}/settings/members`, icon: Users, action: "membership.read" },
    {
      label: "Subscription",
      href: `${root}/settings/subscription`,
      icon: CreditCard,
      action: "tenant.subscription.read",
    },
  ];
}

export function platformNavigation(): NavItem[] {
  return [
    { label: "Overview", href: platformPath, icon: LayoutDashboard },
    { label: "Tenants", href: `${platformPath}/tenants`, icon: Building2, action: "tenant.read" },
    { label: "Plans", href: `${platformPath}/plans`, icon: Package, action: "plan.read" },
    { label: "Diagnostics", href: `${platformPath}/diagnostics`, icon: Activity, action: "platform.outbox.read" },
    { label: "Audit", href: `${platformPath}/audit`, icon: ScrollText, action: "platform.audit.read" },
  ];
}

export function isAllowed(item: NavItem, actions: readonly string[]): boolean {
  return item.action === undefined || actions.includes(item.action);
}

export function visibleNavigation(items: readonly NavItem[], actions: readonly string[]): NavItem[] {
  return items.filter((item) => isAllowed(item, actions));
}

/** The entry a path belongs to: itself or a page below it; a workspace's overview matches only its own path. */
export function currentItem(items: readonly NavItem[], pathname: string): NavItem | undefined {
  const [overview, ...sections] = items;
  return sections.find((item) => pathname === item.href || pathname.startsWith(`${item.href}/`)) ??
    (overview && pathname === overview.href ? overview : undefined);
}
