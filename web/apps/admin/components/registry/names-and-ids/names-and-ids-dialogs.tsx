"use client";

// The dialogs of Names & IDs (S07 D10): add an identifier (a value is never given twice within its authority), end
// one (it stays listed), add an alias and remove one with a reason. Each keeps the version it opened on (#110 R1).
import { zodResolver } from "@hookform/resolvers/zod";
import { useMutation } from "@tanstack/react-query";
import { useEffect, useState } from "react";
import { useForm } from "react-hook-form";
import { z } from "zod";
import { ActionDialog } from "@/components/action-dialog";
import { CommandDialog } from "@/components/command-dialog";
import { FormField } from "@/components/form-field";
import { SelectField } from "@/components/select-field";
import { formatDay } from "@/lib/entitlements";
import { applyFieldErrors, needsAlert } from "@/lib/form-errors";
import { todayIn, useTenantTimeZoneOnceRead } from "@/lib/tenant-settings";
import {
  aliasKindLabels,
  authorityMessage,
  authorityPattern,
  identifierValueMaxLength,
  reasonMaxLength,
  validFromLaterMessage,
  type AliasKind,
  type NamedRecord,
  type NamesAndIdsApi,
  type NamesAndIdsConfig,
  type OpenedRecord,
  type RecordAlias,
  type RecordIdentifier,
} from "./names-and-ids-data";

export type DialogProps<R extends NamedRecord> = {
  record: R;
  config: NamesAndIdsConfig;
  api: NamesAndIdsApi<R>;
  onOpenChange: (open: boolean) => void;
  onSaved: (announcement: string) => void;
  onCloseAutoFocus?: (event: Event) => void;
};

const auditHint = "Kept in the audit trail with your name. Don't include personal details.";

/** The tenant's today, or empty until its time zone is read: a date isn't checked against UTC's (#111 R3). */
function useTenantToday() {
  const zone = useTenantTimeZoneOnceRead();
  return { today: zone.isLoading ? "" : todayIn(zone.timeZone), loading: zone.isLoading };
}

/** The dialog's basis (#110 R1), its change (keeping the record it answers) and its Reload, which closes it. */
function useChange<R extends NamedRecord, V>(
  { record, api, onOpenChange }: DialogProps<R>,
  call: (opened: OpenedRecord, variables: V) => Promise<R>,
) {
  const [opened] = useState<OpenedRecord>(() => ({ id: record.id, version: record.version }));
  const change = useMutation({ mutationFn: (variables: V) => call(opened, variables), onSuccess: api.keep });
  return {
    change,
    onReload: () => {
      void api.reload();
      onOpenChange(false);
    },
  };
}

type IdentifierValues = { authority: string; value: string; validFrom: string };
const identifierFields: Readonly<Record<string, keyof IdentifierValues>> = {
  authority: "authority",
  value: "value",
  validFrom: "validFrom",
};

const identifierSchema = (today: string) =>
  z.object({
    authority: z
      .string()
      .transform((value) => value.trim().toUpperCase())
      .refine((value) => authorityPattern.test(value), authorityMessage),
    value: z
      .string()
      .trim()
      .min(1, "Enter the identifier.")
      .max(identifierValueMaxLength, `Keep the identifier to ${identifierValueMaxLength} characters.`),
    validFrom: z.string().refine((value) => !today || value <= today, validFromLaterMessage),
  });

export function AddIdentifierDialog<R extends NamedRecord>(props: Readonly<DialogProps<R>>) {
  const { config, api, onOpenChange, onSaved, onCloseAutoFocus } = props;
  const { today, loading } = useTenantToday();
  const form = useForm<IdentifierValues, unknown, IdentifierValues>({
    resolver: zodResolver(identifierSchema(today)),
    defaultValues: { authority: "", value: "", validFrom: "" },
  });
  const { change: add, onReload } = useChange(props, api.addIdentifier);
  const errors = form.formState.errors;

  return (
    <CommandDialog
      title={`Add an external identifier for ${config.name}`}
      description={`A number another body gave the ${config.noun}, such as a registration number. Once given, a number is never given to another ${config.noun}.`}
      submitLabel="Add identifier"
      pending={add.isPending}
      error={add.error}
      showAlert={add.isError && needsAlert(add.error, identifierFields)}
      onSubmit={form.handleSubmit((values) =>
        add.mutate(
          { authority: values.authority, value: values.value, validFrom: values.validFrom || null },
          {
            onSuccess: () => onSaved("Identifier added."),
            onError: (error) => applyFieldErrors(error, identifierFields, form.setError),
          },
        ),
      )}
      onOpenChange={onOpenChange}
      onCloseAutoFocus={onCloseAutoFocus}
      onReload={onReload}
    >
      <FormField
        id="identifier-authority"
        label="Issued by (code)"
        hint={`Such as ${config.authorityExamples}.`}
        autoCapitalize="characters"
        autoComplete="off"
        error={errors.authority?.message}
        {...form.register("authority")}
      />
      <FormField
        id="identifier-value"
        label="Identifier"
        autoComplete="off"
        maxLength={identifierValueMaxLength}
        error={errors.value?.message}
        {...form.register("value")}
      />
      <FormField
        id="identifier-valid-from"
        label="Valid from (optional)"
        type="date"
        max={today || undefined}
        disabled={loading}
        error={errors.validFrom?.message}
        {...form.register("validFrom")}
      />
    </CommandDialog>
  );
}

type EndValues = { validTo: string; reason: string };
const endFields: Readonly<Record<string, keyof EndValues>> = { validTo: "validTo", reason: "reason" };

