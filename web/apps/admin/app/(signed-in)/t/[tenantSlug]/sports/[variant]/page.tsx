"use client";

import { useParams, usePathname, useRouter, useSearchParams } from "next/navigation";
import { Loading } from "@/components/loading";
import { ProblemAlert } from "@/components/problem-alert";
import { SportViewer, sportTab, type SportTab } from "@/components/sports/sport-viewer";
import { ForbiddenView, NotFoundView } from "@/components/status-page";
import { BackLink } from "@/components/viewer/facts";
import { isApiProblem } from "@/lib/api";
import { useTenantTimeZone } from "@/lib/tenant-settings";
import { useTenantWorkspace } from "@/lib/tenant-workspace";
import { versionParam } from "@/lib/version-param";
import { tenantPath } from "@/lib/workspace";
import { useSportDefinition } from "../sports-data";

/**
 * Tenant workspace → Sports → a variant (S05 D17): its definition in tabs. The tab is in the address (`?tab=`) so it
 * can be linked; `?version=` opens an older version.
 */
export default function SportVariantPage() {
  const { variant } = useParams<{ variant: string }>();
  const searchParams = useSearchParams();
  const pathname = usePathname();
  const router = useRouter();
  const { tenant, can } = useTenantWorkspace();
  const timeZone = useTenantTimeZone();
  const version = versionParam(searchParams.get("version"));
  const definition = useSportDefinition(variant, version ?? undefined, version !== null);

  if (!can("sport.read")) {
    return <ForbiddenView />;
  }
  // No such variant or version, or a `?version=` that isn't one (never asked: it can't exist).
  if (version === null || isApiProblem(definition.error, "SPORT_DEFINITION_NOT_FOUND")) {
    return <NotFoundView />;
  }

  const changeTab = (tab: SportTab) => {
    const next = new URLSearchParams(searchParams.toString());
    next.set("tab", tab);
    router.replace(`${pathname}?${next.toString()}`, { scroll: false });
  };
  const back = <BackLink href={`${tenantPath(tenant.slug)}/sports`}>Sports</BackLink>;

  if (definition.isPending || definition.isError) {
    return (
      <section className="grid gap-4">
        {back}
        {definition.isPending ? <Loading /> : <ProblemAlert error={definition.error} />}
      </section>
    );
  }
  return (
    <SportViewer
      definition={definition.data}
      tab={sportTab(searchParams.get("tab"))}
      onTabChange={changeTab}
      timeZone={timeZone}
      back={back}
    />
  );
}
