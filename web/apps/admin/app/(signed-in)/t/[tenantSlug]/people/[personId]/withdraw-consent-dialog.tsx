"use client";

// `person.consent.withdraw` (S06 D14, T6a): some or all of a consent's scopes, with a reason (audit
// `REQUIRED_WITH_REASON`). The consent is closed; the scopes left, if any, go on in a new record.
import { zodResolver } from "@hookform/resolvers/zod";
import { Label } from "@msp/ui/components/label";
import { Textarea } from "@msp/ui/components/textarea";
import { useState } from "react";
import { Controller, useForm } from "react-hook-form";
import { z } from "zod";
import { reasonMaxLength } from "@/components/action-dialog";
import { CommandDialog } from "@/components/command-dialog";
import { formatDay } from "@/lib/entitlements";
import { applyFieldErrors, needsAlert } from "@/lib/form-errors";
import type { Person } from "../people-data";
import { consentScopes, useWithdrawConsent, type ConsentScope } from "./consent-data";
import { useReloadPerson, type OpenedPerson } from "./profile-data";
import { ScopeCheckboxes } from "./scope-checkboxes";

type Consent = Person["consents"][number];

const schema = z.object({
  scopes: z.array(z.enum(consentScopes)).min(1, "Choose at least one thing to withdraw."),
  reason: z
    .string()
    .trim()
    .min(1, "Give a reason: it is kept in the audit trail.")
    .max(reasonMaxLength, `Keep the reason under ${reasonMaxLength} characters.`),
});
type Values = { scopes: ConsentScope[]; reason: string };
const fields: Readonly<Record<string, keyof Values>> = { scopes: "scopes", reason: "reason" };

export function WithdrawConsentDialog({
  person,
  consent,
  onOpenChange,
  onSaved,
  onCloseAutoFocus,
}: Readonly<{
  person: Person;
  consent: Consent;
  onOpenChange: (open: boolean) => void;
  /** The announcement and the consent to focus: the one that carries the scopes left, or the withdrawn one. */
  onSaved: (announcement: string, consentId: string) => void;
  onCloseAutoFocus?: (event: Event) => void;
}>) {
  const form = useForm<Values>({
    resolver: zodResolver(schema),
    // One scope: it is the one to withdraw.
    defaultValues: { scopes: consent.scopes.length === 1 ? [...consent.scopes] : [], reason: "" },
  });
  // The version the dialog opened on (#110 R1).
  const [opened] = useState<OpenedPerson>(() => ({ id: person.id, version: person.version }));
  const withdraw = useWithdrawConsent(opened, consent.id);
  const reload = useReloadPerson(person.id);
  const errors = form.formState.errors;

  const submit = form.handleSubmit((values) =>
    withdraw.mutate(
      { scopes: values.scopes, reason: values.reason.trim() },
      {
        onSuccess: (saved) => {
          const successor = saved.consents.find((item) => item.replacesConsentId === consent.id);
          onSaved(
            successor ? "Consent withdrawn in part; what is left is in a new record." : "Consent withdrawn.",
            successor?.id ?? consent.id,
          );
        },
        onError: (error) => applyFieldErrors(error, fields, form.setError),
      },
    ),
  );

  return (
    <CommandDialog
      title={`Withdraw the consent given on ${formatDay(consent.givenOn)}`}
      description="The record is kept and marked withdrawn. Anything not withdrawn stays in force in a new record."
      submitLabel="Withdraw"
      pending={withdraw.isPending}
      error={withdraw.error}
      showAlert={withdraw.isError && needsAlert(withdraw.error, fields)}
      onSubmit={submit}
      onOpenChange={onOpenChange}
      onCloseAutoFocus={onCloseAutoFocus}
      onReload={() => {
        void reload();
        onOpenChange(false);
      }}
    >
      <Controller
        control={form.control}
        name="scopes"
        render={({ field }) => (
          <ScopeCheckboxes
            id="withdraw-scopes"
            legend="Withdraw"
            scopes={consent.scopes}
            value={field.value}
            onChange={field.onChange}
            error={errors.scopes?.message}
          />
        )}
      />
      <div className="grid gap-2">
        <Label htmlFor="withdraw-reason">Reason</Label>
        <Textarea
          id="withdraw-reason"
          maxLength={reasonMaxLength}
          aria-invalid={errors.reason ? true : undefined}
          aria-describedby={errors.reason ? "withdraw-reason-error" : "withdraw-reason-hint"}
          {...form.register("reason")}
        />
        {errors.reason ? (
          <p id="withdraw-reason-error" className="text-sm text-destructive">
            {errors.reason.message}
          </p>
        ) : (
          <p id="withdraw-reason-hint" className="text-sm text-muted-foreground">
            Kept on the record and in the audit trail with your name. Don&apos;t include personal details.
          </p>
        )}
      </div>
    </CommandDialog>
  );
}
