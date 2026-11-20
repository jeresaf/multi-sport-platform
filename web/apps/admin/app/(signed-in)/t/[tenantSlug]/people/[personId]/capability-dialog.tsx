"use client";

// `staff_profile.create`/`.update` and `official_profile.create`/`.update` (S06 D12, T4b): 1–10 capabilities, one per
// sport and role, replaced as a whole. A staff capability adds a qualification; an official one a grade, a licence
// number and the licence's last day. Roles are the sport's definition's, named in its words (AR-29). On an archived or
// deceased person held capabilities may be kept, removed or changed in their details, never added (T4b1 R2).
import { zodResolver } from "@hookform/resolvers/zod";
import { Button } from "@msp/ui/components/button";
import { Plus, Trash2 } from "lucide-react";
import { useFieldArray, useForm, useWatch, type FieldPath } from "react-hook-form";
import { z } from "zod";
import { CommandDialog } from "@/components/command-dialog";
import { FormField } from "@/components/form-field";
import { SelectField } from "@/components/select-field";
import { applyFieldErrors, needsAlert } from "@/lib/form-errors";
import { codeLabel } from "@/lib/sport-definition";
import type { Person } from "../people-data";
import {
  isClosedPerson,
  maxCapabilities,
  useReloadPerson,
  useSaveCapabilities,
  type CapabilityKind,
} from "./profile-data";
import type { SportLabels } from "./sport-labels";

type StaffProfile = Person["staffProfiles"][number];
type OfficialProfile = Person["officialProfiles"][number];

const qualificationMaxLength = 100;
const gradeMaxLength = 50;
const licenceNumberMaxLength = 50;

const text = (max: number, what: string) => z.string().trim().max(max, `Keep the ${what} under ${max} characters.`);

const schema = z
  .object({
    capabilities: z
      .array(
        z.object({
          variant: z.string().min(1, "Choose a sport."),
          role: z.string().min(1, "Choose a role."),
          qualification: text(qualificationMaxLength, "qualification"),
          grade: text(gradeMaxLength, "grade"),
          licenceNumber: text(licenceNumberMaxLength, "licence number"),
          licenceValidUntil: z.string(),
          /** Held before this change: on an archived or deceased person its sport and role can't change. */
          held: z.boolean(),
        }),
      )
      .min(1, "Add at least one role.")
      .max(maxCapabilities, `A profile holds at most ${maxCapabilities} roles.`),
  })
  .superRefine((values, context) => {
    values.capabilities.forEach((capability, index) => {
      const first = values.capabilities.findIndex(
        (other) => other.variant === capability.variant && other.role === capability.role,
      );
      if (capability.role !== "" && first !== index) {
        context.addIssue({ code: "custom", path: ["capabilities", index, "role"], message: "This sport and role is already listed." });
      }
    });
  });
type Values = z.infer<typeof schema>;
type Row = Values["capabilities"][number];

const emptyRow = (variant: string): Row => ({
  variant,
  role: "",
  qualification: "",
  grade: "",
  licenceNumber: "",
  licenceValidUntil: "",
  held: false,
});

/** The API's field names (`capabilities[3].role`) by the form's. */
const fields: Readonly<Record<string, FieldPath<Values>>> = {
  capabilities: "capabilities",
  ...Object.fromEntries(
    Array.from({ length: maxCapabilities }, (_, index) =>
      (["variant", "role", "qualification", "grade", "licenceNumber", "licenceValidUntil"] as const).map((name) => [
        `capabilities[${index}].${name}`,
        `capabilities.${index}.${name}` as FieldPath<Values>,
      ]),
    ).flat(),
  ),
};

const kindWords = { STAFF: "staff", OFFICIAL: "official" } as const;

