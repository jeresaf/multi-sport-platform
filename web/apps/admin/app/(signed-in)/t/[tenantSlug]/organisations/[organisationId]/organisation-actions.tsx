"use client";

// The organisation header's status moves (S07 D12; state-machines.md, Organisation). Every move needs a reason (audit
// `REQUIRED_WITH_REASON`). Only the moves the machine allows from the organisation's status are offered; unarchiving
// goes back to the status it had before it was archived, read from its history. Its teams' statuses never follow.
// Edit details joins these with T6b-2.
import { Button } from "@msp/ui/components/button";
import { useRef, useState } from "react";
import { ActionDialog } from "@/components/action-dialog";
import { UnavailableNote } from "@/components/unavailable-note";
import { humanizeCode } from "@/lib/codes";
import { useTenantWorkspace } from "@/lib/tenant-workspace";
import {
  organisationMovesFrom,
  unarchiveTo,
  useChangeOrganisationStatus,
  useOrganisationStatusChanges,
  useReloadOrganisation,
  type OpenedOrganisation,
  type Organisation,
  type OrganisationStatus,
} from "../organisations-data";

export const statusAction = "organisation.status.change";

export type OrganisationMove = "activate" | "deactivate" | "suspend" | "lift" | "dissolve" | "archive" | "unarchive";

type MoveWords = { label: string; description: string; done: string; destructive?: boolean };

const teamsKeep = "Its teams keep their own statuses.";

export const organisationMoves: Readonly<Record<OrganisationMove, MoveWords>> = {
  activate: {
    label: "Reactivate",
    description: `It is active again. ${teamsKeep}`,
    done: "reactivated",
  },
  deactivate: {
    label: "Make inactive",
    description: `It isn't taking part for now (between seasons, say) and can be reactivated. ${teamsKeep}`,
    done: "made inactive",
  },
  suspend: {
    label: "Suspend",
    description: `An administrative stop by your organisation's staff, which can be lifted. ${teamsKeep}`,
    done: "suspended",
    destructive: true,
  },
  lift: {
    label: "Lift suspension",
    description: `It is active again. ${teamsKeep}`,
    done: "suspension lifted",
  },
  dissolve: {
    label: "Dissolve",
    description: `It no longer exists. This is final: it can't take a parent, member organisations, new teams, aliases or identifiers, and can only be archived. ${teamsKeep}`,
    done: "dissolved",
    destructive: true,
  },
  archive: {
    label: "Archive",
    description:
      "It leaves the directory and the public site. The record and its history stay and can still be corrected. It can be unarchived.",
    done: "archived",
  },
  unarchive: {
    label: "Unarchive",
    description: "It goes back to the status it had before it was archived.",
    done: "unarchived",
  },
};

/** The moves offered from a status, as the dialog names them. */
export function organisationMovesOffered(status: OrganisationStatus): OrganisationMove[] {
  if (status === "ARCHIVED") {
    return ["unarchive"];
  }
  return organisationMovesFrom(status).map((target): OrganisationMove => {
    switch (target) {
      case "ACTIVE":
        return status === "SUSPENDED" ? "lift" : "activate";
      case "INACTIVE":
        return "deactivate";
      case "SUSPENDED":
        return "suspend";
      case "DISSOLVED":
        return "dissolve";
      default:
        return "archive";
    }
  });
}

const targets: Readonly<Record<Exclude<OrganisationMove, "unarchive">, OrganisationStatus>> = {
  activate: "ACTIVE",
  deactivate: "INACTIVE",
  suspend: "SUSPENDED",
  lift: "ACTIVE",
  dissolve: "DISSOLVED",
  archive: "ARCHIVED",
};

const noteId = "organisation-actions-note";
/** The header's status badge, focused after a move. */
export const organisationStatusId = "organisation-status";

export function OrganisationActions({ organisation }: Readonly<{ organisation: Organisation }>) {
  const { can, unavailable } = useTenantWorkspace();
  const [open, setOpen] = useState<OrganisationMove | null>(null);
  const [announcement, setAnnouncement] = useState("");
  const moved = useRef(false);

  // A viewer holds neither the action nor a reason it is unavailable: no control at all.
  const refusal = can(statusAction) ? null : unavailable(statusAction);
  if (!can(statusAction) && refusal === null) {
    return null;
  }
  const moves = organisationMovesOffered(organisation.status);
  const disabled = refusal === null ? {} : { disabled: true, "aria-describedby": noteId };

  return (
    <div className="grid gap-2">
      <div className="flex flex-wrap gap-2">
        {moves.map((move) => (
          <Button
            key={move}
            variant={organisationMoves[move].destructive ? "destructive" : "outline"}
            onClick={() => setOpen(move)}
            {...disabled}
          >
            {organisationMoves[move].label}
          </Button>
        ))}
      </div>
      {refusal ? <UnavailableNote id={noteId} reason={refusal} /> : null}
      <p role="status" className="sr-only">
        {announcement}
      </p>
      {open ? (
        <MoveDialog
          organisation={organisation}
          move={open}
          onOpenChange={(value) => !value && setOpen(null)}
          onSaved={(message) => {
            moved.current = true;
            setAnnouncement(message);
            setOpen(null);
          }}
          onCloseAutoFocus={(event) => {
            const target = moved.current ? document.getElementById(organisationStatusId) : null;
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
  organisation,
  move,
  onOpenChange,
  onSaved,
  onCloseAutoFocus,
}: Readonly<{
  organisation: Organisation;
  move: OrganisationMove;
  onOpenChange: (open: boolean) => void;
  onSaved: (announcement: string) => void;
  onCloseAutoFocus: (event: Event) => void;
}>) {
  // The version the dialog opened on: a change is based on what the user saw.
  const [opened] = useState<OpenedOrganisation>(() => ({ id: organisation.id, version: organisation.version }));
  const words = organisationMoves[move];
  const change = useChangeOrganisationStatus(opened);
  const reload = useReloadOrganisation(organisation.id);
  const history = useOrganisationStatusChanges(organisation.id);
  const before = move === "unarchive" ? unarchiveTo(history.data?.items ?? []) : null;
  const target = move === "unarchive" ? before : targets[move];
  const description =
    move === "unarchive" && before
      ? `It goes back to ${humanizeCode(before).toLowerCase()}, the status it had before it was archived.`
      : words.description;
  return (
    <ActionDialog
      open
      onOpenChange={onOpenChange}
      title={`${words.label}: ${organisation.displayName}`}
      description={description}
      confirmLabel={words.label}
      destructive={words.destructive}
      withReason
      reasonHint="Kept in the status history and the audit trail with your name. Don't include personal details."
      pending={change.isPending}
      error={change.error ?? (history.isError ? history.error : null)}
      // While the history is read again, an earlier archiving's status could still be shown (review #136 N2).
      confirmDisabled={target === null || (move === "unarchive" && history.isFetching)}
      onConfirm={(reason) => {
        if (target) {
          change.mutate(
            { status: target, reason: reason ?? "" },
            { onSuccess: () => onSaved(`${organisation.displayName}: ${words.done}.`) },
          );
        }
      }}
      onReload={() => {
        void reload();
        onOpenChange(false);
      }}
      onCloseAutoFocus={onCloseAutoFocus}
    />
  );
}
