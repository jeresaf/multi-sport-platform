"use client";

import { ArrowLeft } from "lucide-react";
import Link from "next/link";
import { useParams, usePathname, useRouter, useSearchParams } from "next/navigation";
import { Loading } from "@/components/loading";
import { ProblemAlert } from "@/components/problem-alert";
import { SportViewer, sportTab, type SportTab } from "@/components/sports/sport-viewer";
import { ForbiddenView, NotFoundView } from "@/components/status-page";
import { isApiProblem } from "@/lib/api";
import { useTenantTimeZone } from "@/lib/tenant-settings";
import { useTenantWorkspace } from "@/lib/tenant-workspace";
import { tenantPath } from "@/lib/workspace";
import { useSportDefinition } from "../sports-data";

/** `?version=` as a version number; anything else means the latest. */
function versionParam(value: string | null): number | undefined {
  return value && /^[1-9][0-9]{0,8}$/.test(value) ? Number(value) : undefined;
}

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
  const definition = useSportDefinition(variant, version);

  if (!can("sport.read")) {
    return <ForbiddenView />;
  }
  if (isApiProblem(definition.error, "SPORT_DEFINITION_NOT_FOUND")) {
    return <NotFoundView />;
  }

  const changeTab = (tab: SportTab) => {
    const next = new URLSearchParams(searchParams.toString());
    next.set("tab", tab);
    router.replace(`${pathname}?${next.toString()}`, { scroll: false });
  };
  const back = (
    <Link
      href={`${tenantPath(tenant.slug)}/sports`}
      className="inline-flex min-h-11 w-fit items-center gap-1 text-sm text-primary underline-offset-4 hover:underline"
    >
      <ArrowLeft aria-hidden className="size-4" />
      Sports
    </Link>
  );

  if (definition.isPending) return <Loading />;
  if (definition.isError) return <ProblemAlert error={definition.error} />;
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