export function EndIdentifierDialog<R extends NamedRecord>(
  props: Readonly<DialogProps<R> & { identifier: RecordIdentifier }>,
) {
  const { config, api, identifier, onOpenChange, onSaved, onCloseAutoFocus } = props;
  const { today, loading } = useTenantToday();
  const from = identifier.validFrom;
  const schema = z.object({
    validTo: z
      .string()
      .min(1, "Enter the last day it was valid.")
      .refine((value) => !today || value <= today, "It can't end later than today.")
      .refine((value) => !from || value >= from, `It can't end before it started (${from ? formatDay(from) : ""}).`),
    reason: z.string().trim().max(reasonMaxLength, `Keep the reason under ${reasonMaxLength} characters.`),
  });
  const form = useForm<EndValues>({ resolver: zodResolver(schema), defaultValues: { validTo: today, reason: "" } });
  const { getFieldState, setValue } = form;
  useEffect(() => {
    // Once the zone is read, the end starts at its today, unless someone has already typed one.
    if (today && !getFieldState("validTo").isDirty) {
      setValue("validTo", today);
    }
  }, [today, getFieldState, setValue]);
  const { change: end, onReload } = useChange(props, (opened, body: { validTo: string; reason: string | null }) =>
    api.endIdentifier(opened, identifier.id, body),
  );
  const errors = form.formState.errors;

  return (
    <CommandDialog
      title={`End ${identifier.authority} ${identifier.value}`}
      description={`It stays on the ${config.noun}'s record as ended, and is never given to another ${config.noun}.`}
      submitLabel="End identifier"
      pending={end.isPending}
      error={end.error}
      showAlert={end.isError && needsAlert(end.error, endFields)}
      onSubmit={form.handleSubmit((values) =>
        end.mutate(
          { validTo: values.validTo, reason: values.reason.trim() || null },
          {
            onSuccess: () => onSaved("Identifier ended."),
            onError: (error) => applyFieldErrors(error, endFields, form.setError),
          },
        ),
      )}
      onOpenChange={onOpenChange}
      onCloseAutoFocus={onCloseAutoFocus}
      onReload={onReload}
    >
      <FormField
        id="identifier-valid-to"
        label="Last day valid"
        type="date"
        min={from ?? undefined}
        max={today || undefined}
        disabled={loading}
        error={errors.validTo?.message}
        {...form.register("validTo")}
      />
      <FormField
        id="identifier-end-reason"
        label="Reason (optional)"
        hint={auditHint}
        autoComplete="off"
        maxLength={reasonMaxLength}
        error={errors.reason?.message}
        {...form.register("reason")}
      />
    </CommandDialog>
  );
}

type AliasValues = { name: string; kind: string };
const aliasFields: Readonly<Record<string, keyof AliasValues>> = { name: "name", kind: "kind" };

export function AddAliasDialog<R extends NamedRecord>(props: Readonly<DialogProps<R>>) {
  const { config, api, onOpenChange, onSaved, onCloseAutoFocus } = props;
  const max = config.aliasNameMaxLength;
  const schema = z.object({
    name: z.string().trim().min(1, "Enter the name.").max(max, `Keep the name to ${max} characters.`),
    kind: z.string().min(1, "Choose what kind of name it is."),
  });
  const form = useForm<AliasValues>({ resolver: zodResolver(schema), defaultValues: { name: "", kind: "" } });
  const { change: add, onReload } = useChange(props, api.addAlias);
  const errors = form.formState.errors;

  return (
    <CommandDialog
      title={`Add an alias for ${config.name}`}
      description={`Another name the ${config.noun} is known by. The directory's search finds it by it.`}
      submitLabel="Add alias"
      pending={add.isPending}
      error={add.error}
      showAlert={add.isError && needsAlert(add.error, aliasFields)}
      onSubmit={form.handleSubmit((values) =>
        add.mutate(
          { name: values.name.trim(), kind: values.kind as AliasKind },
          {
            onSuccess: () => onSaved("Alias added."),
            onError: (error) => applyFieldErrors(error, aliasFields, form.setError),
          },
        ),
      )}
      onOpenChange={onOpenChange}
      onCloseAutoFocus={onCloseAutoFocus}
      onReload={onReload}
    >
      <FormField
        id="alias-name"
        label="Name"
        autoComplete="off"
        maxLength={max}
        error={errors.name?.message}
        {...form.register("name")}
      />
      <SelectField id="alias-kind" label="Kind" error={errors.kind?.message} {...form.register("kind")}>
        <option value="">Choose…</option>
        {config.aliasKinds.map((kind) => (
          <option key={kind} value={kind}>
            {aliasKindLabels[kind]}
          </option>
        ))}
      </SelectField>
    </CommandDialog>
  );
}

export function RemoveAliasDialog<R extends NamedRecord>(props: Readonly<DialogProps<R> & { alias: RecordAlias }>) {
  const { config, api, alias, onOpenChange, onSaved, onCloseAutoFocus } = props;
  const { change: remove, onReload } = useChange(props, (opened, reason: string) =>
    api.removeAlias(opened, alias.id, reason),
  );
  return (
    <ActionDialog
      open
      onOpenChange={onOpenChange}
      title={`Remove the alias ${alias.name}`}
      description={`${aliasKindLabels[alias.kind]}. The directory's search no longer finds ${config.name} by it; the record keeps it as removed.`}
      confirmLabel="Remove alias"
      destructive
      withReason
      pending={remove.isPending}
      error={remove.error}
      onConfirm={(reason) => remove.mutate(reason ?? "", { onSuccess: () => onSaved("Alias removed.") })}
      onReload={onReload}
      onCloseAutoFocus={onCloseAutoFocus}
    />
  );
}
