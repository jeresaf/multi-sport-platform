"use client";

// The subscription's period commands (S04 D4): assign a period, renew, change to a higher plan. Each dialog sends its
// command and hands the subscription it answers with to the panel; a refusal stays in the dialog, under its field
// when it names one.
import { zodResolver } from "@hookform/resolvers/zod";
import { Button } from "@msp/ui/components/button";
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from "@msp/ui/components/dialog";
import { useMutation } from "@tanstack/react-query";
import type { ReactNode } from "react";
import { useForm, type FieldValues, type UseFormSetError } from "react-hook-form";
import { z } from "zod";
import { FormField } from "@/components/form-field";
import { ProblemAlert } from "@/components/problem-alert";
import { SelectField } from "@/components/select-field";
import { api, call } from "@/lib/api";
import { humanizeCode } from "@/lib/codes";
import { formatDay } from "@/lib/entitlements";
import { applyFieldErrors, needsAlert, type ApiFields } from "@/lib/form-errors";
import type { Plan } from "../../plans/plans-data";
import { subscriptionKinds, type Subscription, type SubscriptionKind, type SubscriptionPeriod } from "./subscription-data";

export type SubscriptionDialogProps = {
  tenantId: string;
  tenantName: string;
  subscription: Subscription;
  /** The active plans in rank order. */
  plans: readonly Plan[];
  onOpenChange: (open: boolean) => void;
  /** The command's answer, and what the page announces. */
  onSaved: (subscription: Subscription, announcement: string) => void;
};

const days = z
  .string()
  .trim()
  .regex(/^\d*$/, "Enter a whole number of days from 0 to 365.")
  .refine((value) => value === "" || Number(value) <= 365, "Enter a whole number of days from 0 to 365.");
/** An optional number of days: empty → the default (null), else the number. */
const daysOrNull = (value: string) => (value.trim() === "" ? null : Number(value));

/** A command from a form: the field errors under their fields, the rest in the dialog's alert. */
function useCommand<T extends FieldValues, V>(
  send: (values: V) => Promise<Subscription>,
  fields: ApiFields<T>,
  setError: UseFormSetError<T>,
  onSaved: (subscription: Subscription) => void,
) {
  const command = useMutation({
    mutationFn: send,
    onSuccess: onSaved,
    onError: (error) => applyFieldErrors(error, fields, setError),
  });
  const alert = command.isError && needsAlert(command.error, fields);
  return { command, alert };
}

/** A command's dialog: title, what it does, its alert, its fields and its buttons. */
export function CommandDialog({
  title,
  description,
  submitLabel,
  pending,
  error,
  showAlert,
  onSubmit,
  onOpenChange,
  children,
}: Readonly<{
  title: string;
  description: ReactNode;
  submitLabel: string;
  pending: boolean;
  error: unknown;
  showAlert: boolean;
  onSubmit: () => void;
  onOpenChange: (open: boolean) => void;
  children: ReactNode;
}>) {
  return (
    <Dialog open onOpenChange={(open) => !pending && onOpenChange(open)}>
      <DialogContent className="max-h-[90dvh] overflow-y-auto">
        <form
          className="grid gap-4"
          noValidate
          onSubmit={(event) => {
            event.preventDefault();
            onSubmit();
          }}
        >
          <DialogHeader>
            <DialogTitle>{title}</DialogTitle>
            <DialogDescription>{description}</DialogDescription>
          </DialogHeader>
          {showAlert ? <ProblemAlert error={error} /> : null}
          {children}
          <DialogFooter>
            <Button variant="outline" disabled={pending} onClick={() => onOpenChange(false)}>
              Cancel
            </Button>
            <Button type="submit" disabled={pending}>
              {pending ? "Saving…" : submitLabel}
            </Button>
          </DialogFooter>
        </form>
      </DialogContent>
    </Dialog>
  );
}

function PlanOptions({ plans }: Readonly<{ plans: readonly Plan[] }>) {
  return plans.map((plan) => (
    <option key={plan.code} value={plan.code}>
      {plan.name}
    </option>
  ));
}

function KindOptions() {
  return subscriptionKinds.map((kind) => (
    <option key={kind} value={kind}>
      {humanizeCode(kind)}
    </option>
  ));
}

const windowsHint = "Optional. Expiring starts this many days before the last day; grace lasts this many days after it.";

const assignSchema = z
  .object({
    planCode: z.string().min(1, "Choose a plan."),
    kind: z.enum(subscriptionKinds as [SubscriptionKind, ...SubscriptionKind[]]),
    startsOn: z.string().min(1, "Choose the first day."),
    endsOn: z.string().min(1, "Choose the last day."),
    expiringWindowDays: days,
    graceDays: days,
  })
  .refine((values) => values.endsOn === "" || values.startsOn === "" || values.endsOn >= values.startsOn, {
    path: ["endsOn"],
    message: "The last day can't be before the first day.",
  });
