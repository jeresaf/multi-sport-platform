"use client";

import { useEffect, useMemo, type ReactNode } from "react";
import { Loading } from "@/components/loading";
import { ProblemAlert } from "@/components/problem-alert";
import { AppShell } from "@/components/shell/app-shell";
import { ForbiddenView } from "@/components/status-page";
import { useMe } from "@/lib/account";
import { isApiProblem, isUnauthenticated } from "@/lib/api";
import { usePlatformCapabilities } from "@/lib/capabilities";
import { platformNavigation, visibleNavigation } from "@/lib/navigation";
import { platformPath, rememberWorkspace } from "@/lib/workspace";
import { WorkspaceProvider } from "@/lib/workspace-context";

const workspace = "Platform console";

/** The platform console (`/platform`), for platform staff; its navigation filtered by their platform capabilities. */
export default function PlatformLayout({ children }: Readonly<{ children: ReactNode }>) {
  const me = useMe();
  const isPlatformStaff = (me.data?.platformRoles.length ?? 0) > 0;
  const capabilities = usePlatformCapabilities(isPlatformStaff);
  const items = useMemo(() => platformNavigation(), []);

  // As for tenants: only a console that opens is remembered.
  useEffect(() => {
    if (capabilities.isSuccess) {
      rememberWorkspace(platformPath);
    }
  }, [capabilities.isSuccess]);

  if (!isPlatformStaff || !capabilities.data) {
    return (
      <AppShell workspace={workspace} navigation={[]}>
        {!isPlatformStaff ? <ForbiddenView /> : null}
        {/* A 401 is the guard's: it sends the user to sign in. */}
        {isPlatformStaff && (capabilities.isPending || isUnauthenticated(capabilities.error)) ? <Loading /> : null}
        {capabilities.isError && isApiProblem(capabilities.error) && capabilities.error.status === 403 ? (
          <ForbiddenView error={capabilities.error} />
        ) : capabilities.isError && !isUnauthenticated(capabilities.error) ? (
          <ProblemAlert error={capabilities.error} />
        ) : null}
      </AppShell>
    );
  }

  const { actions } = capabilities.data;
  return (
    <WorkspaceProvider value={{ items, actions }}>
      <AppShell workspace={workspace} navigation={visibleNavigation(items, actions)}>
        {children}
      </AppShell>
    </WorkspaceProvider>
  );
}
