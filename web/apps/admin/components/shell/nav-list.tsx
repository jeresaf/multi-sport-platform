"use client";

import { cn } from "@msp/ui/lib/utils";
import Link from "next/link";
import { usePathname } from "next/navigation";
import { currentItem, type NavItem } from "@/lib/navigation";

/**
 * The workspace's navigation. `compact` is the tablet rail: icons with the label kept for screen readers and as a
 * tooltip, full labels again from desktop width.
 */
export function NavList({
  items,
  compact = false,
  onNavigate,
}: Readonly<{ items: readonly NavItem[]; compact?: boolean; onNavigate?: () => void }>) {
  const current = currentItem(items, usePathname());

  return (
    <ul className="grid gap-1">
      {items.map((item) => {
        const Icon = item.icon;
        const active = item === current;
        return (
          <li key={item.href}>
            <Link
              href={item.href}
              title={compact ? item.label : undefined}
              aria-current={active ? "page" : undefined}
              onClick={onNavigate}
              className={cn(
                "flex min-h-11 items-center gap-3 rounded-md px-3 text-sm font-medium hover:bg-muted focus-visible:ring-2 focus-visible:ring-ring focus-visible:outline-none",
                active ? "bg-muted text-foreground" : "text-muted-foreground",
                compact && "justify-center desktop:justify-start",
              )}
            >
              <Icon aria-hidden="true" className="size-5 shrink-0" />
              <span className={cn(compact && "sr-only desktop:not-sr-only")}>{item.label}</span>
            </Link>
          </li>
        );
      })}
    </ul>
  );
}
