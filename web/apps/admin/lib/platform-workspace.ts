"use client";

// The platform console on screen, for its pages: the actions the user holds there. Platform calls use `api` (no
// tenant header); the console's layout renders pages only once the capabilities are known.
import { useWorkspaceNavigation } from "./workspace-context";

export function usePlatformCan(): (action: string) => boolean {
  const { actions } = useWorkspaceNavigation();
  return (action) => actions.includes(action);
}
