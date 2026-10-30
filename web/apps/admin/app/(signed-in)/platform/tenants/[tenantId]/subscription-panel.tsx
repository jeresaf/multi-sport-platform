"use client";

import { Button } from "@msp/ui/components/button";
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from "@msp/ui/components/table";
import { useMutation } from "@tanstack/react-query";
import { useState, type ReactNode } from "react";
import { ActionDialog } from "@/components/action-dialog";
import { EmptyState } from "@/components/empty-state";
import { Loading } from "@/components/loading";
import { ProblemAlert } from "@/components/problem-alert";
import { QuotaUsage } from "@/components/quota-usage";
import { StatusBadge } from "@/components/status-badge";
import { api, call } from "@/lib/api";
import { humanizeCode } from "@/lib/codes";
import { formatDay, formatEntitlementValue, type EntitlementDefinition } from "@/lib/entitlements";
import { usePlatformCan } from "@/lib/platform-workspace";
import { formatDate } from "@/lib/tenant-settings";
import { usePlans, type Plan } from "../../plans/plans-data";
import type { Tenant } from "../tenants-data";
import { OverrideDialog } from "./override-dialog";
import { AssignPeriodDialog, ChangePlanDialog, RenewDialog } from "./subscription-dialogs";
import {
  currentAndNext,
  inEffectToday,
  isCancellable,
  useSubscription,
  useSubscriptionReload,
  useSubscriptionSaved,
  type EntitlementOverride,
  type Subscription,
  type SubscriptionPeriod,
} from "./subscription-data";

type Open = "assign" | "renew" | "change" | "cancel" | "override" | { remove: EntitlementOverride } | null;

/**
 * When the tenant is read-only for want of a period (S04 D8): an active one now; the others once their own state
 * machine lets its members work (state-machines.md, Tenant: activated, or restored).
 */
const whenReadOnly: Readonly<Record<Tenant["status"], string>> = {
  ACTIVE: "the tenant is read-only",
  PROVISIONING: "once activated, the tenant is read-only",
  SUSPENDED: "once restored, the tenant is read-only",
  ARCHIVED: "once unarchived and restored, the tenant is read-only",
};
const capitalise = (text: string) => text.charAt(0).toUpperCase() + text.slice(1);

/** Columns shown from tablet width up. */
const wide = "hidden tablet:table-cell";

function Detail({ label, children }: Readonly<{ label: string; children: ReactNode }>) {
  return (
    <div className="grid gap-1">
      <dt className="text-sm text-muted-foreground">{label}</dt>
      <dd className="break-words">{children}</dd>
    </div>
  );
}

/**
 * The tenant detail's Subscription panel (admin-app.md, S04 D19): the period that decides access and the next one,
 * every period, the effective entitlements with their usage, and the overrides with their reasons. Platform
 * administrators change it here; support staff read it.
 */
export function SubscriptionPanel({ tenant }: Readonly<{ tenant: Tenant }>) {
  const can = usePlatformCan();
  // The panel needs both reads: the plans give the entitlements' names, units and types. Every platform role that
  // reads subscriptions also reads plans.
  const readable = can("subscription.read") && can("plan.read");
  const subscription = useSubscription(tenant.id, readable);
  const plans = usePlans(readable);

  if (!readable) {
    return null;
  }
  return (
    <section className="grid gap-6" aria-labelledby="subscription-heading">
      <h2 id="subscription-heading" className="text-xl font-semibold">
        Subscription
      </h2>
      {subscription.isPending || plans.isPending ? <Loading /> : null}
      {subscription.isError ? <ProblemAlert error={subscription.error} /> : null}
      {plans.isError ? <ProblemAlert error={plans.error} /> : null}
      {/* Kept while a later read fails (TanStack keeps the data), so an open dialog and its refusal stay. */}
      {subscription.data && plans.data ? (
        <SubscriptionDetail tenant={tenant} subscription={subscription.data} plans={plans.data.items} />
      ) : null}
    </section>
  );
}

/**
 * The first day `subscription.renew` gives the new period: the day after the latest non-cancelled period ends, or today
 * if that day has passed (the API's `TenantSubscription.RenewalStartOn`).
 */
export function renewalStart(subscription: Subscription): string {
  const ends = subscription.periods.filter((period) => period.status !== "CANCELLED").map((period) => period.endsOn);
  if (ends.length === 0) {
    return subscription.today;
  }
  const latest = ends.reduce((a, b) => (a > b ? a : b));
  const next = new Date(`${latest}T00:00:00Z`);
  next.setUTCDate(next.getUTCDate() + 1);
  const day = next.toISOString().slice(0, 10);
  return day > subscription.today ? day : subscription.today;
}

