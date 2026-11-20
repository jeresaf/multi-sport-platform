"use client";

// The consent's evidence (a signed form, S06 D14): one file, a PDF, JPEG or PNG up to 10 MB. The picker offers those
// types; the API decides by the file's bytes (MEDIA_TYPE_NOT_ALLOWED, MEDIA_TOO_LARGE), and its refusal shows here.
import { FileField, fileFields, fileSizeError } from "@/components/file-field";
import { evidenceAccept, evidenceMaxBytes } from "./consent-data";

/** A file the API would refuse for its size, said before it is sent; undefined when it may go. */
export const evidenceSizeError = (file: File | undefined) => fileSizeError(file, evidenceMaxBytes);

export function EvidenceField(
  props: Readonly<{
    id: string;
    label: string;
    error?: string;
    disabled?: boolean;
    onChange: (file: File | undefined) => void;
  }>,
) {
  return (
    <FileField
      {...props}
      accept={evidenceAccept}
      hint={
        <>
          A PDF, JPEG or PNG up to 10 MB, such as the signed form. It is private: only people who may see the
          guardian&apos;s details can download it.
        </>
      }
    />
  );
}

/** The API's refusal of the file (`file`), under the field rather than in the alert. */
export const evidenceFields = fileFields;
