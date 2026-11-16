"use client";

import { useParams, useSearchParams } from "next/navigation";
import { FormatView } from "@/components/formats/format-view";
import { Loading } from "@/components/loading";
import { ProblemAlert } from "@/components/problem-alert";
import { ForbiddenView, NotFoundView } from "@/components/status-page";
import { BackLink } from "@/components/viewer/facts";
import { isApiProblem } from "@/lib/api";
import { useTenantTimeZoneOnceRead } from "@/lib/tenant-settings";
import { useTenantWorkspace } from "@/lib/tenant-workspace";
import { versionParam } from "@/lib/version-param";
import { tenantPath } from "@/lib/workspace";
import { useFormat } from "../formats-data";

/** Tenant workspace → Formats → a format (S05 D17); `?version=` opens an older version. */
export default function FormatPage() {
  const { code } = useParams<{ code: string }>();
  const searchParams = useSearchParams();
  const { tenant, can } = useTenantWorkspace();
  const { timeZone, isLoading: zoneLoading } = useTenantTimeZoneOnceRead();
  const version = versionParam(searchParams.get("version"));
  const format = useFormat(code, version ?? undefined, version !== null);

  if (!can("format.read")) {
    return <ForbiddenView />;
  }
  // No such format or version, or a `?version=` that isn't one (never asked: it can't exist).
  if (version === null || isApiProblem(format.error, "FORMAT_DEFINITION_NOT_FOUND")) {
    return <NotFoundView />;
  }

  const back = <BackLink href={`${tenantPath(tenant.slug)}/formats`}>Formats</BackLink>;

  // The tenant's zone is waited for too, so the publication date doesn't change day under the reader.
  if (format.isPending || format.isError || zoneLoading) {
    return (
      <section className="grid gap-4">
        {back}
        {format.isError ? <ProblemAlert error={format.error} /> : <Loading />}
      </section>
    );
  }
  return <FormatView format={format.data} timeZone={timeZone} back={back} />;
}
