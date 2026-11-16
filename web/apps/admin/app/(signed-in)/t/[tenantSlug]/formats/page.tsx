"use client";

import { EmptyState } from "@/components/empty-state";
import { FormatsTable } from "@/components/formats/formats-table";
import { Loading } from "@/components/loading";
import { ProblemAlert } from "@/components/problem-alert";
import { ForbiddenView } from "@/components/status-page";
import { useTenantWorkspace } from "@/lib/tenant-workspace";
import { tenantPath } from "@/lib/workspace";
import { useFormats } from "./formats-data";

/**
 * Tenant workspace → Formats (S05 D17): every published format (how a competition is organised), each opening its
 * stages and parameters. Read-only: a competition chooses its format and parameters when it is set up.
 */
export default function FormatsPage() {
  const { tenant, can } = useTenantWorkspace();
  const formats = useFormats();

  if (!can("format.read")) {
    return <ForbiddenView />;
  }

  const items = formats.data?.items ?? [];
  return (
    <section className="grid gap-6">
      <header className="grid gap-1">
        <h1 className="text-2xl font-semibold">Formats</h1>
        <p className="text-muted-foreground">
          How a competition can be organised: its stages and the parameters it chooses. Read-only.
        </p>
      </header>

      {formats.isPending ? <Loading /> : null}
      {formats.isError ? <ProblemAlert error={formats.error} /> : null}
      {formats.isSuccess && items.length === 0 ? (
        <EmptyState title="No formats yet">No formats are published yet. Ask the platform team.</EmptyState>
      ) : null}
      {items.length > 0 ? (
        <FormatsTable items={items} href={(code) => `${tenantPath(tenant.slug)}/formats/${encodeURIComponent(code)}`} />
      ) : null}
    </section>
  );
}
