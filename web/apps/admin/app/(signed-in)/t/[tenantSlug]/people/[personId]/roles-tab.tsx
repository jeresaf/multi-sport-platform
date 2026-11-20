"use client";

import { Badge } from "@msp/ui/components/badge";
import { Button } from "@msp/ui/components/button";
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from "@msp/ui/components/table";
import { Pencil, Plus } from "lucide-react";
import { useRef, useState, type ReactNode } from "react";
import { EmptyState } from "@/components/empty-state";
import { ProblemAlert } from "@/components/problem-alert";
import { StatusBadge } from "@/components/status-badge";
import { UnavailableNote } from "@/components/unavailable-note";
import { Facts, RowName, ViewerSection, wide } from "@/components/viewer/facts";
import { humanizeCode } from "@/lib/codes";
import { formatDay } from "@/lib/entitlements";
import { useTenantWorkspace } from "@/lib/tenant-workspace";
import { useTenantSports } from "../../settings/sports/sports-settings-data";
import type { Person, ProfileKind } from "../people-data";
import { AthleteProfileDialog } from "./athlete-profile-dialog";
import { CapabilityDialog } from "./capability-dialog";
import {
  isClosedPerson,
  isOpen,
  profileActions,
  profileMoves,
  type CapabilityKind,
  type ProfileStatus,
} from "./profile-data";
import { moveLabels, ProfileStatusDialog } from "./profile-status-dialog";
import { useSportLabels } from "./sport-labels";

type AthleteProfile = Person["athleteProfiles"][number];
type StaffProfile = Person["staffProfiles"][number];
type OfficialProfile = Person["officialProfiles"][number];

type OpenDialog =
  | { type: "athlete"; profile?: AthleteProfile }
  | { type: "capabilities"; kind: CapabilityKind; profile?: StaffProfile | OfficialProfile }
  | { type: "status"; kind: ProfileKind; profileId: string; title: string; target: ProfileStatus };

const unavailableId = "roles-unavailable";
const noSportsId = "roles-no-sports";

/** Every profile's id, to find the one a create added. */
const profileIds = (person: Person) => [
  ...person.athleteProfiles.map((profile) => profile.id),
  ...person.staffProfiles.map((profile) => profile.id),
  ...person.officialProfiles.map((profile) => profile.id),
];

/** A profile's card: its title (focused after a change to it), status and actions, then what it holds. */
function ProfileCard({
  id,
  title,
  status,
  actions,
  children,
}: Readonly<{ id: string; title: string; status: string; actions: ReactNode; children: ReactNode }>) {
  return (
    <article className="grid gap-3 rounded-lg border p-4" aria-labelledby={`profile-${id}`}>
      <header className="flex flex-wrap items-center gap-2">
        <h4 id={`profile-${id}`} tabIndex={-1} className="font-semibold break-words outline-none focus-visible:ring-2 focus-visible:ring-ring">
          {title}
        </h4>
        <StatusBadge status={status} />
        {actions ? <div className="flex w-full flex-wrap gap-2 tablet:ml-auto tablet:w-auto">{actions}</div> : null}
      </header>
      {children}
    </article>
  );
}

/**
 * Roles: the person's athlete profiles (one per sport), staff profile and official profile (S06 D12), archived ones
 * included, each with its status. Sports, positions and roles are named by the definitions. With the profile actions,
 * a profile is added, changed and moved between statuses here (T7c); an archived or deceased person gets no new one.
 */
