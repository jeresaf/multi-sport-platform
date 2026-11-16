"use client";

import { Badge } from "@msp/ui/components/badge";
import { Button } from "@msp/ui/components/button";
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from "@msp/ui/components/table";
import Link from "next/link";
import { useState } from "react";
import { ActionDialog } from "@/components/action-dialog";
import { EmptyState } from "@/components/empty-state";
import { Loading } from "@/components/loading";
import { ProblemAlert } from "@/components/problem-alert";
import { QuotaUsage } from "@/components/quota-usage";
import { ForbiddenView } from "@/components/status-page";
import { UnavailableNote } from "@/components/unavailable-note";
import { isApiProblem } from "@/lib/api";
import { codeLabel } from "@/lib/sport-definition";
import { formatDate, useTenantTimeZone } from "@/lib/tenant-settings";
import { useTenantWorkspace } from "@/lib/tenant-workspace";
import { tenantPath } from "@/lib/workspace";
import {
  sportLimitReason,
  sportsUnit,
  useDisableSport,
  useEnableSport,
  useRefreshTenantSports,
  useTenantSports,
  type TenantSport,
} from "./sports-settings-data";

/** Columns shown from tablet width up. */
const wide = "hidden tablet:table-cell";

// A refusal that says the list was out of date (someone else changed it, or the quota filled meanwhile): read it again.
const staleCodes = ["TENANT_SPORT_ALREADY_ENABLED", "TENANT_SPORT_NOT_ENABLED", "ENTITLEMENT_QUOTA_EXCEEDED"];

/**
 * Tenant settings → Enabled sports (S05 D17): which sport variants the tenant uses. Enabling takes one of the plan's
 * sports (two variants of one sport count once, D13) and happens at once; disabling asks for a reason and changes
 * nothing that already uses the variant. Each change is a new enablement or closes one: the history is kept (D12).
 */
