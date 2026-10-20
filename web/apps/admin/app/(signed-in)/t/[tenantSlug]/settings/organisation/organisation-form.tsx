"use client";

import { zodResolver } from "@hookform/resolvers/zod";
import type { components } from "@msp/api-client";
import { Button } from "@msp/ui/components/button";
import { Checkbox } from "@msp/ui/components/checkbox";
import { Label } from "@msp/ui/components/label";
import { useMutation, useQueryClient } from "@tanstack/react-query";
import { useEffect } from "react";
import { Controller, useForm, useWatch } from "react-hook-form";
import { z } from "zod";
import { ConflictAlert } from "@/components/conflict-alert";
import { FormField } from "@/components/form-field";
import { ProblemAlert } from "@/components/problem-alert";
import { SelectField } from "@/components/select-field";
import { call } from "@/lib/api";
import { humanizeCode } from "@/lib/codes";
import { ifMatch, isConcurrencyConflict } from "@/lib/concurrency";
import { applyFieldErrors, needsAlert } from "@/lib/form-errors";
import { useHydrated } from "@/lib/hydrated";
import { useCurrencies, useTimeZones } from "@/lib/reference-data";
import { settingsQueryKey } from "@/lib/tenant-settings";
import { useTenantWorkspace } from "@/lib/tenant-workspace";

type Settings = components["schemas"]["TenantSettingsResponse"];
type Consent = components["schemas"]["MinorRegistrationConsent"];

// TenantSettingsValues on the server: 5–180 minutes.
const startingSoon = { min: 5, max: 180 };
const consents: readonly Consent[] = ["REQUIRED", "RECOMMENDED", "OFF"];

const schema = z.object({
  timeZone: z.string().min(1, "Choose a time zone."),
  defaultLocale: z.string().trim().min(1, "Enter a language code, such as en or en-UG."),
  currencyCode: z.string().min(1, "Choose a currency."),
  publicEnabled: z.boolean(),
  startingSoonMinutes: z
    .number({ error: `Enter a number of minutes from ${startingSoon.min} to ${startingSoon.max}.` })
    .int(`Enter whole minutes.`)
    .min(startingSoon.min, `Enter at least ${startingSoon.min} minutes.`)
    .max(startingSoon.max, `Enter at most ${startingSoon.max} minutes.`),
  minorRegistrationConsent: z.enum(["REQUIRED", "RECOMMENDED", "OFF"]),
});
type Values = z.infer<typeof schema>;
type Field = keyof Values;

// The API names the fields as the form does.
const fields: Readonly<Record<Field, Field>> = {
  timeZone: "timeZone",
  defaultLocale: "defaultLocale",
  currencyCode: "currencyCode",
  publicEnabled: "publicEnabled",
  startingSoonMinutes: "startingSoonMinutes",
  minorRegistrationConsent: "minorRegistrationConsent",
};

function toValues(settings: Settings): Values {
  return {
    timeZone: settings.timeZone,
    defaultLocale: settings.defaultLocale,
    currencyCode: settings.currencyCode,
    publicEnabled: settings.publicEnabled,
    startingSoonMinutes: settings.startingSoonMinutes,
    minorRegistrationConsent: settings.minorRegistrationConsent,
  };
}

/** Only the settings that changed are sent: someone else's change to another setting is not overwritten. */
export function changedSettings(before: Settings, values: Values): Partial<Values> {
  const original = toValues(before);
  return Object.fromEntries(
    (Object.keys(values) as Field[]).filter((key) => values[key] !== original[key]).map((key) => [key, values[key]]),
  );
}

const consentHints: Readonly<Record<Consent, string>> = {
  REQUIRED: "A guardian's consent is needed before a minor is registered.",
  RECOMMENDED: "Registration asks for a guardian's consent but doesn't wait for it.",
  OFF: "Registration doesn't ask for a guardian's consent.",
};

