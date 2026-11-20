"use client";

// `person.consent.record` and, in the same dialog, `person.consent_evidence.upload` (S06 D14, D19): the consent is
// recorded first (JSON), then its evidence is uploaded (multipart) on the version that answered. If the upload fails,
// the consent stays recorded: the dialog says so and offers the upload again.
import { zodResolver } from "@hookform/resolvers/zod";
import { useEffect, useState } from "react";
import { Controller, useForm } from "react-hook-form";
import { z } from "zod";
import { CommandDialog } from "@/components/command-dialog";
import { FormField } from "@/components/form-field";
import { SelectField } from "@/components/select-field";
import { humanizeCode } from "@/lib/codes";
import { applyFieldErrors, fieldError, needsAlert } from "@/lib/form-errors";
import { todayIn, useTenantTimeZoneOnceRead } from "@/lib/tenant-settings";
import type { Person } from "../people-data";
import {
  consentScopes,
  guardianRelationships,
  useRecordConsent,
  useRefreshPrivate,
  useUploadEvidence,
  type ConsentScope,
  type GuardianRelationship,
} from "./consent-data";
import { EvidenceField, evidenceFields, evidenceSizeError } from "./evidence-field";
import { useReloadPerson, type OpenedPerson } from "./profile-data";
import { ScopeCheckboxes } from "./scope-checkboxes";

const textMaxLength = 200;

const schema = z.object({
  guardianName: z
    .string()
    .trim()
    .min(1, "Enter the guardian's name.")
    .max(textMaxLength, `Keep the name under ${textMaxLength} characters.`),
  relationship: z.string().min(1, "Choose the guardian's relationship."),
  contact: z
    .string()
    .trim()
    .min(1, "Enter a phone number or email address.")
    .max(textMaxLength, `Keep the contact under ${textMaxLength} characters.`),
  scopes: z.array(z.enum(consentScopes)).min(1, "Choose at least one thing the guardian agrees to."),
  givenOn: z.string().min(1, "Enter the date the consent was given."),
});
type Values = { guardianName: string; relationship: string; contact: string; scopes: ConsentScope[]; givenOn: string };

/** The API's field names by the form's. */
const fields: Readonly<Record<string, keyof Values>> = {
  guardianName: "guardianName",
  relationship: "relationship",
  contact: "contact",
  scopes: "scopes",
  givenOn: "givenOn",
};

/**
 * What each scope lets the organisation do. Data processing names where data is stored and the sub-processors (D14,
 * ADR-0015, ADR-0016); its wording is a placeholder until the owner's approved text (Q13), and says so.
 */
export function scopeExplanation(scope: ConsentScope) {
  switch (scope) {
    case "DATA_PROCESSING":
      return (
        <>
          <strong className="font-medium">Placeholder wording, to be replaced by the approved text:</strong> the
          person&apos;s details are stored in South Africa (Microsoft Azure) and handled by the platform&apos;s
          sub-processors: Microsoft Azure (South Africa), Cloudflare (global network, public pages only) and the email
          provider.
        </>
      );
    case "PUBLIC_PROFILE":
      return "The display name may appear on the organisation's public site, once the public profile is turned on.";
    case "PHOTO":
      return "The photo may appear with the public profile.";
  }
}

