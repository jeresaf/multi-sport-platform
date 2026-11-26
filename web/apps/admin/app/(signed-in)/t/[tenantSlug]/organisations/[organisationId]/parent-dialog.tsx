"use client";

// `organisation.parent.set` (S07 D5, T2c-1): the organisation's parent, found by a search of the directory, or none,
// with a reason (audit `REQUIRED_WITH_REASON`). The open link ends on the tenant's today and the new one starts. The
// organisation itself and its direct children aren't offered and a dissolved one is shown unavailable; a descendant
// further down only the server knows, so its `ORGANISATION_PARENT_CYCLE` shows under the choice.
import { zodResolver } from "@hookform/resolvers/zod";
import { Input } from "@msp/ui/components/input";
import { Label } from "@msp/ui/components/label";
import { Textarea } from "@msp/ui/components/textarea";
import { useEffect, useState } from "react";
import { Controller, useForm } from "react-hook-form";
import { z } from "zod";
import { reasonMaxLength } from "@/components/action-dialog";
import { CommandDialog } from "@/components/command-dialog";
import { Loading } from "@/components/loading";
import { ProblemAlert } from "@/components/problem-alert";
import { StatusBadge } from "@/components/status-badge";
import { humanizeCode } from "@/lib/codes";
import { applyFieldErrors, needsAlert } from "@/lib/form-errors";
import {
  acceptsNewChildren,
  useParentCandidates,
  useReloadOrganisation,
  useSetOrganisationParent,
  type OpenedOrganisation,
  type Organisation,
} from "../organisations-data";

export const parentAction = "organisation.parent.set";

/** The choice that clears the parent. */
const noParent = "none";
const searchDelay = 300;

const schema = z.object({
  parentId: z.string().min(1, "Choose the parent, or no parent."),
  reason: z
    .string()
    .trim()
    .min(1, "Give a reason: it is kept in the audit trail.")
    .max(reasonMaxLength, `Keep the reason under ${reasonMaxLength} characters.`),
});
type Values = z.infer<typeof schema>;
const fields: Readonly<Record<string, keyof Values>> = { parentId: "parentId", reason: "reason" };

