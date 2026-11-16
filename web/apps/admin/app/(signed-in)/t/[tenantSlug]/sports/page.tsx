"use client";

import { EmptyState } from "@/components/empty-state";
import { Loading } from "@/components/loading";
import { ProblemAlert } from "@/components/problem-alert";
import { SportsTable } from "@/components/sports/sports-table";
import { ForbiddenView } from "@/components/status-page";
import { useTenantWorkspace } from "@/lib/tenant-workspace";
import { tenantPath } from "@/lib/workspace";
import { useSportVariants } from "./sports-data";

/**
 * Tenant workspace → Sports (S05 D17): every published sport variant, each opening its definition. Read-only: which
 * sports the tenant uses is Settings → Sports.
 */
export default function SportsPage() {
  const { tenant, can } = useTenantWorkspace();
  const variants = useSportVariants();

  if (!can("sport.read")) {
    return <ForbiddenView />;
  }

  const items = variants.data?.items ?? [];
  return (
    <section className="grid gap-6">
      <header className="grid gap-1">
        <h1 className="text-2xl font-semibold">Sports</h1>
        <p className="text-muted-foreground">
          The sports the platform describes: their periods, events, roles, statistics and scorer pad. Read-only.
        </p>
      </header>

      {variants.isPending ? <Loading /> : null}
      {variants.isError ? <ProblemAlert error={variants.error} /> : null}
      {variants.isSuccess && items.length === 0 ? (
        <EmptyState title="No sports yet">No sports are published yet. Ask the platform team.</EmptyState>
      ) : null}
      {items.length > 0 ? (
        <SportsTable items={items} href={(variant) => `${tenantPath(tenant.slug)}/sports/${encodeURIComponent(variant)}`} />
      ) : null}
    </section>
  );
}
