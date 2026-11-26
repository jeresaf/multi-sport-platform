"use client";

import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from "@msp/ui/components/table";
import Link from "next/link";
import { Loading } from "@/components/loading";
import { ProblemAlert } from "@/components/problem-alert";
import { StatusHistory } from "@/components/registry/status-history";
import { RowName, ViewerSection, wide } from "@/components/viewer/facts";
import { formatDay } from "@/lib/entitlements";
import { useTenantWorkspace } from "@/lib/tenant-workspace";
import { tenantPath } from "@/lib/workspace";
import {
  useOrganisationParentLinks,
  useOrganisationStatusChanges,
  type Organisation,
  type OrganisationParentLink,
} from "../organisations-data";

/** A link's dates: "From 1 Jan 2024" while open, "1 Jan 2024 to 3 Mar 2026" once ended. */
export function linkPeriod(link: Pick<OrganisationParentLink, "validFrom" | "validTo">): string {
  return link.validTo ? `${formatDay(link.validFrom)} to ${formatDay(link.validTo)}` : `From ${formatDay(link.validFrom)}`;
}

/**
 * History (S07 D23): the status changes, newest first (as people's and venues'), then the parent links (D5) with their
 * dates and reasons, the current one first. A parent removed without a new one has its reason in the audit trail only.
 */
export function HistoryTab({ organisation }: Readonly<{ organisation: Organisation }>) {
  const { tenant } = useTenantWorkspace();
  const links = useOrganisationParentLinks(organisation.id);
  const items = links.data?.items ?? [];
  const parentHref = (id: string) => `${tenantPath(tenant.slug)}/organisations/${id}`;
  return (
    <>
      <ViewerSection title="Status">
        <StatusHistory history={useOrganisationStatusChanges(organisation.id)} />
      </ViewerSection>
      <ViewerSection title="Parent organisations">
        {links.isPending ? <Loading /> : null}
        {links.isError ? <ProblemAlert error={links.error} /> : null}
        {links.isSuccess && items.length === 0 ? (
          <p className="text-muted-foreground">{organisation.displayName} has never had a parent organisation.</p>
        ) : null}
        {items.length > 0 ? (
          <Table>
            <TableHeader>
              <TableRow>
                <TableHead>Parent</TableHead>
                <TableHead className={wide}>When</TableHead>
                <TableHead className={wide}>Reason</TableHead>
              </TableRow>
            </TableHeader>
            <TableBody>
              {items.map((link) => {
                const period = linkPeriod(link);
                return (
                  <TableRow key={link.id}>
                    <TableHead scope="row" className="h-auto py-2 align-top whitespace-normal">
                      <RowName
                        name={
                          <Link
                            href={parentHref(link.parent.id)}
                            className="break-words text-primary underline-offset-4 hover:underline"
                          >
                            {link.parent.displayName}
                          </Link>
                        }
                        details={[
                          ["When", period],
                          ["Reason", link.reason],
                        ]}
                      />
                    </TableHead>
                    <TableCell className={wide}>{period}</TableCell>
                    <TableCell className={`${wide} max-w-96 break-words whitespace-normal`}>{link.reason}</TableCell>
                  </TableRow>
                );
              })}
            </TableBody>
          </Table>
        ) : null}
      </ViewerSection>
    </>
  );
}
