"use client";

import { Button } from "@msp/ui/components/button";
import { useMutation, useQueryClient } from "@tanstack/react-query";
import { ArrowLeft } from "lucide-react";
import Link from "next/link";
import { useParams } from "next/navigation";
import { useRef, useState, type ReactNode } from "react";
import { ActionDialog } from "@/components/action-dialog";
import { Loading } from "@/components/loading";
import { ProblemAlert } from "@/components/problem-alert";
import { StatusBadge } from "@/components/status-badge";
import { ForbiddenView, NotFoundView } from "@/components/status-page";
import { api, call, isApiProblem } from "@/lib/api";
import { ifMatch } from "@/lib/concurrency";
import { usePlatformCan } from "@/lib/platform-workspace";
import { useCountries } from "@/lib/reference-data";
import { formatDate, formatDateTime } from "@/lib/tenant-settings";
import { platformPath } from "@/lib/workspace";
import { tenantQueryKey, useTenant, useTenantSaved, type Tenant, type TenantStatus } from "../tenants-data";
import { OwnerInviteDialog, type OwnerInvitation } from "./owner-invite-dialog";
import { inEffectToday, useSubscription } from "./subscription-data";
import { SubscriptionPanel } from "./subscription-panel";

type Change = "activate" | "suspend" | "restore" | "archive" | "unarchive";

// The tenant state machine (state-machines.md): what each status allows, and the action each change needs.
const changes: Readonly<
  Record<
    Change,
    {
      action: string;
      from: readonly TenantStatus[];
      label: string;
      description: string;
      withReason: boolean;
      destructive: boolean;
    }
  >
> = {
  activate: {
    action: "tenant.activate",
    from: ["PROVISIONING"],
    label: "Activate",
    description: "All its members can work in it, and its public site is shown if it is turned on.",
    withReason: false,
    destructive: false,
  },
  suspend: {
    action: "tenant.suspend",
    from: ["ACTIVE"],
    label: "Suspend",
    description:
      "Its members can't work in it and its public site is offline until it is restored. Its owners still see that it is suspended, since when, and this reason.",
    withReason: true,
    destructive: true,
  },
  restore: {
    action: "tenant.restore",
    from: ["SUSPENDED"],
    label: "Restore",
    description: "Its members can work in it again, and its public site comes back if it is turned on.",
    withReason: false,
    destructive: false,
  },
  archive: {
    action: "tenant.archive",
    from: ["ACTIVE", "SUSPENDED"],
    label: "Archive",
    description:
      "The tenant is closed: no one can work in it, join it or be invited to it. Its owners still see that it is archived and this reason. It can be unarchived later, which leaves it suspended. Its slug is never reused.",
    withReason: true,
    destructive: true,
  },
  unarchive: {
    action: "tenant.unarchive",
    from: ["ARCHIVED"],
    label: "Unarchive",
    description:
      "It comes back suspended: restore it afterwards to let its members work again. Its owners then see this reason instead of the earlier one.",
    withReason: true,
    destructive: false,
  },
};

// What the page announces once a change is saved (the badge shows it too).
const done: Readonly<Record<Change, string>> = {
  activate: "Activated.",
  suspend: "Suspended.",
  restore: "Restored.",
  archive: "Archived.",
  unarchive: "Unarchived: the tenant is suspended until it is restored.",
};

const changeOrder: readonly Change[] = ["activate", "restore", "suspend", "unarchive", "archive"];

function Detail({ label, children }: Readonly<{ label: string; children: ReactNode }>) {
  return (
    <div className="grid gap-1">
      <dt className="text-sm text-muted-foreground">{label}</dt>
      <dd className="break-words">{children}</dd>
    </div>
  );
}

/** Platform console → a tenant (admin-app.md): its profile, its lifecycle, its owner's invitation and its subscription. */
export default function TenantPage() {
  const { tenantId } = useParams<{ tenantId: string }>();
  const can = usePlatformCan();
  const tenant = useTenant(tenantId, can("tenant.read"));

  if (!can("tenant.read")) {
    return <ForbiddenView />;
  }
  if (tenant.isPending) {
    return <Loading />;
  }
  if (tenant.isError) {
    return isApiProblem(tenant.error) && tenant.error.status === 404 ? (
      <NotFoundView />
    ) : (
      <ProblemAlert error={tenant.error} />
    );
  }
  return <TenantDetail tenant={tenant.data} />;
}

