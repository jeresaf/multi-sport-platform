"use client";

// A registry record's public image (S07 D15, T4b): an organisation's logo, a team's logo or badge. The set action adds
// or replaces it (one PNG, JPEG or WebP up to 2 MB, at least 64 px on its shorter side; the API keeps WebP copies with
// the transparency, not the original) and the remove action takes it down. A record that takes no new image (a
// dissolved or archived organisation) says why; removing works in any status (a correction). The area supplies the
// calls (`RecordImageApi`) and the words (`RecordImageConfig`); every change sends the version its dialog opened on.
import { Button } from "@msp/ui/components/button";
import { useMutation } from "@tanstack/react-query";
import { ImageUp, Trash2 } from "lucide-react";
import { useRef, useState } from "react";
import { ActionDialog } from "@/components/action-dialog";
import { CommandDialog } from "@/components/command-dialog";
import { clearFileInput, FileField, fileFields, fileRefused, fileSizeError } from "@/components/file-field";
import { UnavailableNote } from "@/components/unavailable-note";
import { useChosenFile } from "@/components/use-chosen-file";
import { fieldError, needsAlert } from "@/lib/form-errors";
import { useTenantWorkspace } from "@/lib/tenant-workspace";

/** The images the API takes for a logo or badge (D15): what the picker offers; the API decides by the file's bytes. */
export const imageAccept = "image/png,image/jpeg,image/webp";
export const imageMaxBytes = 2 * 1024 * 1024;

type OpenedRecord = { id: string; version: number };

/** The area's calls, each answering the record as changed; `keep` puts it in the page's cache. */
export type RecordImageApi<R extends OpenedRecord> = {
  set: (opened: OpenedRecord, file: File) => Promise<R>;
  remove: (opened: OpenedRecord) => Promise<R>;
  keep: (record: R) => void;
  /** Reads the record again (after a concurrency conflict). */
  reload: () => unknown;
};

export type RecordImageConfig = {
  /** Unique on the page: a team shows a logo and a badge control ("logo", "badge"). */
  id: string;
  /** The image, in words ("logo", "badge"). */
  image: string;
  /** The record's name, as titles say it ("Add the logo of Ntinda Swifts SC"). */
  name: string;
  actions: { set: string; remove: string };
  /** Whether the record has the image now. */
  hasImage: boolean;
  /** Why the record takes no new image (dissolved, archived…), or null when it takes one. */
  closedNote: string | null;
};

type Open = "set" | "remove" | null;

const capitalised = (word: string) => word.charAt(0).toUpperCase() + word.slice(1);

export function RecordImageControl<R extends OpenedRecord>({
  record,
  config,
  api,
}: Readonly<{ record: R; config: RecordImageConfig; api: RecordImageApi<R> }>) {
  const { can, unavailable } = useTenantWorkspace();
  const [open, setOpen] = useState<Open>(null);
  const [announcement, setAnnouncement] = useState("");
  // After a change the focus goes to Add or Change: Remove is gone once the image is.
  const changed = useRef(false);
  const offered = (action: string) => can(action) || unavailable(action) !== null;
  const setButtonId = `${config.id}-set`;
  const noteId = `${config.id}-note`;
  const word = capitalised(config.image);
  const setOffered = offered(config.actions.set);
  const removeOffered = config.hasImage && offered(config.actions.remove);
  if (!setOffered && !removeOffered) {
    return null;
  }

  const setReason = unavailable(config.actions.set) ?? config.closedNote;
  const removeReason = unavailable(config.actions.remove);
  // Each button points to its own reason; one note when both have the same (a read-only tenant).
  const notes = [
    setOffered && setReason !== null ? { id: `${noteId}-set`, reason: setReason } : null,
    removeOffered && removeReason !== null && removeReason !== setReason
      ? { id: `${noteId}-remove`, reason: removeReason }
      : null,
  ].filter((note) => note !== null);
  const describedBy = (reason: string | null) =>
    reason !== null
      ? { disabled: true, "aria-describedby": notes.find((note) => note.reason === reason)?.id }
      : {};

  const saved = (message: string) => {
    changed.current = true;
    setAnnouncement(message);
    setOpen(null);
  };
  const focusChanged = (event: Event) => {
    const target = changed.current ? document.getElementById(setButtonId) : null;
    changed.current = false;
    if (target) {
      event.preventDefault();
      target.focus();
    }
  };
  const dialogProps = {
    record,
    config,
    api,
    onOpenChange: (value: boolean) => !value && setOpen(null),
    onSaved: saved,
    onCloseAutoFocus: focusChanged,
  };

  return (
    <div className="grid gap-2">
      <div className="flex flex-wrap gap-2">
        {setOffered ? (
          <Button id={setButtonId} variant="outline" onClick={() => setOpen("set")} {...describedBy(setReason)}>
            <ImageUp aria-hidden="true" />
            {config.hasImage ? `Change ${config.image}` : `Add ${config.image}`}
          </Button>
        ) : null}
        {removeOffered ? (
          <Button variant="outline" onClick={() => setOpen("remove")} {...describedBy(removeReason)}>
            <Trash2 aria-hidden="true" />
            {`Remove ${config.image}`}
          </Button>
        ) : null}
      </div>
      {notes.map((note) => (
        <UnavailableNote key={note.id} id={note.id} reason={note.reason} />
      ))}
      <p role="status" className="sr-only">
        {announcement}
      </p>
      {open === "set" ? <SetImageDialog {...dialogProps} word={word} /> : null}
      {open === "remove" ? <RemoveImageDialog {...dialogProps} word={word} /> : null}
    </div>
  );
}

