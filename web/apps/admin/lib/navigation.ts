// The shell's navigation (admin-app.md): every entry names the action its screen needs, and the shell shows only the
// entries the user's capabilities allow. Entries whose screens come later open a "not available yet" page. A tenant
// workspace's entries are grouped (Registry, Sports configuration, Settings); a group with no entry the user may open
// isn't shown.
import {
  Activity,
  BookOpen,
  Building2,
  CreditCard,
  LayoutDashboard,
  ListChecks,
  Network,
  Package,
  ScrollText,
  Settings,
  Trophy,
  UserRound,
  Users,
  type LucideIcon,
} from "lucide-react";
import { platformPath, tenantPath } from "./workspace";

export type NavItem = {
  label: string;
  href: string;
  icon: LucideIcon;
  /** The action the screen needs; none for the workspace's overview. */
  action?: string;
  /** The heading the entry is listed under; none for the overview and the platform console's entries. */
  group?: string;
};

const registry = "Registry";
const sportsConfiguration = "Sports configuration";
const settings = "Settings";

export function tenantNavigation(slug: string): NavItem[] {
  const root = tenantPath(slug);
  return [
    { label: "Overview", href: root, icon: LayoutDashboard },
    { label: "People", href: `${root}/people`, icon: UserRound, action: "person.read", group: registry },
    { label: "Sports", href: `${root}/sports`, icon: Trophy, action: "sport.read", group: sportsConfiguration },
    { label: "Formats", href: `${root}/formats`, icon: Network, action: "format.read", group: sportsConfiguration },
    {
      label: "Rules templates",
      href: `${root}/rules-templates`,
      icon: BookOpen,
      action: "ruleset.read",
      group: sportsConfiguration,
    },
    {
      label: "Organisation",
      href: `${root}/settings/organisation`,
      icon: Settings,
      action: "tenant.settings.read",
      group: settings,
    },
    { label: "Members & roles", href: `${root}/settings/members`, icon: Users, action: "membership.read", group: settings },
    {
      label: "Enabled sports",
      href: `${root}/settings/sports`,
      icon: ListChecks,
      action: "tenant.sport.read",
      group: settings,
    },
    {
      label: "Subscription",
      href: `${root}/settings/subscription`,
      icon: CreditCard,
      action: "tenant.subscription.read",
      group: settings,
    },
  ];
}

export function platformNavigation(): NavItem[] {
  return [
    { label: "Overview", href: platformPath, icon: LayoutDashboard },
    { label: "Tenants", href: `${platformPath}/tenants`, icon: Building2, action: "tenant.read" },
    { label: "Plans", href: `${platformPath}/plans`, icon: Package, action: "plan.read" },
    { label: "Sports", href: `${platformPath}/sports`, icon: Trophy, action: "platform.sport.read" },
    { label: "Formats", href: `${platformPath}/formats`, icon: Network, action: "platform.sport.read" },
    { label: "Rules templates", href: `${platformPath}/rules-templates`, icon: BookOpen, action: "platform.sport.read" },
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

export type NavSection = { group: string | undefined; items: NavItem[] };

/** Consecutive entries of one group, in order: what the navigation lists under each heading. */
export function navigationSections(items: readonly NavItem[]): NavSection[] {
  const sections: NavSection[] = [];
  for (const item of items) {
    const last = sections.at(-1);
    if (last && last.group === item.group) {
      last.items.push(item);
    } else {
      sections.push({ group: item.group, items: [item] });
    }
  }
  return sections;
}
