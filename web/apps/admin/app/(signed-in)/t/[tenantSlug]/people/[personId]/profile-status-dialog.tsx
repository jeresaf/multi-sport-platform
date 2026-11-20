"use client";

// `*_profile.status.change` (S06 D12, T4a): a profile's move to another status, always with a reason (audit
// `REQUIRED_WITH_REASON`). Archiving is final.
import { useState } from "react";
import { ActionDialog } from "@/components/action-dialog";
import type { Person, ProfileKind } from "../people-data";
import { useChangeProfileStatus, useReloadPerson, type OpenedPerson, type ProfileStatus } from "./profile-data";

/** The button's and the dialog's words for a move to each status. */
export const moveLabels: Readonly<Record<ProfileStatus, string>> = {
  ACTIVE: "Make active",
  INACTIVE: "Make inactive",
  RETIRED: "Retire",
  ARCHIVED: "Archive",
};

const moveDone: Readonly<Record<ProfileStatus, string>> = {
  ACTIVE: "made active",
  INACTIVE: "made inactive",
  RETIRED: "retired",
  ARCHIVED: "archived",
};

const moveDescriptions: Readonly<Record<ProfileStatus, string>> = {
  ACTIVE: "The profile is in use again.",
  INACTIVE: "The profile stays on record and can be made active again.",
  RETIRED: "The profile stays on record as retired and can be made active again.",
  ARCHIVED: "Archiving is final: the profile stays in the history but can't be changed or made active again.",
};

export function ProfileStatusDialog({
  person,
  kind,
  profileId,
  title,
  target,
  onOpenChange,
  onSaved,
  onCloseAutoFocus,
}: Readonly<{
  person: Person;
  kind: ProfileKind;
  profileId: string;
  /** The profile's name in the dialog's title ("the Ringball 7s profile"). */
  title: string;
  target: ProfileStatus;
  onOpenChange: (open: boolean) => void;
  onSaved: (announcement: string, saved: Person) => void;
  onCloseAutoFocus?: (event: Event) => void;
}>) {
  // The version the dialog opened on (R1): a newer read while it is open must not become the change's basis.
  const [opened] = useState<OpenedPerson>(() => ({ id: person.id, version: person.version }));
  const change = useChangeProfileStatus(opened, kind, profileId);
  const reload = useReloadPerson(person.id);
  return (
    <ActionDialog
      open
      onOpenChange={onOpenChange}
      title={`${moveLabels[target]}: ${title}`}
      description={moveDescriptions[target]}
      confirmLabel={moveLabels[target]}
      destructive={target === "ARCHIVED"}
      withReason
      reasonHint="Kept in the profile's history and the audit trail with your name. Don't include personal details."
      pending={change.isPending}
      error={change.error}
      onConfirm={(reason) =>
        change.mutate(
          { status: target, reason: reason ?? "" },
          { onSuccess: (saved) => onSaved(`${title}: ${moveDone[target]}.`, saved) },
        )
      }
      onReload={() => {
        void reload();
        onOpenChange(false);
      }}
      onCloseAutoFocus={onCloseAutoFocus}
    />
  );
}
