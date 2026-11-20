"use client";

// The public profile (S06 D15, T6b): `person.public_profile.enable` and `.disable`, each confirmed, no reason. Turning
// it on needs, for anyone who isn't known to be an adult, an active consent to the public profile; an archived or
// deceased person isn't turned on. Turning off works in any status. The control says why it can't be used rather
// than sending a change the API would refuse.
import { Button } from "@msp/ui/components/button";
import { useState } from "react";
import { ActionDialog } from "@/components/action-dialog";
import { UnavailableNote } from "@/components/unavailable-note";
import { useTenantWorkspace } from "@/lib/tenant-workspace";
import type { Person } from "../people-data";
import { consentActions, useSwitchPublicProfile } from "./consent-data";
import { isClosedPerson, useReloadPerson, type OpenedPerson } from "./profile-data";

const noteId = "public-profile-note";

/** Why the public site doesn't show a person whose profile is on (D18), or null when it does. */
export function hiddenBecause(person: Person): string | null {
  if (!person.publicProfileEnabled) {
    return null;
  }
  if (person.status !== "ACTIVE") {
    return "not shown while the person isn't active";
  }
  if (person.ageStatus !== "ADULT" && !person.activeConsentScopes.includes("PUBLIC_PROFILE")) {
    return "not shown without a guardian's consent to the public profile";
  }
  return null;
}

/** Why the profile can't be turned on now, or null. */
function cannotEnable(person: Person): string | null {
  if (isClosedPerson(person)) {
    return `${person.displayName} is ${person.status === "DECEASED" ? "deceased" : "archived"}: the public profile can't be turned on.`;
  }
  if (person.ageStatus !== "ADULT" && !person.activeConsentScopes.includes("PUBLIC_PROFILE")) {
    return person.ageStatus === "MINOR"
      ? "A minor's public profile needs a guardian's consent to the public profile: record it on the Consent tab."
      : "Without a date of birth, the public profile needs a guardian's consent to it: record it on the Consent tab.";
  }
  return null;
}

export function PublicProfileSwitch({ person }: Readonly<{ person: Person }>) {
  const { can, unavailable } = useTenantWorkspace();
  const on = person.publicProfileEnabled;
  const action = on ? consentActions.disable : consentActions.enable;
  const [open, setOpen] = useState(false);
  const [announcement, setAnnouncement] = useState("");
  const offered = can(action) || unavailable(action) !== null;
  const hidden = hiddenBecause(person);

  const reason = unavailable(action) ?? (on ? null : cannotEnable(person));
  return (
    <div className="grid gap-2">
      <div className="flex flex-wrap items-center gap-2">
        <span className="text-sm text-muted-foreground">
          Public profile {on ? "on" : "off"}
          {hidden ? ` (${hidden})` : null}
        </span>
        {offered ? (
          <Button
            variant="outline"
            disabled={reason !== null}
            aria-describedby={reason !== null ? noteId : undefined}
            onClick={() => setOpen(true)}
          >
            {on ? "Turn off public profile" : "Turn on public profile"}
          </Button>
        ) : null}
      </div>
      {offered && reason ? <UnavailableNote id={noteId} reason={reason} /> : null}
      <p role="status" className="sr-only">
        {announcement}
      </p>
      {open ? (
        <SwitchDialog
          person={person}
          on={!on}
          onOpenChange={setOpen}
          onSaved={(message) => {
            setAnnouncement(message);
            setOpen(false);
          }}
        />
      ) : null}
    </div>
  );
}

function SwitchDialog({
  person,
  on,
  onOpenChange,
  onSaved,
}: Readonly<{ person: Person; on: boolean; onOpenChange: (open: boolean) => void; onSaved: (message: string) => void }>) {
  // The version the dialog opened on (#110 R1).
  const [opened] = useState<OpenedPerson>(() => ({ id: person.id, version: person.version }));
  const change = useSwitchPublicProfile(opened, on);
  const reload = useReloadPerson(person.id);
  const description = on
    ? person.ageStatus === "ADULT"
      ? "The organisation's public site shows their display name, nationality, photo and athlete profiles while they are active."
      : "The organisation's public site shows only their display name, and their photo with a guardian's consent to the photo, while they are active and the consent stands."
    : "The organisation's public site no longer shows them. Nothing else changes.";
  return (
    <ActionDialog
      open
      onOpenChange={onOpenChange}
      title={`${on ? "Turn on" : "Turn off"} the public profile of ${person.displayName}`}
      description={description}
      confirmLabel={on ? "Turn on" : "Turn off"}
      pending={change.isPending}
      error={change.error}
      onConfirm={() => change.mutate(undefined, { onSuccess: () => onSaved(`Public profile turned ${on ? "on" : "off"}.`) })}
      onReload={() => {
        void reload();
        onOpenChange(false);
      }}
    />
  );
}
