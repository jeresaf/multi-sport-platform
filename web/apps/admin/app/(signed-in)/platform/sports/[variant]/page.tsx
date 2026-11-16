"use client";

import { useParams, usePathname, useRouter, useSearchParams } from "next/navigation";
import { Loading } from "@/components/loading";
import { ProblemAlert } from "@/components/problem-alert";
import { SportViewer, sportTab, type SportTab } from "@/components/sports/sport-viewer";
import { ForbiddenView, NotFoundView } from "@/components/status-page";
import { BackLink } from "@/components/viewer/facts";
import { isApiProblem } from "@/lib/api";
import { usePlatformCan } from "@/lib/platform-workspace";
import { versionParam } from "@/lib/version-param";
import { platformPath } from "@/lib/workspace";
import { usePlatformSportDefinition } from "../sports-data";

/**
 * Platform console → Sports → a variant (S05 D17): the workspace's viewer over the platform read. The tab is in the
 * address (`?tab=`); `?version=` opens an older version. Dates are in UTC: the console has no time zone.
 */
export default function PlatformSportVariantPage() {
  const { variant } = useParams<{ variant: string }>();
  const searchParams = useSearchParams();
  const pathname = usePathname();
  const router = useRouter();
  const can = usePlatformCan();
  const version = versionParam(searchParams.get("version"));
  const definition = usePlatformSportDefinition(variant, version ?? undefined, version !== null);

  if (!can("platform.sport.read")) {
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
  const back = <BackLink href={`${platformPath}/sports`}>Sports</BackLink>;

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
      timeZone="UTC"
      back={back}
    />
  );
}