export function ParentDialog({
  organisation,
  onClose,
  onSaved,
  onCloseAutoFocus,
}: Readonly<{
  organisation: Organisation;
  onClose: () => void;
  /** After the save: what to announce (the dialog closes). */
  onSaved: (announcement: string) => void;
  /** Where focus goes when the dialog closes (by default, back to what opened it). */
  onCloseAutoFocus?: (event: Event) => void;
}>) {
  // The version the dialog opened on: a change is based on what the user saw.
  const [opened] = useState<OpenedOrganisation>(() => ({ id: organisation.id, version: organisation.version }));
  const set = useSetOrganisationParent(opened);
  const reload = useReloadOrganisation(organisation.id);
  const form = useForm<Values>({ resolver: zodResolver(schema), defaultValues: { parentId: "", reason: "" } });
  const errors = form.formState.errors;
  const [typed, setTyped] = useState("");
  const [q, setQ] = useState("");
  const [chosenName, setChosenName] = useState<string | null>(null);

  useEffect(() => {
    const text = typed.trim();
    const timer = setTimeout(() => setQ(text), searchDelay);
    return () => clearTimeout(timer);
  }, [typed]);
  const candidates = useParentCandidates(q);
  const children = new Set(organisation.children.map((child) => child.id));
  // Itself, its current parent (nothing would change) and its direct children (a cycle) aren't choices.
  // The answer for the text shown: none for an empty box, and an earlier search's kept list (`keepPreviousData`)
  // is still shown while the new one is asked, but isn't described as the answer to it.
  const found =
    q === ""
      ? []
      : (candidates.data?.items ?? []).filter(
          (item) => item.id !== organisation.id && item.id !== organisation.parent?.id && !children.has(item.id),
        );
  const searching = q !== "" && (candidates.isPending || candidates.isPlaceholderData);
  const answered = q !== "" && candidates.isSuccess && !candidates.isPlaceholderData;
  // Read out as the answer changes (a live region), not only when it is looked at.
  const announcement = answered ? (found.length === 0 ? "No organisation found." : `${found.length} found.`) : "";

  const submit = form.handleSubmit(({ parentId, reason }) =>
    set.mutate(
      { parentId: parentId === noParent ? null : parentId, reason: reason.trim() },
      {
        onSuccess: () =>
          onSaved(
            parentId === noParent
              ? `${organisation.displayName}: parent removed.`
              : `${organisation.displayName}: parent changed to ${chosenName ?? "the organisation chosen"}.`,
          ),
        onError: (error) => applyFieldErrors(error, fields, form.setError) },
    ),
  );

  const choiceError = errors.parentId?.message;
  const reasonError = errors.reason?.message;
  const current = organisation.parent;
  // A dissolved or archived organisation can only have its parent cleared (D5): no search.
  const clearOnly = !acceptsNewChildren(organisation.status);
  const name = organisation.displayName;
  const title = !current ? `Set the parent of ${name}` : clearOnly ? `Remove the parent of ${name}` : `Change the parent of ${name}`;
  const description = !current
    ? "The organisation it belongs to, such as the club a school's team plays for or a federation. Its status and its teams' don't change."
    : clearOnly
      ? `Its parent is ${current.displayName}. ${humanizeCode(organisation.status)}, it can't take another; the link ends today and stays in the history.`
      : `Its parent is ${current.displayName}. The link ends today and stays in the history; its status and its teams' don't change.`;

  return (
    <CommandDialog
      title={title}
      description={description}
      submitLabel="Save parent"
      pending={set.isPending}
      error={set.error}
      showAlert={set.isError && needsAlert(set.error, fields)}
      onSubmit={() => void submit()}
      onOpenChange={(open) => !open && onClose()}
      onReload={() => {
        void reload();
        onClose();
      }}
      onCloseAutoFocus={onCloseAutoFocus}
    >
      {clearOnly ? null : (
        <div className="grid gap-2">
          <Label htmlFor="parent-search">Find the parent</Label>
          <Input
            id="parent-search"
            type="search"
            value={typed}
            onChange={(event) => setTyped(event.target.value)}
            placeholder="Name, alias or identifier"
            autoComplete="off"
            aria-describedby="parent-search-hint"
          />
          <p id="parent-search-hint" className="text-sm text-muted-foreground">
            Archived organisations aren&apos;t listed.
          </p>
          <p role="status" className="sr-only">
            {announcement}
          </p>
        </div>
      )}
      <Controller
        control={form.control}
        name="parentId"
        render={({ field }) => (
          <fieldset
            className="grid gap-2"
            aria-invalid={choiceError ? true : undefined}
            aria-describedby={choiceError ? "parent-choice-error" : undefined}
          >
            <legend className="mb-2 text-sm font-medium">Parent</legend>
            {current ? (
              <Choice
                id="parent-none"
                checked={field.value === noParent}
                onChange={() => {
                  field.onChange(noParent);
                  setChosenName(null);
                }}
                label="No parent"
                detail={`${organisation.displayName} stands on its own.`}
              />
            ) : null}
            {field.value && field.value !== noParent && !found.some((item) => item.id === field.value) && chosenName ? (
              // The chosen one stays offered while another search is typed.
              <Choice id={`parent-${field.value}`} checked onChange={() => undefined} label={chosenName} />
            ) : null}
            {searching ? <Loading /> : null}
            {candidates.isError ? <ProblemAlert error={candidates.error} /> : null}
            {answered && found.length === 0 ? (
              <p className="text-sm text-muted-foreground">No organisation found for “{q}”.</p>
            ) : null}
            {found.map((item) => {
              const available = acceptsNewChildren(item.status);
              return (
                <Choice
                  key={item.id}
                  id={`parent-${item.id}`}
                  checked={field.value === item.id}
                  disabled={!available}
                  onChange={() => {
                    field.onChange(item.id);
                    setChosenName(item.displayName);
                  }}
                  label={item.displayName}
                  detail={
                    available
                      ? [humanizeCode(item.type), item.city].filter(Boolean).join(" · ")
                      : `${humanizeCode(item.status)}: it can't take member organisations.`
                  }
                  status={item.status}
                />
              );
            })}
            {choiceError ? (
              <p id="parent-choice-error" className="text-sm text-destructive">
                {choiceError}
              </p>
            ) : null}
          </fieldset>
        )}
      />
      <div className="grid gap-2">
        <Label htmlFor="parent-reason">Reason</Label>
        <Textarea
          id="parent-reason"
          maxLength={reasonMaxLength}
          aria-invalid={reasonError ? true : undefined}
          aria-describedby={reasonError ? "parent-reason-error" : "parent-reason-hint"}
          {...form.register("reason")}
        />
        {reasonError ? (
          <p id="parent-reason-error" className="text-sm text-destructive">
            {reasonError}
          </p>
        ) : (
          <p id="parent-reason-hint" className="text-sm text-muted-foreground">
            Kept in the parent history and the audit trail with your name. Don&apos;t include personal details.
          </p>
        )}
      </div>
    </CommandDialog>
  );
}

function Choice({
  id,
  checked,
  disabled = false,
  onChange,
  label,
  detail,
  status,
}: Readonly<{
  id: string;
  checked: boolean;
  disabled?: boolean;
  onChange: () => void;
  label: string;
  detail?: string;
  status?: string;
}>) {
  return (
    <div className="flex min-h-11 items-start gap-3">
      <input
        id={id}
        type="radio"
        name="parent"
        checked={checked}
        disabled={disabled}
        onChange={onChange}
        aria-describedby={detail ? `${id}-detail` : undefined}
        className="mt-1 size-5 accent-primary"
      />
      <span className="grid gap-0.5">
        <label htmlFor={id} className="flex flex-wrap items-center gap-2 font-medium">
          {label}
          {status && status !== "ACTIVE" ? <StatusBadge status={status} /> : null}
        </label>
        {detail ? (
          <span id={`${id}-detail`} className="text-sm text-muted-foreground">
            {detail}
          </span>
        ) : null}
      </span>
    </div>
  );
}
