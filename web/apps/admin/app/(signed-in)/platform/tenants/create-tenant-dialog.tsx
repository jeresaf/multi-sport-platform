"use client";

import { zodResolver } from "@hookform/resolvers/zod";
import { errorMessage } from "@msp/api-client/errors";
import { Button } from "@msp/ui/components/button";
import { Checkbox } from "@msp/ui/components/checkbox";
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from "@msp/ui/components/dialog";
import { Label } from "@msp/ui/components/label";
import { useMutation } from "@tanstack/react-query";
import { useRouter } from "next/navigation";
import { Controller, useForm } from "react-hook-form";
import { z } from "zod";
import { FormField } from "@/components/form-field";
import { ProblemAlert } from "@/components/problem-alert";
import { SelectField } from "@/components/select-field";
import { api, call, isApiProblem } from "@/lib/api";
import { applyFieldErrors, needsAlert } from "@/lib/form-errors";
import { useCountries, useCurrencies, useTimeZones } from "@/lib/reference-data";
import { platformPath } from "@/lib/workspace";
import { useTenantSaved } from "./tenants-data";

// TenantSlug and TenantProfile on the server; the server checks the reserved words and the reference data.
const slugPattern = /^[a-z0-9](?:[a-z0-9-]{1,38}[a-z0-9])$/;
const legalNameMax = 200;
const displayNameMax = 100;

const schema = z.object({
  slug: z
    .string()
    .trim()
    .regex(slugPattern, "Use 3 to 40 lower-case letters, digits and hyphens, starting and ending with a letter or digit."),
  displayName: z
    .string()
    .trim()
    .min(1, "Enter the name people know it by.")
    .max(displayNameMax, `Keep the name under ${displayNameMax} characters.`),
  legalName: z
    .string()
    .trim()
    .min(1, "Enter its registered name.")
    .max(legalNameMax, `Keep the legal name under ${legalNameMax} characters.`),
  countryCode: z.string().min(1, "Choose a country."),
  timeZone: z.string().min(1, "Choose a time zone."),
  defaultLocale: z.string().trim().min(1, "Enter a language code, such as en or en-UG."),
  currencyCode: z.string().min(1, "Choose a currency."),
  publicEnabled: z.boolean(),
});
type Values = z.infer<typeof schema>;
type Field = keyof Values;

// The API names the fields as the form does.
const fields: Readonly<Record<Field, Field>> = {
  slug: "slug",
  displayName: "displayName",
  legalName: "legalName",
  countryCode: "countryCode",
  timeZone: "timeZone",
  defaultLocale: "defaultLocale",
  currencyCode: "currencyCode",
  publicEnabled: "publicEnabled",
};

const defaultValues: Values = {
  slug: "",
  displayName: "",
  legalName: "",
  countryCode: "",
  timeZone: "",
  defaultLocale: "en",
  currencyCode: "",
  publicEnabled: false,
};