type DialogProps<R extends OpenedRecord> = Readonly<{
  record: R;
  config: RecordImageConfig;
  api: RecordImageApi<R>;
  word: string;
  onOpenChange: (open: boolean) => void;
  onSaved: (announcement: string) => void;
  onCloseAutoFocus: (event: Event) => void;
}>;

function SetImageDialog<R extends OpenedRecord>({
  record,
  config,
  api,
  word,
  onOpenChange,
  onSaved,
  onCloseAutoFocus,
}: DialogProps<R>) {
  // The version the dialog opened on (#110 R1): the record's image URLs are read again while it is open.
  const [opened] = useState<OpenedRecord>(() => ({ id: record.id, version: record.version }));
  const [replacing] = useState(config.hasImage);
  const { file, preview, choose } = useChosenFile();
  const [fileError, setFileError] = useState<string | undefined>();
  const save = useMutation({ mutationFn: (chosen: File) => api.set(opened, chosen), onSuccess: api.keep });
  const fileId = `${config.id}-file`;

  return (
    <CommandDialog
      title={`${replacing ? "Change" : "Add"} the ${config.image} of ${config.name}`}
      description={
        replacing
          ? `The new ${config.image} replaces the current one, here and on the public site.`
          : `The ${config.image} shows here and on the public site.`
      }
      submitLabel={replacing ? `Change ${config.image}` : `Add ${config.image}`}
      pending={save.isPending}
      error={save.error}
      showAlert={save.isError && needsAlert(save.error, fileFields)}
      confirmDisabled={file === undefined}
      onSubmit={() => {
        const sizeError = fileSizeError(file, imageMaxBytes);
        setFileError(sizeError);
        if (file && !sizeError) {
          save.mutate(file, {
            onSuccess: () => onSaved(`${word} ${replacing ? "changed" : "added"}.`),
            // A refused file isn't sent again: another is chosen (#111 R4, #112 R4).
            onError: (error) => {
              if (fileRefused(error)) {
                choose(undefined);
                clearFileInput(fileId, true);
              }
            },
          });
        }
      }}
      onOpenChange={onOpenChange}
      onCloseAutoFocus={onCloseAutoFocus}
      onReload={() => {
        void api.reload();
        onOpenChange(false);
      }}
    >
      <FileField
        id={fileId}
        label={word}
        accept={imageAccept}
        hint="A PNG, JPEG or WebP up to 2 MB, at least 64 pixels on its shorter side. A PNG keeps its transparent background."
        error={fileError ?? fieldError(save.error, "file")}
        onChange={(chosen) => {
          choose(chosen);
          setFileError(undefined);
          save.reset();
        }}
      />
      {preview ? (
        // A local preview of the chosen file (a blob: URL), not a page asset: next/image has nothing to optimise.
        // eslint-disable-next-line @next/next/no-img-element
        <img
          src={preview}
          alt={`The chosen ${config.image}`}
          className="size-40 justify-self-center rounded-md border object-contain"
        />
      ) : null}
    </CommandDialog>
  );
}

function RemoveImageDialog<R extends OpenedRecord>({
  record,
  config,
  api,
  word,
  onOpenChange,
  onSaved,
  onCloseAutoFocus,
}: DialogProps<R>) {
  const [opened] = useState<OpenedRecord>(() => ({ id: record.id, version: record.version }));
  const remove = useMutation({ mutationFn: () => api.remove(opened), onSuccess: api.keep });
  return (
    <ActionDialog
      open
      onOpenChange={onOpenChange}
      title={`Remove the ${config.image} of ${config.name}`}
      description={`The ${config.image} is no longer shown, here or on the public site. A new one can be added later.`}
      confirmLabel={`Remove ${config.image}`}
      destructive
      pending={remove.isPending}
      error={remove.error}
      onConfirm={() => remove.mutate(undefined, { onSuccess: () => onSaved(`${word} removed.`) })}
      onReload={() => {
        void api.reload();
        onOpenChange(false);
      }}
      onCloseAutoFocus={onCloseAutoFocus}
    />
  );
}