function SubscriptionDetail({
  tenant,
  subscription,
  plans,
}: Readonly<{ tenant: Tenant; subscription: Subscription; plans: readonly Plan[] }>) {
  const can = usePlatformCan();
  const saved = useSubscriptionSaved();
  const { reload, reloadOnConflict } = useSubscriptionReload(tenant.id);
  const [open, setOpen] = useState<Open>(null);
  const [announcement, setAnnouncement] = useState("");
  const { current, next } = currentAndNext(subscription);
  const latest = subscription.periods[0];
  const timeZone = subscription.timeZone;
  const planName = (code: string) => plans.find((plan) => plan.code === code)?.name ?? code;
  // Every plan lists every declared entitlement: the first gives the definitions.
  const definitions: readonly EntitlementDefinition[] = plans[0]?.entitlements ?? [];
  const definition = (code: string) => definitions.find((item) => item.code === code);
  // Change plan is offered from an active plan only: a period on a plan since deactivated is renewed instead.
  const currentRank = plans.find((plan) => plan.code === current?.planCode)?.rank;
  const higher = currentRank === undefined ? [] : plans.filter((plan) => plan.rank > currentRank);

  const onSaved = (next: Subscription, message: string) => {
    saved(next);
    setAnnouncement(message);
    setOpen(null);
  };
  const dialogProps = {
    tenantId: tenant.id,
    tenantName: tenant.displayName,
    subscription,
    plans,
    onOpenChange: (isOpen: boolean) => (isOpen ? null : setOpen(null)),
    onSaved,
  };

  const reasoned = useMutation({
    mutationFn: ({ reason, remove }: { reason: string; remove?: EntitlementOverride }) =>
      remove
        ? call(
            api.POST("/api/platform/v1/tenants/{tenantId}/entitlement-overrides/{overrideId}/remove", {
              params: { path: { tenantId: tenant.id, overrideId: remove.id } },
              body: { reason },
            }),
          )
        : call(
            api.POST("/api/platform/v1/tenants/{tenantId}/subscription/cancel", {
              params: { path: { tenantId: tenant.id } },
              body: { reason },
            }),
          ),
    onSuccess: (next, { remove }) => onSaved(next, remove ? "Override removed." : "Subscription cancelled."),
    onError: (error) => void reloadOnConflict(error),
  });

  // What each command needs (state-machines.md, S04 D4); the API refuses the rest.
  const commands = [
    { name: "assign", action: "subscription.assign", label: "Assign a period", shown: true },
    { name: "renew", action: "subscription.renew", label: "Renew", shown: latest !== undefined },
    {
      name: "change",
      action: "subscription.change_plan",
      label: "Change plan",
      shown: (current?.status === "ACTIVE" || current?.status === "EXPIRING") && higher.length > 0,
    },
    {
      name: "cancel",
      action: "subscription.cancel",
      label: "Cancel subscription",
      shown: subscription.periods.some(isCancellable),
    },
  ] as const;
  const allowed = commands.filter((command) => command.shown && can(command.action));
  const overrides = subscription.overrides.filter((item) => item.removedAt === null);
  const removedOverrides = subscription.overrides.filter((item) => item.removedAt !== null);

  return (
    <div className="grid gap-8">
      <p role="status" className="sr-only">
        {announcement}
      </p>

      <div className="grid gap-4">
        {current ? (
          <PeriodSummary
            title="Current period"
            period={current}
            planName={planName(current.planCode)}
            timeZone={timeZone}
          />
        ) : null}
        {current && !inEffectToday(subscription) ? (
          <p>
            {`No period is in effect today: ${whenReadOnly[tenant.status]} until one starts or is assigned.`}
          </p>
        ) : null}
        {!current ? (
          <EmptyState title="No subscription">
            {`${capitalise(whenReadOnly[tenant.status])} until a period is assigned.`}
          </EmptyState>
        ) : null}
        {next && next.id !== current?.id ? (
          <p>
            {`Next: ${planName(next.planCode)}, ${humanizeCode(next.kind).toLowerCase()}, from ${formatDay(next.startsOn)}`}
            {` to ${formatDay(next.endsOn)}.`}
          </p>
        ) : null}
        <p className="text-sm text-muted-foreground">
          Days are the tenant&apos;s ({timeZone}); today is {formatDay(subscription.today)} there.
        </p>
        {allowed.length > 0 ? (
          <div className="flex flex-wrap gap-2">
            {allowed.map((command) => (
              <Button
                key={command.name}
                variant={command.name === "assign" ? "default" : "outline"}
                onClick={() => {
                  reasoned.reset();
                  setOpen(command.name);
                }}
              >
                {command.label}
              </Button>
            ))}
          </div>
        ) : null}
        {!can("subscription.assign") ? (
          <p className="text-sm text-muted-foreground">
            Changing the subscription or its overrides needs a platform administrator.
          </p>
        ) : null}
      </div>

      {subscription.periods.length > 0 ? (
        <div className="grid gap-3">
          <h3 className="text-lg font-semibold">Periods</h3>
          <Table>
            <TableHeader>
              <TableRow>
                <TableHead>Plan</TableHead>
                <TableHead>Dates</TableHead>
                <TableHead>Status</TableHead>
                <TableHead className={wide}>Kind</TableHead>
              </TableRow>
            </TableHeader>
            <TableBody>
              {subscription.periods.map((period) => (
                <TableRow key={period.id}>
                  <TableCell className="align-top">{planName(period.planCode)}</TableCell>
                  <TableCell className="align-top whitespace-normal">
                    {formatDay(period.startsOn)} – {formatDay(period.endsOn)}
                    {period.cancelledAt ? (
                      <p className="text-sm text-muted-foreground">
                        Cancelled {formatDate(period.cancelledAt, timeZone)}: {period.cancelReason}
                      </p>
                    ) : null}
                  </TableCell>
                  <TableCell className="align-top">
                    <StatusBadge status={period.status} />
                  </TableCell>
                  <TableCell className={`${wide} align-top`}>{humanizeCode(period.kind)}</TableCell>
                </TableRow>
              ))}
            </TableBody>
          </Table>
        </div>
      ) : null}

      <div className="grid gap-3">
        <h3 className="text-lg font-semibold">Entitlements today</h3>
        <ul className="grid gap-3 tablet:grid-cols-2">
          {subscription.entitlements.map((entitlement) => {
            const item = definition(entitlement.code);
            const usage = subscription.usage.find((quota) => quota.code === entitlement.code);
            return (
              <li key={entitlement.code} className="grid gap-1 rounded-lg border p-3">
                <p className="font-medium">{item?.name ?? entitlement.code}</p>
                <p>
                  {formatEntitlementValue(entitlement.value, item?.unit)}{" "}
                  <span className="text-sm text-muted-foreground">
                    ·{" "}
                    {entitlement.source === "PLAN"
                      ? `${current ? planName(current.planCode) : "Plan"} plan`
                      : entitlement.source === "OVERRIDE"
                        ? "Override"
                        : "Default (no plan)"}
                  </span>
                </p>
                {usage ? <QuotaUsage usage={usage} unit={item?.unit} /> : null}
              </li>
            );
          })}
        </ul>
      </div>

      <div className="grid gap-3">
        <div className="flex flex-wrap items-end justify-between gap-3">
          <h3 className="text-lg font-semibold">Overrides</h3>
          {can("entitlement.override") ? (
            <Button variant="outline" onClick={() => setOpen("override")}>
              Set an override
            </Button>
          ) : null}
        </div>
        <p className="text-sm text-muted-foreground">
          A tenant&apos;s exception to its plan. Reasons are for platform staff; the tenant sees only the value.
        </p>
        {overrides.length === 0 ? <p className="text-muted-foreground">No overrides.</p> : null}
        <ul className="grid gap-3">
          {overrides.map((item) => (
            <li key={item.id} className="flex flex-wrap items-start justify-between gap-3 rounded-lg border p-3">
              <div className="grid min-w-0 gap-1">
                <p className="font-medium">
                  {definition(item.entitlementCode)?.name ?? item.entitlementCode}:{" "}
                  {formatEntitlementValue(item.value, definition(item.entitlementCode)?.unit)}
                </p>
                <p className="text-sm break-words">{item.reason}</p>
                <p className="text-sm text-muted-foreground">
                  Set {formatDate(item.grantedAt, timeZone)}
                  {item.effectiveUntil ? `, until ${formatDay(item.effectiveUntil)}` : ", until removed"}
                  {item.inEffect ? "." : ": not in effect (its last day has passed)."}
                </p>
              </div>
              {can("entitlement.override.remove") ? (
                <Button
                  variant="outline"
                  aria-label={`Remove the override of ${definition(item.entitlementCode)?.name ?? item.entitlementCode}`}
                  onClick={() => {
                    reasoned.reset();
                    setOpen({ remove: item });
                  }}
                >
                  Remove
                </Button>
              ) : null}
            </li>
          ))}
        </ul>
        {removedOverrides.length > 0 ? (
          <details className="grid gap-2">
            <summary className="flex min-h-11 cursor-pointer items-center text-sm text-primary">
              Earlier overrides ({removedOverrides.length})
            </summary>
            <ul className="grid gap-2">
              {removedOverrides.map((item) => (
                <li key={item.id} className="text-sm">
                  {definition(item.entitlementCode)?.name ?? item.entitlementCode}:{" "}
                  {formatEntitlementValue(item.value, definition(item.entitlementCode)?.unit)} — {item.reason}.{" "}
                  <span className="text-muted-foreground">
                    Set {formatDate(item.grantedAt, timeZone)}, removed{" "}
                    {item.removedAt ? formatDate(item.removedAt, timeZone) : ""}: {item.removeReason}
                  </span>
                </li>
              ))}
            </ul>
          </details>
        ) : null}
      </div>

      {open === "assign" ? <AssignPeriodDialog {...dialogProps} /> : null}
      {open === "renew" && latest ? (
        <RenewDialog {...dialogProps} latest={latest} startsOn={renewalStart(subscription)} />
      ) : null}
      {open === "change" && current ? (
        <ChangePlanDialog
          {...dialogProps}
          current={current}
          higher={higher}
          available={allowed.some((command) => command.name === "change")}
        />
      ) : null}
      {open === "override" ? <OverrideDialog {...dialogProps} definitions={definitions} /> : null}
      {open === "cancel" || (typeof open === "object" && open !== null) ? (
        <ActionDialog
          open
          onOpenChange={(isOpen) => (isOpen ? null : setOpen(null))}
          title={
            open === "cancel"
              ? `Cancel ${tenant.displayName}'s subscription?`
              : `Remove the override of ${definition(open.remove.entitlementCode)?.name ?? open.remove.entitlementCode}?`
          }
          description={
            open === "cancel"
              ? "The current period and any scheduled one end now. The tenant becomes read-only until a new period is assigned; its public site stays on. Its owners see that it is read-only, not this reason."
              : "The plan's value applies again from now. The override stays in the history with this reason."
          }
          confirmLabel={open === "cancel" ? "Cancel subscription" : "Remove override"}
          // After a refusal the subscription is read again: what it shows no longer to cancel or remove can't be sent.
          confirmDisabled={
            open === "cancel"
              ? !allowed.some((command) => command.name === "cancel")
              : !overrides.some((item) => item.id === open.remove.id)
          }
          destructive
          withReason
          pending={reasoned.isPending}
          error={reasoned.error}
          onConfirm={(reason) =>
            reasoned.mutate({ reason: reason ?? "", remove: open === "cancel" ? undefined : open.remove })
          }
          onReload={() => {
            void reload();
            setOpen(null);
          }}
          reasonHint="Kept in the audit trail with your name, for platform staff only. Don't include personal details."
        />
      ) : null}
    </div>
  );
}

function PeriodSummary({
  title,
  period,
  planName,
  timeZone,
}: Readonly<{ title: string; period: SubscriptionPeriod; planName: string; timeZone: string }>) {
  return (
    <div className="grid gap-3">
      <div className="flex flex-wrap items-center gap-3">
        <h3 className="text-lg font-semibold">{title}</h3>
        <StatusBadge status={period.status} />
      </div>
      <dl className="grid gap-4 tablet:grid-cols-2 desktop:grid-cols-3">
        <Detail label="Plan">{planName}</Detail>
        <Detail label="Kind">{humanizeCode(period.kind)}</Detail>
        <Detail label="Dates">
          {formatDay(period.startsOn)} – {formatDay(period.endsOn)}
        </Detail>
        <Detail label="Expiring from">{formatDay(period.expiringFrom)}</Detail>
        <Detail label="Grace until">{formatDay(period.graceEndsOn)}</Detail>
        {period.cancelledAt ? (
          <Detail label="Cancelled">
            {formatDate(period.cancelledAt, timeZone)}: {period.cancelReason}
          </Detail>
        ) : null}
      </dl>
    </div>
  );
}
