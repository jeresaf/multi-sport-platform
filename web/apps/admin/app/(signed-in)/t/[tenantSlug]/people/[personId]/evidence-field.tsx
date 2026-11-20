"use client";

// The consent's evidence (a signed form, S06 D14): one file, a PDF, JPEG or PNG up to 10 MB. The picker offers those
// types; the API decides by the file's bytes (MEDIA_TYPE_NOT_ALLOWED, MEDIA_TOO_LARGE), and its refusal shows here.
import { Input } from "@msp/ui/components/input";
import { Label } from "@msp/ui/components/label";
import { evidenceAccept, evidenceMaxBytes } from "./consent-data";

/** A file the API would refuse for its size, said before it is sent; undefined when it may go. */
export function evidenceSizeError(file: File | undefined): string | undefined {
  return file && file.size > evidenceMaxBytes ? "The file is larger than 10 MB. Choose a smaller one." : undefined;
}

export function EvidenceField({
  id,
  label,
  error,
  disabled,
  onChange,
}: Readonly<{
  id: string;
  label: string;
  error?: string;
  disabled?: boolean;
  onChange: (file: File | undefined) => void;
}>) {
  const hintId = `${id}-hint`;
  const errorId = `${id}-error`;
  return (
    <div className="grid gap-2">
      <Label htmlFor={id}>{label}</Label>
      <Input
        id={id}
        type="file"
        accept={evidenceAccept}
        disabled={disabled}
        aria-invalid={error ? true : undefined}
        aria-describedby={error ? `${hintId} ${errorId}` : hintId}
        className="h-auto py-2"
        onChange={(event) => onChange(event.target.files?.[0] ?? undefined)}
      />
      <p id={hintId} className="text-sm text-muted-foreground">
        A PDF, JPEG or PNG up to 10 MB, such as the signed form. It is private: only people who may see the guardian&apos;s
        details can download it.
      </p>
      {error ? (
        <p id={errorId} className="text-sm text-destructive">
          {error}
        </p>
      ) : null}
    </div>
  );
}

/** The API's refusal of the file (`file`), under the field rather than in the alert. */
export const evidenceFields = { file: "file" } as const;
