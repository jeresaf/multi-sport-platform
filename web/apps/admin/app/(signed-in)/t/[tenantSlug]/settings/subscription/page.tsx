"use client";

import { Loading } from "@/components/loading";
import { ProblemAlert } from "@/components/problem-alert";
import { ForbiddenView } from "@/components/status-page";
import { useTenantWorkspace } from "@/lib/tenant-workspace";
import { useTenantSubscription } from "./subscription-data";
import { SubscriptionView } from "./subscription-view";

/** Tenant settings → Subscription (S04 D19): the plan, its dates, what it includes and the usage; no self-service. */
export default function SubscriptionPage() {
  const { can } = useTenantWorkspace();
  const subscription = useTenantSubscription();

  if (!can("tenant.subscription.read")) {
    return <ForbiddenView />;
  }

  return (
    <section className="grid max-w-4xl gap-6">
      <header className="grid gap-1">
        <h1 className="text-2xl font-semibold">Subscription</h1>
        <p className="text-muted-foreground">
          Your organisation&apos;s plan and its limits. To change your plan or renew it, contact the platform team.
        </p>
      </header>
      {subscription.isPending ? <Loading /> : null}
      {subscription.isError ? <ProblemAlert error={subscription.error} /> : null}
      {subscription.data ? <SubscriptionView subscription={subscription.data} /> : null}
    </section>
  );
}
