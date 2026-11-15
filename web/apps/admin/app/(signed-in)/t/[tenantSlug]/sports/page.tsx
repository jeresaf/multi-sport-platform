"use client";

import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from "@msp/ui/components/table";
import Link from "next/link";
import { EmptyState } from "@/components/empty-state";
import { Loading } from "@/components/loading";
import { ProblemAlert } from "@/components/problem-alert";
import { ForbiddenView } from "@/components/status-page";
import { codeLabel } from "@/lib/sport-definition";
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
        <EmptyState title="No sports yet">
          Sports are published by the configuration seed (<code>seed --category CONFIGURATION</code>).
        </EmptyState>
      ) : null}
      {items.length > 0 ? (
        <Table>
          <TableHeader>
            <TableRow>
              <TableHead scope="col">Name</TableHead>
              <TableHead scope="col">Sport</TableHead>
              <TableHead scope="col">Scoring family</TableHead>
              <TableHead scope="col">Shape</TableHead>
              <TableHead scope="col">Latest version</TableHead>
            </TableRow>
          </TableHeader>
          <TableBody>
            {items.map((item) => (
              <TableRow key={item.variant}>
                <TableHead scope="row" className="font-medium">
                  <Link
                    href={`${tenantPath(tenant.slug)}/sports/${encodeURIComponent(item.variant)}`}
                    className="inline-flex min-h-11 items-center text-primary underline-offset-4 hover:underline"
                  >
                    {item.name}
                  </Link>
                </TableHead>
                <TableCell>{codeLabel(item.sport)}</TableCell>
                <TableCell>{codeLabel(item.scoringFamily)}</TableCell>
                <TableCell>{codeLabel(item.shape)}</TableCell>
                <TableCell>{item.latestVersion}</TableCell>
              </TableRow>
            ))}
          </TableBody>
        </Table>
      ) : null}
    </section>
  );
}
