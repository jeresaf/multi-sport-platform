"use client";

import { ArrowLeft } from "lucide-react";
import Link from "next/link";
import { useParams, useSearchParams } from "next/navigation";
import { FormatView } from "@/components/formats/format-view";
import { Loading } from "@/components/loading";
import { ProblemAlert } from "@/components/problem-alert";
import { ForbiddenView, NotFoundView } from "@/components/status-page";
import { isApiProblem } from "@/lib/api";
import { useTenantTimeZone } from "@/lib/tenant-settings";
import { useTenantWorkspace } from "@/lib/tenant-workspace";
import { versionParam } from "@/lib/version-param";
import { tenantPath } from "@/lib/workspace";
import { useFormat } from "../formats-data";

/** Tenant workspace → Formats → a format (S05 D17); `?version=` opens an older version. */
export default function FormatPage() {
  const { code } = useParams<{ code: string }>();
  const searchParams = useSearchParams();
  const { tenant, can } = useTenantWorkspace();
  const timeZone = useTenantTimeZone();
  const version = versionParam(searchParams.get("version"));
  const format = useFormat(code, version ?? undefined, version !== null);

  if (!can("format.read")) {
    return <ForbiddenView />;
  }
  // No such format or version, or a `?version=` that isn't one (never asked: it can't exist).
  if (version === null || isApiProblem(format.error, "FORMAT_DEFINITION_NOT_FOUND")) {
    return <NotFoundView />;
  }

  const back = (
    <Link
      href={`${tenantPath(tenant.slug)}/formats`}
      className="inline-flex min-h-11 w-fit items-center gap-1 text-sm text-primary underline-offset-4 hover:underline"
    >
      <ArrowLeft aria-hidden className="size-4" />
      Formats
    </Link>
  );

  if (format.isPending || format.isError) {
    return (
      <section className="grid gap-4">
        {back}
        {format.isPending ? <Loading /> : <ProblemAlert error={format.error} />}
      </section>
    );
  }
  return <FormatView format={format.data} timeZone={timeZone} back={back} />;
}
