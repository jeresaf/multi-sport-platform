"use client";

import { Loading } from "@/components/loading";
import { ProblemAlert } from "@/components/problem-alert";
import { StatusPage } from "@/components/status-page";
import { isApiProblem } from "@/lib/api";
import { useTenantAccess } from "@/lib/capabilities";
import { humanizeCode } from "@/lib/codes";
import type { MeTenant } from "@/lib/workspace";

// UTC until this page reads `tenant/access` (S04-T4): it carries the tenant's time zone since S04-T2b; `/me` doesn't.
const dateFormat = new Intl.DateTimeFormat("en-GB", { dateStyle: "long", timeZone: "UTC" });

/**
 * A suspended or archived tenant's workspace (S03 D11): its status, and — for whoever the server lets read
 * `tenant.access.read` (the owner) — since when and why.
 */
export function BlockedTenant({ tenant }: Readonly<{ tenant: MeTenant }>) {
  const access = useTenantAccess(tenant.tenantId, true);
  const status = humanizeCode(tenant.status).toLowerCase();

  return (
    <StatusPage title={`${tenant.displayName} is ${status}`}>
      <p>Its workspace is closed while the organisation is {status}.</p>
      {access.data ? (
        <dl className="grid gap-1">
          <div>
            <dt className="inline font-medium text-foreground">Since: </dt>
            <dd className="inline">{dateFormat.format(new Date(access.data.since))}</dd>
          </div>
          {access.data.reason ? (
            <div>
              <dt className="inline font-medium text-foreground">Reason: </dt>
              <dd className="inline">{access.data.reason}</dd>
            </div>
          ) : null}
        </dl>
      ) : null}
      {access.isPending ? <Loading /> : null}
      {access.isError && isApiProblem(access.error, "AUTH_FORBIDDEN") ? (
        <p>Ask the organisation&apos;s owner why, or contact MSP support.</p>
      ) : access.isError ? (
        <ProblemAlert error={access.error} title="Couldn't load why" />
      ) : null}
    </StatusPage>
  );
}
