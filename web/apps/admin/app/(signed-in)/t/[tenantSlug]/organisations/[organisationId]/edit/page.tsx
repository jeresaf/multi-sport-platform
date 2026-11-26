"use client";

import { useMutation, useQueryClient } from "@tanstack/react-query";
import { useParams, useRouter } from "next/navigation";
import { useState } from "react";
import { Loading } from "@/components/loading";
import { ProblemAlert } from "@/components/problem-alert";
import { ForbiddenView, NotFoundView } from "@/components/status-page";
import { UnavailableNote } from "@/components/unavailable-note";
import { BackLink } from "@/components/viewer/facts";
import { actionAccess } from "@/lib/action-access";
import { call, isApiProblem } from "@/lib/api";
import { ifMatch } from "@/lib/concurrency";
import { useTenantWorkspace } from "@/lib/tenant-workspace";
import { tenantPath } from "@/lib/workspace";
import { OrganisationEditor, type OrganisationSave } from "../../organisation-editor";
import { fromOrganisation, toUpdateRequest } from "../../organisation-form";
import {
  editOrganisationActions,
  organisationQueryKey,
  useKeepOrganisation,
  useOrganisation,
  type Organisation,
} from "../../organisations-data";

/**
 * Registry → Organisations → an organisation → Edit details (S07 D4, D23): every detail, the web address included, in
 * any status (a correction). The `PUT` replaces them all from the version the page opened on (`If-Match`); a change to
 * a name, the type or the city runs the duplicate warning again. The status, parent, aliases, identifiers and logo have
 * their own actions.
 */
export default function EditOrganisationPage() {
  const { organisationId } = useParams<{ organisationId: string }>();
  const { tenant, can, unavailable } = useTenantWorkspace();
  const { allowed, reason } = actionAccess(editOrganisationActions, can, unavailable);
  const queryClient = useQueryClient();
  const organisation = useOrganisation(organisationId, true);
  // The form starts once the organisation has been read since the page opened (a cached one may be older than its version)
  // and never resets what is being typed; only Reload starts it over (as People's Edit details, review #113 R3).
  const [started, setStarted] = useState(false);
  const [reloading, setReloading] = useState(false);
  if (!started && !reloading && organisation.isFetchedAfterMount && organisation.isSuccess && !organisation.isFetching) {
    setStarted(true);
  }
  const reload = async () => {
    setStarted(false);
    setReloading(true);
    try {
      await queryClient.refetchQueries({ queryKey: organisationQueryKey(tenant.tenantId, organisationId) });
    } finally {
      setReloading(false);
    }
  };

  if (!can("organisation.read")) {
    return <ForbiddenView />;
  }
  if (isApiProblem(organisation.error, "ORGANISATION_NOT_FOUND")) {
    return <NotFoundView />;
  }
  const organisationHref = `${tenantPath(tenant.slug)}/organisations/${organisationId}`;
  const back = <BackLink href={organisationHref}>{organisation.data?.displayName ?? "Back"}</BackLink>;
  if (!allowed) {
    return reason ? (
      <section className="grid gap-4">
        {back}
        <h1 className="text-2xl font-semibold">Edit details</h1>
        <UnavailableNote id="edit-organisation-unavailable" reason={reason} />
      </section>
    ) : (
      <ForbiddenView />
    );
  }
  if (!started || !organisation.data) {
    const error = reloading ? null : organisation.error;
    return (
      <section className="grid gap-4">
        {back}
        {error ? <ProblemAlert error={error} /> : <Loading />}
      </section>
    );
  }
  return (
    <EditDetails
      organisation={organisation.data}
      organisationHref={organisationHref}
      onReload={() => void reload()}
    />
  );
}

function EditDetails({
  organisation,
  organisationHref,
  onReload,
}: Readonly<{ organisation: Organisation; organisationHref: string; onReload: () => void }>) {
  const { client } = useTenantWorkspace();
  const router = useRouter();
  const keep = useKeepOrganisation();
  // The organisation as the page opened on it: the change's basis and what the warning compares with.
  const [opened] = useState(() => ({ version: organisation.version, values: fromOrganisation(organisation) }));
  const save = useMutation({
    mutationFn: ({ values, decision }: OrganisationSave) =>
      call(
        client.PUT("/api/admin/v1/organisations/{organisationId}", {
          params: { path: { organisationId: organisation.id }, header: ifMatch(opened.version) },
          body: toUpdateRequest(values, decision),
        }),
      ),
    onSuccess: (saved) => {
      keep(saved);
      router.push(organisationHref);
    },
  });

  return (
    <section className="grid grid-cols-[minmax(0,1fr)] gap-6">
      <BackLink href={organisationHref}>{organisation.displayName}</BackLink>
      <header className="grid gap-1">
        <p className="text-sm text-muted-foreground break-words">{organisation.displayName}</p>
        <h1 className="text-2xl font-semibold">Edit details</h1>
        <p className="text-muted-foreground">
          A change to a name, the type or the city is checked against the organisations you already have. The status,
          parent, other names, identifiers and logo are changed on the organisation&apos;s page.
        </p>
      </header>
      <OrganisationEditor
        initial={opened.values}
        organisation={organisation}
        save={save}
        submitLabel="Save"
        pendingLabel="Saving…"
        cancelHref={organisationHref}
        onReload={onReload}
      />
    </section>
  );
}
