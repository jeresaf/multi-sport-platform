"use client";

import { useMutation } from "@tanstack/react-query";
import { useRouter } from "next/navigation";
import { ForbiddenView } from "@/components/status-page";
import { UnavailableNote } from "@/components/unavailable-note";
import { BackLink } from "@/components/viewer/facts";
import { actionAccess } from "@/lib/action-access";
import { call } from "@/lib/api";
import { useTenantWorkspace } from "@/lib/tenant-workspace";
import { tenantPath } from "@/lib/workspace";
import { OrganisationEditor, type OrganisationSave } from "../organisation-editor";
import { emptyOrganisation, toCreateRequest } from "../organisation-form";
import { addOrganisationActions, useKeepOrganisation } from "../organisations-data";

/**
 * Registry → Organisations → Add organisation (S07 D4, D13, D23): the organisation's details with the live duplicate
 * warning; the country is the tenant's unless one is chosen, and the web address is proposed by the server from the
 * name (D9). Saved, it opens the new organisation. The parent, aliases, identifiers and logo are set on its page.
 */
export default function AddOrganisationPage() {
  const { tenant, client, can, unavailable } = useTenantWorkspace();
  const router = useRouter();
  const keep = useKeepOrganisation();
  const { allowed, reason } = actionAccess(addOrganisationActions, can, unavailable);
  const listHref = `${tenantPath(tenant.slug)}/organisations`;
  const save = useMutation({
    mutationFn: ({ values, decision }: OrganisationSave) =>
      call(client.POST("/api/admin/v1/organisations", { body: toCreateRequest(values, decision) })),
    onSuccess: (organisation) => {
      keep(organisation);
      router.push(`${listHref}/${organisation.id}`);
    },
  });

  if (!allowed && !reason) {
    return <ForbiddenView />;
  }
  return (
    <section className="grid grid-cols-[minmax(0,1fr)] gap-6">
      <BackLink href={listHref}>Organisations</BackLink>
      <header className="grid gap-1">
        <h1 className="text-2xl font-semibold">Add organisation</h1>
        <p className="text-muted-foreground">
          A club, school, federation or other body. Its names, type and city are checked against the organisations you
          already have. The parent, other names, identifiers and logo are added on its page.
        </p>
      </header>
      {!allowed ? (
        <UnavailableNote id="add-organisation-unavailable" reason={reason!} />
      ) : (
        <OrganisationEditor
          initial={emptyOrganisation()}
          save={save}
          submitLabel="Add organisation"
          pendingLabel="Adding…"
          cancelHref={listHref}
        />
      )}
    </section>
  );
}
