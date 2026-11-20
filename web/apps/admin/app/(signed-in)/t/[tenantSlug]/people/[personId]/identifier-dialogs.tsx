"use client";

// External identifiers and aliases (S06 D10, T3b): add an identifier (the API refuses a value any person has or had,
// and the national identity documents ADR-0015 keeps out), end one (it stays listed and is never reused), add an alias
// and remove one with a reason. Each dialog keeps the version it opened on (#110 R1).
import { zodResolver } from "@hookform/resolvers/zod";
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
import { authorityMessage, authorityPattern, limits, validFromLaterMessage } from "../new/person-form";
import type { Person } from "../people-data";
import {
  aliasKinds,
  useAddAlias,
  useAddIdentifier,
  useEndIdentifier,
  useRemoveAlias,
  type AliasKind,
} from "./identity-data";
import { useReloadPerson, type OpenedPerson } from "./profile-data";

type Identifier = Person["externalIdentifiers"][number];
type Alias = Person["aliases"][number];

type DialogProps = {
  person: Person;
  onOpenChange: (open: boolean) => void;
  onSaved: (announcement: string) => void;
  onCloseAutoFocus?: (event: Event) => void;
};

const reasonMaxLength = 500;
/** An alias's name (PersonAlias on the server). */
const aliasNameMaxLength = 200;
const auditHint = "Kept in the audit trail with your name. Don't include personal details.";

/** The tenant's today (D13), or empty until its time zone is read: a date isn't checked against UTC's (#111 R3). */
function useTenantToday() {
  const zone = useTenantTimeZoneOnceRead();
  return { today: zone.isLoading ? "" : todayIn(zone.timeZone), loading: zone.isLoading };
}

/** The dialog's basis (#110 R1) and its Reload, which reads the person again and closes it. */
function useOpened(person: Person, onOpenChange: (open: boolean) => void) {
  const [opened] = useState<OpenedPerson>(() => ({ id: person.id, version: person.version }));
  const reload = useReloadPerson(person.id);
  return {
    opened,
    onReload: () => {
      void reload();
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
      .max(limits.identifierValue, `Keep the identifier to ${limits.identifierValue} characters.`),
    validFrom: z.string().refine((value) => !today || value <= today, validFromLaterMessage),
  });

export function AddIdentifierDialog({ person, onOpenChange, onSaved, onCloseAutoFocus }: Readonly<DialogProps>) {
  const { today, loading } = useTenantToday();
  const form = useForm<IdentifierValues, unknown, IdentifierValues>({
    resolver: zodResolver(identifierSchema(today)),
    defaultValues: { authority: "", value: "", validFrom: "" },
  });
  const { opened, onReload } = useOpened(person, onOpenChange);
  const add = useAddIdentifier(opened);
  const errors = form.formState.errors;

  return (
    <CommandDialog
      title={`Add an external identifier for ${person.displayName}`}
      description="A number another body gave them, such as a registration or admission number. Once given, a number is never given to anyone else."
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
        hint="Such as KMSA_REG_NO or SCHOOL_ADMISSION_NO. National ID and passport numbers aren't kept."
        autoCapitalize="characters"
        autoComplete="off"
        error={errors.authority?.message}
        {...form.register("authority")}
      />
      <FormField
        id="identifier-value"
        label="Identifier"
        autoComplete="off"
        maxLength={limits.identifierValue}
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

export function EndIdentifierDialog({
  person,
  identifier,
  onOpenChange,
  onSaved,
  onCloseAutoFocus,
}: Readonly<DialogProps & { identifier: Identifier }>) {
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
  const { opened, onReload } = useOpened(person, onOpenChange);
  const end = useEndIdentifier(opened, identifier.id);
  const errors = form.formState.errors;

  return (
    <CommandDialog
      title={`End ${identifier.authority} ${identifier.value}`}
      description="It stays on the person's record as ended, and is never given to anyone else."
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
const aliasSchema = z.object({
  name: z
    .string()
    .trim()
    .min(1, "Enter the name.")
    .max(aliasNameMaxLength, `Keep the name to ${aliasNameMaxLength} characters.`),
  kind: z.string().min(1, "Choose what kind of name it is."),
});

/** What each kind means, as the choice says it. */
export const aliasKindLabels: Readonly<Record<AliasKind, string>> = {
  FORMER_NAME: "Former name",
  NICKNAME: "Nickname",
  SPELLING: "Another spelling",
};

export function AddAliasDialog({ person, onOpenChange, onSaved, onCloseAutoFocus }: Readonly<DialogProps>) {
  const form = useForm<AliasValues>({ resolver: zodResolver(aliasSchema), defaultValues: { name: "", kind: "" } });
  const { opened, onReload } = useOpened(person, onOpenChange);
  const add = useAddAlias(opened);
  const errors = form.formState.errors;

  return (
    <CommandDialog
      title={`Add an alias for ${person.displayName}`}
      description="Another name they are known by. People's search finds them by it."
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
        maxLength={aliasNameMaxLength}
        error={errors.name?.message}
        {...form.register("name")}
      />
      <SelectField id="alias-kind" label="Kind" error={errors.kind?.message} {...form.register("kind")}>
        <option value="">Choose…</option>
        {aliasKinds.map((kind) => (
          <option key={kind} value={kind}>
            {aliasKindLabels[kind]}
          </option>
        ))}
      </SelectField>
    </CommandDialog>
  );
}

export function RemoveAliasDialog({
  person,
  alias,
  onOpenChange,
  onSaved,
  onCloseAutoFocus,
}: Readonly<DialogProps & { alias: Alias }>) {
  const { opened, onReload } = useOpened(person, onOpenChange);
  const remove = useRemoveAlias(opened, alias.id);
  return (
    <ActionDialog
      open
      onOpenChange={onOpenChange}
      title={`Remove the alias ${alias.name}`}
      description={`${aliasKindLabels[alias.kind]}. People's search no longer finds ${person.displayName} by it; the record keeps it as removed.`}
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
