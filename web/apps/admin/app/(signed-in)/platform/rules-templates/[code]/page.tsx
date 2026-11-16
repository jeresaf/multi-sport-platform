"use client";

import { useParams, useSearchParams } from "next/navigation";
import { Loading } from "@/components/loading";
import { ProblemAlert } from "@/components/problem-alert";
import { RulesetTemplateView } from "@/components/rulesets/ruleset-template-view";
import { ForbiddenView, NotFoundView } from "@/components/status-page";
import { BackLink } from "@/components/viewer/facts";
import { isApiProblem } from "@/lib/api";
import { usePlatformCan } from "@/lib/platform-workspace";
import { namesByCode } from "@/lib/rulesets";
import { versionParam } from "@/lib/version-param";
import { platformPath } from "@/lib/workspace";
import { usePlatformFormats } from "../../formats/formats-data";
import { usePlatformSportDefinition } from "../../sports/sports-data";
import { usePlatformRulesetTemplate } from "../rules-templates-data";

/**
 * Platform console → Rules templates → a template (S05 D17); `?version=` opens an older version. The pinned sport
 * definition version gives the period, position and official labels; if its read fails the page still shows every
 * rule, with those codes written as words. Dates are in UTC.
 */
export default function PlatformRulesTemplatePage() {
  const { code } = useParams<{ code: string }>();
  const searchParams = useSearchParams();
  const can = usePlatformCan();
  const version = versionParam(searchParams.get("version"));
  const template = usePlatformRulesetTemplate(code, version ?? undefined, version !== null);
  const sport = template.data?.sport;
  const definition = usePlatformSportDefinition(sport?.variant ?? "", sport?.version, sport !== undefined);
  const formatNames = namesByCode(usePlatformFormats().data?.items, (item) => item.code, (item) => item.name);

  if (!can("platform.sport.read")) {
    return <ForbiddenView />;
  }
  // No such template or version, or a `?version=` that isn't one (never asked: it can't exist).
  if (version === null || isApiProblem(template.error, "RULESET_TEMPLATE_NOT_FOUND")) {
    return <NotFoundView />;
  }

  const back = <BackLink href={`${platformPath}/rules-templates`}>Rules templates</BackLink>;
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
      sportHref={`${platformPath}/sports/${encodeURIComponent(variant)}?version=${sportVersion}`}
      formatHref={(format) => `${platformPath}/formats/${encodeURIComponent(format)}`}
      timeZone="UTC"
      back={back}
    />
  );
}
