"use client";

// `person.consent_evidence.upload` on an active consent (S06 D19, T6a): adds its evidence, or replaces the file it had.
import { useState } from "react";
import { CommandDialog } from "@/components/command-dialog";
import { fieldError, needsAlert } from "@/lib/form-errors";
import { formatDay } from "@/lib/entitlements";
import type { Person } from "../people-data";
import { useUploadEvidence } from "./consent-data";
import { EvidenceField, evidenceFields, evidenceSizeError } from "./evidence-field";
import { useReloadPerson } from "./profile-data";

type Consent = Person["consents"][number];

export function EvidenceDialog({
  person,
  consent,
  onOpenChange,
  onSaved,
  onCloseAutoFocus,
}: Readonly<{
  person: Person;
  consent: Consent;
  onOpenChange: (open: boolean) => void;
  onSaved: (announcement: string) => void;
  onCloseAutoFocus?: (event: Event) => void;
}>) {
  // The version the dialog opened on (#110 R1).
  const [version] = useState(person.version);
  const [file, setFile] = useState<File | undefined>();
  const [fileError, setFileError] = useState<string | undefined>();
  const upload = useUploadEvidence(person.id);
  const reload = useReloadPerson(person.id);
  const replacing = consent.hasEvidence;

  return (
    <CommandDialog
      title={`${replacing ? "Replace" : "Upload"} the evidence of the consent given on ${formatDay(consent.givenOn)}`}
      description={
        replacing
          ? "The new file replaces the one on record. The consent itself doesn't change."
          : "The consent itself doesn't change."
      }
      submitLabel={replacing ? "Replace evidence" : "Upload evidence"}
      pending={upload.isPending}
      error={upload.error}
      showAlert={upload.isError && needsAlert(upload.error, evidenceFields)}
      confirmDisabled={file === undefined}
      onSubmit={() => {
        const sizeError = evidenceSizeError(file);
        setFileError(sizeError);
        if (file && !sizeError) {
          upload.mutate(
            { consentId: consent.id, version, file },
            { onSuccess: () => onSaved(replacing ? "Evidence replaced." : "Evidence uploaded.") },
          );
        }
      }}
      onOpenChange={onOpenChange}
      onCloseAutoFocus={onCloseAutoFocus}
      onReload={() => {
        void reload();
        onOpenChange(false);
      }}
    >
      <EvidenceField
        id="evidence-file"
        label="Evidence"
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