export function CapabilityDialog({
  kind,
  person,
  profile,
  sports,
  labels,
  onOpenChange,
  onSaved,
  onCloseAutoFocus,
}: Readonly<{
  kind: CapabilityKind;
  person: Person;
  /** The profile to change; without it, a new one. */
  profile?: StaffProfile | OfficialProfile;
  /** The tenant's enabled sports (variant and name): a capability added must be for one of them. */
  sports: ReadonlyArray<{ variant: string; name: string }>;
  labels: SportLabels;
  onOpenChange: (open: boolean) => void;
  /** The change's announcement and the person it answered. */
  onSaved: (announcement: string, saved: Person) => void;
  onCloseAutoFocus?: (event: Event) => void;
}>) {
  const closed = isClosedPerson(person);
  const form = useForm<Values>({
    resolver: zodResolver(schema),
    defaultValues: {
      capabilities: profile
        ? profile.capabilities.map((capability) => ({
            ...emptyRow(capability.variant),
            role: capability.role,
            qualification: "qualification" in capability ? (capability.qualification ?? "") : "",
            grade: "grade" in capability ? (capability.grade ?? "") : "",
            licenceNumber: "licenceNumber" in capability ? (capability.licenceNumber ?? "") : "",
            licenceValidUntil: "licenceValidUntil" in capability ? (capability.licenceValidUntil ?? "") : "",
            held: true,
          }))
        : [emptyRow(sports[0]?.variant ?? "")],
    },
  });
  const rows = useFieldArray({ control: form.control, name: "capabilities" });
  const values = useWatch({ control: form.control, name: "capabilities" });
  const reload = useReloadPerson(person.id);
  const save = useSaveCapabilities(person, kind, profile?.id);
  const errors = form.formState.errors;
  const words = kindWords[kind];

  // A held capability may stay on a sport since disabled (T4b1); an added one needs an enabled sport.
  const heldVariants = (profile?.capabilities ?? [])
    .map((capability) => capability.variant)
    .filter((variant) => !sports.some((sport) => sport.variant === variant));
  const sportOptions = [
    ...sports,
    ...[...new Set(heldVariants)].map((variant) => ({ variant, name: labels.variant(variant) })),
  ];
  const roleOptions = (variant: string, held: string | undefined) => {
    const definition = labels.definition(variant);
    const roles = (kind === "STAFF" ? definition?.staffRoles : definition?.officialRoles) ?? [];
    return held && !roles.some((role) => role.code === held) ? [...roles, { code: held, label: codeLabel(held) }] : roles;
  };
  const roleLabel = (variant: string, role: string) =>
    kind === "STAFF" ? labels.staffRole(variant, role) : labels.officialRole(variant, role);

  const submit = form.handleSubmit(({ capabilities }) => {
    const body =
      kind === "STAFF"
        ? capabilities.map((row) => ({ variant: row.variant, role: row.role, qualification: row.qualification.trim() || null }))
        : capabilities.map((row) => ({
            variant: row.variant,
            role: row.role,
            grade: row.grade.trim() || null,
            licenceNumber: row.licenceNumber.trim() || null,
            licenceValidUntil: row.licenceValidUntil || null,
          }));
    save.mutate(body, {
      onSuccess: (saved) =>
        onSaved(profile ? `The ${words} profile is saved.` : `${person.displayName} now has a ${words} profile.`, saved),
      onError: (error) => applyFieldErrors(error, fields, form.setError),
    });
  });

  const listError = errors.capabilities?.root?.message ?? errors.capabilities?.message;

  return (
    <CommandDialog
      title={profile ? `Change the ${words} profile` : `Add a ${words} profile for ${person.displayName}`}
      description={
        closed
          ? `${person.displayName} is ${person.status === "DECEASED" ? "deceased" : "archived"}: the roles held can be kept, changed in their details or removed, but none added.`
          : `Each role is for one sport, named by the sport's definition. Up to ${maxCapabilities} roles.`
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
      <ol className="grid gap-4" aria-label="Roles">
        {rows.fields.map((row, index) => {
          const current = values[index] ?? row;
          const rowErrors = errors.capabilities?.[index];
          const locked = closed && row.held;
          const number = index + 1;
          return (
            <li key={row.id} className="grid gap-3 rounded-lg border p-3">
              <div className="flex items-center justify-between gap-2">
                <h3 className="text-sm font-semibold">
                  {locked ? `${labels.variant(current.variant)}: ${roleLabel(current.variant, current.role)}` : `Role ${number}`}
                </h3>
                <Button
                  variant="ghost"
                  aria-label={`Remove role ${number}`}
                  disabled={rows.fields.length === 1}
                  onClick={() => rows.remove(index)}
                >
                  <Trash2 aria-hidden="true" />
                  Remove
                </Button>
              </div>
              {locked ? null : (
                <div className="grid gap-3 tablet:grid-cols-2">
                  <SelectField
                    id={`capability-${index}-variant`}
                    label="Sport"
                    error={rowErrors?.variant?.message}
                    {...form.register(`capabilities.${index}.variant`, {
                      // Another sport has other roles.
                      onChange: () => form.setValue(`capabilities.${index}.role`, ""),
                    })}
                  >
                    {sportOptions.map((sport) => (
                      <option key={sport.variant} value={sport.variant}>
                        {sport.name}
                      </option>
                    ))}
                  </SelectField>
                  <SelectField
                    id={`capability-${index}-role`}
                    label="Role"
                    error={rowErrors?.role?.message}
                    {...form.register(`capabilities.${index}.role`)}
                  >
                    <option value="">Choose a role</option>
                    {roleOptions(current.variant, row.held ? row.role : undefined).map((role) => (
                      <option key={role.code} value={role.code}>
                        {role.label}
                      </option>
                    ))}
                  </SelectField>
                </div>
              )}
              {kind === "STAFF" ? (
                <FormField
                  id={`capability-${index}-qualification`}
                  label="Qualification (optional)"
                  maxLength={qualificationMaxLength}
                  error={rowErrors?.qualification?.message}
                  {...form.register(`capabilities.${index}.qualification`)}
                />
              ) : (
                <div className="grid gap-3 tablet:grid-cols-3">
                  <FormField
                    id={`capability-${index}-grade`}
                    label="Grade (optional)"
                    maxLength={gradeMaxLength}
                    error={rowErrors?.grade?.message}
                    {...form.register(`capabilities.${index}.grade`)}
                  />
                  <FormField
                    id={`capability-${index}-licence-number`}
                    label="Licence number (optional)"
                    maxLength={licenceNumberMaxLength}
                    error={rowErrors?.licenceNumber?.message}
                    {...form.register(`capabilities.${index}.licenceNumber`)}
                  />
                  <FormField
                    id={`capability-${index}-licence-until`}
                    label="Licence valid until (optional)"
                    type="date"
                    error={rowErrors?.licenceValidUntil?.message}
                    {...form.register(`capabilities.${index}.licenceValidUntil`)}
                  />
                </div>
              )}
            </li>
          );
        })}
      </ol>
      {listError ? <p className="text-sm text-destructive">{listError}</p> : null}
      {closed ? null : (
        <Button
          variant="outline"
          className="justify-self-start"
          disabled={rows.fields.length >= maxCapabilities}
          onClick={() => rows.append(emptyRow(sports[0]?.variant ?? ""))}
        >
          <Plus aria-hidden="true" />
          Add another role
        </Button>
      )}
    </CommandDialog>
  );
}
