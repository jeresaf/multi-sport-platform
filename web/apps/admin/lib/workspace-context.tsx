"use client";

import { createContext, useContext, type ReactNode } from "react";
import type { NavItem } from "./navigation";

/** The workspace on screen: its navigation (every entry) and the actions the user holds in it. */
export type WorkspaceNavigation = { items: readonly NavItem[]; actions: readonly string[] };

const WorkspaceContext = createContext<WorkspaceNavigation | null>(null);

export function WorkspaceProvider({ value, children }: Readonly<{ value: WorkspaceNavigation; children: ReactNode }>) {
  return <WorkspaceContext.Provider value={value}>{children}</WorkspaceContext.Provider>;
}

export function useWorkspaceNavigation(): WorkspaceNavigation {
  const value = useContext(WorkspaceContext);
  if (!value) {
    throw new Error("useWorkspaceNavigation needs a WorkspaceProvider (a workspace layout).");
  }
  return value;
}
