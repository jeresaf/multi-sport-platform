"use client";

// The team header's status moves (S07 D12; state-machines.md, Team). Every move needs a reason (audit
// `REQUIRED_WITH_REASON`). Only the moves the machine allows from the team's status are offered; unarchiving goes back
// to the status it had before it was archived, read from its history. Reforming a disbanded team and unarchiving one
// take a place under the plan's team limit (D14): at the limit the dialog shows the refusal with the limit and stays
// open. Edit details (D11) opens its own page in any status; Rename (D8) its own page while the team isn't disbanded or
// archived.
import { Button } from "@msp/ui/components/button";
import { PenLine, Pencil } from "lucide-react";
import Link from "next/link";
import { useRef, useState } from "react";
import { ActionDialog } from "@/components/action-dialog";
import { UnavailableNote } from "@/components/unavailable-note";
import { actionAccess } from "@/lib/action-access";
import { humanizeCode } from "@/lib/codes";
import { useTenantWorkspace } from "@/lib/tenant-workspace";
import { tenantPath } from "@/lib/workspace";
import {
  acceptsRename,
  editTeamActions,
  renameClosedReason,
  renameTeamActions,
  teamMovesFrom,
  unarchiveTo,
  useChangeTeamStatus,
  useReloadTeam,
  useTeamStatusChanges,
  type OpenedTeam,
  type Team,
  type TeamStatus,
} from "../teams-data";

export const statusAction = "team.status.change";

export type TeamMove = "activate" | "deactivate" | "suspend" | "lift" | "disband" | "reform" | "archive" | "unarchive";

type MoveWords = { label: string; description: string; done: string; destructive?: boolean };

const takesPlace = "It counts toward your plan's team limit again.";
const freesPlace = "It frees a place under your plan's team limit.";

