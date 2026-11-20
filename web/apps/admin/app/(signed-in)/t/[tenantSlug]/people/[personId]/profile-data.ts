"use client";

// Changing a person's role profiles (S06 D12): profiles are children of the person, so every change sends the
// person's version (`If-Match`) and answers the person, which replaces the one the page shows. The version is the one
// the dialog opened on (`OpenedPerson`), not the latest read: the page reads the person again while a dialog is open
// (the photo's URLs expire), and sending that newer version would overwrite a change the user never saw (#110 R1).
import type { components } from "@msp/api-client";
import { useMutation, useQueryClient } from "@tanstack/react-query";
import { call } from "@/lib/api";
import { ifMatch } from "@/lib/concurrency";
import { useTenantWorkspace } from "@/lib/tenant-workspace";
import { personQueryKey, type Person, type ProfileKind } from "../people-data";

type Schemas = components["schemas"];
export type ProfileStatus = Schemas["ProfileStatus"];
export type DominantSide = NonNullable<Schemas["DominantSide"]>;
export type AthleteProfileDetails = Omit<Schemas["UpdateAthleteProfileRequest"], "version">;
export type StaffCapabilityRequest = Schemas["StaffCapabilityRequest"];
export type OfficialCapabilityRequest = Schemas["OfficialCapabilityRequest"];
/** The profile kinds that hold capabilities (variant and role) rather than one sport. */
export type CapabilityKind = Exclude<ProfileKind, "ATHLETE">;

export const dominantSides: readonly DominantSide[] = ["LEFT", "RIGHT", "BOTH"];
/** The most other positions, and capabilities, a profile holds (S06 T4a, T4b). */
export const maxOtherPositions = 5;
export const maxCapabilities = 10;

/** A profile that isn't archived still counts as open (S06 T4a): one per variant (athlete) or one per person. */
export const isOpen = (profile: { status: ProfileStatus }) => profile.status !== "ARCHIVED";

/** An archived or deceased person takes no new profile, no new capability and no move back to `ACTIVE` (D9, T4a). */
export const isClosedPerson = (person: Person) => person.status === "ARCHIVED" || person.status === "DECEASED";

/**
 * The statuses a profile may move to from its own (S06 T4a; state-machines.md, Registry): `ACTIVE ⇄ INACTIVE`,
 * `ACTIVE | INACTIVE → RETIRED`, `RETIRED → ACTIVE`, anything but `ARCHIVED` → `ARCHIVED` (final). On an archived or
 * deceased person only the closing moves remain.
 */
export function profileMoves(status: ProfileStatus, person: Person): ProfileStatus[] {
  const moves: Record<ProfileStatus, ProfileStatus[]> = {
    ACTIVE: ["INACTIVE", "RETIRED", "ARCHIVED"],
    INACTIVE: ["ACTIVE", "RETIRED", "ARCHIVED"],
    RETIRED: ["ACTIVE", "ARCHIVED"],
    ARCHIVED: [],
  };
  return moves[status].filter((target) => target !== "ACTIVE" || !isClosedPerson(person));
}

/** Each kind's actions (actions.md). */
export const profileActions = {
  ATHLETE: { create: "athlete_profile.create", update: "athlete_profile.update", status: "athlete_profile.status.change" },
  STAFF: { create: "staff_profile.create", update: "staff_profile.update", status: "staff_profile.status.change" },
  OFFICIAL: {
    create: "official_profile.create",
    update: "official_profile.update",
    status: "official_profile.status.change",
  },
} as const satisfies Record<ProfileKind, { create: string; update: string; status: string }>;

/** The person as a dialog first showed them: what a change is based on. */
export type OpenedPerson = Pick<Person, "id" | "version">;

/** Keeps the person a change answered, and marks the directory stale (its role and sport filters may change). */
function useKeepPerson() {
  const { tenant } = useTenantWorkspace();
  const queryClient = useQueryClient();
  return (person: Person) => {
    queryClient.setQueryData(personQueryKey(tenant.tenantId, person.id), person);
    void queryClient.invalidateQueries({ queryKey: ["people", tenant.tenantId] });
  };
}

/** Reads the person again (after a concurrency conflict, or a refusal that says the page is out of date). */
export function useReloadPerson(personId: string) {
  const { tenant } = useTenantWorkspace();
  const queryClient = useQueryClient();
  return () => queryClient.invalidateQueries({ queryKey: personQueryKey(tenant.tenantId, personId) });
}

/** Creates an athlete profile (`variant` set) or replaces an existing one's details (`profileId` set). */
export function useSaveAthleteProfile(person: OpenedPerson, profileId: string | undefined) {
  const { client } = useTenantWorkspace();
  const keep = useKeepPerson();
  return useMutation({
    mutationFn: ({ variant, details }: { variant: string; details: AthleteProfileDetails }) => {
      const header = ifMatch(person.version);
      return call(
        profileId
          ? client.PUT("/api/admin/v1/people/{personId}/athlete-profiles/{profileId}", {
              params: { path: { personId: person.id, profileId }, header },
              body: details,
            })
          : client.POST("/api/admin/v1/people/{personId}/athlete-profiles", {
              params: { path: { personId: person.id }, header },
              body: { variant, ...details },
            }),
      );
    },
    onSuccess: keep,
  });
}

/** Creates a staff or official profile, or replaces its capabilities as a whole (`profileId` set). */
export function useSaveCapabilities(person: OpenedPerson, kind: CapabilityKind, profileId: string | undefined) {
  const { client } = useTenantWorkspace();
  const keep = useKeepPerson();
  return useMutation({
    mutationFn: (capabilities: StaffCapabilityRequest[] | OfficialCapabilityRequest[]) => {
      const header = ifMatch(person.version);
      if (kind === "STAFF") {
        const body = { capabilities: capabilities as StaffCapabilityRequest[] };
        return call(
          profileId
            ? client.PUT("/api/admin/v1/people/{personId}/staff-profiles/{profileId}", {
                params: { path: { personId: person.id, profileId }, header },
                body,
              })
            : client.POST("/api/admin/v1/people/{personId}/staff-profiles", {
                params: { path: { personId: person.id }, header },
                body,
              }),
        );
      }
      const body = { capabilities: capabilities as OfficialCapabilityRequest[] };
      return call(
        profileId
          ? client.PUT("/api/admin/v1/people/{personId}/official-profiles/{profileId}", {
              params: { path: { personId: person.id, profileId }, header },
              body,
            })
          : client.POST("/api/admin/v1/people/{personId}/official-profiles", {
              params: { path: { personId: person.id }, header },
              body,
            }),
      );
    },
    onSuccess: keep,
  });
}

/** Moves a profile to another status, with the reason the audit trail keeps. */
export function useChangeProfileStatus(person: OpenedPerson, kind: ProfileKind, profileId: string) {
  const { client } = useTenantWorkspace();
  const keep = useKeepPerson();
  return useMutation({
    mutationFn: ({ status, reason }: { status: ProfileStatus; reason: string }) => {
      const params = { path: { personId: person.id, profileId }, header: ifMatch(person.version) };
      const body = { status, reason };
      switch (kind) {
        case "ATHLETE":
          return call(client.POST("/api/admin/v1/people/{personId}/athlete-profiles/{profileId}/status", { params, body }));
        case "STAFF":
          return call(client.POST("/api/admin/v1/people/{personId}/staff-profiles/{profileId}/status", { params, body }));
        case "OFFICIAL":
          return call(client.POST("/api/admin/v1/people/{personId}/official-profiles/{profileId}/status", { params, body }));
      }
    },
    onSuccess: keep,
  });
}
