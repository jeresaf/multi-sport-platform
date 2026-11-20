"use client";

// Guardian consent and the public profile (S06 D14, D15, D19): consents are children of the person, so every change
// sends the version the dialog opened on (`If-Match`) and answers the person, which replaces the one the page shows.
// The private read (the guardians and the evidence) is read again after a consent change: the records it names changed.
import type { components } from "@msp/api-client";
import { useMutation, useQueryClient } from "@tanstack/react-query";
import { call } from "@/lib/api";
import { ifMatch } from "@/lib/concurrency";
import { useTenantWorkspace } from "@/lib/tenant-workspace";
import { personQueryKey, type Person } from "../people-data";
import type { OpenedPerson } from "./profile-data";

type Schemas = components["schemas"];
export type ConsentScope = Schemas["ConsentScope"];
export type GuardianRelationship = NonNullable<Schemas["GuardianRelationship"]>;
export type RecordConsentRequest = Omit<Schemas["RecordConsentRequest"], "version">;

export const consentScopes: readonly ConsentScope[] = ["DATA_PROCESSING", "PUBLIC_PROFILE", "PHOTO"];
export const guardianRelationships: readonly GuardianRelationship[] = ["MOTHER", "FATHER", "GUARDIAN", "OTHER"];

/** Each action (actions.md). */
export const consentActions = {
  record: "person.consent.record",
  withdraw: "person.consent.withdraw",
  evidence: "person.consent_evidence.upload",
  enable: "person.public_profile.enable",
  disable: "person.public_profile.disable",
} as const;

/** The evidence a consent takes (D16): what the file picker offers; the API decides by the file's bytes. */
export const evidenceAccept = "application/pdf,image/jpeg,image/png";
export const evidenceMaxBytes = 10 * 1024 * 1024;

/** Keeps the person a change answered; with `consents`, the private read is asked again (its records changed). */
function useKeepPerson() {
  const { tenant } = useTenantWorkspace();
  const queryClient = useQueryClient();
  return (person: Person, consents: boolean) => {
    queryClient.setQueryData(personQueryKey(tenant.tenantId, person.id), person);
    if (consents) {
      void queryClient.invalidateQueries({ queryKey: ["person-private", tenant.tenantId, person.id] });
    }
  };
}

/** Records a guardian's consent. */
export function useRecordConsent(person: OpenedPerson) {
  const { client } = useTenantWorkspace();
  const keep = useKeepPerson();
  return useMutation({
    mutationFn: (body: RecordConsentRequest) =>
      call(
        client.POST("/api/admin/v1/people/{personId}/consents", {
          params: { path: { personId: person.id }, header: ifMatch(person.version) },
          body,
        }),
      ),
    onSuccess: (saved) => keep(saved, true),
  });
}

/**
 * Uploads a consent's evidence (multipart, one file in `file`). The version is passed with each upload: after
 * recording a consent, the upload is based on the person that answered.
 */
export function useUploadEvidence(personId: string) {
  const { client } = useTenantWorkspace();
  const keep = useKeepPerson();
  return useMutation({
    mutationFn: ({ consentId, version, file }: { consentId: string; version: number; file: File }) => {
      const form = new FormData();
      form.append("file", file);
      return call(
        client.PUT("/api/admin/v1/people/{personId}/consents/{consentId}/evidence", {
          params: { path: { personId, consentId }, header: ifMatch(version) },
          body: {},
          // FormData goes as it is: the browser sets the multipart Content-Type and its boundary.
          bodySerializer: () => form,
        }),
      );
    },
    onSuccess: (saved) => keep(saved, true),
  });
}

/** Withdraws some or all of a consent's scopes, with the reason the audit trail keeps. */
export function useWithdrawConsent(person: OpenedPerson, consentId: string) {
  const { client } = useTenantWorkspace();
  const keep = useKeepPerson();
  return useMutation({
    mutationFn: (body: { scopes: ConsentScope[]; reason: string }) =>
      call(
        client.POST("/api/admin/v1/people/{personId}/consents/{consentId}/withdraw", {
          params: { path: { personId: person.id, consentId }, header: ifMatch(person.version) },
          body,
        }),
      ),
    onSuccess: (saved) => keep(saved, true),
  });
}

/** Turns the public profile on or off (no body). */
export function useSwitchPublicProfile(person: OpenedPerson, on: boolean) {
  const { client } = useTenantWorkspace();
  const keep = useKeepPerson();
  return useMutation({
    mutationFn: () => {
      const params = { path: { personId: person.id }, header: ifMatch(person.version) };
      return call(
        on
          ? client.POST("/api/admin/v1/people/{personId}/public-profile/enable", { params })
          : client.POST("/api/admin/v1/people/{personId}/public-profile/disable", { params }),
      );
    },
    onSuccess: (saved) => keep(saved, false),
  });
}