export const teamMoves: Readonly<Record<TeamMove, MoveWords>> = {
  activate: {
    label: "Reactivate",
    description: "It is active again.",
    done: "reactivated",
  },
  deactivate: {
    label: "Make inactive",
    description: "It isn't taking part for now (between seasons, say) and can be reactivated.",
    done: "made inactive",
  },
  suspend: {
    label: "Suspend",
    description: "An administrative stop by your organisation's staff, which can be lifted.",
    done: "suspended",
    destructive: true,
  },
  lift: {
    label: "Lift suspension",
    description: "It is active again.",
    done: "suspension lifted",
  },
  disband: {
    label: "Disband",
    description:
      "The team no longer plays. It takes no new aliases, identifiers or images and frees a place under your plan's team limit. It can be reformed later.",
    done: "disbanded",
    destructive: true,
  },
  reform: {
    label: "Reform",
    description: `The team plays again and is active. ${takesPlace}`,
    done: "reformed",
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
export function teamMovesOffered(status: TeamStatus): TeamMove[] {
  if (status === "ARCHIVED") {
    return ["unarchive"];
  }
  return teamMovesFrom(status).map((target): TeamMove => {
    switch (target) {
      case "ACTIVE":
        return status === "SUSPENDED" ? "lift" : status === "DISBANDED" ? "reform" : "activate";
      case "INACTIVE":
        return "deactivate";
      case "SUSPENDED":
        return "suspend";
      case "DISBANDED":
        return "disband";
      default:
        return "archive";
    }
  });
}

const targets: Readonly<Record<Exclude<TeamMove, "unarchive">, TeamStatus>> = {
  activate: "ACTIVE",
  deactivate: "INACTIVE",
  suspend: "SUSPENDED",
  lift: "ACTIVE",
  disband: "DISBANDED",
  reform: "ACTIVE",
  archive: "ARCHIVED",
};

const noteId = "team-actions-note";
const editNoteId = "team-edit-note";
const renameNoteId = "team-rename-note";
/** The header's status badge, focused after a move. */
export const teamStatusId = "team-status";

/** Whether going back to this status takes a place under `MAX_TEAMS` (D14: active, inactive or suspended). */
function counted(status: TeamStatus | null): boolean {
  return status === "ACTIVE" || status === "INACTIVE" || status === "SUSPENDED";
}

export function TeamActions({ team }: Readonly<{ team: Team }>) {
  const { tenant, can, unavailable } = useTenantWorkspace();
  const [open, setOpen] = useState<TeamMove | null>(null);
  const [announcement, setAnnouncement] = useState("");
  const moved = useRef(false);

  // A viewer holds none of the actions nor a reason one is unavailable: no control at all.
  const refusal = can(statusAction) ? null : unavailable(statusAction);
  const movesOffered = can(statusAction) || refusal !== null;
  const edit = actionAccess(editTeamActions, can, unavailable);
  const rename = actionAccess(renameTeamActions, can, unavailable);
  const renameOffered = rename.allowed || rename.reason !== null;
  if (!movesOffered && !edit.allowed && edit.reason === null && !renameOffered) {
    return null;
  }
  const moves = movesOffered ? teamMovesOffered(team.status) : [];
  const disabled = refusal === null ? {} : { disabled: true, "aria-describedby": noteId };
  // Each disabled control points at its own reason; one note when several are unavailable for the same one.
  const editNote = edit.reason !== null && edit.reason !== refusal ? edit.reason : null;
  const renameReason = rename.reason ?? (acceptsRename(team.status) ? null : renameClosedReason(team));
  const renameNote = renameReason !== null && renameReason !== refusal && renameReason !== editNote ? renameReason : null;
  const renameNoteFor =
    renameReason === null ? undefined : renameNote ? renameNoteId : renameReason === editNote ? editNoteId : noteId;
  const teamHref = `${tenantPath(tenant.slug)}/teams/${team.id}`;

  return (
    <div className="grid gap-2">
      <div className="flex flex-wrap gap-2">
        {edit.allowed ? (
          <Button asChild variant="outline">
            <Link href={`${teamHref}/edit`}>
              <Pencil aria-hidden="true" />
              Edit details
            </Link>
          </Button>
        ) : edit.reason ? (
          <Button variant="outline" disabled aria-describedby={editNote ? editNoteId : noteId}>
            <Pencil aria-hidden="true" />
            Edit details
          </Button>
        ) : null}
        {renameOffered ? (
          renameReason === null ? (
            <Button asChild variant="outline">
              <Link href={`${teamHref}/rename`}>
                <PenLine aria-hidden="true" />
                Rename
              </Link>
            </Button>
          ) : (
            <Button variant="outline" disabled aria-describedby={renameNoteFor}>
              <PenLine aria-hidden="true" />
              Rename
            </Button>
          )
        ) : null}
        {moves.map((move) => (
          <Button
            key={move}
            variant={teamMoves[move].destructive ? "destructive" : "outline"}
            onClick={() => setOpen(move)}
            {...disabled}
          >
            {teamMoves[move].label}
          </Button>
        ))}
      </div>
      {refusal ? <UnavailableNote id={noteId} reason={refusal} /> : null}
      {editNote ? <UnavailableNote id={editNoteId} reason={editNote} /> : null}
      {renameNote ? <UnavailableNote id={renameNoteId} reason={renameNote} /> : null}
      <p role="status" className="sr-only">
        {announcement}
      </p>
      {open ? (
        <MoveDialog
          team={team}
          move={open}
          onOpenChange={(value) => !value && setOpen(null)}
          onSaved={(message) => {
            moved.current = true;
            setAnnouncement(message);
            setOpen(null);
          }}
          onCloseAutoFocus={(event) => {
            const target = moved.current ? document.getElementById(teamStatusId) : null;
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
  team,
  move,
  onOpenChange,
  onSaved,
  onCloseAutoFocus,
}: Readonly<{
  team: Team;
  move: TeamMove;
  onOpenChange: (open: boolean) => void;
  onSaved: (announcement: string) => void;
  onCloseAutoFocus: (event: Event) => void;
}>) {
  // The version the dialog opened on: a change is based on what the user saw.
  const [opened] = useState<OpenedTeam>(() => ({ id: team.id, version: team.version }));
  const words = teamMoves[move];
  const change = useChangeTeamStatus(opened);
  const reload = useReloadTeam(team.id);
  const history = useTeamStatusChanges(team.id);
  const before = move === "unarchive" ? unarchiveTo(history.data?.items ?? []) : null;
  const target = move === "unarchive" ? before : targets[move];
  // Only a team that counts toward the limit frees a place by archiving: a disbanded one already has (D14).
  const description =
    move === "unarchive" && before
      ? `It goes back to ${humanizeCode(before).toLowerCase()}, the status it had before it was archived.${counted(before) ? ` ${takesPlace}` : ""}`
      : move === "archive" && counted(team.status)
        ? `${words.description} ${freesPlace}`
        : words.description;
  return (
    <ActionDialog
      open
      onOpenChange={onOpenChange}
      title={`${words.label}: ${team.name}`}
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
          change.mutate({ status: target, reason: reason ?? "" }, { onSuccess: () => onSaved(`${team.name}: ${words.done}.`) });
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
