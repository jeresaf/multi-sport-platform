import type { ReactNode } from "react";
import { EmptyState } from "@/components/empty-state";
import { QuotaUsage } from "@/components/quota-usage";
import { StatusBadge } from "@/components/status-badge";
import { humanizeCode } from "@/lib/codes";
import { formatDay, formatEntitlementValue } from "@/lib/entitlements";
import type { TenantSubscription, TenantSubscriptionPeriod } from "./subscription-data";

const inEffect: ReadonlySet<string> = new Set(["ACTIVE", "EXPIRING", "GRACE"]);

// Where a value comes from, as a member reads it: the plan, an arrangement with the platform (an override; its reason
// is the platform's), or nothing (no plan in effect).
const sources = {
  PLAN: (plan: string | undefined) => (plan ? `${plan} plan` : "Your plan"),
  OVERRIDE: () => "Arranged for your organisation",
  DEFAULT: () => "No plan",
} as const;

/**
 * The tenant's subscription (S04 D15, D19): the current period (or none), until when the workspace is read-only when
 * it is, the next period, and every entitlement with its value today and, for a quota a module measures, the usage.
 * Days are the tenant's dates, in its time zone. Whether the workspace is read-only is the access mode's (`readOnly`),
 * not the period's: a tenant being set up has full access without one (state-machines.md, "Access mode").
 */
export function SubscriptionView({
  subscription,
  readOnly,
}: Readonly<{ subscription: TenantSubscription; readOnly: boolean }>) {
  const { current, next } = subscription;

  return (
    <div className="grid gap-8">
      {current ? (
        <Period title={`${current.planName} plan`} period={current} />
      ) : (
        <EmptyState title="No subscription">
          {readOnly
            ? "The workspace is read-only until the platform team assigns one."
            : "The platform team assigns your organisation's subscription."}
        </EmptyState>
      )}
      {current && readOnly ? (
        <p>{readOnlyUntil(current, next)}</p>
      ) : null}
      {next ? <Period title="Next period" period={next} /> : null}
      <p className="text-sm text-muted-foreground">Days begin and end in {subscription.timeZone} time.</p>

      <div className="grid gap-3">
        <h2 className="text-lg font-semibold">What your plan includes</h2>
        <ul className="grid gap-3 tablet:grid-cols-2">
          {subscription.entitlements.map((entitlement) => {
            const usage = subscription.usage.find((quota) => quota.code === entitlement.code);
            return (
              <li key={entitlement.code} className="grid content-start gap-1 rounded-lg border p-3">
                <p className="font-medium">{entitlement.name}</p>
                <p className="text-sm text-muted-foreground">{entitlement.description}</p>
                <p>
                  {formatEntitlementValue(entitlement.value, entitlement.unit)}{" "}
                  <span className="text-sm text-muted-foreground">· {sources[entitlement.source](current?.planName)}</span>
                </p>
                {usage ? <QuotaUsage usage={usage} unit={entitlement.unit} /> : null}
              </li>
            );
          })}
        </ul>
      </div>
    </div>
  );
}

function readOnlyUntil(current: TenantSubscriptionPeriod, next: TenantSubscriptionPeriod | null): string {
  // The earliest period to come: the next one, else a current one that hasn't started (#72 R2).
  const start = next ?? (current.status === "SCHEDULED" ? current : null);
  return start
    ? `The workspace is read-only until the period starts on ${formatDay(start.startsOn)}.`
    : "The workspace is read-only until the platform team renews the subscription.";
}

function Period({ title, period }: Readonly<{ title: string; period: TenantSubscriptionPeriod }>) {
  return (
    <div className="grid gap-3">
      <div className="flex flex-wrap items-center gap-3">
        <h2 className="text-lg font-semibold">{title}</h2>
        <StatusBadge status={period.status} />
      </div>
      <dl className="grid gap-4 tablet:grid-cols-2 desktop:grid-cols-4">
        <Detail label="Plan">{period.planName}</Detail>
        <Detail label="Kind">{humanizeCode(period.kind)}</Detail>
        <Detail label="Dates">
          {formatDay(period.startsOn)} – {formatDay(period.endsOn)}
        </Detail>
        {inEffect.has(period.status) && period.graceEndsOn !== period.endsOn ? (
          <Detail label="Full access until">{formatDay(period.graceEndsOn)}</Detail>
        ) : null}
      </dl>
    </div>
  );
}

function Detail({ label, children }: Readonly<{ label: string; children: ReactNode }>) {
  return (
    <div className="grid gap-1">
      <dt className="text-sm text-muted-foreground">{label}</dt>
      <dd className="break-words">{children}</dd>
    </div>
  );
}
