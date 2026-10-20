"use client";

import { useMe } from "@/lib/account";
import { humanizeCode } from "@/lib/codes";

/** The platform console's first page: who is working in it, with which platform roles. */
export default function PlatformOverviewPage() {
  const me = useMe();

  return (
    <section className="grid gap-4">
      <h1 className="text-2xl font-semibold">Platform console</h1>
      <p className="text-muted-foreground">Your roles: {me.data?.platformRoles.map(humanizeCode).join(", ")}</p>
    </section>
  );
}
