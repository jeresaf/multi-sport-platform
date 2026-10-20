"use client";

// The signed-in landing page until the workspace shell arrives (S03-T6b2): who you are, your workspaces, sign out.
import { Badge } from "@msp/ui/components/badge";
import { Button } from "@msp/ui/components/button";
import { useRouter } from "next/navigation";
import { useEffect } from "react";
import { ProblemAlert } from "@/components/problem-alert";
import { useMe, useSignOut } from "@/lib/account";
import { isUnauthenticated } from "@/lib/api";
import { humanizeCode } from "@/lib/codes";
import { signInPath } from "@/lib/return-to";

export function Home() {
  const router = useRouter();
  const me = useMe();
  const signOut = useSignOut();
  const signedOut = me.isError && isUnauthenticated(me.error);

  useEffect(() => {
    if (signedOut) {
      router.replace(signInPath("/"));
    }
  }, [signedOut, router]);

  return (
    <main className="mx-auto flex min-h-screen w-full max-w-3xl flex-col gap-6 px-4 py-12">
      <h1 className="text-3xl font-semibold">MSP Admin</h1>
      {me.isPending || signedOut ? <p className="text-muted-foreground">Loading…</p> : null}
      {me.isError && !signedOut ? <ProblemAlert error={me.error} /> : null}
      {me.data ? (
        <>
          <div className="flex flex-wrap items-center justify-between gap-4">
            <p>
              Signed in as <strong>{me.data.email}</strong>
              {me.data.platformRoles.length > 0
                ? ` (${me.data.platformRoles.map(humanizeCode).join(", ")})`
                : null}
            </p>
            <Button
              variant="outline"
              disabled={signOut.isPending}
              onClick={() => signOut.mutate(undefined, { onSuccess: () => router.replace("/sign-in") })}
            >
              Sign out
            </Button>
          </div>
          {signOut.isError ? <ProblemAlert error={signOut.error} title="Couldn't sign you out" /> : null}
          <section className="grid gap-3" aria-labelledby="workspaces">
            <h2 id="workspaces" className="text-xl font-semibold">
              Your workspaces
            </h2>
            {me.data.tenants.length === 0 ? (
              <p className="text-muted-foreground">You aren&apos;t a member of any organisation yet.</p>
            ) : (
              <ul className="grid gap-2">
                {me.data.tenants.map((tenant) => (
                  <li key={tenant.tenantId} className="flex flex-wrap items-center gap-2 rounded-md border p-3">
                    <span className="font-medium">{tenant.displayName}</span>
                    {tenant.status === "ACTIVE" ? null : <Badge variant="outline">{humanizeCode(tenant.status)}</Badge>}
                    <span className="text-sm text-muted-foreground">{tenant.roles.map(humanizeCode).join(", ")}</span>
                  </li>
                ))}
              </ul>
            )}
          </section>
        </>
      ) : null}
    </main>
  );
}