type AssignValues = z.infer<typeof assignSchema>;
const assignFields = {
  planCode: "planCode",
  kind: "kind",
  startsOn: "startsOn",
  endsOn: "endsOn",
  expiringWindowDays: "expiringWindowDays",
  graceDays: "graceDays",
} as const satisfies ApiFields<AssignValues>;

/** `subscription.assign`: a new period, which mustn't overlap another that isn't cancelled. */
export function AssignPeriodDialog({
  tenantId,
  tenantName,
  subscription,
  plans,
  onOpenChange,
  onSaved,
}: Readonly<SubscriptionDialogProps>) {
  const form = useForm<AssignValues>({
    resolver: zodResolver(assignSchema),
    defaultValues: {
      planCode: plans[0]?.code ?? "",
      kind: "PAID",
      startsOn: subscription.today,
      endsOn: "",
      expiringWindowDays: "",
      graceDays: "",
    },
  });
  const { command, alert } = useCommand(
    (values: AssignValues) =>
      call(
        api.POST("/api/platform/v1/tenants/{tenantId}/subscription/periods", {
          params: { path: { tenantId } },
          body: {
            planCode: values.planCode,
            kind: values.kind,
            startsOn: values.startsOn,
            endsOn: values.endsOn,
            expiringWindowDays: daysOrNull(values.expiringWindowDays),
            graceDays: daysOrNull(values.graceDays),
          },
        }),
      ),
    assignFields,
    form.setError,
    (saved) => onSaved(saved, "Period assigned."),
  );
  const errors = form.formState.errors;

  return (
    <CommandDialog
      title={`Assign a period to ${tenantName}`}
      description={`Days are the tenant's (${subscription.timeZone}), both included. A period can't overlap another one that isn't cancelled.`}
      submitLabel="Assign period"
      pending={command.isPending}
      error={command.error}
      showAlert={alert}
      onSubmit={form.handleSubmit((values) => command.mutate(values))}
      onOpenChange={onOpenChange}
    >
      <SelectField id="assign-plan" label="Plan" error={errors.planCode?.message} {...form.register("planCode")}>
        <PlanOptions plans={plans} />
      </SelectField>
      <SelectField id="assign-kind" label="Kind" error={errors.kind?.message} {...form.register("kind")}>
        <KindOptions />
      </SelectField>
      <div className="grid gap-4 tablet:grid-cols-2">
        <FormField
          id="assign-starts"
          label="First day"
          type="date"
          error={errors.startsOn?.message}
          {...form.register("startsOn")}
        />
        <FormField
          id="assign-ends"
          label="Last day"
          type="date"
          error={errors.endsOn?.message}
          {...form.register("endsOn")}
        />
        <FormField
          id="assign-expiring"
          label="Expiring warning (days)"
          inputMode="numeric"
          placeholder="30"
          error={errors.expiringWindowDays?.message}
          {...form.register("expiringWindowDays")}
        />
        <FormField
          id="assign-grace"
          label="Grace (days)"
          inputMode="numeric"
          placeholder="14"
          error={errors.graceDays?.message}
          {...form.register("graceDays")}
        />
      </div>
      <p className="text-sm text-muted-foreground">{windowsHint}</p>
    </CommandDialog>
  );
}

const sameAsBefore = "";
/** The renewal's last day can't be before its first day (`startsOn`, the API's `RenewalStartOn`). */
const renewSchema = (startsOn: string) =>
  z.object({
    endsOn: z
      .string()
      .min(1, "Choose the last day.")
      .refine((endsOn) => endsOn >= startsOn, `The last day can't be before ${formatDay(startsOn)}.`),
    planCode: z.string(),
    kind: z.string(),
    expiringWindowDays: days,
    graceDays: days,
  });
type RenewValues = z.infer<ReturnType<typeof renewSchema>>;
const renewFields = {
  endsOn: "endsOn",
  planCode: "planCode",
  kind: "kind",
  expiringWindowDays: "expiringWindowDays",
  graceDays: "graceDays",
} as const satisfies ApiFields<RenewValues>;

/**
 * `subscription.renew`: a new period from the day after the latest one ends (or from today, if that day has passed).
 * What isn't chosen is the latest period's; a lower plan is chosen here (pricing-and-plans.md).
 */