export default function EnabledSportsPage() {
  const { tenant, can, unavailable } = useTenantWorkspace();
  const timeZone = useTenantTimeZone();
  const sports = useTenantSports();
  const refresh = useRefreshTenantSports();
  const enable = useEnableSport();
  const disable = useDisableSport();
  const [disabling, setDisabling] = useState<TenantSport | null>(null);
  const [announcement, setAnnouncement] = useState("");

  if (!can("tenant.sport.read")) {
    return <ForbiddenView />;
  }

  const variants = sports.data?.variants ?? [];
  const quota = sports.data?.sportQuota ?? null;
  // Shown disabled with the reason (S04 D19) when the roles allow it but the tenant can't now (read-only).
  const enableRefusal = unavailable("tenant.sport.enable");
  const disableRefusal = unavailable("tenant.sport.disable");
  const showsEnable = can("tenant.sport.enable") || enableRefusal !== null;
  const showsDisable = can("tenant.sport.disable") || disableRefusal !== null;
  // The plan's limit, when every sport it allows is taken: the note says it once, and Enable on a new sport points to it.
  const atLimit = variants.map((sport) => sportLimitReason(sport, variants, quota)).find((reason) => reason !== null);
  const limitReason = can("tenant.sport.enable") ? (atLimit ?? null) : null;
  const refusal = enableRefusal ?? limitReason ?? disableRefusal;

  const enableSport = (sport: TenantSport) => {
    disable.reset();
    setAnnouncement("");
    enable.mutate(sport, {
      onSuccess: () => setAnnouncement(`${sport.name} is enabled.`),
      onError: (error) => {
        if (staleCodes.some((code) => isApiProblem(error, code))) {
          void refresh();
        }
      },
    });
  };

  return (
    <section className="grid gap-6">
      <header className="grid gap-1">
        <h1 className="text-2xl font-semibold">Enabled sports</h1>
        <p className="text-muted-foreground">
          The sports {tenant.displayName} runs competitions in. Disabling one changes nothing that already uses it.
        </p>
        <p role="status" className="text-sm">
          {announcement}
        </p>
      </header>

      {quota ? (
        <section className="grid gap-1" aria-labelledby="sport-quota-heading">
          <h2 id="sport-quota-heading" className="text-lg font-semibold">
            Your plan
          </h2>
          <QuotaUsage usage={quota} unit={sportsUnit} />
          <p className="text-sm text-muted-foreground">Two variants of one sport count as one sport.</p>
        </section>
      ) : null}
      {refusal ? <UnavailableNote id="sports-unavailable" reason={refusal} /> : null}
      {enable.isError ? <ProblemAlert error={enable.error} /> : null}

      {sports.isPending ? <Loading /> : null}
      {sports.isError ? <ProblemAlert error={sports.error} /> : null}
      {sports.isSuccess && variants.length === 0 ? (
        <EmptyState title="No sports yet">No sports are published yet. Ask the platform team.</EmptyState>
      ) : null}
      {variants.length > 0 ? (
        <Table>
          <TableHeader>
            <TableRow>
              <TableHead scope="col">Name</TableHead>
              <TableHead scope="col" className={wide}>
                Sport
              </TableHead>
              <TableHead scope="col" className={wide}>
                Status
              </TableHead>
              <TableHead scope="col">
                <span className="sr-only">Actions</span>
              </TableHead>
            </TableRow>
          </TableHeader>
          <TableBody>
            {variants.map((sport) => {
              const status = sport.enabled ? (
                <span className="inline-flex flex-wrap items-center gap-2">
                  <Badge variant="outline">Enabled</Badge>
                  {sport.enabledSince ? (
                    <span className="text-sm text-muted-foreground">since {formatDate(sport.enabledSince, timeZone)}</span>
                  ) : null}
                </span>
              ) : (
                <span className="text-muted-foreground">Not enabled</span>
              );
              const limit = can("tenant.sport.enable") ? sportLimitReason(sport, variants, quota) : null;
              const pendingHere = enable.isPending && enable.variables?.variant === sport.variant;
              return (
                <TableRow key={sport.variant}>
                  <TableHead scope="row" className="h-auto py-2 align-top font-medium whitespace-normal">
                    {can("sport.read") ? (
                      <Link
                        href={`${tenantPath(tenant.slug)}/sports/${encodeURIComponent(sport.variant)}`}
                        className="inline-flex min-h-11 items-center text-primary underline-offset-4 hover:underline"
                      >
                        {sport.name}
                      </Link>
                    ) : (
                      <span className="inline-flex min-h-11 items-center">{sport.name}</span>
                    )}
                    {/* On a phone the sport and status sit under the name instead of in their own columns. */}
                    <div className="grid gap-1 pb-2 font-normal tablet:hidden">
                      <span>{codeLabel(sport.sport)}</span>
                      {status}
                    </div>
                  </TableHead>
                  <TableCell className={`align-top ${wide}`}>
                    <span className="inline-flex min-h-11 items-center">{codeLabel(sport.sport)}</span>
                  </TableCell>
                  <TableCell className={`align-top ${wide}`}>
                    <span className="inline-flex min-h-11 items-center">{status}</span>
                  </TableCell>
                  <TableCell className="text-right align-top">
                    {sport.enabled && showsDisable ? (
                      <Button
                        variant="outline"
                        disabled={disableRefusal !== null || enable.isPending}
                        aria-label={`Disable ${sport.name}`}
                        aria-describedby={disableRefusal ? "sports-unavailable" : undefined}
                        onClick={() => {
                          enable.reset();
                          disable.reset();
                          setAnnouncement("");
                          setDisabling(sport);
                        }}
                      >
                        Disable
                      </Button>
                    ) : null}
                    {!sport.enabled && showsEnable ? (
                      <Button
                        disabled={enableRefusal !== null || limit !== null || enable.isPending}
                        aria-label={`Enable ${sport.name}`}
                        aria-describedby={enableRefusal || limit ? "sports-unavailable" : undefined}
                        onClick={() => enableSport(sport)}
                      >
                        {pendingHere ? "Enabling…" : "Enable"}
                      </Button>
                    ) : null}
                  </TableCell>
                </TableRow>
              );
            })}
          </TableBody>
        </Table>
      ) : null}

      {disabling ? (
        <ActionDialog
          open
          onOpenChange={(next) => (next ? null : setDisabling(null))}
          title={`Disable ${disabling.name}?`}
          description="New competitions can't use it. Competitions, teams and results that already use it stay as they are. You can enable it again later."
          confirmLabel="Disable"
          destructive
          withReason
          reasonHint="Kept with the enablement and in the audit trail. Don't include personal details."
          pending={disable.isPending}
          error={disable.error}
          onConfirm={(reason) =>
            disable.mutate(
              { sport: disabling, reason: reason ?? "" },
              {
                onSuccess: () => {
                  setAnnouncement(`${disabling.name} is disabled.`);
                  setDisabling(null);
                },
                onError: (error) => {
                  if (staleCodes.some((code) => isApiProblem(error, code))) {
                    void refresh();
                  }
                },
              },
            )
          }
          onReload={() => {
            void refresh();
            setDisabling(null);
          }}
        />
      ) : null}
    </section>
  );
}
