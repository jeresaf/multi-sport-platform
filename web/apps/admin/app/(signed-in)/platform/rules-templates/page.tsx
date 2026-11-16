"use client";

import { EmptyState } from "@/components/empty-state";
import { Loading } from "@/components/loading";
import { ProblemAlert } from "@/components/problem-alert";
import { RulesTemplatesTable } from "@/components/rulesets/rules-templates-table";
import { ForbiddenView } from "@/components/status-page";
import { usePlatformCan } from "@/lib/platform-workspace";
import { namesByCode } from "@/lib/rulesets";
import { platformPath } from "@/lib/workspace";
import { usePlatformFormats } from "../formats/formats-data";
import { usePlatformSportVariants } from "../sports/sports-data";
import { usePlatformRulesetTemplates } from "./rules-templates-data";

/**
 * Platform console → Rules templates (S05 D17): every published ruleset template, each opening its parameters and
 * rules in the workspace's view. Read-only: a template changes only by publishing a new version (a reviewed seed).
 */
export default function PlatformRulesTemplatesPage() {
  const can = usePlatformCan();
  const templates = usePlatformRulesetTemplates();
  const sportNames = namesByCode(usePlatformSportVariants().data?.items, (item) => item.variant, (item) => item.name);
  const formatNames = namesByCode(usePlatformFormats().data?.items, (item) => item.code, (item) => item.name);

  if (!can("platform.sport.read")) {
    return <ForbiddenView />;
  }

  const items = templates.data?.items ?? [];
  return (
    <section className="grid gap-6">
      <header className="grid gap-1">
        <h1 className="text-2xl font-semibold">Rules templates</h1>
        <p className="text-muted-foreground">
          A sport&apos;s rules for a kind of competition, and the parameters a competition may change. Tenants see the
          same pages. Read-only.
        </p>
      </header>

      {templates.isPending ? <Loading /> : null}
      {templates.isError ? <ProblemAlert error={templates.error} /> : null}
      {templates.isSuccess && items.length === 0 ? (
        <EmptyState title="No rules templates yet">
          No rules templates are published yet. They are published by the configuration seed.
        </EmptyState>
      ) : null}
      {items.length > 0 ? (
        <RulesTemplatesTable
          items={items}
          sportNames={sportNames}
          formatNames={formatNames}
          href={(code) => `${platformPath}/rules-templates/${encodeURIComponent(code)}`}
        />
      ) : null}
    </section>
  );
}
