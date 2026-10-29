"use client";

import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from "@msp/ui/components/table";
import { EmptyState } from "@/components/empty-state";
import { Loading } from "@/components/loading";
import { ProblemAlert } from "@/components/problem-alert";
import { ForbiddenView } from "@/components/status-page";
import { formatEntitlementValue } from "@/lib/entitlements";
import { usePlatformCan } from "@/lib/platform-workspace";
import { usePlans } from "./plans-data";

/**
 * Platform console → Plans (admin-app.md, S04 D19): every entitlement against every active plan, read-only. A plan
 * changes by a reviewed seed, not here (D2).
 */
export default function PlansPage() {
  const can = usePlatformCan();
  const plans = usePlans(can("plan.read"));

  if (!can("plan.read")) {
    return <ForbiddenView />;
  }

  const list = plans.data?.items ?? [];
  // Every plan lists every declared entitlement in the same order (by code): the first plan's give the rows.
  const rows = list[0]?.entitlements ?? [];
  return (
    <section className="grid gap-6">
      <header className="grid gap-1">
        <h1 className="text-2xl font-semibold">Plans</h1>
        <p className="text-muted-foreground">
          What each plan includes. Plans change through a reviewed update, not here; a tenant&apos;s own exceptions are
          overrides on its subscription.
        </p>
      </header>

      {plans.isPending ? <Loading /> : null}
      {plans.isError ? <ProblemAlert error={plans.error} /> : null}
      {plans.isSuccess && list.length === 0 ? (
        <EmptyState title="No plans yet">
          Plans are written by the configuration seed (<code>seed --category CONFIGURATION</code>).
        </EmptyState>
      ) : null}
      {rows.length > 0 ? (
        <Table>
          <TableHeader>
            <TableRow>
              <TableHead>Entitlement</TableHead>
              {list.map((plan) => (
                <TableHead key={plan.code} scope="col">
                  {plan.name}
                </TableHead>
              ))}
            </TableRow>
          </TableHeader>
          <TableBody>
            {rows.map((row) => (
              <TableRow key={row.code}>
                <TableHead scope="row" className="h-auto py-2 align-top font-normal whitespace-normal">
                  <p className="font-medium">{row.name}</p>
                  <p className="text-sm text-muted-foreground">{row.description}</p>
                  <p className="text-sm text-muted-foreground">Enforced by {row.enforcedBy}</p>
                </TableHead>
                {list.map((plan) => {
                  const value = plan.entitlements.find((entitlement) => entitlement.code === row.code)?.value;
                  return (
                    <TableCell key={plan.code} className="align-top whitespace-normal">
                      {/* Declared in code but not written by the seed yet: never shown as unlimited or none. */}
                      {value ? formatEntitlementValue(value, row.unit) : "Not seeded"}
                    </TableCell>
                  );
                })}
              </TableRow>
            ))}
          </TableBody>
        </Table>
      ) : null}
    </section>
  );
}
