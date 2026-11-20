"use client";

import { cn } from "@msp/ui/lib/utils";
import Link from "next/link";
import { usePathname } from "next/navigation";
import { Fragment, useId } from "react";
import { currentItem, navigationSections, type NavItem } from "@/lib/navigation";

/**
 * The workspace's navigation, its entries under their group's heading (a tenant workspace's Registry, Sports
 * configuration and Settings). `compact` is the tablet rail: icons with the label kept for screen readers and as a
 * tooltip, full labels and headings again from desktop width; on the rail a group starts after a rule.
 */
export function NavList({
  items,
  compact = false,
  onNavigate,
}: Readonly<{ items: readonly NavItem[]; compact?: boolean; onNavigate?: () => void }>) {
  const current = currentItem(items, usePathname());
  const id = useId();

  const entries = (list: readonly NavItem[]) =>
    list.map((item) => {
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
    });

  return (
    <ul className="grid gap-1">
      {navigationSections(items).map(({ group, items: list }, index) => {
        if (group === undefined) {
          return <Fragment key={`ungrouped-${index}`}>{entries(list)}</Fragment>;
        }
        const headingId = `${id}-group-${index}`;
        return (
          <li key={group} className={cn("grid gap-1 pt-3", compact && "border-t desktop:border-t-0")}>
            <p
              id={headingId}
              className={cn(
                "px-3 text-xs font-semibold tracking-wide text-muted-foreground uppercase",
                compact && "sr-only desktop:not-sr-only",
              )}
            >
              {group}
            </p>
            <ul aria-labelledby={headingId} className="grid gap-1">
              {entries(list)}
            </ul>
          </li>
        );
      })}
    </ul>
  );
}
