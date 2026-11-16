"use client";

import { useParams, useSearchParams } from "next/navigation";
import { Loading } from "@/components/loading";
import { ProblemAlert } from "@/components/problem-alert";
import { RulesetTemplateView } from "@/components/rulesets/ruleset-template-view";
import { ForbiddenView, NotFoundView } from "@/components/status-page";
import { BackLink } from "@/components/viewer/facts";
import { isApiProblem } from "@/lib/api";
import { namesByCode } from "@/lib/rulesets";
import { useTenantTimeZone } from "@/lib/tenant-settings";
import { useTenantWorkspace } from "@/lib/tenant-workspace";
import { versionParam } from "@/lib/version-param";
import { tenantPath } from "@/lib/workspace";
import { useFormats } from "../../formats/formats-data";
import { useSportDefinition } from "../../sports/sports-data";
import { useRulesetTemplate } from "../rules-templates-data";

/**
 * Tenant workspace → Rules templates → a template (S05 D17); `?version=` opens an older version. The pinned sport
 * definition version gives the period, position and official labels; without it (no `sport.read`, or its read failed)
 * the page still shows every rule, with those codes written as words.
 */
export default function RulesTemplatePage() {
  const { code } = useParams<{ code: string }>();
  const searchParams = useSearchParams();
  const { tenant, can } = useTenantWorkspace();
  const timeZone = useTenantTimeZone();
  const version = versionParam(searchParams.get("version"));
  const template = useRulesetTemplate(code, version ?? undefined, version !== null);
  const sport = template.data?.sport;
  const definition = useSportDefinition(sport?.variant ?? "", sport?.version, sport !== undefined);
  const formatNames = namesByCode(useFormats().data?.items, (item) => item.code, (item) => item.name);

  if (!can("ruleset.read")) {
    return <ForbiddenView />;
  }
  // No such template or version, or a `?version=` that isn't one (never asked: it can't exist).
  if (version === null || isApiProblem(template.error, "RULESET_TEMPLATE_NOT_FOUND")) {
    return <NotFoundView />;
  }

  const root = tenantPath(tenant.slug);
  const back = <BackLink href={`${root}/rules-templates`}>Rules templates</BackLink>;

  // The definition is waited for only while it is being read, so its labels don't change under the reader.
  if (template.isPending || template.isError || definition.fetchStatus === "fetching") {
    return (
      <section className="grid gap-4">
        {back}
        {template.isError ? <ProblemAlert error={template.error} /> : <Loading />}
      </section>
    );
  }
  const { variant, version: sportVersion } = template.data.sport;
  return (
    <RulesetTemplateView
      template={template.data}
      definition={definition.data}
      formatNames={formatNames}
      sportHref={can("sport.read") ? `${root}/sports/${encodeURIComponent(variant)}?version=${sportVersion}` : undefined}
      formatHref={can("format.read") ? (format) => `${root}/formats/${encodeURIComponent(format)}` : undefined}
      timeZone={timeZone}
      back={back}
    />
  );
}
