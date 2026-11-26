"use client";

import { Label } from "@msp/ui/components/label";
import { Textarea } from "@msp/ui/components/textarea";
import type { ReactNode } from "react";
import { Controller, type UseFormReturn } from "react-hook-form";
import { FormField } from "@/components/form-field";
import { SelectField } from "@/components/select-field";
import { humanizeCode } from "@/lib/codes";
import { useCountries } from "@/lib/reference-data";
import { limits, type OrganisationValues } from "./organisation-form";
import { organisationTypes } from "./organisations-data";
import { PrimaryVenueField } from "./primary-venue-field";

function Section({ title, children }: Readonly<{ title: string; children: ReactNode }>) {
  return (
    <fieldset className="grid gap-4 rounded-md border p-4">
      <legend className="px-1 font-semibold">{title}</legend>
      {children}
    </fieldset>
  );
}

/**
 * The organisation's details, Add organisation's and Edit details' (S07 D4): its names and type, where it is, and its
 * own contacts (not a person's: people are in the person registry). `edit` adds the web address (D9: Add leaves it to
 * the server). The duplicate warning goes under the names, type and city it compares.
 */
export function OrganisationFields({
  form,
  edit = false,
  savedVenue,
  warning,
}: Readonly<{
  form: UseFormReturn<OrganisationValues>;
  edit?: boolean;
  /** Edit: the organisation's primary venue as saved. */
  savedVenue?: { id: string; name: string } | null;
  warning?: ReactNode;
}>) {
  const countries = useCountries();
  const { register, control, getValues } = form;
  const errors = form.formState.errors;

  // A saved country the list doesn't hold (not loaded, or no longer offered) stays a choice, so it isn't dropped.
  const countryOptions = (countries.data?.items ?? []).map((country) => ({ code: country.code, name: country.name }));
  const savedCountry = getValues("country");
  if (savedCountry && !countryOptions.some((country) => country.code === savedCountry)) {
    countryOptions.unshift({ code: savedCountry, name: savedCountry });
  }
  const descriptionError = errors.description?.message;

  return (
    <div className="grid gap-5">
      <Section title="The organisation">
        <FormField
          id="organisation-display-name"
          label="Name"
          hint="As it is usually written, such as Ntinda United FC."
          autoComplete="off"
          maxLength={limits.displayName}
          error={errors.displayName?.message}
          {...register("displayName")}
        />
        <div className="grid gap-4 tablet:grid-cols-2">
          <FormField
            id="organisation-legal-name"
            label="Legal name (optional)"
            hint="As registered, when it differs."
            autoComplete="off"
            maxLength={limits.legalName}
            error={errors.legalName?.message}
            {...register("legalName")}
          />
          <FormField
            id="organisation-short-name"
            label="Short name (optional)"
            hint="For tight spaces such as fixtures lists."
            autoComplete="off"
            maxLength={limits.shortName}
            error={errors.shortName?.message}
            {...register("shortName")}
          />
          <SelectField id="organisation-type" label="Type" error={errors.type?.message} {...register("type")}>
            <option value="">Choose…</option>
            {organisationTypes.map((type) => (
              <option key={type} value={type}>
                {humanizeCode(type)}
              </option>
            ))}
          </SelectField>
          <FormField
            id="organisation-founded"
            label="Founded (optional)"
            hint="The year, such as 1998."
            inputMode="numeric"
            autoComplete="off"
            maxLength={4}
            error={errors.foundedYear?.message}
            {...register("foundedYear")}
          />
        </div>
        {edit ? (
          <FormField
            id="organisation-slug"
            label="Web address"
            hint="The last part of the organisation's address on the public site, such as ntinda-united-fc."
            autoComplete="off"
            autoCapitalize="none"
            error={errors.slug?.message}
            {...register("slug")}
          />
        ) : null}
      </Section>

      <Section title="Where it is">
        <div className="grid gap-4 tablet:grid-cols-2">
          <FormField
            id="organisation-city"
            label="City or town (optional)"
            autoComplete="off"
            maxLength={limits.city}
            error={errors.city?.message}
            {...register("city")}
          />
          <SelectField
            id="organisation-country"
            label="Country"
            error={errors.country?.message}
            {...register("country")}
          >
            {edit ? null : <option value="">Your organisation&apos;s country</option>}
            {countryOptions.map((country) => (
              <option key={country.code} value={country.code}>
                {country.name}
              </option>
            ))}
          </SelectField>
        </div>
        <Controller
          control={control}
          name="primaryVenueId"
          render={({ field, fieldState }) => (
            <PrimaryVenueField
              value={field.value}
              onChange={field.onChange}
              saved={savedVenue}
              error={fieldState.error?.message}
            />
          )}
        />
      </Section>

      {warning}

      <Section title="Contact and description">
        <div className="grid gap-4 tablet:grid-cols-2">
          <FormField
            id="organisation-website"
            label="Website (optional)"
            type="url"
            inputMode="url"
            autoComplete="off"
            autoCapitalize="none"
            maxLength={limits.website}
            error={errors.website?.message}
            {...register("website")}
          />
          <FormField
            id="organisation-email"
            label="Email (optional)"
            hint="The organisation's own address, such as its office's."
            type="email"
            autoComplete="off"
            autoCapitalize="none"
            maxLength={limits.email}
            error={errors.email?.message}
            {...register("email")}
          />
          <FormField
            id="organisation-phone"
            label="Phone (optional)"
            hint="The organisation's own number."
            type="tel"
            autoComplete="off"
            maxLength={limits.phone}
            error={errors.phone?.message}
            {...register("phone")}
          />
        </div>
        <div className="grid gap-2">
          <Label htmlFor="organisation-description">Description (optional)</Label>
          <Textarea
            id="organisation-description"
            rows={5}
            maxLength={limits.description}
            aria-invalid={descriptionError ? true : undefined}
            aria-describedby={descriptionError ? "organisation-description-error" : "organisation-description-hint"}
            {...register("description")}
          />
          {descriptionError ? (
            <p id="organisation-description-error" className="text-sm text-destructive">
              {descriptionError}
            </p>
          ) : (
            <p id="organisation-description-hint" className="text-sm text-muted-foreground">
              Plain text, up to {limits.description.toLocaleString("en-GB")} characters. It is shown on the public site.
            </p>
          )}
        </div>
      </Section>
    </div>
  );
}