function TenantDetail({ tenant }: Readonly<{ tenant: Tenant }>) {
  const can = usePlatformCan();
  const queryClient = useQueryClient();
  const saved = useTenantSaved();
  const countries = useCountries();
  // Shared with the Subscription panel (one request): activating a tenant without a period in effect leaves it
  // read-only (D8, D9).
  const subscription = useSubscription(tenant.id, can("subscription.read") && can("plan.read"));
  const [pending, setPending] = useState<Change | null>(null);
  const [inviting, setInviting] = useState(false);
  const [invited, setInvited] = useState<OwnerInvitation | null>(null);
  const [announcement, setAnnouncement] = useState("");
  // The button that opened the dialog is gone after a change (Suspend becomes Restore): focus goes to the heading.
  const heading = useRef<HTMLHeadingElement>(null);
  const changed = useRef(false);
  const reload = () => queryClient.invalidateQueries({ queryKey: tenantQueryKey(tenant.id) });

  const change = useMutation({
    mutationFn: ({ change, reason }: { change: Change; reason?: string }) => {
      const params = { path: { tenantId: tenant.id }, header: ifMatch(tenant.version) };
      switch (change) {
        case "activate":
          return call(api.POST("/api/platform/v1/tenants/{tenantId}/activate", { params }));
        case "restore":
          return call(api.POST("/api/platform/v1/tenants/{tenantId}/restore", { params }));
        case "suspend":
          return call(
            api.POST("/api/platform/v1/tenants/{tenantId}/suspend", { params, body: { reason: reason ?? null } }),
          );
        case "archive":
          return call(
            api.POST("/api/platform/v1/tenants/{tenantId}/archive", { params, body: { reason: reason ?? null } }),
          );
        case "unarchive":
          return call(
            api.POST("/api/platform/v1/tenants/{tenantId}/unarchive", { params, body: { reason: reason ?? null } }),
          );
      }
    },
    onSuccess: async (next, { change }) => {
      await saved(next);
      changed.current = true;
      setAnnouncement(done[change]);
      setPending(null);
    },
    // Defensive: a change sent with the shown version that someone else made first is a concurrency conflict (the
    // version is checked first), so this refusal shouldn't come from this page; should it, show the tenant as it is.
    onError: (error) => {
      if (isApiProblem(error, "TENANT_INVALID_TRANSITION")) {
        void reload();
      }
    },
  });

  const allowed = changeOrder.filter(
    (name) => can(changes[name].action) && changes[name].from.includes(tenant.status),
  );
  const open = pending ? changes[pending] : null;
  const country = countries.data?.items.find((item) => item.code === tenant.countryCode)?.name ?? tenant.countryCode;
  const timeZone = tenant.timeZone;
  // An archived tenant can't be invited to (AUTH_TENANT_ARCHIVED); every other status can.
  const canInvite = can("tenant.owner.invite") && tenant.status !== "ARCHIVED";
  const withoutSubscription = subscription.isSuccess && !inEffectToday(subscription.data);

  return (
    <div className="grid gap-8">
      <Link
        href={`${platformPath}/tenants`}
        className="flex min-h-11 items-center gap-2 justify-self-start text-sm text-primary underline-offset-4 hover:underline"
      >
        <ArrowLeft aria-hidden="true" className="size-4" />
        Tenants
      </Link>

      <header className="flex flex-wrap items-start justify-between gap-4">
        <div className="grid min-w-0 gap-2">
          <h1 ref={heading} tabIndex={-1} className="text-2xl font-semibold break-words outline-none">
            {tenant.displayName}
          </h1>
          <div className="flex flex-wrap items-center gap-3">
            <StatusBadge status={tenant.status} />
            <span className="text-muted-foreground">{tenant.slug}</span>
          </div>
          <p role="status" className="sr-only">
            {announcement}
          </p>
        </div>
        {allowed.length > 0 ? (
          <div className="flex flex-wrap gap-2">
            {allowed.map((name) => (
              <Button
                key={name}
                variant={changes[name].destructive ? "outline" : "default"}
                onClick={() => {
                  change.reset();
                  setPending(name);
                }}
              >
                {changes[name].label}
              </Button>
            ))}
          </div>
        ) : null}
        {changeOrder.every((name) => !can(changes[name].action)) ? (
          <p className="basis-full text-sm text-muted-foreground">
            You can view this tenant. Changing its status or inviting its owner needs a platform administrator.
          </p>
        ) : null}
      </header>

      <section className="grid gap-4" aria-labelledby="profile-heading">
        <h2 id="profile-heading" className="text-xl font-semibold">
          Profile
        </h2>
        <dl className="grid gap-4 tablet:grid-cols-2">
          <Detail label="Name">{tenant.displayName}</Detail>
          <Detail label="Legal name">{tenant.legalName}</Detail>
          <Detail label="Country">{country}</Detail>
          <Detail label="Time zone">{tenant.timeZone}</Detail>
          <Detail label="Language">{tenant.defaultLocale}</Detail>
          <Detail label="Currency">{tenant.currencyCode}</Detail>
          <Detail label="Public site">{tenant.publicEnabled ? "On" : "Off"}</Detail>
          <Detail label="Created">{formatDate(tenant.createdAt, timeZone)}</Detail>
          <Detail label="Last changed">{formatDate(tenant.updatedAt, timeZone)}</Detail>
        </dl>
        <p className="text-sm text-muted-foreground">Dates are in the tenant&apos;s time zone.</p>
      </section>

      <section className="grid gap-4" aria-labelledby="owner-heading">
        <h2 id="owner-heading" className="text-xl font-semibold">
          Owner
        </h2>
        <p className="text-muted-foreground">
          The owner runs the tenant: they invite its members and manage its settings. Invite them while the tenant is
          being set up; they can work in it before it is activated.
        </p>
        <p role="status" className="text-sm">
          {invited
            ? `Invited ${invited.email}. The email with the link goes out in a moment; the link works until ${formatDateTime(invited.expiresAt, timeZone)} (${timeZone}).`
            : ""}
        </p>
        {canInvite ? (
          <Button variant="outline" className="justify-self-start" onClick={() => setInviting(true)}>
            Invite an owner
          </Button>
        ) : null}
        {can("tenant.owner.invite") && tenant.status === "ARCHIVED" ? (
          <p className="text-sm text-muted-foreground">An archived tenant can&apos;t be invited to.</p>
        ) : null}
        <p className="text-sm text-muted-foreground">
          Owner invitations sent earlier aren&apos;t shown here yet.
        </p>
      </section>

      <SubscriptionPanel tenant={tenant} />

      <section className="grid gap-2" aria-labelledby="later-heading">
        <h2 id="later-heading" className="text-xl font-semibold">
          More about this tenant
        </h2>
        <p className="text-muted-foreground">
          Its status history and reasons, members and domains aren&apos;t available here yet.
        </p>
      </section>

      {inviting ? (
        <OwnerInviteDialog tenant={tenant} onOpenChange={setInviting} onInvited={setInvited} />
      ) : null}
      {pending && open ? (
        <ActionDialog
          open
          onOpenChange={(next) => (next ? null : setPending(null))}
          title={`${open.label} ${tenant.displayName}?`}
          description={
            pending === "activate" && withoutSubscription
              ? `${open.description} No subscription period is in effect today: it will be read-only until one starts or is assigned.`
              : open.description
          }
          confirmLabel={open.label}
          destructive={open.destructive}
          withReason={open.withReason}
          pending={change.isPending}
          error={change.error}
          // After TENANT_INVALID_TRANSITION the tenant is read again: a change its new status doesn't allow can't be sent.
          confirmDisabled={!allowed.includes(pending)}
          onConfirm={(reason) => change.mutate({ change: pending, reason })}
          onReload={() => {
            void reload();
            setPending(null);
          }}
          onCloseAutoFocus={(event) => {
            if (changed.current) {
              changed.current = false;
              event.preventDefault();
              heading.current?.focus();
            }
          }}
          reasonHint="Kept in the audit trail with your name, and shown to the tenant's owners. Don't include personal details."
        />
      ) : null}
    </div>
  );
}
