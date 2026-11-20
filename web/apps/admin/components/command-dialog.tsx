"use client";

import { Button } from "@msp/ui/components/button";
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from "@msp/ui/components/dialog";
import type { ReactNode } from "react";
import { ConflictAlert } from "@/components/conflict-alert";
import { ProblemAlert } from "@/components/problem-alert";
import { isConcurrencyConflict } from "@/lib/concurrency";

/**
 * A command's form in a dialog: title, what it does, its alert, its fields and its buttons. The refusal stays in the
 * dialog; with `onReload`, a concurrency conflict offers to read the record again instead of saving.
 */
export function CommandDialog({
  title,
  description,
  submitLabel,
  pending,
  error,
  showAlert,
  confirmDisabled = false,
  onSubmit,
  onOpenChange,
  onReload,
  onCloseAutoFocus,
  children,
}: Readonly<{
  title: string;
  description: ReactNode;
  submitLabel: string;
  pending: boolean;
  error: unknown;
  showAlert: boolean;
  /** The command no longer applies (the record was read again after a refusal): its refusal stays, sending doesn't. */
  confirmDisabled?: boolean;
  onSubmit: () => void;
  onOpenChange: (open: boolean) => void;
  /** After a concurrency conflict: read the record again and close the dialog. */
  onReload?: () => void;
  /** Where focus goes when the dialog closes (by default, back to what opened it). */
  onCloseAutoFocus?: (event: Event) => void;
  children: ReactNode;
}>) {
  const conflict = onReload !== undefined && isConcurrencyConflict(error);
  return (
    <Dialog open onOpenChange={(open) => !pending && onOpenChange(open)}>
      <DialogContent className="max-h-[90dvh] overflow-y-auto" onCloseAutoFocus={onCloseAutoFocus}>
        <form
          className="grid gap-4"
          noValidate
          onSubmit={(event) => {
            event.preventDefault();
            onSubmit();
          }}
        >
          <DialogHeader>
            <DialogTitle>{title}</DialogTitle>
            <DialogDescription>{description}</DialogDescription>
          </DialogHeader>
          {conflict ? <ConflictAlert onReload={onReload} /> : showAlert ? <ProblemAlert error={error} /> : null}
          {children}
          <DialogFooter>
            <Button variant="outline" disabled={pending} onClick={() => onOpenChange(false)}>
              Cancel
            </Button>
            <Button type="submit" disabled={pending || confirmDisabled || conflict}>
              {pending ? "Saving…" : submitLabel}
            </Button>
          </DialogFooter>
        </form>
      </DialogContent>
    </Dialog>
  );
}