export function RenewDialog({
  tenantId,
  tenantName,
  subscription,
  plans,
  latest,
  startsOn,
  onOpenChange,
  onSaved,
}: Readonly<SubscriptionDialogProps & { latest: SubscriptionPeriod; startsOn: string }>) {
  const form = useForm<RenewValues>({
    resolver: zodResolver(renewSchema(startsOn)),
    defaultValues: { endsOn: "", planCode: sameAsBefore, kind: sameAsBefore, expiringWindowDays: "", graceDays: "" },
  });
  const { command, alert } = useCommand(
    (values: RenewValues) =>
      call(
        api.POST("/api/platform/v1/tenants/{tenantId}/subscription/renew", {
          params: { path: { tenantId } },
          body: {
            endsOn: values.endsOn,
            planCode: values.planCode === sameAsBefore ? null : values.planCode,
            kind: values.kind === sameAsBefore ? null : (values.kind as SubscriptionKind),
            expiringWindowDays: daysOrNull(values.expiringWindowDays),
            graceDays: daysOrNull(values.graceDays),
          },
        }),
      ),
    renewFields,
    form.setError,
    (saved) => onSaved(saved, "Renewed."),
  );
  const errors = form.formState.errors;
  const latestPlan = plans.find((plan) => plan.code === latest.planCode)?.name ?? latest.planCode;

  return (
    <CommandDialog
      title={`Renew ${tenantName}'s subscription`}
      description={`The new period starts on ${formatDay(startsOn)}: the day after the latest period ends, or today if that day has passed. What you leave as it is stays as in the latest period (${latestPlan}, ${humanizeCode(latest.kind).toLowerCase()}). A lower plan takes effect here.`}
      submitLabel="Renew"
      pending={command.isPending}
      error={command.error}
      showAlert={alert}
      onSubmit={form.handleSubmit((values) => command.mutate(values))}
      onOpenChange={onOpenChange}
    >
      <FormField
        id="renew-ends"
        label="Last day"
        type="date"
        hint={`The tenant's date (${subscription.timeZone}), included.`}
        error={errors.endsOn?.message}
        {...form.register("endsOn")}
      />
      <SelectField id="renew-plan" label="Plan" error={errors.planCode?.message} {...form.register("planCode")}>
        <option value={sameAsBefore}>Same plan ({latestPlan})</option>
        <PlanOptions plans={plans} />
      </SelectField>
      <SelectField id="renew-kind" label="Kind" error={errors.kind?.message} {...form.register("kind")}>
        <option value={sameAsBefore}>Same kind ({humanizeCode(latest.kind)})</option>
        <KindOptions />
      </SelectField>
      <div className="grid gap-4 tablet:grid-cols-2">
        <FormField
          id="renew-expiring"
          label="Expiring warning (days)"
          inputMode="numeric"
          placeholder={String(latest.expiringWindowDays)}
          error={errors.expiringWindowDays?.message}
          {...form.register("expiringWindowDays")}
        />
        <FormField
          id="renew-grace"
          label="Grace (days)"
          inputMode="numeric"
          placeholder={String(latest.graceDays)}
          error={errors.graceDays?.message}
          {...form.register("graceDays")}
        />
      </div>
      <p className="text-sm text-muted-foreground">{windowsHint}</p>
    </CommandDialog>
  );
}

const changePlanSchema = z.object({ planCode: z.string().min(1, "Choose a plan.") });
type ChangePlanValues = z.infer<typeof changePlanSchema>;
const changePlanFields = { planCode: "planCode" } as const satisfies ApiFields<ChangePlanValues>;

/**
 * `subscription.change_plan`: moves to a higher plan today, to the same last day. Only higher plans are offered; a
 * lower one waits for the renewal (`TENANT_SUBSCRIPTION_DOWNGRADE_AT_RENEWAL`).
 */
export function ChangePlanDialog({
  tenantId,
  tenantName,
  subscription,
  current,
  higher,
  onOpenChange,
  onSaved,
}: Readonly<Omit<SubscriptionDialogProps, "plans"> & { current: SubscriptionPeriod; higher: readonly Plan[] }>) {
  const form = useForm<ChangePlanValues>({
    resolver: zodResolver(changePlanSchema),
    defaultValues: { planCode: higher[0]?.code ?? "" },
  });
  const { command, alert } = useCommand(
    (values: ChangePlanValues) =>
      call(
        api.POST("/api/platform/v1/tenants/{tenantId}/subscription/change-plan", {
          params: { path: { tenantId } },
          body: values,
        }),
      ),
    changePlanFields,
    form.setError,
    (saved) => onSaved(saved, "Plan changed."),
  );

  return (
    <CommandDialog
      title={`Change ${tenantName}'s plan`}
      description={`From today (${formatDay(subscription.today)}) to ${formatDay(current.endsOn)}: the current period is cancelled and a new one on the chosen plan takes its place. Only a higher plan can start now; a lower one is chosen when renewing.`}
      submitLabel="Change plan"
      pending={command.isPending}
      error={command.error}
      showAlert={alert}
      onSubmit={form.handleSubmit((values) => command.mutate(values))}
      onOpenChange={onOpenChange}
    >
      <SelectField
        id="change-plan"
        label="New plan"
        error={form.formState.errors.planCode?.message}
        {...form.register("planCode")}
      >
        <PlanOptions plans={higher} />
      </SelectField>
    </CommandDialog>
  );
}