/** Creates a tenant; it starts PROVISIONING, and its detail page opens to invite its owner and activate it. */
export function CreateTenantDialog({ onOpenChange }: Readonly<{ onOpenChange: (open: boolean) => void }>) {
  const router = useRouter();
  const saved = useTenantSaved();
  const countries = useCountries();
  const timeZones = useTimeZones();
  const currencies = useCurrencies();
  const form = useForm<Values>({ resolver: zodResolver(schema), defaultValues });

  const create = useMutation({
    mutationFn: (values: Values) => call(api.POST("/api/platform/v1/tenants", { body: values })),
    onSuccess: async (tenant) => {
      await saved(tenant);
      router.push(`${platformPath}/tenants/${tenant.id}`);
    },
    onError: (error) => {
      applyFieldErrors(error, fields, form.setError);
      // Slugs are never reused: a fact about the slug, so it goes under the slug.
      if (isApiProblem(error, "TENANT_SLUG_TAKEN")) {
        form.setError("slug", { type: "server", message: errorMessage("TENANT_SLUG_TAKEN") });
      }
    },
  });

  const { errors } = form.formState;
  const alert = create.isError && !isApiProblem(create.error, "TENANT_SLUG_TAKEN") && needsAlert(create.error, fields);
  const listsFailed = countries.isError || timeZones.isError || currencies.isError;

  return (
    <Dialog open onOpenChange={(open) => !create.isPending && onOpenChange(open)}>
      <DialogContent className="max-h-[90vh] overflow-y-auto">
        <form className="grid gap-4" noValidate onSubmit={form.handleSubmit((values) => create.mutate(values))}>
          <DialogHeader>
            <DialogTitle>New tenant</DialogTitle>
            <DialogDescription>
              It starts as provisioning: invite its owner, then activate it when it is ready. The slug can&apos;t be
              changed later.
            </DialogDescription>
          </DialogHeader>
          {alert ? <ProblemAlert error={create.error} /> : null}
          {listsFailed ? (
            <ProblemAlert
              error={countries.error ?? timeZones.error ?? currencies.error}
              title="The lists of choices didn't load"
            />
          ) : null}
          <FormField
            id="tenant-slug"
            label="Slug"
            autoComplete="off"
            hint="Its address on the platform, such as kmsa."
            error={errors.slug?.message}
            {...form.register("slug")}
          />
          <FormField
            id="tenant-displayName"
            label="Name"
            autoComplete="off"
            error={errors.displayName?.message}
            {...form.register("displayName")}
          />
          <FormField
            id="tenant-legalName"
            label="Legal name"
            autoComplete="off"
            error={errors.legalName?.message}
            {...form.register("legalName")}
          />
          <SelectField
            id="tenant-countryCode"
            label="Country"
            error={errors.countryCode?.message}
            {...form.register("countryCode")}
          >
            <option value="">Choose a country</option>
            {(countries.data?.items ?? []).map((country) => (
              <option key={country.code} value={country.code}>
                {country.name}
              </option>
            ))}
          </SelectField>
          <SelectField
            id="tenant-timeZone"
            label="Time zone"
            error={errors.timeZone?.message}
            {...form.register("timeZone")}
          >
            <option value="">Choose a time zone</option>
            {(timeZones.data?.items ?? []).map((zone) => (
              <option key={zone.code} value={zone.code}>
                {zone.code}
              </option>
            ))}
          </SelectField>
          <FormField
            id="tenant-defaultLocale"
            label="Language"
            hint="A language code, with a country if needed: en, en-UG, sw."
            error={errors.defaultLocale?.message}
            {...form.register("defaultLocale")}
          />
          <SelectField
            id="tenant-currencyCode"
            label="Currency"
            error={errors.currencyCode?.message}
            {...form.register("currencyCode")}
          >
            <option value="">Choose a currency</option>
            {(currencies.data?.items ?? []).map((currency) => (
              <option key={currency.code} value={currency.code}>
                {`${currency.code} — ${currency.name}`}
              </option>
            ))}
          </SelectField>
          <div className="flex min-h-11 items-start gap-3">
            <Controller
              control={form.control}
              name="publicEnabled"
              render={({ field }) => (
                <Checkbox
                  id="tenant-publicEnabled"
                  className="mt-0.5"
                  checked={field.value}
                  onCheckedChange={(checked) => field.onChange(checked === true)}
                  onBlur={field.onBlur}
                  aria-describedby="tenant-publicEnabled-hint"
                />
              )}
            />
            <div className="grid gap-1">
              <Label htmlFor="tenant-publicEnabled">Public site</Label>
              <p id="tenant-publicEnabled-hint" className="text-sm text-muted-foreground">
                Shown to the public once the tenant is active. Its owner can change this later.
              </p>
            </div>
          </div>
          <DialogFooter>
            <Button variant="outline" disabled={create.isPending} onClick={() => onOpenChange(false)}>
              Cancel
            </Button>
            <Button type="submit" disabled={create.isPending}>
              {create.isPending ? "Creating…" : "Create tenant"}
            </Button>
          </DialogFooter>
        </form>
      </DialogContent>
    </Dialog>
  );
}
