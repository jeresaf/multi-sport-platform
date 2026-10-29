"use client";

// `entitlement.override` (S04 D5): a value in place of the plan's for one entitlement, with a reason and an optional
// last day. The value's input follows the entitlement's type. A new override replaces the one in place.
import { zodResolver } from "@hookform/resolvers/zod";
import { Checkbox } from "@msp/ui/components/checkbox";
import { Label } from "@msp/ui/components/label";
import { Textarea } from "@msp/ui/components/textarea";
import { useMutation } from "@tanstack/react-query";
import { Controller, useForm, useWatch } from "react-hook-form";
import { z } from "zod";
import { reasonMaxLength } from "@/components/action-dialog";
import { FormField } from "@/components/form-field";
import { SelectField } from "@/components/select-field";
import { api, call } from "@/lib/api";
import { humanizeCode } from "@/lib/codes";
import { formatEntitlementValue, type EntitlementDefinition, type EntitlementValue } from "@/lib/entitlements";
import { applyFieldErrors, needsAlert } from "@/lib/form-errors";
import { useSubscriptionReload, type Subscription } from "./subscription-data";
import { CommandDialog } from "./subscription-dialogs";

const schema = (definitions: readonly EntitlementDefinition[]) =>
  z
    .object({
      entitlementCode: z.string().min(1, "Choose an entitlement."),
      enabled: z.enum(["true", "false"]),
      limit: z.string().trim(),
      unlimited: z.boolean(),
      level: z.string(),
      reason: z
        .string()
        .trim()
        .min(1, "Give a reason: it is kept in the audit trail.")
        .max(reasonMaxLength, `Keep the reason under ${reasonMaxLength} characters.`),
      effectiveUntil: z.string(),
    })
    .superRefine((values, context) => {
      const definition = definitions.find((item) => item.code === values.entitlementCode);
      if (definition?.type === "QUOTA" && !values.unlimited && !/^\d+$/.test(values.limit)) {
        context.addIssue({ code: "custom", path: ["limit"], message: "Enter a whole number, or choose Unlimited." });
      }
    });
type Values = z.infer<ReturnType<typeof schema>>;

/** The form's value as the API takes it, by the entitlement's type. */
function valueOf(definition: EntitlementDefinition, values: Values): EntitlementValue {
  switch (definition.type) {
    case "BOOLEAN":
      return { enabled: values.enabled === "true" };
    case "QUOTA":
      return { limit: values.unlimited ? null : Number(values.limit) };
    case "LEVEL":
      return { level: values.level === "" ? null : values.level };
  }
}

/** The form field that shows the API's refusal of `value`. */
const valueField = { BOOLEAN: "enabled", QUOTA: "limit", LEVEL: "level" } as const;

