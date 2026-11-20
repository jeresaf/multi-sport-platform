"use client";

// `athlete_profile.create` and `.update` (S06 D12, T4a): the sport (only when adding: it never changes), a primary
// position, up to five other positions, the dominant side and the name shown publicly. Positions are the sport's
// definition's, named in its words (AR-29).
import { zodResolver } from "@hookform/resolvers/zod";
import { Checkbox } from "@msp/ui/components/checkbox";
import { Label } from "@msp/ui/components/label";
import { Controller, useForm, useWatch } from "react-hook-form";
import { useState } from "react";
import { z } from "zod";
import { CommandDialog } from "@/components/command-dialog";
import { FormField } from "@/components/form-field";
import { SelectField } from "@/components/select-field";
import { humanizeCode } from "@/lib/codes";
import { applyFieldErrors, needsAlert } from "@/lib/form-errors";
import { codeLabel } from "@/lib/sport-definition";
import type { Person } from "../people-data";
import {
  dominantSides,
  maxOtherPositions,
  useReloadPerson,
  useSaveAthleteProfile,
  type DominantSide,
  type OpenedPerson,
} from "./profile-data";
import type { SportLabels } from "./sport-labels";

type AthleteProfile = Person["athleteProfiles"][number];

const publicNameMaxLength = 200;

const schema = z
  .object({
    variant: z.string().min(1, "Choose a sport."),
    primaryPosition: z.string(),
    otherPositions: z.array(z.string()),
    dominantSide: z.string(),
    publicName: z.string().trim().max(publicNameMaxLength, `Keep the public name under ${publicNameMaxLength} characters.`),
  })
  .superRefine((values, context) => {
    // Counted as sent: the primary position isn't one of the others.
    if (otherThanPrimary(values).length > maxOtherPositions) {
      context.addIssue({
        code: "custom",
        path: ["otherPositions"],
        message: `Choose at most ${maxOtherPositions} other positions.`,
      });
    }
  });
type Values = { variant: string; primaryPosition: string; otherPositions: string[]; dominantSide: string; publicName: string };

function otherThanPrimary(values: Pick<Values, "primaryPosition" | "otherPositions">) {
  return values.otherPositions.filter((code) => code !== values.primaryPosition);
}

const noLonger = " (no longer in the sport's definition)";

/** The API's field names (`otherPositions[2]` included) by the form's. */
const fields: Readonly<Record<string, keyof Values>> = {
  variant: "variant",
  primaryPosition: "primaryPosition",
  otherPositions: "otherPositions",
  ...Object.fromEntries(Array.from({ length: maxOtherPositions + 1 }, (_, index) => [`otherPositions[${index}]`, "otherPositions"])),
  dominantSide: "dominantSide",
  publicName: "publicName",
};

