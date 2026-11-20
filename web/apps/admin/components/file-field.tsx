"use client";

// One file for an upload (S06 D19): the picker offers the types the API takes, the hint says what they are, and the
// API's refusal of the file (`MEDIA_TYPE_NOT_ALLOWED`, `MEDIA_TOO_LARGE`, on `file`) shows under it.
import { Input } from "@msp/ui/components/input";
import { Label } from "@msp/ui/components/label";
import type { ReactNode } from "react";

/** A file the API would refuse for its size, said before it is sent; undefined when it may go. */
export function fileSizeError(file: File | undefined, maxBytes: number): string | undefined {
  return file && file.size > maxBytes
    ? `The file is larger than ${maxBytes / (1024 * 1024)} MB. Choose a smaller one.`
    : undefined;
}

/** Clears a file input, so a refused file isn't sent again (#111 R4); with `focus`, the input takes the focus. */
export function clearFileInput(id: string, focus = false) {
  const input = document.getElementById(id) as HTMLInputElement | null;
  if (input) {
    input.value = "";
    if (focus) {
      input.focus();
    }
  }
}

export function FileField({
  id,
  label,
  accept,
  hint,
  error,
  disabled,
  onChange,
}: Readonly<{
  id: string;
  label: string;
  accept: string;
  hint: ReactNode;
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
        accept={accept}
        disabled={disabled}
        aria-invalid={error ? true : undefined}
        aria-describedby={error ? `${hintId} ${errorId}` : hintId}
        className="h-auto py-2"
        onChange={(event) => onChange(event.target.files?.[0] ?? undefined)}
      />
      <p id={hintId} className="text-sm text-muted-foreground">
        {hint}
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
export const fileFields = { file: "file" } as const;
