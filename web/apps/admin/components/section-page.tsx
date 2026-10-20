"use client";

import { usePathname } from "next/navigation";
import { ForbiddenView, NotAvailableView, NotFoundView } from "@/components/status-page";
import { currentItem, isAllowed } from "@/lib/navigation";
import { useWorkspaceNavigation } from "@/lib/workspace-context";

/** A workspace path without its own page: a planned screen (not available yet, or not allowed), or nothing (404). */
export function SectionPage() {
  const pathname = usePathname();
  const { items, actions } = useWorkspaceNavigation();
  const item = currentItem(items, pathname);

  if (!item || item.href !== pathname) {
    return <NotFoundView />;
  }
  if (!isAllowed(item, actions)) {
    return <ForbiddenView />;
  }
  return <NotAvailableView label={item.label} />;
}