export function AthleteProfileDialog({
  person,
  profile,
  sports,
  labels,
  onOpenChange,
  onSaved,
  onCloseAutoFocus,
}: Readonly<{
  person: Person;
  /** The profile to change; without it, a new one. */
  profile?: AthleteProfile;
  /** The sports a new profile may be for: enabled, without an open profile of this person's (variant and name). */
  sports: ReadonlyArray<{ variant: string; name: string }>;
  labels: SportLabels;
  onOpenChange: (open: boolean) => void;
  /** The change's announcement and the person it answered. */
  onSaved: (announcement: string, saved: Person) => void;
  onCloseAutoFocus?: (event: Event) => void;
}>) {
  const form = useForm<Values>({
    resolver: zodResolver(schema),
    defaultValues: {
      variant: profile?.variant ?? sports[0]?.variant ?? "",
      primaryPosition: profile?.primaryPosition ?? "",
      otherPositions: profile?.otherPositions ?? [],
      dominantSide: profile?.dominantSide ?? "",
      publicName: profile?.publicName ?? "",
    },
  });
  const variant = useWatch({ control: form.control, name: "variant" });
  const primary = useWatch({ control: form.control, name: "primaryPosition" });
  const reload = useReloadPerson(person.id);
  // The version the dialog opened on (R1): a newer read while it is open must not become the change's basis.
  const [opened] = useState<OpenedPerson>(() => ({ id: person.id, version: person.version }));
  const save = useSaveAthleteProfile(opened, profile?.id);
  const errors = form.formState.errors;

  // The definition's positions, and any the profile holds that the latest definition no longer has: shown, marked, so
  // they can be replaced (the API refuses them on save, T4a).
  const definition = labels.definition(variant);
  const definitionPositions = definition?.positions ?? [];
  const isDropped = (code: string) =>
    definition !== undefined && !definitionPositions.some((position) => position.code === code);
  // Until the definition is read (or if it can't be), the held positions are still choices, so the form shows what it
  // holds (#110 R3).
  const held = [profile?.primaryPosition, ...(profile?.otherPositions ?? [])].filter(
    (code): code is string =>
      typeof code === "string" && code !== "" && !definitionPositions.some((position) => position.code === code),
  );
  const positions = [
    ...definitionPositions,
    ...[...new Set(held)].map((code) => ({ code, label: definition ? `${codeLabel(code)}${noLonger}` : codeLabel(code) })),
  ];
  const sportName = profile ? labels.variant(profile.variant) : undefined;

  const submit = form.handleSubmit((values) => {
    const droppedMessage = (codes: string[]) =>
      `${codes.map(codeLabel).join(", ")} ${codes.length === 1 ? "is" : "are"} no longer in the sport's definition: choose another position or none.`;
    const others = otherThanPrimary(values);
    const droppedOthers = others.filter(isDropped);
    if (values.primaryPosition && isDropped(values.primaryPosition)) {
      form.setError("primaryPosition", { type: "dropped", message: droppedMessage([values.primaryPosition]) });
    }
    if (droppedOthers.length > 0) {
      form.setError("otherPositions", { type: "dropped", message: droppedMessage(droppedOthers) });
    }
    if ((values.primaryPosition && isDropped(values.primaryPosition)) || droppedOthers.length > 0) {
      return;
    }
    const details = {
      primaryPosition: values.primaryPosition || null,
      // A position can't be both: the primary one wins.
      otherPositions: others,
      dominantSide: (values.dominantSide || null) as DominantSide | null,
      publicName: values.publicName.trim() || null,
    };
    save.mutate(
      { variant: values.variant, details },
      {
        onSuccess: (saved) =>
          onSaved(profile ? `${sportName} profile saved.` : `${labels.variant(values.variant)} profile added.`, saved),
        onError: (error) => applyFieldErrors(error, fields, form.setError),
      },
    );
  });

  return (
    <CommandDialog
      title={profile ? `Change the ${sportName} profile` : `Add an athlete profile for ${person.displayName}`}
      description={
        profile
          ? "The details are replaced as a whole. The sport doesn't change."
          : "One profile per sport. Positions come from the sport's definition."
      }
      submitLabel={profile ? "Save profile" : "Add profile"}
      pending={save.isPending}
      error={save.error}
      showAlert={save.isError && needsAlert(save.error, fields)}
      onSubmit={submit}
      onOpenChange={onOpenChange}
      onCloseAutoFocus={onCloseAutoFocus}
      onReload={() => {
        void reload();
        onOpenChange(false);
      }}
    >
      {profile ? null : (
        <SelectField
          id="athlete-variant"
          label="Sport"
          error={errors.variant?.message}
          {...form.register("variant", {
            // Another sport has other positions.
            onChange: () => {
              form.setValue("primaryPosition", "");
              form.setValue("otherPositions", []);
            },
          })}
        >
          {sports.map((sport) => (
            <option key={sport.variant} value={sport.variant}>
              {sport.name}
            </option>
          ))}
        </SelectField>
      )}

      <SelectField
        id="athlete-primary-position"
        label="Primary position (optional)"
        error={errors.primaryPosition?.message}
        {...form.register("primaryPosition")}
      >
        <option value="">None</option>
        {positions.map((position) => (
          <option key={position.code} value={position.code}>
            {position.label}
          </option>
        ))}
      </SelectField>

      <fieldset className="grid gap-2" aria-describedby={errors.otherPositions ? "athlete-other-positions-error" : undefined}>
        <legend className="mb-1 text-sm font-medium">Other positions (optional, up to {maxOtherPositions})</legend>
        {labels.definitionFailed(variant) ? (
          <p role="alert" className="text-sm text-destructive">
            The sport&apos;s positions couldn&apos;t be read. Close this and try again.
          </p>
        ) : positions.length === 0 ? (
          <p className="text-sm text-muted-foreground">
            {definition ? "The sport names no positions." : "Reading the sport's positions…"}
          </p>
        ) : null}
        <Controller
          control={form.control}
          name="otherPositions"
          render={({ field }) => (
            <div className="grid gap-1 tablet:grid-cols-2">
              {positions
                .filter((position) => position.code !== primary)
                .map((position) => {
                  const id = `athlete-other-${position.code}`;
                  return (
                    <div key={position.code} className="flex min-h-11 items-center gap-3">
                      <Checkbox
                        id={id}
                        checked={field.value.includes(position.code)}
                        onCheckedChange={(checked) =>
                          field.onChange(
                            checked === true
                              ? [...field.value, position.code]
                              : field.value.filter((code) => code !== position.code),
                          )
                        }
                      />
                      <Label htmlFor={id} className="font-normal">
                        {position.label}
                      </Label>
                    </div>
                  );
                })}
            </div>
          )}
        />
        {errors.otherPositions ? (
          <p id="athlete-other-positions-error" className="text-sm text-destructive">
            {errors.otherPositions.message}
          </p>
        ) : null}
      </fieldset>

      <SelectField
        id="athlete-dominant-side"
        label="Dominant side (optional)"
        error={errors.dominantSide?.message}
        {...form.register("dominantSide")}
      >
        <option value="">Not recorded</option>
        {dominantSides.map((side) => (
          <option key={side} value={side}>
            {humanizeCode(side)}
          </option>
        ))}
      </SelectField>

      <FormField
        id="athlete-public-name"
        label="Public name (optional)"
        hint={`Shown on public pages, e.g. a short form of the name. Empty: ${person.displayName}.`}
        maxLength={publicNameMaxLength}
        error={errors.publicName?.message}
        {...form.register("publicName")}
      />
    </CommandDialog>
  );
}
