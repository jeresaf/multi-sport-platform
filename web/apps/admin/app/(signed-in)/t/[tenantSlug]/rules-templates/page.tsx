"use client";

import { EmptyState } from "@/components/empty-state";
import { Loading } from "@/components/loading";
import { ProblemAlert } from "@/components/problem-alert";
import { RulesTemplatesTable } from "@/components/rulesets/rules-templates-table";
import { ForbiddenView } from "@/components/status-page";
import { namesByCode } from "@/lib/rulesets";
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
        <RulesTemplatesTable
          items={items}
          sportNames={sportNames}
          formatNames={formatNames}
          href={(code) => `${tenantPath(tenant.slug)}/rules-templates/${encodeURIComponent(code)}`}
        />
      ) : null}
    </section>
  );
}
