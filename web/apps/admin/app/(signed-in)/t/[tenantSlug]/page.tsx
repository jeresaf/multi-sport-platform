"use client";

import { Badge } from "@msp/ui/components/badge";
import { useParams } from "next/navigation";
import { useMe } from "@/lib/account";
import { humanizeCode } from "@/lib/codes";
import { findTenant } from "@/lib/workspace";

/** The tenant workspace's first page: the organisation and the user's roles in it. */
export default function TenantOverviewPage() {
  const { tenantSlug } = useParams<{ tenantSlug: string }>();
  const me = useMe();
  const tenant = me.data ? findTenant(me.data, tenantSlug) : undefined;
  if (!tenant) {
    return null;
  }

  return (
    <section className="grid gap-4">
      <div className="flex flex-wrap items-center gap-3">
        <h1 className="text-2xl font-semibold">{tenant.displayName}</h1>
        {tenant.status === "ACTIVE" ? null : <Badge variant="outline">{humanizeCode(tenant.status)}</Badge>}
      </div>
      <p className="text-muted-foreground">
        Your roles: {tenant.roles.length > 0 ? tenant.roles.map(humanizeCode).join(", ") : "none"}
      </p>
    </section>
  );
}
