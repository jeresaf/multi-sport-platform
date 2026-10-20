import { Label } from "@msp/ui/components/label";
import { cn } from "@msp/ui/lib/utils";
import type { ComponentProps } from "react";

type SelectFieldProps = ComponentProps<"select"> & {
  id: string;
  label: string;
  error?: string;
  hint?: string;
};

/**
 * A labelled native select with its hint and error, styled like an input. Native, not a popover: long lists (time
 * zones) stay searchable by typing and work with the phone's own picker.
 */
export function SelectField({ id, label, error, hint, className, children, ...select }: Readonly<SelectFieldProps>) {
  const hintId = hint ? `${id}-hint` : undefined;
  const errorId = error ? `${id}-error` : undefined;

  return (
    <div className="grid gap-2">
      <Label htmlFor={id}>{label}</Label>
      <select
        id={id}
        aria-invalid={error ? true : undefined}
        aria-describedby={[hintId, errorId].filter(Boolean).join(" ") || undefined}
        className={cn(
          "h-11 w-full min-w-0 rounded-md border border-input bg-background px-3 text-base shadow-xs outline-none disabled:cursor-not-allowed disabled:opacity-50 tablet:text-sm",
          "focus-visible:border-ring focus-visible:ring-[3px] focus-visible:ring-ring",
          "aria-invalid:border-destructive",
          className,
        )}
        {...select}
      >
        {children}
      </select>
      {hint ? (
        <p id={hintId} className="text-sm text-muted-foreground">
          {hint}
        </p>
      ) : null}
      {error ? (
        <p id={errorId} className="text-sm text-destructive">
          {error}
        </p>
      ) : null}
    </div>
  );
}
