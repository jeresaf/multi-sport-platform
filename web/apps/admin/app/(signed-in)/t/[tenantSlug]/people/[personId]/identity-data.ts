"use client";

// A person's photo, external identifiers and aliases (S06 D10, D19): children of the person, so every change sends
// the version the dialog opened on (`If-Match`, #110 R1) and answers the person, which replaces the one the page shows.
import type { components } from "@msp/api-client";
import { useMutation } from "@tanstack/react-query";
import { call } from "@/lib/api";
import { ifMatch } from "@/lib/concurrency";
import { useTenantWorkspace } from "@/lib/tenant-workspace";
import { useKeepPerson, type OpenedPerson } from "./profile-data";

export type AliasKind = components["schemas"]["AliasKind"];
export const aliasKinds: readonly AliasKind[] = ["FORMER_NAME", "NICKNAME", "SPELLING"];

/** Each action (actions.md). */
export const identityActions = {
  setPhoto: "person.photo.set",
  removePhoto: "person.photo.remove",
  addIdentifier: "person.external_id.add",
  endIdentifier: "person.external_id.end",
  addAlias: "person.alias.add",
  removeAlias: "person.alias.remove",
} as const;

/** The photos the API takes (D16): what the picker offers; the API decides by the file's bytes. */
export const photoAccept = "image/jpeg,image/png,image/webp";
export const photoMaxBytes = 5 * 1024 * 1024;

/** Sets or replaces the photo (multipart, one file in `file`; the API keeps resized copies, not the original). */
export function useSetPhoto(person: OpenedPerson) {
  const { client } = useTenantWorkspace();
  const keep = useKeepPerson();
  return useMutation({
    mutationFn: (file: File) => {
      const form = new FormData();
      form.append("file", file);
      return call(
        client.PUT("/api/admin/v1/people/{personId}/photo", {
          params: { path: { personId: person.id }, header: ifMatch(person.version) },
          body: {},
          // FormData goes as it is: the browser sets the multipart Content-Type and its boundary.
          bodySerializer: () => form,
        }),
      );
    },
    onSuccess: keep,
  });
}

export function useRemovePhoto(person: OpenedPerson) {
  const { client } = useTenantWorkspace();
  const keep = useKeepPerson();
  return useMutation({
    mutationFn: () =>
      call(
        client.DELETE("/api/admin/v1/people/{personId}/photo", {
          params: { path: { personId: person.id }, header: ifMatch(person.version) },
        }),
      ),
    onSuccess: keep,
  });
}

export function useAddIdentifier(person: OpenedPerson) {
  const { client } = useTenantWorkspace();
  const keep = useKeepPerson();
  return useMutation({
    mutationFn: (body: { authority: string; value: string; validFrom: string | null }) =>
      call(
        client.POST("/api/admin/v1/people/{personId}/external-identifiers", {
          params: { path: { personId: person.id }, header: ifMatch(person.version) },
          body,
        }),
      ),
    onSuccess: keep,
  });
}

/** Ends an identifier; without `validTo` the API ends it on the tenant's today. It is never reused (D10). */
export function useEndIdentifier(person: OpenedPerson, identifierId: string) {
  const { client } = useTenantWorkspace();
  const keep = useKeepPerson();
  return useMutation({
    mutationFn: (body: { validTo: string | null; reason: string | null }) =>
      call(
        client.POST("/api/admin/v1/people/{personId}/external-identifiers/{identifierId}/end", {
          params: { path: { personId: person.id, identifierId }, header: ifMatch(person.version) },
          body,
        }),
      ),
    onSuccess: keep,
  });
}

export function useAddAlias(person: OpenedPerson) {
  const { client } = useTenantWorkspace();
  const keep = useKeepPerson();
  return useMutation({
    mutationFn: (body: { name: string; kind: AliasKind }) =>
      call(
        client.POST("/api/admin/v1/people/{personId}/aliases", {
          params: { path: { personId: person.id }, header: ifMatch(person.version) },
          body,
        }),
      ),
    onSuccess: keep,
  });
}

/** Removes an alias with the reason the audit trail keeps; the alias's history stays (D10). */
export function useRemoveAlias(person: OpenedPerson, aliasId: string) {
  const { client } = useTenantWorkspace();
  const keep = useKeepPerson();
  return useMutation({
    mutationFn: (reason: string) =>
      call(
        client.POST("/api/admin/v1/people/{personId}/aliases/{aliasId}/remove", {
          params: { path: { personId: person.id, aliasId }, header: ifMatch(person.version) },
          body: { reason },
        }),
      ),
    onSuccess: keep,
  });
}
