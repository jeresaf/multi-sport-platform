"use client";

// The person's photo (S06 D16, D19): `person.photo.set` adds or replaces it (one JPEG, PNG or WebP up to 5 MB; the API
// keeps resized copies without their EXIF data, not the original) and `person.photo.remove` takes it down. An archived
// or deceased person gets no new photo (D9); removing works in any status (a correction).
import { Button } from "@msp/ui/components/button";
import { ImageUp, Trash2 } from "lucide-react";
import { useRef, useState } from "react";
import { ActionDialog } from "@/components/action-dialog";
import { CommandDialog } from "@/components/command-dialog";
import { clearFileInput, FileField, fileFields, fileRefused, fileSizeError } from "@/components/file-field";
import { UnavailableNote } from "@/components/unavailable-note";
import { useChosenFile } from "@/components/use-chosen-file";
import { fieldError, needsAlert } from "@/lib/form-errors";
import { useTenantWorkspace } from "@/lib/tenant-workspace";
import type { Person } from "../people-data";
import { identityActions, photoAccept, photoMaxBytes, useRemovePhoto, useSetPhoto } from "./identity-data";
import { isClosedPerson, useReloadPerson, type OpenedPerson } from "./profile-data";

const noteId = "photo-note";
const setButtonId = "photo-set";

type Open = "set" | "remove" | null;

export function PhotoControl({ person }: Readonly<{ person: Person }>) {
  const { can, unavailable } = useTenantWorkspace();
  const [open, setOpen] = useState<Open>(null);
  const [announcement, setAnnouncement] = useState("");
  // After a change the focus goes to Add or Change photo: Remove photo is gone once the photo is.
  const changed = useRef(false);
  const offered = (action: string) => can(action) || unavailable(action) !== null;
  const hasPhoto = person.photo !== null;
  const setOffered = offered(identityActions.setPhoto);
  const removeOffered = hasPhoto && offered(identityActions.removePhoto);
  if (!setOffered && !removeOffered) {
    return null;
  }

  const setReason =
    unavailable(identityActions.setPhoto) ??
    (isClosedPerson(person)
      ? `${person.displayName} is ${person.status === "DECEASED" ? "deceased" : "archived"}: no new photo can be added.`
      : null);
  const removeReason = unavailable(identityActions.removePhoto);
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

  return (
    <div className="grid gap-2">
      <div className="flex flex-wrap gap-2">
        {setOffered ? (
          <Button id={setButtonId} variant="outline" onClick={() => setOpen("set")} {...describedBy(setReason)}>
            <ImageUp aria-hidden="true" />
            {hasPhoto ? "Change photo" : "Add photo"}
          </Button>
        ) : null}
        {removeOffered ? (
          <Button variant="outline" onClick={() => setOpen("remove")} {...describedBy(removeReason)}>
            <Trash2 aria-hidden="true" />
            Remove photo
          </Button>
        ) : null}
      </div>
      {notes.map((note) => (
        <UnavailableNote key={note.id} id={note.id} reason={note.reason} />
      ))}
      <p role="status" className="sr-only">
        {announcement}
      </p>
      {open === "set" ? (
        <PhotoDialog
          person={person}
          onOpenChange={(value) => !value && setOpen(null)}
          onSaved={saved}
          onCloseAutoFocus={focusChanged}
        />
      ) : null}
      {open === "remove" ? (
        <RemovePhotoDialog
          person={person}
          onOpenChange={(value) => !value && setOpen(null)}
          onSaved={saved}
          onCloseAutoFocus={focusChanged}
        />
      ) : null}
    </div>
  );
}

type DialogProps = Readonly<{
  person: Person;
  onOpenChange: (open: boolean) => void;
  onSaved: (announcement: string) => void;
  onCloseAutoFocus: (event: Event) => void;
}>;

function PhotoDialog({ person, onOpenChange, onSaved, onCloseAutoFocus }: DialogProps) {
  // The version the dialog opened on (#110 R1): the photo's URLs are read again while it is open.
  const [opened] = useState<OpenedPerson>(() => ({ id: person.id, version: person.version }));
  const [replacing] = useState(person.photo !== null);
  const { file, preview, choose } = useChosenFile();
  const [fileError, setFileError] = useState<string | undefined>();
  const save = useSetPhoto(opened);
  const reload = useReloadPerson(person.id);

  return (
    <CommandDialog
      title={`${replacing ? "Change" : "Add"} the photo of ${person.displayName}`}
      description={
        replacing
          ? "The new photo replaces the current one, here and wherever the photo is shown."
          : "The photo shows here and, with their public profile on, on the organisation's public site."
      }
      submitLabel={replacing ? "Change photo" : "Add photo"}
      pending={save.isPending}
      error={save.error}
      showAlert={save.isError && needsAlert(save.error, fileFields)}
      confirmDisabled={file === undefined}
      onSubmit={() => {
        const sizeError = fileSizeError(file, photoMaxBytes);
        setFileError(sizeError);
        if (file && !sizeError) {
          save.mutate(file, {
            onSuccess: () => onSaved(replacing ? "Photo changed." : "Photo added."),
            // A refused file isn't sent again: another is chosen (#111 R4, #112 R4).
            onError: (error) => {
              if (fileRefused(error)) {
                choose(undefined);
                clearFileInput("photo-file", true);
              }
            },
          });
        }
      }}
      onOpenChange={onOpenChange}
      onCloseAutoFocus={onCloseAutoFocus}
      onReload={() => {
        void reload();
        onOpenChange(false);
      }}
    >
      <FileField
        id="photo-file"
        label="Photo"
        accept={photoAccept}
        hint="A JPEG, PNG or WebP up to 5 MB, of the person's face. Smaller copies are kept, without the camera's details such as where it was taken."
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
          alt="The chosen photo"
          className="size-40 justify-self-center rounded-full border object-cover"
        />
      ) : null}
    </CommandDialog>
  );
}

function RemovePhotoDialog({ person, onOpenChange, onSaved, onCloseAutoFocus }: DialogProps) {
  const [opened] = useState<OpenedPerson>(() => ({ id: person.id, version: person.version }));
  const remove = useRemovePhoto(opened);
  const reload = useReloadPerson(person.id);
  return (
    <ActionDialog
      open
      onOpenChange={onOpenChange}
      title={`Remove the photo of ${person.displayName}`}
      description="The photo is no longer shown, here or on the public site. A new one can be added later."
      confirmLabel="Remove photo"
      destructive
      pending={remove.isPending}
      error={remove.error}
      onConfirm={() => remove.mutate(undefined, { onSuccess: () => onSaved("Photo removed.") })}
      onReload={() => {
        void reload();
        onOpenChange(false);
      }}
      onCloseAutoFocus={onCloseAutoFocus}
    />
  );
}
