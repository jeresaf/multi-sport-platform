"use client";

import { EmptyState } from "@/components/empty-state";
import { FormatsTable } from "@/components/formats/formats-table";
import { Loading } from "@/components/loading";
import { ProblemAlert } from "@/components/problem-alert";
import { ForbiddenView } from "@/components/status-page";
import { usePlatformCan } from "@/lib/platform-workspace";
import { platformPath } from "@/lib/workspace";
import { usePlatformFormats } from "./formats-data";

/**
 * Platform console → Formats (S05 D17): every published format, each opening its stages and parameters in the
 * workspace's view. Read-only: a format changes only by publishing a new version (a reviewed seed).
 */
export default function PlatformFormatsPage() {
  const can = usePlatformCan();
  const formats = usePlatformFormats();

  if (!can("platform.sport.read")) {
    return <ForbiddenView />;
  }

  const items = formats.data?.items ?? [];
  return (
    <section className="grid gap-6">
      <header className="grid gap-1">
        <h1 className="text-2xl font-semibold">Formats</h1>
        <p className="text-muted-foreground">
          How a competition can be organised: its stages and the parameters it chooses. Tenants see the same pages.
          Read-only.
        </p>
      </header>

      {formats.isPending ? <Loading /> : null}
      {formats.isError ? <ProblemAlert error={formats.error} /> : null}
      {formats.isSuccess && items.length === 0 ? (
        <EmptyState title="No formats yet">No formats are published yet. They are published by the configuration seed.</EmptyState>
      ) : null}
      {items.length > 0 ? (
        <FormatsTable items={items} href={(code) => `${platformPath}/formats/${encodeURIComponent(code)}`} />
      ) : null}
    </section>
  );
}