export function OverrideDialog({
  tenantId,
  tenantName,
  subscription,
  definitions,
  onOpenChange,
  onSaved,
}: Readonly<{
  tenantId: string;
  tenantName: string;
  subscription: Subscription;
  definitions: readonly EntitlementDefinition[];
  onOpenChange: (open: boolean) => void;
  onSaved: (subscription: Subscription, announcement: string) => void;
}>) {
  const form = useForm<Values>({
    resolver: zodResolver(schema(definitions)),
    defaultValues: {
      entitlementCode: definitions[0]?.code ?? "",
      enabled: "true",
      limit: "",
      unlimited: false,
      level: "",
      reason: "",
      effectiveUntil: "",
    },
  });
  const entitlementCode = useWatch({ control: form.control, name: "entitlementCode" });
  const unlimited = useWatch({ control: form.control, name: "unlimited" });
  const definition = definitions.find((item) => item.code === entitlementCode);
  const fields = {
    entitlementCode: "entitlementCode",
    value: definition ? valueField[definition.type] : "limit",
    reason: "reason",
    effectiveUntil: "effectiveUntil",
  } as const;
  const { reloadOnConflict } = useSubscriptionReload(tenantId);
  const effective = subscription.entitlements.find((item) => item.code === definition?.code);

  const grant = useMutation({
    mutationFn: (values: Values) =>
      call(
        api.POST("/api/platform/v1/tenants/{tenantId}/entitlement-overrides", {
          params: { path: { tenantId } },
          body: {
            entitlementCode: values.entitlementCode,
            value: definition ? valueOf(definition, values) : null,
            reason: values.reason.trim(),
            effectiveUntil: values.effectiveUntil === "" ? null : values.effectiveUntil,
          },
        }),
      ),
    onSuccess: (saved) => onSaved(saved, "Override set."),
    onError: (error) => {
      applyFieldErrors(error, fields, form.setError);
      void reloadOnConflict(error);
    },
  });
  const errors = form.formState.errors;

  return (
    <CommandDialog
      title={`Set an override for ${tenantName}`}
      description="The value applies in place of the plan's until its last day or until it is removed. An override already set for the entitlement is replaced. The tenant sees the value, not the reason."
      submitLabel="Set override"
      pending={grant.isPending}
      error={grant.error}
      showAlert={grant.isError && needsAlert(grant.error, fields)}
      onSubmit={form.handleSubmit((values) => grant.mutate(values))}
      onOpenChange={onOpenChange}
    >
      <SelectField
        id="override-entitlement"
        label="Entitlement"
        hint={
          definition && effective
            ? `${definition.description} Now: ${formatEntitlementValue(effective.value, definition.unit)}.`
            : definition?.description
        }
        error={errors.entitlementCode?.message}
        {...form.register("entitlementCode")}
      >
        {definitions.map((item) => (
          <option key={item.code} value={item.code}>
            {item.name}
          </option>
        ))}
      </SelectField>

      {definition?.type === "BOOLEAN" ? (
        <SelectField id="override-enabled" label="Value" error={errors.enabled?.message} {...form.register("enabled")}>
          <option value="true">Included</option>
          <option value="false">Not included</option>
        </SelectField>
      ) : null}
      {definition?.type === "QUOTA" ? (
        <div className="grid gap-2">
          <FormField
            id="override-limit"
            label={definition.unit ? `Limit (${definition.unit})` : "Limit"}
            inputMode="numeric"
            disabled={unlimited}
            error={errors.limit?.message}
            {...form.register("limit")}
          />
          <Controller
            control={form.control}
            name="unlimited"
            render={({ field }) => (
              <div className="flex min-h-11 items-center gap-3">
                <Checkbox
                  id="override-unlimited"
                  checked={field.value}
                  onCheckedChange={(checked) => field.onChange(checked === true)}
                />
                <Label htmlFor="override-unlimited">Unlimited</Label>
              </div>
            )}
          />
        </div>
      ) : null}
      {definition?.type === "LEVEL" ? (
        <SelectField id="override-level" label="Level" error={errors.level?.message} {...form.register("level")}>
          <option value="">None</option>
          {definition.levels.map((level) => (
            <option key={level} value={level}>
              {humanizeCode(level)}
            </option>
          ))}
        </SelectField>
      ) : null}

      <FormField
        id="override-until"
        label="Last day (optional)"
        type="date"
        hint={`The tenant's date (${subscription.timeZone}), included. Leave it empty to keep the override until it is removed.`}
        error={errors.effectiveUntil?.message}
        {...form.register("effectiveUntil")}
      />
      <div className="grid gap-2">
        <Label htmlFor="override-reason">Reason</Label>
        <Textarea
          id="override-reason"
          maxLength={reasonMaxLength}
          aria-invalid={errors.reason ? true : undefined}
          aria-describedby={errors.reason ? "override-reason-error" : "override-reason-hint"}
          {...form.register("reason")}
        />
        {errors.reason ? (
          <p id="override-reason-error" className="text-sm text-destructive">
            {errors.reason.message}
          </p>
        ) : (
          <p id="override-reason-hint" className="text-sm text-muted-foreground">
            Kept in the audit trail with your name, for platform staff only. Don&apos;t include personal details.
          </p>
        )}
      </div>
    </CommandDialog>
  );
}
