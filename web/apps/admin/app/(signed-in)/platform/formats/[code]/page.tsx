"use client";

import { useParams, useSearchParams } from "next/navigation";
import { FormatView } from "@/components/formats/format-view";
import { Loading } from "@/components/loading";
import { ProblemAlert } from "@/components/problem-alert";
import { ForbiddenView, NotFoundView } from "@/components/status-page";
import { BackLink } from "@/components/viewer/facts";
import { isApiProblem } from "@/lib/api";
import { usePlatformCan } from "@/lib/platform-workspace";
import { versionParam } from "@/lib/version-param";
import { platformPath } from "@/lib/workspace";
import { usePlatformFormat } from "../formats-data";

/** Platform console → Formats → a format (S05 D17); `?version=` opens an older version. Dates are in UTC. */
export default function PlatformFormatPage() {
  const { code } = useParams<{ code: string }>();
  const searchParams = useSearchParams();
  const can = usePlatformCan();
  const version = versionParam(searchParams.get("version"));
  const format = usePlatformFormat(code, version ?? undefined, version !== null);

  if (!can("platform.sport.read")) {
    return <ForbiddenView />;
  }
  // No such format or version, or a `?version=` that isn't one (never asked: it can't exist).
  if (version === null || isApiProblem(format.error, "FORMAT_DEFINITION_NOT_FOUND")) {
    return <NotFoundView />;
  }

  const back = <BackLink href={`${platformPath}/formats`}>Formats</BackLink>;
  if (format.isPending || format.isError) {
    return (
      <section className="grid gap-4">
        {back}
        {format.isPending ? <Loading /> : <ProblemAlert error={format.error} />}
      </section>
    );
  }
  return <FormatView format={format.data} timeZone="UTC" back={back} />;
}
