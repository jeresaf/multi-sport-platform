"use client";

import { useRouter } from "next/navigation";
import { useEffect, type ReactNode } from "react";
import { Loading } from "@/components/loading";
import { ProblemAlert } from "@/components/problem-alert";
import { useMe } from "@/lib/account";
import { isUnauthenticated } from "@/lib/api";
import { signInPath } from "@/lib/return-to";

/** Every signed-in page: without a session (401) the user signs in and comes back to the page they asked for. */
export default function SignedInLayout({ children }: Readonly<{ children: ReactNode }>) {
  const router = useRouter();
  const me = useMe();
  const signedOut = me.isError && isUnauthenticated(me.error);

  useEffect(() => {
    if (signedOut) {
      router.replace(signInPath(window.location.pathname + window.location.search));
    }
  }, [signedOut, router]);

  if (me.data) {
    return children;
  }
  return (
    <main className="mx-auto grid w-full max-w-xl gap-4 px-4 py-12">
      {me.isError && !signedOut ? <ProblemAlert error={me.error} /> : <Loading />}
    </main>
  );
}
