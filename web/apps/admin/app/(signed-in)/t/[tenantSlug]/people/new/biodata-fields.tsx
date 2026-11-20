"use client";

// The biodata's fields (S06 D2), shared by Add person (its first two steps) and Edit details: the names, then the
// other details. Either form holds at least the biodata's values (`BiodataValues`).
import type { FieldErrors, Path, UseFormReturn } from "react-hook-form";
import { FormField } from "@/components/form-field";
import { SelectField } from "@/components/select-field";
import type { BiodataValues } from "./person-form";

type Country = { code: string; name: string };

function useFields<T extends BiodataValues>(form: UseFormReturn<T>) {
  return {
    register: (name: keyof BiodataValues) => form.register(name as Path<T>),
    errors: form.formState.errors as FieldErrors<BiodataValues>,
  };
}

/** First, last, middle and other names. */
export function NameFields<T extends BiodataValues>({ form }: Readonly<{ form: UseFormReturn<T> }>) {
  const { register, errors } = useFields(form);
  return (
    <div className="grid items-start gap-4 tablet:grid-cols-2">
      <FormField
        id="firstName"
        label="First name"
        autoComplete="off"
        error={errors.firstName?.message}
        {...register("firstName")}
      />
      <FormField
        id="lastName"
        label="Last name"
        autoComplete="off"
        error={errors.lastName?.message}
        {...register("lastName")}
      />
      <FormField
        id="middleName"
        label="Middle name (optional)"
        autoComplete="off"
        error={errors.middleName?.message}
        {...register("middleName")}
      />
      <FormField
        id="otherNames"
        label="Other names (optional)"
        hint="A clan name, say."
        autoComplete="off"
        error={errors.otherNames?.message}
        {...register("otherNames")}
      />
    </div>
  );
}

/** Display name, gender, date of birth, nationalities, height and weight. */
export function DetailFields<T extends BiodataValues>({
  form,
  countries,
  displayNameHint,
}: Readonly<{ form: UseFormReturn<T>; countries: readonly Country[]; displayNameHint: string }>) {
  const { register, errors } = useFields(form);
  return (
    <div className="grid items-start gap-4 tablet:grid-cols-2">
      <FormField
        id="displayName"
        label="Display name (optional)"
        hint={displayNameHint}
        autoComplete="off"
        error={errors.displayName?.message}
        {...register("displayName")}
      />
      <SelectField id="gender" label="Gender" error={errors.gender?.message} {...register("gender")}>
        <option value="">Not recorded</option>
        <option value="FEMALE">Female</option>
        <option value="MALE">Male</option>
      </SelectField>
      <FormField
        id="dateOfBirth"
        label="Date of birth (optional)"
        type="date"
        hint="Under 18s are kept private until a guardian agrees."
        error={errors.dateOfBirth?.message}
        {...register("dateOfBirth")}
      />
      <SelectField id="nationality" label="Nationality" error={errors.nationality?.message} {...register("nationality")}>
        <option value="">Not recorded</option>
        {countries.map((country) => (
          <option key={country.code} value={country.code}>
            {country.name}
          </option>
        ))}
      </SelectField>
      <SelectField
        id="secondaryNationality"
        label="Second nationality"
        error={errors.secondaryNationality?.message}
        {...register("secondaryNationality")}
      >
        <option value="">None</option>
        {countries.map((country) => (
          <option key={country.code} value={country.code}>
            {country.name}
          </option>
        ))}
      </SelectField>
      <FormField
        id="heightCm"
        label="Height in cm (optional)"
        inputMode="numeric"
        error={errors.heightCm?.message}
        {...register("heightCm")}
      />
      <FormField
        id="weightKg"
        label="Weight in kg (optional)"
        inputMode="decimal"
        error={errors.weightKg?.message}
        {...register("weightKg")}
      />
    </div>
  );
}
