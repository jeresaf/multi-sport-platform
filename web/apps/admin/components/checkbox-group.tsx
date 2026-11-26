"use client";

import { Checkbox } from "@msp/ui/components/checkbox";
import { Label } from "@msp/ui/components/label";
import type { ReactNode } from "react";

/**
 * A set of choices as checkboxes in a fieldset with its legend, hint and error (a venue's surfaces, the sports it
 * supports). The value keeps the options' order, whatever order they were ticked in.
 */
export function CheckboxGroup<T extends string>({
  id,
  legend,
  options,
  value,
  onChange,
  hint,
  error,
  empty,
}: Readonly<{
  id: string;
  legend: string;
  options: ReadonlyArray<{ value: T; label: string }>;
  value: readonly T[];
  onChange: (value: T[]) => void;
  hint?: string;
  error?: string;
  /** Said instead of the checkboxes when there is nothing to choose. */
  empty?: ReactNode;
}>) {
  const hintId = hint ? `${id}-hint` : undefined;
  const errorId = error ? `${id}-error` : undefined;
  return (
    <fieldset
      className="grid gap-2"
      aria-describedby={[hintId, errorId].filter(Boolean).join(" ") || undefined}
      aria-invalid={error ? true : undefined}
    >
      <legend className="mb-1 text-sm font-medium">{legend}</legend>
      {hint ? (
        <p id={hintId} className="text-sm text-muted-foreground">
          {hint}
        </p>
      ) : null}
      {options.length === 0 && empty ? (
        <p className="text-sm text-muted-foreground">{empty}</p>
      ) : (
        <div className="grid gap-x-6 tablet:grid-cols-2 desktop:grid-cols-3">
          {options.map((option) => {
            const checkboxId = `${id}-${option.value}`;
            return (
              <div key={option.value} className="flex min-h-11 items-center gap-3">
                <Checkbox
                  id={checkboxId}
                  checked={value.includes(option.value)}
                  onCheckedChange={(checked) =>
                    onChange(
                      options
                        .map((item) => item.value)
                        .filter((item) =>
                          item === option.value ? checked === true : value.includes(item),
                        ),
                    )
                  }
                />
                <Label htmlFor={checkboxId} className="font-normal">
                  {option.label}
                </Label>
              </div>
            );
          })}
        </div>
      )}
      {error ? (
        <p id={errorId} className="text-sm text-destructive">
          {error}
        </p>
      ) : null}
    </fieldset>
  );
}