export function ConsentDialog({
  person,
  onOpenChange,
  onSaved,
  onCloseAutoFocus,
}: Readonly<{
  person: Person;
  onOpenChange: (open: boolean) => void;
  /** The announcement and the consent to focus. */
  onSaved: (announcement: string, consentId: string) => void;
  onCloseAutoFocus?: (event: Event) => void;
}>) {
  // The tenant's date (D13): until its time zone is read, the date isn't offered (UTC can be another day, #111 R3).
  const zone = useTenantTimeZoneOnceRead();
  const today = zone.isLoading ? "" : todayIn(zone.timeZone);
  const form = useForm<Values>({
    resolver: zodResolver(schema),
    defaultValues: { guardianName: "", relationship: "", contact: "", scopes: [], givenOn: today },
  });
  const { getFieldState, setValue } = form;
  useEffect(() => {
    // Once the zone is read, the date starts at its today, unless someone has already typed one.
    if (today && !getFieldState("givenOn").isDirty) {
      setValue("givenOn", today);
    }
  }, [today, getFieldState, setValue]);
  const [file, setFile] = useState<File | undefined>();
  const [fileError, setFileError] = useState<string | undefined>();
  // The version the dialog opened on (#110 R1).
  const [opened] = useState<OpenedPerson>(() => ({ id: person.id, version: person.version }));
  // Once the consent is recorded: its id and the person's version that answered (the upload's basis).
  const [recorded, setRecorded] = useState<{ consentId: string; version: number } | undefined>();
  // An upload after the record was refused: the dialog now only offers the upload again.
  const [uploadRefused, setUploadRefused] = useState(false);
  const record = useRecordConsent(opened);
  const upload = useUploadEvidence(person.id);
  const reload = useReloadPerson(person.id);
  const refreshPrivate = useRefreshPrivate(person.id);
  const errors = form.formState.errors;

  const sendEvidence = (consentId: string, version: number, chosen: File) =>
    upload.mutate(
      { consentId, version, file: chosen },
      {
        onSuccess: () => onSaved("Consent recorded with its evidence.", consentId),
        onError: () => {
          setUploadRefused(true);
          // The refused file isn't sent again: another is chosen (#111 R4). Its refusal stays under the field.
          setFile(undefined);
          const input = document.getElementById("consent-evidence") as HTMLInputElement | null;
          if (input) {
            input.value = "";
            input.focus();
          }
        },
      },
    );

  const submit = recorded
    ? () => {
        // The consent is on record: only the upload is tried again.
        const sizeError = evidenceSizeError(file);
        setFileError(sizeError);
        if (file && !sizeError) {
          sendEvidence(recorded.consentId, recorded.version, file);
        }
      }
    : form.handleSubmit((values) => {
        const sizeError = evidenceSizeError(file);
        setFileError(sizeError);
        if (sizeError) {
          return;
        }
        const before = new Set(person.consents.map((consent) => consent.id));
        record.mutate(
          {
            body: {
              guardianName: values.guardianName.trim(),
              relationship: values.relationship as GuardianRelationship,
              contact: values.contact.trim(),
              scopes: values.scopes,
              givenOn: values.givenOn,
            },
            evidenceFollows: file !== undefined,
          },
          {
            onSuccess: (saved) => {
              const consentId = saved.consents.find((consent) => !before.has(consent.id))?.id;
              if (!consentId) {
                // The API answers the person with the new consent (T6a): without it, this page is wrong.
                throw new Error("The recorded consent isn't in the person the API answered.");
              }
              if (file) {
                setRecorded({ consentId, version: saved.version });
                sendEvidence(consentId, saved.version, file);
              } else {
                onSaved("Consent recorded.", consentId);
              }
            },
            onError: (error) => applyFieldErrors(error, fields, form.setError),
          },
        );
      });

  // After the record: closing keeps the consent (announced) without its evidence; the private read waited for this.
  const close = (open: boolean) => {
    if (!open && recorded) {
      void refreshPrivate();
      onSaved("Consent recorded without its evidence.", recorded.consentId);
      return;
    }
    onOpenChange(open);
  };

  const error = recorded ? upload.error : record.error;
  const refusedNote = "The consent is recorded. Its evidence wasn't uploaded: choose the file again, or close to add it later.";

  return (
    <CommandDialog
      title={`Record consent for ${person.displayName}`}
      description={
        uploadRefused ? refusedNote : "What a parent or guardian agreed to. The guardian's details are private."
      }
      submitLabel={uploadRefused ? "Upload evidence" : "Record consent"}
      pending={record.isPending || upload.isPending}
      error={error}
      showAlert={recorded ? upload.isError && needsAlert(upload.error, evidenceFields) : record.isError && needsAlert(record.error, fields)}
      confirmDisabled={recorded !== undefined && file === undefined}
      onSubmit={submit}
      onOpenChange={close}
      onCloseAutoFocus={onCloseAutoFocus}
      onReload={() => {
        void reload();
        close(false);
      }}
    >
      {/* Said to screen readers as it happens: the description changed, but isn't announced. */}
      <p role="status" className="sr-only">
        {uploadRefused ? refusedNote : ""}
      </p>
      <FormField
        id="consent-guardian-name"
        label="Guardian's name"
        autoComplete="off"
        maxLength={textMaxLength}
        disabled={recorded !== undefined}
        error={errors.guardianName?.message}
        {...form.register("guardianName")}
      />
      <SelectField
        id="consent-relationship"
        label="Relationship"
        disabled={recorded !== undefined}
        error={errors.relationship?.message}
        {...form.register("relationship")}
      >
        <option value="">Choose…</option>
        {guardianRelationships.map((relationship) => (
          <option key={relationship} value={relationship}>
            {humanizeCode(relationship)}
          </option>
        ))}
      </SelectField>
      <FormField
        id="consent-contact"
        label="Contact"
        hint="A phone number or email address."
        autoComplete="off"
        maxLength={textMaxLength}
        disabled={recorded !== undefined}
        error={errors.contact?.message}
        {...form.register("contact")}
      />
      <Controller
        control={form.control}
        name="scopes"
        render={({ field }) => (
          <ScopeCheckboxes
            id="consent-scopes"
            legend="The guardian agrees to"
            scopes={consentScopes}
            value={field.value}
            onChange={field.onChange}
            explain={scopeExplanation}
            disabled={recorded !== undefined}
            error={errors.scopes?.message}
          />
        )}
      />
      <FormField
        id="consent-given-on"
        label="Given on"
        type="date"
        max={today || undefined}
        disabled={recorded !== undefined || zone.isLoading}
        error={errors.givenOn?.message}
        {...form.register("givenOn")}
      />
      <EvidenceField
        id="consent-evidence"
        label={uploadRefused ? "Evidence" : "Evidence (optional)"}
        error={fileError ?? fieldError(upload.error, "file")}
        onChange={(chosen) => {
          setFile(chosen);
          setFileError(undefined);
          upload.reset();
        }}
      />
    </CommandDialog>
  );
}
