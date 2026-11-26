"use client";

import type { ReactNode } from "react";
import { Controller, type UseFormReturn } from "react-hook-form";
import { CheckboxGroup } from "@/components/checkbox-group";
import { FormField } from "@/components/form-field";
import { SelectField } from "@/components/select-field";
import { humanizeCode } from "@/lib/codes";
import { useCountries, useTimeZones } from "@/lib/reference-data";
import { useSportVariants } from "../sports/sports-data";
import { limits, type VenueValues } from "./venue-form";
import { venueSurfaces, venueTypes } from "./venues-data";

function Section({ title, children }: Readonly<{ title: string; children: ReactNode }>) {
  return (
    <fieldset className="grid gap-4 rounded-md border p-4">
      <legend className="px-1 font-semibold">{title}</legend>
      {children}
    </fieldset>
  );
}

/**
 * The venue's details, Add venue's and Edit details' (S07 D3): what it is, where it is and the sports it supports.
 * The sports are every published variant, named from their definitions (AR-29): a ground can host a sport the
 * organisation doesn't run yet. `edit` adds the web address (D9: Add leaves it to the server).
 */
export function VenueFields({
  form,
  edit = false,
  warning,
}: Readonly<{
  form: UseFormReturn<VenueValues>;
  edit?: boolean;
  /** The duplicate warning, shown under the name and place it compares (D13). */
  warning?: ReactNode;
}>) {
  const countries = useCountries();
  const timeZones = useTimeZones();
  const sports = useSportVariants();
  const { register, control, getValues } = form;
  const errors = form.formState.errors;

  // A saved sport or zone the lists don't hold (not loaded, or no longer offered) stays a choice, so it isn't dropped.
  const savedVariants = getValues("supportedVariants");
  const sportOptions = (sports.data?.items ?? []).map((item) => ({ value: item.variant, label: item.name }));
  for (const variant of savedVariants) {
    if (!sportOptions.some((option) => option.value === variant)) {
      sportOptions.push({ value: variant, label: humanizeCode(variant) });
    }
  }
  const zones = (timeZones.data?.items ?? []).map((zone) => zone.code);
  const savedZone = getValues("timeZone");
  if (savedZone && !zones.includes(savedZone)) {
    zones.unshift(savedZone);
  }

  return (
    <div className="grid gap-5">
      <Section title="The venue">
        <FormField
          id="venue-name"
          label="Name"
          autoComplete="off"
          maxLength={limits.name}
          error={errors.name?.message}
          {...register("name")}
        />
        <div className="grid gap-4 tablet:grid-cols-2">
          <FormField
            id="venue-short-name"
            label="Short name (optional)"
            hint="For tight spaces such as fixtures lists."
            autoComplete="off"
            maxLength={limits.shortName}
            error={errors.shortName?.message}
            {...register("shortName")}
          />
          <SelectField id="venue-type" label="Type" error={errors.type?.message} {...register("type")}>
            <option value="">Choose…</option>
            {venueTypes.map((type) => (
              <option key={type} value={type}>
                {humanizeCode(type)}
              </option>
            ))}
          </SelectField>
        </div>
        <FormField
          id="venue-capacity"
          label="Capacity (optional)"
          hint="Seats or places for spectators."
          inputMode="numeric"
          autoComplete="off"
          error={errors.capacity?.message}
          {...register("capacity")}
        />
        {edit ? (
          <FormField
            id="venue-slug"
            label="Web address"
            hint="The last part of the venue's address on the public site, such as bukoto-community-stadium."
            autoComplete="off"
            autoCapitalize="none"
            maxLength={limits.slug.max}
            error={errors.slug?.message}
            {...register("slug")}
          />
        ) : null}
      </Section>

      <Section title="Where it is">
        <FormField
          id="venue-address"
          label="Address (optional)"
          autoComplete="off"
          maxLength={limits.addressLine}
          error={errors.addressLine?.message}
          {...register("addressLine")}
        />
        <div className="grid gap-4 tablet:grid-cols-2">
          <FormField
            id="venue-city"
            label="City or town"
            autoComplete="off"
            maxLength={limits.city}
            error={errors.city?.message}
            {...register("city")}
          />
          <FormField
            id="venue-region"
            label="Region (optional)"
            autoComplete="off"
            maxLength={limits.region}
            error={errors.region?.message}
            {...register("region")}
          />
          <SelectField id="venue-country" label="Country" error={errors.country?.message} {...register("country")}>
            {edit ? null : <option value="">Your organisation&apos;s country</option>}
            {(countries.data?.items ?? []).map((country) => (
              <option key={country.code} value={country.code}>
                {country.name}
              </option>
            ))}
          </SelectField>
          <SelectField
            id="venue-time-zone"
            label="Time zone"
            hint="Contests here are shown in this time zone."
            error={errors.timeZone?.message}
            {...register("timeZone")}
          >
            <option value="">Choose…</option>
            {zones.map((zone) => (
              <option key={zone} value={zone}>
                {zone}
              </option>
            ))}
          </SelectField>
          <FormField
            id="venue-latitude"
            label="Latitude (optional)"
            hint="Decimal degrees, such as 0.3476."
            inputMode="decimal"
            autoComplete="off"
            error={errors.latitude?.message}
            {...register("latitude")}
          />
          <FormField
            id="venue-longitude"
            label="Longitude (optional)"
            hint="Decimal degrees, such as 32.5925."
            inputMode="decimal"
            autoComplete="off"
            error={errors.longitude?.message}
            {...register("longitude")}
          />
        </div>
      </Section>

      {warning}

      <Section title="Playing areas">
        <Controller
          control={control}
          name="surfaces"
          render={({ field, fieldState }) => (
            <CheckboxGroup
              id="venue-surfaces"
              legend="Surfaces (optional)"
              hint={`Up to ${limits.surfaces}.`}
              options={venueSurfaces.map((surface) => ({ value: surface, label: humanizeCode(surface) }))}
              value={field.value}
              onChange={field.onChange}
              error={fieldState.error?.message}
            />
          )}
        />
        <Controller
          control={control}
          name="supportedVariants"
          render={({ field, fieldState }) => (
            <CheckboxGroup
              id="venue-sports"
              legend="Sports it can host (optional)"
              hint="Any published sport, whether or not your organisation runs it yet."
              options={sportOptions}
              value={field.value}
              onChange={field.onChange}
              error={fieldState.error?.message}
              empty={sports.isLoading ? "Loading the sports…" : "No sport is published yet."}
            />
          )}
        />
      </Section>
    </div>
  );
}
