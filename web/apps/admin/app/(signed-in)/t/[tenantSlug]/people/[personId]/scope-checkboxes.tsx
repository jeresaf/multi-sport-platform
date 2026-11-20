"use client";

import { Checkbox } from "@msp/ui/components/checkbox";
import { Label } from "@msp/ui/components/label";
import type { ReactNode } from "react";
import type { ConsentScope } from "./consent-data";
import { scopeLabels } from "./consent-tab";

/** Consent scopes as checkboxes in a fieldset, each with its own explanation under it. */
export function ScopeCheckboxes({
  id,
  legend,
  scopes,
  value,
  onChange,
  explain,
  error,
  disabled,
}: Readonly<{
  id: string;
  legend: string;
  scopes: readonly ConsentScope[];
  value: readonly ConsentScope[];
  onChange: (value: ConsentScope[]) => void;
  explain?: (scope: ConsentScope) => ReactNode;
  error?: string;
  disabled?: boolean;
}>) {
  const errorId = `${id}-error`;
  return (
    <fieldset className="grid gap-2" aria-describedby={error ? errorId : undefined} disabled={disabled}>
      <legend className="mb-1 text-sm font-medium">{legend}</legend>
      {scopes.map((scope) => {
        const checkboxId = `${id}-${scope}`;
        const explanation = explain?.(scope);
        return (
          <div key={scope} className="grid gap-1">
            <div className="flex min-h-11 items-center gap-3">
              <Checkbox
                id={checkboxId}
                checked={value.includes(scope)}
                disabled={disabled}
                aria-describedby={explanation ? `${checkboxId}-explain` : undefined}
                onCheckedChange={(checked) =>
                  onChange(
                    checked === true
                      ? scopes.filter((item) => item === scope || value.includes(item))
                      : value.filter((item) => item !== scope),
                  )
                }
              />
              <Label htmlFor={checkboxId} className="font-normal">
                {scopeLabels[scope]}
              </Label>
            </div>
            {explanation ? (
              <p id={`${checkboxId}-explain`} className="pl-8 text-sm text-muted-foreground">
                {explanation}
              </p>
            ) : null}
          </div>
        );
      })}
      {error ? (
        <p id={errorId} className="text-sm text-destructive">
          {error}
        </p>
      ) : null}
    </fieldset>
  );
}
