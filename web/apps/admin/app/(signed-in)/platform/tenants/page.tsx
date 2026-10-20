"use client";

import { Button } from "@msp/ui/components/button";
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from "@msp/ui/components/table";
import { Plus } from "lucide-react";
import Link from "next/link";
import { useState } from "react";
import { EmptyState } from "@/components/empty-state";
import { Loading } from "@/components/loading";
import { ProblemAlert } from "@/components/problem-alert";
import { SelectField } from "@/components/select-field";
import { StatusBadge } from "@/components/status-badge";
import { ForbiddenView } from "@/components/status-page";
import { humanizeCode } from "@/lib/codes";
import { usePlatformCan } from "@/lib/platform-workspace";
import { useCountries } from "@/lib/reference-data";
import { formatDate } from "@/lib/tenant-settings";
import { platformPath } from "@/lib/workspace";
import { CreateTenantDialog } from "./create-tenant-dialog";
import { useTenants, type TenantStatus } from "./tenants-data";

const statusFilters: readonly TenantStatus[] = ["PROVISIONING", "ACTIVE", "SUSPENDED", "ARCHIVED"];
const allStatuses = "ALL";

/** Columns shown from tablet width up. */
const wide = "hidden tablet:table-cell";

/** Platform console → Tenants (admin-app.md): every tenant by slug, with its status; new tenants start here. */
export default function TenantsPage() {
  const can = usePlatformCan();
  const [status, setStatus] = useState<TenantStatus | undefined>(undefined);
  const [creating, setCreating] = useState(false);
  const tenants = useTenants(status, can("tenant.read"));
  const countries = useCountries();
  const countryName = (code: string) => countries.data?.items.find((item) => item.code === code)?.name ?? code;

  if (!can("tenant.read")) {
    return <ForbiddenView />;
  }

  const rows = tenants.data?.pages.flatMap((page) => page.items) ?? [];
  return (
    <section className="grid gap-6">
      <header className="flex flex-wrap items-end justify-between gap-3">
        <div className="grid gap-1">
          <h1 className="text-2xl font-semibold">Tenants</h1>
          <p className="text-muted-foreground">The organisations that lease the platform, by slug.</p>
        </div>
        {can("tenant.create") ? (
          <Button onClick={() => setCreating(true)}>
            <Plus aria-hidden="true" />
            New tenant
          </Button>
        ) : null}
      </header>

      <div className="w-48">
        <SelectField
          id="tenant-status"
          label="Tenants to show"
          value={status ?? allStatuses}
          onChange={(event) =>
            setStatus(event.target.value === allStatuses ? undefined : (event.target.value as TenantStatus))
          }
        >
          <option value={allStatuses}>All</option>
          {statusFilters.map((value) => (
            <option key={value} value={value}>
              {humanizeCode(value)}
            </option>
          ))}
        </SelectField>
      </div>

      {tenants.isPending ? <Loading /> : null}
      {tenants.isError ? <ProblemAlert error={tenants.error} /> : null}
      {tenants.isSuccess && rows.length === 0 ? (
        <EmptyState title={status ? `No ${humanizeCode(status).toLowerCase()} tenants` : "No tenants yet"}>
          {status ? "Choose another status to see others." : "Create the first one with New tenant."}
        </EmptyState>
      ) : null}
      {rows.length > 0 ? (
        <Table>
          <TableHeader>
            <TableRow>
              <TableHead>Tenant</TableHead>
              <TableHead className={wide}>Country</TableHead>
              <TableHead>Status</TableHead>
              <TableHead className={wide}>Created (UTC)</TableHead>
            </TableRow>
          </TableHeader>
          <TableBody>
            {rows.map((tenant) => (
              <TableRow key={tenant.id}>
                <TableCell className="max-w-56 tablet:max-w-80">
                  <Link
                    href={`${platformPath}/tenants/${tenant.id}`}
                    className="block truncate font-medium text-primary underline-offset-4 hover:underline"
                  >
                    {tenant.displayName}
                  </Link>
                  <p className="truncate text-sm text-muted-foreground">{tenant.slug}</p>
                </TableCell>
                <TableCell className={wide}>{countryName(tenant.countryCode)}</TableCell>
                <TableCell>
                  <StatusBadge status={tenant.status} />
                </TableCell>
                <TableCell className={wide}>{formatDate(tenant.createdAt, "UTC")}</TableCell>
              </TableRow>
            ))}
          </TableBody>
        </Table>
      ) : null}
      {tenants.hasNextPage ? (
        <Button
          variant="outline"
          className="justify-self-start"
          disabled={tenants.isFetchingNextPage}
          onClick={() => void tenants.fetchNextPage()}
        >
          {tenants.isFetchingNextPage ? "Loading…" : "Show more"}
        </Button>
      ) : null}

      {creating ? <CreateTenantDialog onOpenChange={setCreating} /> : null}
    </section>
  );
}
