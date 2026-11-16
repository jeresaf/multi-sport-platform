"use client";

import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from "@msp/ui/components/table";
import Link from "next/link";
import { EmptyState } from "@/components/empty-state";
import { Loading } from "@/components/loading";
import { ProblemAlert } from "@/components/problem-alert";
import { ForbiddenView } from "@/components/status-page";
import { RowName, orNone, wide } from "@/components/viewer/facts";
import { namesByCode } from "@/lib/rulesets";
import { codeLabel } from "@/lib/sport-definition";
import { useTenantWorkspace } from "@/lib/tenant-workspace";
import { tenantPath } from "@/lib/workspace";
import { useFormats } from "../formats/formats-data";
import { useSportVariants } from "../sports/sports-data";
import { useRulesetTemplates } from "./rules-templates-data";

/**
 * Tenant workspace → Rules templates (S05 D17): every published ruleset template (a sport's rules for a kind of
 * competition), each opening its parameters and rules. Read-only: a competition picks a template and changes its
 * parameters when it is set up (S09). The sport and format names come from their lists when the user may read them,
 * else the codes are written as words.
 */
export default function RulesTemplatesPage() {
  const { tenant, can } = useTenantWorkspace();
  const templates = useRulesetTemplates();
  const sportNames = namesByCode(useSportVariants().data?.items, (item) => item.variant, (item) => item.name);
  const formatNames = namesByCode(useFormats().data?.items, (item) => item.code, (item) => item.name);

  if (!can("ruleset.read")) {
    return <ForbiddenView />;
  }

  const items = templates.data?.items ?? [];
  const sportOf = (variant: string) => sportNames[variant] ?? codeLabel(variant);
  const formatsOf = (codes: readonly string[]) => orNone(codes.map((code) => formatNames[code] ?? codeLabel(code)));
  return (
    <section className="grid gap-6">
      <header className="grid gap-1">
        <h1 className="text-2xl font-semibold">Rules templates</h1>
        <p className="text-muted-foreground">
          A sport&apos;s rules for a kind of competition, and the parameters a competition may change. Read-only.
        </p>
      </header>

      {templates.isPending ? <Loading /> : null}
      {templates.isError ? <ProblemAlert error={templates.error} /> : null}
      {templates.isSuccess && items.length === 0 ? (
        <EmptyState title="No rules templates yet">No rules templates are published yet. Ask the platform team.</EmptyState>
      ) : null}
      {items.length > 0 ? (
        <Table>
          <TableHeader>
            <TableRow>
              <TableHead scope="col">Name</TableHead>
              <TableHead scope="col" className={wide}>
                Sport
              </TableHead>
              <TableHead scope="col" className={wide}>
                Formats
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
                        href={`${tenantPath(tenant.slug)}/rules-templates/${encodeURIComponent(item.code)}`}
                        className="inline-flex min-h-11 items-center text-primary underline-offset-4 hover:underline"
                      >
                        {item.name}
                      </Link>
                    }
                    details={[
                      ["Sport", sportOf(item.sport.variant)],
                      ["Formats", formatsOf(item.formats)],
                      ["Latest version", item.version],
                    ]}
                  />
                  {item.description ? (
                    <p className="mt-1 text-sm font-normal text-muted-foreground">{item.description}</p>
                  ) : null}
                </TableHead>
                <TableCell className={`${wide} align-top`}>{sportOf(item.sport.variant)}</TableCell>
                <TableCell className={`${wide} align-top whitespace-normal`}>{formatsOf(item.formats)}</TableCell>
                <TableCell className={`${wide} align-top`}>{item.version}</TableCell>
              </TableRow>
            ))}
          </TableBody>
        </Table>
      ) : null}
    </section>
  );
}