export function RolesTab({ person }: Readonly<{ person: Person }>) {
  const { athleteProfiles, staffProfiles, officialProfiles } = person;
  const { can, unavailable } = useTenantWorkspace();
  const [dialog, setDialog] = useState<OpenDialog | null>(null);
  const [announcement, setAnnouncement] = useState("");
  // The profile to focus once the dialog has closed after a change (its card's heading).
  const focusAfter = useRef<string | null>(null);

  const offered = (action: string) => can(action) || unavailable(action) !== null;
  const anyCreate = Object.values(profileActions).some((actions) => offered(actions.create));
  // Only a form needs the tenant's sports and their definitions: the read-only tab doesn't ask for them.
  const tenantSports = useTenantSports(anyCreate || dialog !== null);
  const sports = (anyCreate || dialog ? tenantSports.data?.variants ?? [] : [])
    .filter((sport) => sport.enabled)
    .map(({ variant, name }) => ({ variant, name }));
  const labels = useSportLabels([
    ...athleteProfiles.map((profile) => profile.variant),
    ...staffProfiles.flatMap((profile) => profile.capabilities.map((capability) => capability.variant)),
    ...officialProfiles.flatMap((profile) => profile.capabilities.map((capability) => capability.variant)),
    // Read ahead for the forms, so their positions and roles are there when one opens.
    ...sports.map((sport) => sport.variant),
  ]);

  // One note for every profile action the tenant can't use now (read-only): the disabled controls point to it.
  const refusal =
    Object.values(profileActions)
      .flatMap((actions) => [actions.create, actions.update, actions.status])
      .map((action) => unavailable(action))
      .find((reason) => reason !== null) ?? null;

  const closed = isClosedPerson(person);
  const freeAthleteSports = sports.filter(
    (sport) => !athleteProfiles.some((profile) => profile.variant === sport.variant && isOpen(profile)),
  );
  const noSports = tenantSports.isSuccess && sports.length === 0;
  const adds: Array<{ action: string; label: string; open: OpenDialog; shown: boolean }> = [
    {
      action: profileActions.ATHLETE.create,
      label: "Add athlete profile",
      open: { type: "athlete" },
      shown: noSports || freeAthleteSports.length > 0,
    },
    {
      action: profileActions.STAFF.create,
      label: "Add staff profile",
      open: { type: "capabilities", kind: "STAFF" },
      shown: !staffProfiles.some(isOpen),
    },
    {
      action: profileActions.OFFICIAL.create,
      label: "Add official profile",
      open: { type: "capabilities", kind: "OFFICIAL" },
      shown: !officialProfiles.some(isOpen),
    },
  ];
  const shownAdds = closed ? [] : adds.filter((add) => add.shown && offered(add.action));

  const saved = (message: string, answer: Person, profileId?: string) => {
    focusAfter.current = profileId ?? profileIds(answer).find((id) => !profileIds(person).includes(id)) ?? null;
    setAnnouncement(message);
    setDialog(null);
  };
  const focusChanged = (event: Event) => {
    const id = focusAfter.current;
    focusAfter.current = null;
    const heading = id ? document.getElementById(`profile-${id}`) : null;
    if (heading) {
      event.preventDefault();
      heading.focus();
    }
  };

  /** A profile's Edit and status moves: none for an archived profile; each only with its action. */
  const profileButtons = (kind: ProfileKind, profileId: string, status: ProfileStatus, title: string, edit: OpenDialog) => {
    if (status === "ARCHIVED") {
      return null;
    }
    const actions = profileActions[kind];
    const buttons: ReactNode[] = [];
    const disabled = (action: string) =>
      unavailable(action) !== null ? { disabled: true, "aria-describedby": unavailableId } : {};
    if (offered(actions.update)) {
      buttons.push(
        // The name says which profile; it starts with the visible word (WCAG 2.5.3).
        <Button
          key="edit"
          variant="outline"
          aria-label={`Edit: ${title}`}
          onClick={() => setDialog(edit)}
          {...disabled(actions.update)}
        >
          <Pencil aria-hidden="true" />
          Edit
        </Button>,
      );
    }
    if (offered(actions.status)) {
      for (const target of profileMoves(status, person)) {
        buttons.push(
          <Button
            key={target}
            variant="outline"
            aria-label={`${moveLabels[target]}: ${title}`}
            onClick={() => setDialog({ type: "status", kind, profileId, title, target })}
            {...disabled(actions.status)}
          >
            {moveLabels[target]}
          </Button>,
        );
      }
    }
    return buttons.length > 0 ? buttons : null;
  };

  const none = athleteProfiles.length + staffProfiles.length + officialProfiles.length === 0;

  return (
    <>
      <p role="status" className="sr-only">
        {announcement}
      </p>

      {refusal ? <UnavailableNote id={unavailableId} reason={refusal} /> : null}
      {closed && anyCreate ? (
        <p className="text-sm text-muted-foreground">
          {person.displayName} is {person.status === "DECEASED" ? "deceased" : "archived"}: no new role can be added.
        </p>
      ) : null}
      {!closed && anyCreate && tenantSports.isError ? (
        <ProblemAlert error={tenantSports.error} />
      ) : null}
      {!closed && noSports && anyCreate ? (
        <p id={noSportsId} className="text-sm text-muted-foreground">
          No sport is enabled for your organisation yet. A role is for a sport: enable one in Settings → Enabled sports.
        </p>
      ) : null}
      {shownAdds.length > 0 ? (
        <div className="flex flex-wrap gap-2">
          {shownAdds.map((add) => (
            <Button
              key={add.action}
              variant="outline"
              disabled={unavailable(add.action) !== null || sports.length === 0}
              aria-describedby={
                unavailable(add.action) !== null ? unavailableId : noSports ? noSportsId : undefined
              }
              onClick={() => setDialog(add.open)}
            >
              <Plus aria-hidden="true" />
              {add.label}
            </Button>
          ))}
        </div>
      ) : null}

      {none ? (
        <EmptyState title="No roles yet">
          {person.displayName} has no athlete, staff or official profile.
        </EmptyState>
      ) : null}

      {athleteProfiles.length > 0 ? (
        <ViewerSection title="Athlete">
          {athleteProfiles.map((profile) => {
            const title = `${labels.variant(profile.variant)} athlete profile`;
            return (
              <ProfileCard
                key={profile.id}
                id={profile.id}
                title={labels.variant(profile.variant)}
                status={profile.status}
                actions={profileButtons("ATHLETE", profile.id, profile.status, title, { type: "athlete", profile })}
              >
                <Facts
                  items={[
                    [
                      "Primary position",
                      profile.primaryPosition ? labels.position(profile.variant, profile.primaryPosition) : "None",
                    ],
                    [
                      "Other positions",
                      profile.otherPositions.length > 0
                        ? profile.otherPositions.map((code) => labels.position(profile.variant, code)).join(", ")
                        : "None",
                    ],
                    ["Dominant side", profile.dominantSide ? humanizeCode(profile.dominantSide) : "Not recorded"],
                    ["Public name", profile.publicName ?? `${person.displayName} (the display name)`],
                  ]}
                />
              </ProfileCard>
            );
          })}
        </ViewerSection>
      ) : null}

      {staffProfiles.length > 0 ? (
        <ViewerSection title="Staff">
          {staffProfiles.map((profile) => (
            <ProfileCard
              key={profile.id}
              id={profile.id}
              title="Staff profile"
              status={profile.status}
              actions={profileButtons("STAFF", profile.id, profile.status, "Staff profile", {
                type: "capabilities",
                kind: "STAFF",
                profile,
              })}
            >
              <Table>
                <TableHeader>
                  <TableRow>
                    <TableHead>Role</TableHead>
                    <TableHead className={wide}>Sport</TableHead>
                    <TableHead className={wide}>Qualification</TableHead>
                  </TableRow>
                </TableHeader>
                <TableBody>
                  {profile.capabilities.map((capability) => {
                    const sport = labels.variant(capability.variant);
                    const qualification = capability.qualification ?? "None";
                    return (
                      <TableRow key={`${capability.variant} ${capability.role}`}>
                        <TableHead scope="row" className="h-auto py-2 align-top whitespace-normal">
                          <RowName
                            name={labels.staffRole(capability.variant, capability.role)}
                            details={[["Sport", sport], ["Qualification", qualification]]}
                          />
                        </TableHead>
                        <TableCell className={wide}>{sport}</TableCell>
                        <TableCell className={`${wide} whitespace-normal`}>{qualification}</TableCell>
                      </TableRow>
                    );
                  })}
                </TableBody>
              </Table>
            </ProfileCard>
          ))}
        </ViewerSection>
      ) : null}

      {officialProfiles.length > 0 ? (
        <ViewerSection title="Official">
          {officialProfiles.map((profile) => (
            <ProfileCard
              key={profile.id}
              id={profile.id}
              title="Official profile"
              status={profile.status}
              actions={profileButtons("OFFICIAL", profile.id, profile.status, "Official profile", {
                type: "capabilities",
                kind: "OFFICIAL",
                profile,
              })}
            >
              <Table>
                <TableHeader>
                  <TableRow>
                    <TableHead>Role</TableHead>
                    <TableHead className={wide}>Sport</TableHead>
                    <TableHead className={wide}>Grade</TableHead>
                    <TableHead className={wide}>Licence</TableHead>
                  </TableRow>
                </TableHeader>
                <TableBody>
                  {profile.capabilities.map((capability) => {
                    const sport = labels.variant(capability.variant);
                    const grade = capability.grade ?? "None";
                    const licence = (
                      <>
                        {capability.licenceNumber ?? "No licence number"}
                        {capability.licenceValidUntil ? `, valid until ${formatDay(capability.licenceValidUntil)}` : null}
                        {capability.licenceExpired ? (
                          <Badge variant="outline" className="ml-2">
                            Expired
                          </Badge>
                        ) : null}
                      </>
                    );
                    return (
                      <TableRow key={`${capability.variant} ${capability.role}`}>
                        <TableHead scope="row" className="h-auto py-2 align-top whitespace-normal">
                          <RowName
                            name={labels.officialRole(capability.variant, capability.role)}
                            details={[["Sport", sport], ["Grade", grade], ["Licence", licence]]}
                          />
                        </TableHead>
                        <TableCell className={wide}>{sport}</TableCell>
                        <TableCell className={`${wide} whitespace-normal`}>{grade}</TableCell>
                        <TableCell className={`${wide} whitespace-normal`}>{licence}</TableCell>
                      </TableRow>
                    );
                  })}
                </TableBody>
              </Table>
            </ProfileCard>
          ))}
        </ViewerSection>
      ) : null}

      {dialog?.type === "athlete" ? (
        <AthleteProfileDialog
          person={person}
          profile={dialog.profile}
          sports={freeAthleteSports}
          labels={labels}
          onOpenChange={(open) => !open && setDialog(null)}
          onSaved={(message, answer) => saved(message, answer, dialog.profile?.id)}
          onCloseAutoFocus={focusChanged}
        />
      ) : null}
      {dialog?.type === "capabilities" ? (
        <CapabilityDialog
          kind={dialog.kind}
          person={person}
          profile={dialog.profile}
          sports={sports}
          labels={labels}
          onOpenChange={(open) => !open && setDialog(null)}
          onSaved={(message, answer) => saved(message, answer, dialog.profile?.id)}
          onCloseAutoFocus={focusChanged}
        />
      ) : null}
      {dialog?.type === "status" ? (
        <ProfileStatusDialog
          person={person}
          kind={dialog.kind}
          profileId={dialog.profileId}
          title={dialog.title}
          target={dialog.target}
          onOpenChange={(open) => !open && setDialog(null)}
          onSaved={(message, answer) => saved(message, answer, dialog.profileId)}
          onCloseAutoFocus={focusChanged}
        />
      ) : null}
    </>
  );
}