export function OrganisationForm({
  settings,
  editable,
  onReload,
}: Readonly<{ settings: Settings; editable: boolean; onReload: () => void }>) {
  const hydrated = useHydrated();
  const { tenant, client } = useTenantWorkspace();
  const queryClient = useQueryClient();
  const timeZones = useTimeZones();
  const currencies = useCurrencies();

  const form = useForm<Values>({ resolver: zodResolver(schema), defaultValues: toValues(settings) });
  const { reset } = form;
  useEffect(() => reset(toValues(settings)), [settings, reset]);

  const save = useMutation({
    mutationFn: (values: Values) =>
      call(
        client.PATCH("/api/admin/v1/tenant/settings", {
          params: { header: ifMatch(settings.version) },
          body: changedSettings(settings, values),
        }),
      ),
    onSuccess: (saved) => queryClient.setQueryData(settingsQueryKey(tenant.tenantId), saved),
    onError: (error) => applyFieldErrors(error, fields, form.setError),
  });

  const { errors, isDirty } = form.formState;
  const consent = useWatch({ control: form.control, name: "minorRegistrationConsent" });
  // Until the lists load, each shows the saved value alone.
  const zoneOptions = timeZones.data?.items ?? [{ code: settings.timeZone }];
  const currencyOptions = currencies.data?.items ?? [{ code: settings.currencyCode, name: settings.currencyCode }];
  const reload = () => {
    save.reset();
    onReload();
  };

  return (
    <form
      className="grid gap-5"
      noValidate
      onSubmit={form.handleSubmit((values) => save.mutate(values))}
      aria-describedby={editable ? undefined : "read-only-note"}
    >
      {editable ? null : (
        <p id="read-only-note" className="rounded-md border bg-muted p-3 text-sm">
          You can see these settings but not change them. An owner or administrator of {tenant.displayName} can.
        </p>
      )}
      {isConcurrencyConflict(save.error) ? (
        <ConflictAlert onReload={reload} />
      ) : save.isError && needsAlert(save.error, fields) ? (
        <ProblemAlert error={save.error} />
      ) : null}
      {timeZones.isError || currencies.isError ? (
        <ProblemAlert error={timeZones.error ?? currencies.error} title="The lists of choices didn't load" />
      ) : null}

      <fieldset disabled={!editable} className="grid gap-5">
        <legend className="sr-only">Organisation settings</legend>
        <SelectField
          id="timeZone"
          label="Time zone"
          hint="Dates and times are shown in this zone."
          error={errors.timeZone?.message}
          {...form.register("timeZone")}
        >
          {zoneOptions.map((zone) => (
            <option key={zone.code} value={zone.code}>
              {zone.code}
            </option>
          ))}
        </SelectField>
        <FormField
          id="defaultLocale"
          label="Language"
          hint="A language code, with a country if needed: en, en-UG, sw."
          error={errors.defaultLocale?.message}
          {...form.register("defaultLocale")}
        />
        <SelectField
          id="currencyCode"
          label="Currency"
          error={errors.currencyCode?.message}
          {...form.register("currencyCode")}
        >
          {currencyOptions.map((currency) => (
            <option key={currency.code} value={currency.code}>
              {currency.code === currency.name ? currency.code : `${currency.code} — ${currency.name}`}
            </option>
          ))}
        </SelectField>
        <FormField
          id="startingSoonMinutes"
          label="“Starting soon” notice (minutes)"
          type="number"
          inputMode="numeric"
          min={startingSoon.min}
          max={startingSoon.max}
          hint={`How long before a contest the public site says it is starting soon: ${startingSoon.min} to ${startingSoon.max}.`}
          error={errors.startingSoonMinutes?.message}
          {...form.register("startingSoonMinutes", { valueAsNumber: true })}
        />
        <SelectField
          id="minorRegistrationConsent"
          label="Guardian consent for minors"
          hint={consentHints[consent]}
          error={errors.minorRegistrationConsent?.message}
          {...form.register("minorRegistrationConsent")}
        >
          {consents.map((value) => (
            <option key={value} value={value}>
              {humanizeCode(value)}
            </option>
          ))}
        </SelectField>
        <div className="flex min-h-11 items-start gap-3">
          <Controller
            control={form.control}
            name="publicEnabled"
            render={({ field }) => (
              <Checkbox
                id="publicEnabled"
                className="mt-0.5"
                checked={field.value}
                onCheckedChange={(checked) => field.onChange(checked === true)}
                onBlur={field.onBlur}
                aria-describedby="publicEnabled-hint"
              />
            )}
          />
          <div className="grid gap-1">
            <Label htmlFor="publicEnabled">Public site</Label>
            <p id="publicEnabled-hint" className="text-sm text-muted-foreground">
              Fixtures, results and standings are shown on the public site while the organisation is active.
            </p>
          </div>
        </div>
      </fieldset>

      {editable ? (
        <div className="flex flex-wrap items-center gap-3">
          <Button type="submit" disabled={!hydrated || !isDirty || save.isPending}>
            {save.isPending ? "Saving…" : "Save changes"}
          </Button>
          <p role="status" className="text-sm text-muted-foreground">
            {save.isSuccess && !isDirty ? "Saved." : ""}
          </p>
        </div>
      ) : null}
    </form>
  );
}
