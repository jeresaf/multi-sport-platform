"use client";

// The person header's actions (S06 D8, D9; T7c-2b): Edit details, and the moves of the person's status. Deactivate and
// reactivate take an optional reason; archive, unarchive and mark as deceased need one (audit `REQUIRED_WITH_REASON`).
// Only the moves D9 allows from the person's status are offered; marking as deceased is final in S06.
import { Button } from "@msp/ui/components/button";
import { useMutation, useQueryClient } from "@tanstack/react-query";
import { Pencil } from "lucide-react";
import Link from "next/link";
import { useRef, useState } from "react";
import { ActionDialog } from "@/components/action-dialog";
import { UnavailableNote } from "@/components/unavailable-note";
import { call } from "@/lib/api";
import { ifMatch } from "@/lib/concurrency";
import { useTenantWorkspace } from "@/lib/tenant-workspace";
import { tenantPath } from "@/lib/workspace";
import { personStatusChangesQueryKey, type Person, type PersonStatus } from "../people-data";
import { editDetailsActions } from "./edit/edit-access";
import { useKeepPerson, useReloadPerson, type OpenedPerson } from "./profile-data";

export type PersonMove = "deactivate" | "reactivate" | "archive" | "unarchive" | "mark-deceased";

type MoveWords = {
  action: string;
  label: string;
  description: string;
  reason: true | "optional";
  done: string;
  destructive?: boolean;
};

export const personMoves: Readonly<Record<PersonMove, MoveWords>> = {
  deactivate: {
    action: "person.deactivate",
    label: "Deactivate",
    description: "They stay on record as inactive and can be made active again.",
    reason: "optional",
    done: "made inactive",
  },
  reactivate: {
    action: "person.reactivate",
    label: "Reactivate",
    description: "They are active again.",
    reason: "optional",
    done: "made active",
  },
  archive: {
    action: "person.archive",
    label: "Archive",
    description:
      "The record and its history stay and can still be corrected, but take no new role, alias, identifier, consent or photo. It can be unarchived.",
    reason: true,
    done: "archived",
  },
  unarchive: {
    action: "person.unarchive",
    label: "Unarchive",
    description: "They are active again and can take new roles, aliases, identifiers, consents and a photo.",
    reason: true,
    done: "unarchived",
  },
  "mark-deceased": {
    action: "person.mark_deceased",
    label: "Mark as deceased",
    description:
      "This is final: the record and its history stay and can still be corrected, but the status can't be changed again.",
    reason: true,
    done: "marked as deceased",
    destructive: true,
  },
};

/** The moves D9 allows from a status (state-machines.md, Person). */
export function personMovesFrom(status: PersonStatus): PersonMove[] {
  switch (status) {
    case "ACTIVE":
      return ["deactivate", "archive", "mark-deceased"];
    case "INACTIVE":
      return ["reactivate", "archive", "mark-deceased"];
    case "ARCHIVED":
      return ["unarchive"];
    default:
      return [];
  }
}

/** Moves the person's status from the version the dialog opened on; the status history is read again. */
function useMovePerson(person: OpenedPerson, move: PersonMove) {
  const { tenant, client } = useTenantWorkspace();
  const queryClient = useQueryClient();
  const keep = useKeepPerson();
  return useMutation({
    mutationFn: (reason: string | undefined) => {
      const params = { path: { personId: person.id }, header: ifMatch(person.version) };
      switch (move) {
        case "deactivate":
          return call(client.POST("/api/admin/v1/people/{personId}/deactivate", { params, body: { reason: reason ?? null } }));
        case "reactivate":
          return call(client.POST("/api/admin/v1/people/{personId}/reactivate", { params, body: { reason: reason ?? null } }));
        case "archive":
          return call(client.POST("/api/admin/v1/people/{personId}/archive", { params, body: { reason: reason ?? "" } }));
        case "unarchive":
          return call(client.POST("/api/admin/v1/people/{personId}/unarchive", { params, body: { reason: reason ?? "" } }));
        case "mark-deceased":
          return call(
            client.POST("/api/admin/v1/people/{personId}/mark-deceased", { params, body: { reason: reason ?? "" } }),
          );
      }
    },
    onSuccess: (saved) => {
      keep(saved);
      void queryClient.invalidateQueries({ queryKey: personStatusChangesQueryKey(tenant.tenantId, person.id) });
    },
  });
}

