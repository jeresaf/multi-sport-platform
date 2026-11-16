"use client";

import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from "@msp/ui/components/table";
import Link from "next/link";
import { EmptyState } from "@/components/empty-state";
import { participantsLabel, shapesLabel } from "@/components/formats/format-view";
import { Loading } from "@/components/loading";
import { ProblemAlert } from "@/components/problem-alert";
import { RowName, wide } from "@/components/sports/facts";
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
        <Table>
          <TableHeader>
            <TableRow>
              <TableHead scope="col">Name</TableHead>
              <TableHead scope="col" className={wide}>
                Participants
              </TableHead>
              <TableHead scope="col" className={wide}>
                Contest shapes
              </TableHead>
              <TableHead scope="col" className={wide}>
                Latest version
              </TableHead>
            </TableRow>
          </TableHeader>
          <TableBody>
            {items.map((item) => (
              <TableRow key={item.code}>
                <TableHead scope="row" className="h-auto py-2 align-top whitespace-normal">
                  <RowName
                    name={
                      <Link
                        href={`${tenantPath(tenant.slug)}/formats/${encodeURIComponent(item.code)}`}
                        className="inline-flex min-h-11 items-center text-primary underline-offset-4 hover:underline"
                      >
                        {item.name}
                      </Link>
                    }
                    details={[
                      ["Participants", participantsLabel(item)],
                      ["Contest shapes", shapesLabel(item)],
                      ["Latest version", item.version],
                    ]}
                  />
                  {item.description ? (
                    <p className="mt-1 text-sm font-normal text-muted-foreground">{item.description}</p>
                  ) : null}
                </TableHead>
                <TableCell className={`${wide} align-top`}>{participantsLabel(item)}</TableCell>
                <TableCell className={`${wide} align-top`}>{shapesLabel(item)}</TableCell>
                <TableCell className={`${wide} align-top`}>{item.version}</TableCell>
              </TableRow>
            ))}
          </TableBody>
        </Table>
      ) : null}
    </section>
  );
}
