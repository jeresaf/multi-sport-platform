"use client";

import { EmptyState } from "@/components/empty-state";
import { Loading } from "@/components/loading";
import { ProblemAlert } from "@/components/problem-alert";
import { SportsTable } from "@/components/sports/sports-table";
import { ForbiddenView } from "@/components/status-page";
import { usePlatformCan } from "@/lib/platform-workspace";
import { platformPath } from "@/lib/workspace";
import { usePlatformSportVariants } from "./sports-data";

/**
 * Platform console → Sports (S05 D17): every published sport variant, each opening its definition in the workspace's
 * viewer. Read-only: a sport changes only by publishing a new version (a reviewed seed).
 */
export default function PlatformSportsPage() {
  const can = usePlatformCan();
  const variants = usePlatformSportVariants();

  if (!can("platform.sport.read")) {
    return <ForbiddenView />;
  }

  const items = variants.data?.items ?? [];
  return (
    <section className="grid gap-6">
      <header className="grid gap-1">
        <h1 className="text-2xl font-semibold">Sports</h1>
        <p className="text-muted-foreground">
          The sports the platform publishes: their periods, events, roles, statistics and scorer pad. Tenants see the
          same pages. Read-only: a sport changes only by publishing a new version.
        </p>
      </header>

      {variants.isPending ? <Loading /> : null}
      {variants.isError ? <ProblemAlert error={variants.error} /> : null}
      {variants.isSuccess && items.length === 0 ? (
        <EmptyState title="No sports yet">No sports are published yet. They are published by the configuration seed.</EmptyState>
      ) : null}
      {items.length > 0 ? (
        <SportsTable items={items} href={(variant) => `${platformPath}/sports/${encodeURIComponent(variant)}`} />
      ) : null}
    </section>
  );
}
