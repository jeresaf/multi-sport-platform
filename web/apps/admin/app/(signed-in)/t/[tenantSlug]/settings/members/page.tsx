"use client";

import { ForbiddenView } from "@/components/status-page";
import { useTenantWorkspace } from "@/lib/tenant-workspace";
import { InvitationsSection } from "./invitations-section";
import { ownerRole } from "./members-data";
import { MembersSection } from "./members-section";

/** Tenant settings → Members & roles (admin-app.md): members, their tenant-wide roles, and invitations. */
export default function MembersPage() {
  const { tenant, can } = useTenantWorkspace();
  if (!can("membership.read")) {
    return <ForbiddenView />;
  }

  const isOwner = tenant.roles.includes(ownerRole);
  return (
    <div className="grid gap-10">
      <header className="grid gap-1">
        <h1 className="text-2xl font-semibold">Members &amp; roles</h1>
        <p className="text-muted-foreground">Who can work in {tenant.displayName}, and what they can do.</p>
      </header>
      <MembersSection isOwner={isOwner} />
      <InvitationsSection isOwner={isOwner} />
    </div>
  );
}
