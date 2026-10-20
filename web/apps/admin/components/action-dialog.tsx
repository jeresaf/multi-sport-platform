"use client";

import { zodResolver } from "@hookform/resolvers/zod";
import { Button } from "@msp/ui/components/button";
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from "@msp/ui/components/dialog";
import { Label } from "@msp/ui/components/label";
import { Textarea } from "@msp/ui/components/textarea";
import { useEffect, type ReactNode } from "react";
import { useForm } from "react-hook-form";
import { z } from "zod";
import { ConflictAlert } from "@/components/conflict-alert";
import { ProblemAlert } from "@/components/problem-alert";
import { isConcurrencyConflict } from "@/lib/concurrency";
import { applyFieldErrors, needsAlert } from "@/lib/form-errors";

export const reasonMaxLength = 500;

const reasonSchema = z.object({
  reason: z
    .string()
    .trim()
    .min(1, "Give a reason: it is kept in the audit trail.")
    .max(reasonMaxLength, `Keep the reason under ${reasonMaxLength} characters.`),
});
const noReasonSchema = z.object({ reason: z.string() });
type ReasonValues = { reason: string };
const fields = { reason: "reason" } as const;

type ActionDialogProps = {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  title: string;
  description: ReactNode;
  confirmLabel: string;
  /** A change that takes something away (suspend, remove, revoke) is shown in the danger colour. */
  destructive?: boolean;
  /** The action is audited with a reason (`REQUIRED_WITH_REASON`): the dialog asks for one (admin-app.md). */
  withReason?: boolean;
  pending: boolean;
  error: unknown;
  onConfirm: (reason: string | undefined) => void;
  /** After a concurrency conflict: read the record again and close the dialog. */
  onReload: () => void;
  /** Where focus goes when the dialog closes (by default, back to what opened it). */
  onCloseAutoFocus?: (event: Event) => void;
};

/** Confirms a change, asking for its reason when the action needs one; the API's refusal stays in the dialog. */
export function ActionDialog({
  open,
  onOpenChange,
  title,
  description,
  confirmLabel,
  destructive = false,
  withReason = false,
  pending,
  error,
  onConfirm,
  onReload,
  onCloseAutoFocus,
}: Readonly<ActionDialogProps>) {
  const form = useForm<ReasonValues>({
    resolver: zodResolver(withReason ? reasonSchema : noReasonSchema),
    defaultValues: { reason: "" },
  });
  const { reset, setError } = form;

  useEffect(() => {
    if (open) {
      reset({ reason: "" });
    }
  }, [open, reset]);
  useEffect(() => {
    applyFieldErrors(error, fields, setError);
  }, [error, setError]);

  const reasonError = form.formState.errors.reason?.message;
  const conflict = isConcurrencyConflict(error);

  return (
    <Dialog open={open} onOpenChange={(next) => !pending && onOpenChange(next)}>
      <DialogContent onCloseAutoFocus={onCloseAutoFocus}>
        <form
          className="grid gap-4"
          noValidate
          onSubmit={form.handleSubmit(({ reason }) => onConfirm(withReason ? reason.trim() : undefined))}
        >
          <DialogHeader>
            <DialogTitle>{title}</DialogTitle>
            <DialogDescription>{description}</DialogDescription>
          </DialogHeader>
          {conflict ? (
            <ConflictAlert onReload={onReload} />
          ) : error && needsAlert(error, fields) ? (
            <ProblemAlert error={error} />
          ) : null}
          {withReason ? (
            <div className="grid gap-2">
              <Label htmlFor="action-reason">Reason</Label>
              <Textarea
                id="action-reason"
                maxLength={reasonMaxLength}
                aria-invalid={reasonError ? true : undefined}
                aria-describedby={reasonError ? "action-reason-error" : "action-reason-hint"}
                {...form.register("reason")}
              />
              {reasonError ? (
                <p id="action-reason-error" className="text-sm text-destructive">
                  {reasonError}
                </p>
              ) : (
                <p id="action-reason-hint" className="text-sm text-muted-foreground">
                  Kept in the audit trail with your name. Don&apos;t include personal details.
                </p>
              )}
            </div>
          ) : null}
          <DialogFooter>
            <Button variant="outline" disabled={pending} onClick={() => onOpenChange(false)}>
              Cancel
            </Button>
            <Button type="submit" variant={destructive ? "destructive" : "default"} disabled={pending || conflict}>
              {pending ? "Saving…" : confirmLabel}
            </Button>
          </DialogFooter>
        </form>
      </DialogContent>
    </Dialog>
  );
}