const noteId = "person-actions-note";
/** The header's status badge, focused after a move. */
export const personStatusId = "person-status";

export function PersonActions({ person }: Readonly<{ person: Person }>) {
  const { tenant, can, unavailable } = useTenantWorkspace();
  const [open, setOpen] = useState<PersonMove | null>(null);
  const [announcement, setAnnouncement] = useState("");
  const moved = useRef(false);

  const editAllowed = editDetailsActions.every((action) => can(action));
  const editReason = editAllowed ? null : unavailable("person.update");
  const moves = personMovesFrom(person.status).filter(
    (move) => can(personMoves[move].action) || unavailable(personMoves[move].action) !== null,
  );
  if (!editAllowed && editReason === null && moves.length === 0) {
    return null;
  }

  // One note per distinct reason (on a read-only tenant, one for all).
  const reasons = [
    ...new Set(
      [editReason, ...moves.map((move) => unavailable(personMoves[move].action))].filter(
        (reason): reason is string => reason !== null,
      ),
    ),
  ];
  const describedBy = (reason: string | null) =>
    reason === null ? {} : { disabled: true, "aria-describedby": `${noteId}-${reasons.indexOf(reason)}` };
  const editHref = `${tenantPath(tenant.slug)}/people/${person.id}/edit`;

  return (
    <div className="grid gap-2">
      <div className="flex flex-wrap gap-2">
        {editAllowed ? (
          <Button asChild variant="outline">
            <Link href={editHref}>
              <Pencil aria-hidden="true" />
              Edit details
            </Link>
          </Button>
        ) : editReason !== null ? (
          <Button variant="outline" {...describedBy(editReason)}>
            <Pencil aria-hidden="true" />
            Edit details
          </Button>
        ) : null}
        {moves.map((move) => (
          <Button
            key={move}
            variant={personMoves[move].destructive ? "destructive" : "outline"}
            onClick={() => setOpen(move)}
            {...describedBy(unavailable(personMoves[move].action))}
          >
            {personMoves[move].label}
          </Button>
        ))}
      </div>
      {reasons.map((reason, index) => (
        <UnavailableNote key={reason} id={`${noteId}-${index}`} reason={reason} />
      ))}
      <p role="status" className="sr-only">
        {announcement}
      </p>
      {open ? (
        <MoveDialog
          person={person}
          move={open}
          onOpenChange={(value) => !value && setOpen(null)}
          onSaved={(message) => {
            moved.current = true;
            setAnnouncement(message);
            setOpen(null);
          }}
          onCloseAutoFocus={(event) => {
            const target = moved.current ? document.getElementById(personStatusId) : null;
            moved.current = false;
            if (target) {
              event.preventDefault();
              target.focus();
            }
          }}
        />
      ) : null}
    </div>
  );
}

function MoveDialog({
  person,
  move,
  onOpenChange,
  onSaved,
  onCloseAutoFocus,
}: Readonly<{
  person: Person;
  move: PersonMove;
  onOpenChange: (open: boolean) => void;
  onSaved: (announcement: string) => void;
  onCloseAutoFocus: (event: Event) => void;
}>) {
  // The version the dialog opened on (#110 R1): the person is read again while it is open (the photo's URLs).
  const [opened] = useState<OpenedPerson>(() => ({ id: person.id, version: person.version }));
  const words = personMoves[move];
  const change = useMovePerson(opened, move);
  const reload = useReloadPerson(person.id);
  return (
    <ActionDialog
      open
      onOpenChange={onOpenChange}
      title={`${words.label}: ${person.displayName}`}
      description={words.description}
      confirmLabel={words.label}
      destructive={words.destructive}
      withReason={words.reason}
      reasonHint="Kept in the status history and the audit trail with your name. Don't include personal details."
      pending={change.isPending}
      error={change.error}
      onConfirm={(reason) =>
        change.mutate(reason, { onSuccess: () => onSaved(`${person.displayName}: ${words.done}.`) })
      }
      onReload={() => {
        void reload();
        onOpenChange(false);
      }}
      onCloseAutoFocus={onCloseAutoFocus}
    />
  );
}
