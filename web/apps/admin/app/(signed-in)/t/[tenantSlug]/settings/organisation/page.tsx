"use client";

import { Loading } from "@/components/loading";
import { ProblemAlert } from "@/components/problem-alert";
import { ForbiddenView } from "@/components/status-page";
import { useTenantSettings } from "@/lib/tenant-settings";
import { useTenantWorkspace } from "@/lib/tenant-workspace";
import { OrganisationForm } from "./organisation-form";

/** Tenant settings → Organisation (admin-app.md): the tenant's settings, editable with `tenant.settings.update`. */
export default function OrganisationPage() {
  const { tenant, can } = useTenantWorkspace();
  const settings = useTenantSettings();

  if (!can("tenant.settings.read")) {
    return <ForbiddenView />;
  }

  return (
    <section className="grid max-w-2xl gap-6">
      <header className="grid gap-1">
        <h1 className="text-2xl font-semibold">Organisation</h1>
        <p className="text-muted-foreground">
          {tenant.displayName}. Its name is changed by the platform team; ask them if it needs to change.
        </p>
      </header>
      {settings.isPending ? <Loading /> : null}
      {settings.isError ? <ProblemAlert error={settings.error} /> : null}
      {settings.data ? (
        <OrganisationForm
          settings={settings.data}
          editable={can("tenant.settings.update")}
          onReload={() => void settings.refetch()}
        />
      ) : null}
    </section>
  );
}
