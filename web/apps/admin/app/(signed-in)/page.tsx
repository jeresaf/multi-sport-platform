"use client";

import { useRouter } from "next/navigation";
import { useEffect } from "react";
import { Loading } from "@/components/loading";
import { AppShell } from "@/components/shell/app-shell";
import { StatusPage } from "@/components/status-page";
import { useMe } from "@/lib/account";
import { landingPath, readLastWorkspace } from "@/lib/workspace";

/** `/` opens a workspace (lib/workspace.ts `landingPath`); someone with none is told so. */
export default function LandingPage() {
  const router = useRouter();
  const me = useMe();
  const target = me.data ? landingPath(me.data, readLastWorkspace()) : null;

  useEffect(() => {
    if (target) {
      router.replace(target);
    }
  }, [target, router]);

  if (!me.data || target) {
    return (
      <main className="mx-auto grid w-full max-w-xl gap-4 px-4 py-12">
        <Loading />
      </main>
    );
  }
  return (
    <AppShell workspace="No workspace" navigation={[]}>
      <StatusPage title="You aren't a member of any organisation yet">
        <p>When an organisation invites you, its workspace appears here.</p>
      </StatusPage>
    </AppShell>
  );
}
