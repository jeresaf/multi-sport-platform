"use client";

// The venue header's status moves (S07 D12; state-machines.md, Venue). Every move needs a reason (audit
// `REQUIRED_WITH_REASON`). Only the moves the machine allows from the venue's status are offered; unarchiving goes back
// to the status the venue had before it was archived, read from its history.
import { Button } from "@msp/ui/components/button";
import { useRef, useState } from "react";
import { ActionDialog } from "@/components/action-dialog";
import { UnavailableNote } from "@/components/unavailable-note";
import { humanizeCode } from "@/lib/codes";
import { useTenantWorkspace } from "@/lib/tenant-workspace";
import {
  unarchiveTo,
  useChangeVenueStatus,
  useReloadVenue,
  useVenueStatusChanges,
  venueMovesFrom,
  type OpenedVenue,
  type Venue,
  type VenueStatus,
} from "../venues-data";

export const statusAction = "venue.status.change";

export type VenueMove = "unavailable" | "available" | "close" | "reopen" | "archive" | "unarchive";

type MoveWords = { label: string; description: string; done: string; destructive?: boolean };

export const venueMoves: Readonly<Record<VenueMove, MoveWords>> = {
  unavailable: {
    label: "Mark unavailable",
    description: "It can't host contests for now (works, flooding, a booking) and can be made available again.",
    done: "marked temporarily unavailable",
  },
  available: {
    label: "Mark available",
    description: "It can host contests again.",
    done: "made available",
  },
  close: {
    label: "Close venue",
    description:
      "It no longer hosts contests: it can't become a team's home venue or take new aliases or identifiers (teams that already call it home keep it). It can be reopened.",
    done: "closed",
    destructive: true,
  },
  reopen: {
    label: "Reopen",
    description: "It is active again and can host contests.",
    done: "reopened",
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
export function venueMovesOffered(status: VenueStatus): VenueMove[] {
  if (status === "ARCHIVED") {
    return ["unarchive"];
  }
  return venueMovesFrom(status).map((target): VenueMove => {
    switch (target) {
      case "TEMPORARILY_UNAVAILABLE":
        return "unavailable";
      case "CLOSED":
        return "close";
      case "ARCHIVED":
        return "archive";
      default:
        return status === "CLOSED" ? "reopen" : "available";
    }
  });
}

const targets: Readonly<Record<Exclude<VenueMove, "unarchive">, VenueStatus>> = {
  unavailable: "TEMPORARILY_UNAVAILABLE",
  available: "ACTIVE",
  close: "CLOSED",
  reopen: "ACTIVE",
  archive: "ARCHIVED",
};

const noteId = "venue-actions-note";
/** The header's status badge, focused after a move. */
export const venueStatusId = "venue-status";

export function VenueActions({ venue }: Readonly<{ venue: Venue }>) {
  const { can, unavailable } = useTenantWorkspace();
  const [open, setOpen] = useState<VenueMove | null>(null);
  const [announcement, setAnnouncement] = useState("");
  const moved = useRef(false);

  // A viewer holds neither the action nor a reason it is unavailable: no control at all.
  const refusal = can(statusAction) ? null : unavailable(statusAction);
  if (!can(statusAction) && refusal === null) {
    return null;
  }
  const moves = venueMovesOffered(venue.status);
  const disabled = refusal === null ? {} : { disabled: true, "aria-describedby": noteId };

  return (
    <div className="grid gap-2">
      <div className="flex flex-wrap gap-2">
        {moves.map((move) => (
          <Button
            key={move}
            variant={venueMoves[move].destructive ? "destructive" : "outline"}
            onClick={() => setOpen(move)}
            {...disabled}
          >
            {venueMoves[move].label}
          </Button>
        ))}
      </div>
      {refusal ? <UnavailableNote id={noteId} reason={refusal} /> : null}
      <p role="status" className="sr-only">
        {announcement}
      </p>
      {open ? (
        <MoveDialog
          venue={venue}
          move={open}
          onOpenChange={(value) => !value && setOpen(null)}
          onSaved={(message) => {
            moved.current = true;
            setAnnouncement(message);
            setOpen(null);
          }}
          onCloseAutoFocus={(event) => {
            const target = moved.current ? document.getElementById(venueStatusId) : null;
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
  venue,
  move,
  onOpenChange,
  onSaved,
  onCloseAutoFocus,
}: Readonly<{
  venue: Venue;
  move: VenueMove;
  onOpenChange: (open: boolean) => void;
  onSaved: (announcement: string) => void;
  onCloseAutoFocus: (event: Event) => void;
}>) {
  // The version the dialog opened on: a change is based on what the user saw.
  const [opened] = useState<OpenedVenue>(() => ({ id: venue.id, version: venue.version }));
  const words = venueMoves[move];
  const change = useChangeVenueStatus(opened);
  const reload = useReloadVenue(venue.id);
  const history = useVenueStatusChanges(venue.id);
  const before = move === "unarchive" ? unarchiveTo(history.data?.items ?? []) : null;
  const target = move === "unarchive" ? before : targets[move];
  const description =
    move === "unarchive" && before ? `It goes back to ${humanizeCode(before).toLowerCase()}, the status it had before it was archived.` : words.description;
  return (
    <ActionDialog
      open
      onOpenChange={onOpenChange}
      title={`${words.label}: ${venue.name}`}
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
          change.mutate({ status: target, reason: reason ?? "" }, { onSuccess: () => onSaved(`${venue.name}: ${words.done}.`) });
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
